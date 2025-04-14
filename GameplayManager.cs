using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public class GameplayManager : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Variables and Properties

    // Game parameters
    [Header("Game Parameters")]
    [SerializeField] private int pointsToWin = 10;
    [SerializeField] private int maxRounds = 10;
    [SerializeField] private int currentRound = 0;
    [SerializeField] private GamePhase currentPhase = GamePhase.Setup;
    
    // Player and monster tracking
    [Header("Players and Monsters")]
    private Dictionary<int, PlayerController> players = new Dictionary<int, PlayerController>();
    private Dictionary<int, MonsterController> monsters = new Dictionary<int, MonsterController>();
    private Dictionary<int, int> playerMonsterPairs = new Dictionary<int, int>(); // Player ActorNumber -> Monster ActorNumber
    private Dictionary<int, int> playerScores = new Dictionary<int, int>();
    private List<int> activePlayers = new List<int>();
    private List<int> playersFinishedCombat = new List<int>();
    
    // Turn management
    [Header("Turn Management")]
    private int currentPlayerTurn = -1;
    private bool isBattlePhaseActive = false;
    
    // Draft phase
    [Header("Draft Phase")]
    private List<Upgrade> availableUpgrades = new List<Upgrade>();
    private Dictionary<int, List<Upgrade>> draftQueues = new Dictionary<int, List<Upgrade>>();
    private List<int> playersFinishedDraft = new List<int>();
    
    // UI references
    [Header("UI References")]
    private GameObject phaseAnnouncementPanel;
    private TextMeshProUGUI phaseAnnouncementText;
    private GameObject gameOverPanel;
    private TextMeshProUGUI winnerText;
    private GameObject draftPanel;
    
    // References to other managers
    [Header("Managers")]
    private UIManager uiManager;
    private NetworkManager networkManager;
    private DeckManager deckManager;
    private CardUIManager cardUIManager;
    private BattleUIManager battleUIManager;
    private BattleUI battleUI;
    
    // Flag to track prefab spawning
    private bool hasSpawnedPrefabs = false;
    
    // Properties
    public GamePhase CurrentPhase { get => currentPhase; }
    public int CurrentRound { get => currentRound; }
    
    // Enums
    public enum GamePhase
    {
        Setup,
        Battle,
        Draft,
        GameOver
    }
    
    #endregion
    
    #region Unity and Photon Callbacks
    
    private void Awake()
{
    // Verify that we have a PhotonView component
    PhotonView photonView = GetComponent<PhotonView>();
    if (photonView == null)
    {
        Debug.LogError("GameplayManager prefab missing PhotonView component!");
        return;
    }
    
    Debug.Log("GameplayManager initialized with PhotonView ID: " + photonView.ViewID);
    
    // Register this GameplayManager with the GameManager
    if (GameManager.Instance != null)
    {
        GameManager.Instance.RegisterNetworkedGameplayManager(this);
    }
    else
    {
        Debug.LogError("GameManager.Instance is null in GameplayManager.Awake");
    }
    
    // Get references to other managers
    uiManager = FindObjectOfType<UIManager>();
    networkManager = FindObjectOfType<NetworkManager>();
    
    // Create required managers if they don't exist
    if (deckManager == null)
    {
        GameObject deckMgrObj = new GameObject("DeckManager");
        deckMgrObj.transform.SetParent(transform);
        deckManager = deckMgrObj.AddComponent<DeckManager>();
    }
    
    if (cardUIManager == null)
    {
        GameObject cardUIMgrObj = new GameObject("CardUIManager");
        cardUIMgrObj.transform.SetParent(transform);
        cardUIManager = cardUIMgrObj.AddComponent<CardUIManager>();
    }
    
    if (battleUIManager == null)
    {
        GameObject battleUIMgrObj = new GameObject("BattleUIManager");
        battleUIMgrObj.transform.SetParent(transform);
        battleUIManager = battleUIMgrObj.AddComponent<BattleUIManager>();
    }
    
    // Create BattleUI
    GameObject battleUIObj = new GameObject("BattleUI");
    battleUIObj.transform.SetParent(transform);
    battleUI = battleUIObj.AddComponent<BattleUI>();
    
    // Setup cross-references
    deckManager.SetupCardUIManager(cardUIManager);
    
    // Create UI elements
    CreateGameplayUI();
}
    
    private void Start()
    {
        // Initialize BattleUI
        if (battleUI != null)
        {
            battleUI.Initialize();
        }
        
        if (PhotonNetwork.IsMasterClient)
        {
            // Initialize the game when all players are ready
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
            {
                StartCoroutine(StartGameWhenPlayersReady());
            }
        }
    }
    
    private IEnumerator StartGameWhenPlayersReady()
    {
        yield return new WaitUntil(() => AreAllPlayersReady());
        
        // Start the game
        photonView.RPC("RPC_StartGame", RpcTarget.All);
    }
    
    private bool AreAllPlayersReady()
    {
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.CustomProperties.TryGetValue("IsReady", out object isReady))
            {
                if (!(bool)isReady)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        return true;
    }
    
    [PunRPC]
    private void RPC_StartGame()
    {
        // Initialize the game
        currentRound = 1;
        SetGamePhase(GamePhase.Setup);
        
        // Show phase announcement
        ShowPhaseAnnouncement("Game Starting - Round 1");
        
        // Start the first round setup
        StartCoroutine(SetupRound());
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        
        // Handle player leaving during game
        if (currentPhase != GamePhase.Setup)
        {
            // Remove player from tracking
            int actorNumber = otherPlayer.ActorNumber;
            
            if (players.ContainsKey(actorNumber))
            {
                players.Remove(actorNumber);
            }
            
            activePlayers.Remove(actorNumber);
            playersFinishedCombat.Remove(actorNumber);
            playersFinishedDraft.Remove(actorNumber);
            
            // Check if game should continue
            if (activePlayers.Count < 2)
            {
                // End game if less than 2 players
                SetGamePhase(GamePhase.GameOver);
                
                if (activePlayers.Count == 1)
                {
                    // Declare remaining player as winner
                    string winnerName = PhotonNetwork.CurrentRoom.GetPlayer(activePlayers[0]).NickName;
                    ShowWinner(winnerName);
                }
                else
                {
                    ShowWinner("Nobody - All players left!");
                }
            }
        }
    }
    
    // Implement IPunObservable
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Synchronize important game state
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(currentRound);
            stream.SendNext((int)currentPhase);
            stream.SendNext(isBattlePhaseActive);
            stream.SendNext(currentPlayerTurn);
        }
        else
        {
            // Receive data
            currentRound = (int)stream.ReceiveNext();
            currentPhase = (GamePhase)(int)stream.ReceiveNext();
            isBattlePhaseActive = (bool)stream.ReceiveNext();
            currentPlayerTurn = (int)stream.ReceiveNext();
        }
    }
    
    #endregion
    
    #region Player and Monster Registration
    
    public void RegisterPlayer(PlayerController player)
    {
        int actorNumber = player.photonView.Owner.ActorNumber;
        
        if (!players.ContainsKey(actorNumber))
        {
            players.Add(actorNumber, player);
            
            // Initialize player score if needed
            if (!playerScores.ContainsKey(actorNumber))
            {
                playerScores.Add(actorNumber, 0);
            }
            
            // Add to active players if not already there
            if (!activePlayers.Contains(actorNumber))
            {
                activePlayers.Add(actorNumber);
            }
            
            Debug.Log($"Player registered: {player.photonView.Owner.NickName} (ActorNumber: {actorNumber})");
            
            // If this is the local player, set up UI managers
            if (player.photonView.IsMine)
            {
                player.SetupDeckManager(deckManager);
                player.SetupCardUIManager(cardUIManager);
                
                // Update BattleUI with player stats
                if (battleUI != null)
                {
                    battleUI.UpdatePlayerStats(player);
                }
            }
            
            // Check if we should initialize battles
            if (currentPhase == GamePhase.Battle && hasSpawnedPrefabs)
            {
                // If we're already in battle phase, update UI and initialize relationships
                UpdateBattleUI();
                
                // Initialize battle relationships if master client
                if (PhotonNetwork.IsMasterClient && playerMonsterPairs.Count == 0)
                {
                    AssignMonstersToPlayers();
                }
            }
        }
    }
    
    public void RegisterMonster(MonsterController monster)
    {
        int actorNumber = monster.photonView.Owner.ActorNumber;
        
        if (!monsters.ContainsKey(actorNumber))
        {
            monsters.Add(actorNumber, monster);
            
            // If this is owned by the local player, set owning player
            if (monster.photonView.IsMine)
            {
                PlayerController localPlayer = GetLocalPlayer();
                if (localPlayer != null)
                {
                    monster.OwningPlayer = localPlayer;
                }
            }
            
            Debug.Log($"Monster registered for player: {monster.photonView.Owner.NickName} (ActorNumber: {actorNumber})");
            
            // Check if we should initialize battles
            if (currentPhase == GamePhase.Battle && hasSpawnedPrefabs)
            {
                // If we're already in battle phase, update UI
                UpdateBattleUI();
            }
        }
    }
    
    private PlayerController GetLocalPlayer()
    {
        foreach (PlayerController player in players.Values)
        {
            if (player.photonView.IsMine)
            {
                return player;
            }
        }
        return null;
    }
    
    // Method to be called when prefabs are spawned
    public void OnPlayerAndMonsterSpawned()
    {
        Debug.Log("GameplayManager: Player and Monster spawned");
        hasSpawnedPrefabs = true;
        
        // If already in battle phase, initialize it properly
        if (currentPhase == GamePhase.Battle)
        {
            Debug.Log("Already in battle phase, initializing battle");
            InitializeBattlePhase();
        }
    }
    
    private void RefreshPlayerAndMonsterReferences()
    {
        Debug.Log("Refreshing player and monster references");
        
        // Find all PlayerController objects in the scene
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            int actorNumber = player.photonView.Owner.ActorNumber;
            if (!players.ContainsKey(actorNumber))
            {
                players.Add(actorNumber, player);
                
                // Initialize player score if needed
                if (!playerScores.ContainsKey(actorNumber))
                {
                    playerScores.Add(actorNumber, 0);
                }
                
                // Add to active players if not already there
                if (!activePlayers.Contains(actorNumber))
                {
                    activePlayers.Add(actorNumber);
                }
                
                Debug.Log($"Added player {player.PlayerName} with actor number {actorNumber}");
            }
        }
        
        // Find all MonsterController objects in the scene
        MonsterController[] allMonsters = FindObjectsOfType<MonsterController>();
        foreach (MonsterController monster in allMonsters)
        {
            int actorNumber = monster.photonView.Owner.ActorNumber;
            if (!monsters.ContainsKey(actorNumber))
            {
                monsters.Add(actorNumber, monster);
                Debug.Log($"Added monster with actor number {actorNumber}");
            }
        }
        
        // Update BattleUI
        UpdateBattleUI();
    }
    
    private void UpdateBattleUI()
    {
        // Update battle UI with the local player and monsters
        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer != null && battleUI != null)
        {
            battleUI.UpdatePlayerStats(localPlayer);
            
            // Find opponent monster based on pairings
            int playerActorNumber = localPlayer.photonView.Owner.ActorNumber;
            if (playerMonsterPairs.ContainsKey(playerActorNumber))
            {
                int monsterOwnerActorNumber = playerMonsterPairs[playerActorNumber];
                if (monsters.ContainsKey(monsterOwnerActorNumber))
                {
                    MonsterController opponentMonster = monsters[monsterOwnerActorNumber];
                    battleUI.UpdateMonsterStats(opponentMonster);
                }
            }
            
            // Find player's pet monster
            if (monsters.ContainsKey(playerActorNumber))
            {
                MonsterController petMonster = monsters[playerActorNumber];
                battleUI.UpdatePetStats(petMonster);
            }
        }
    }
    
    #endregion
    
    #region Game Phase Management
    
    private void SetGamePhase(GamePhase phase)
    {
        currentPhase = phase;
        
        switch (phase)
        {
            case GamePhase.Setup:
                // Setup phase logic handled in SetupRound coroutine
                break;
                
            case GamePhase.Battle:
                InitializeBattlePhase();
                break;
                
            case GamePhase.Draft:
                InitializeDraftPhase();
                break;
                
            case GamePhase.GameOver:
                ShowGameOver();
                break;
        }
    }
    
    private IEnumerator SetupRound()
    {
        // Reset player states for the new round
        foreach (PlayerController player in players.Values)
        {
            player.ResetForNewRound();
        }
        
        // Reset monster states for the new round
        foreach (MonsterController monster in monsters.Values)
        {
            monster.ResetForNewRound();
        }
        
        // Wait a moment for UI updates
        yield return new WaitForSeconds(2.0f);
        
        // Assign monsters to players for this round (if master client)
        if (PhotonNetwork.IsMasterClient)
        {
            AssignMonstersToPlayers();
        }
        
        // Wait for monster assignments to sync
        yield return new WaitForSeconds(1.0f);
        
        // Start the battle phase
        SetGamePhase(GamePhase.Battle);
    }
    
private void AssignMonstersToPlayers()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Clear previous assignments
        playerMonsterPairs.Clear();
        
        // Get list of player actor numbers
        List<int> playerActors = new List<int>(players.Keys);
        
        if (playerActors.Count < 2)
        {
            Debug.LogError("Not enough players to assign monsters!");
            return;
        }
        
        // Sort the players to ensure consistent order
        playerActors.Sort();
        
        // Assign monsters in a round-robin fashion
        for (int i = 0; i < playerActors.Count; i++)
        {
            int playerActor = playerActors[i];
            int nextPlayerIndex = (i + 1) % playerActors.Count;
            int monsterOwner = playerActors[nextPlayerIndex];
            
            // Store the pairing
            playerMonsterPairs.Add(playerActor, monsterOwner);
            
            // Send RPC to set up the pairing
            photonView.RPC("RPC_SetOpponent", RpcTarget.All, playerActor, monsterOwner);
            
            Debug.Log($"Assigned player {playerActor} to fight monster owned by {monsterOwner}");
        }
    }


    
    [PunRPC]
    private void RPC_SetOpponent(int playerActorNumber, int monsterOwnerActorNumber)
    {
        // Set the player's opponent monster
        if (players.ContainsKey(playerActorNumber) && monsters.ContainsKey(monsterOwnerActorNumber))
        {
            PlayerController player = players[playerActorNumber];
            MonsterController monster = monsters[monsterOwnerActorNumber];
            
            player.SetOpponentMonster(monster);
            monster.SetOpponentPlayer(player);
            
            Debug.Log($"Set player {playerActorNumber} to fight monster owned by {monsterOwnerActorNumber}");
            
            // Update UI if this is the local player
            if (player.photonView.IsMine && battleUI != null)
            {
                battleUI.UpdateMonsterStats(monster);
            }
        }
    }
    
    public void InitializeBattlePhase()
    {
        Debug.Log("Initializing Battle Phase");
        
        // Reset players who have finished combat
        playersFinishedCombat.Clear();
        
        // Ensure we have prefabs spawned
        if (!hasSpawnedPrefabs)
        {
            Debug.LogWarning("Battle phase initialized before prefabs were spawned");
            return;
        }
        
        // Make sure we have the right player and monster references
        RefreshPlayerAndMonsterReferences();
        
        // Set all players and monsters to combat mode
        foreach (PlayerController player in players.Values)
        {
            player.IsInCombat = true;
        }
        
        foreach (MonsterController monster in monsters.Values)
        {
            monster.IsInCombat = true;
        }
        
        // Establish player-pet relationships
        EstablishPlayerPetRelationships();
        
        // IMPORTANT: Force UIManager to hide lobby and show battle UI
        if (uiManager != null)
        {
            // Ensure all panels are hidden first
            uiManager.HideAllPanels();
            
            // Show gameplay UI panel
            uiManager.ShowGameplayUI();
            
            Debug.Log("Forced UI transition to battle UI");
        }
        
        // Show battle UI for all players
        if (battleUI != null)
        {
            battleUI.Show();
            
            // Update player turn status
            PlayerController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                battleUI.ShowPlayerTurn(localPlayer.IsMyTurn);
            }
        }
        
        // Show battle overview UI
        if (battleUIManager != null && playerMonsterPairs.Count > 0)
        {
            battleUIManager.ShowBattleOverview(playerMonsterPairs);
        }
        
        // Show phase announcement
        ShowPhaseAnnouncement($"Battle Phase - Round {currentRound}");
        
        // Assign monsters to players and start combat turns (master client only)
        if (PhotonNetwork.IsMasterClient)
        {
            // Assign monsters to players for this round
            AssignMonstersToPlayers();
            
            // Start the first player's turn after a delay
            StartCoroutine(StartCombatTurns());
        }
    }


 [PunRPC]
    private void RPC_ShowBattleUI()
    {
        // This ensures all clients hide lobby UI and show battle UI
        Debug.Log("RPC: Showing Battle UI for all players");
        
        // First make sure lobby UI is hidden - using UIManager to hide all panels
        if (uiManager != null)
        {
            uiManager.HideAllPanels(); // Explicitly hide all panels including lobby
            uiManager.ShowGameplayUI();
        }
        
        // Show battle UI for the local player
        if (battleUI != null)
        {
            battleUI.Show();
            
            // Update player turn status
            PlayerController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                battleUI.ShowPlayerTurn(localPlayer.IsMyTurn);
            }
        }
        
        // Show battle overview UI
        if (battleUIManager != null && playerMonsterPairs.Count > 0)
        {
            battleUIManager.ShowBattleOverview(playerMonsterPairs);
        }
    }
    
    public void EstablishPlayerPetRelationships()
    {
        // For each player, set their pet monster
        foreach (var playerEntry in players)
        {
            int playerActorNumber = playerEntry.Key;
            PlayerController player = playerEntry.Value;
            
            // Find the monster owned by this player
            if (monsters.ContainsKey(playerActorNumber))
            {
                MonsterController pet = monsters[playerActorNumber];
                
                // Set the relationship
                player.SetPetMonster(pet);
                
                // Update battle UI if this is the local player
                if (player.photonView.IsMine && battleUI != null)
                {
                    battleUI.UpdatePetStats(pet);
                }
            }
        }
    }
    
    private IEnumerator StartCombatTurns()
    {
        // Wait for announcement to be seen
        yield return new WaitForSeconds(2.0f);
        
        // Start the first player's turn
        if (activePlayers.Count > 0)
        {
            isBattlePhaseActive = true;
            
            // Sort active players to ensure consistent order on all clients
            activePlayers.Sort();
            
            // Set first player's turn
            currentPlayerTurn = 0;
            
            // Force UI update for all players
            photonView.RPC("RPC_UpdateBattleUIState", RpcTarget.All);
            
            // Start the first player's turn
            StartPlayerTurn(activePlayers[currentPlayerTurn]);
        }
    }


 [PunRPC]
    private void RPC_UpdateBattleUIState()
    {
        Debug.Log("RPC: Updating Battle UI state");
        
        // Make sure lobby UI is hidden
        if (uiManager != null)
        {
            // Hide lobby panel explicitly
            if (uiManager.GetLobbyPanel() != null)
            {
                uiManager.GetLobbyPanel().SetActive(false);
            }
            
            // Show gameplay panel
            if (uiManager.GetGameplayPanel() != null)
            {
                uiManager.GetGameplayPanel().SetActive(true);
            }
        }
        
        // Make sure battle UI is shown
        if (battleUI != null)
        {
            battleUI.Show();
        }
    }
    
    private void StartPlayerTurn(int playerActorNumber)
    {
        Debug.Log($"Starting turn for player {playerActorNumber}");
        
        if (players.ContainsKey(playerActorNumber))
        {
            // Make sure all clients know whose turn it is
            photonView.RPC("RPC_StartPlayerTurn", RpcTarget.All, playerActorNumber);
            
            // Ensure UI is in proper state for all players
            photonView.RPC("RPC_UpdateBattleUIState", RpcTarget.All);
        }
        else
        {
            // Player not found, move to next
            Debug.LogWarning($"Player {playerActorNumber} not found, skipping turn");
            AdvanceToNextPlayer();
        }
    }



    
    [PunRPC]
    private void RPC_StartPlayerTurn(int playerActorNumber)
    {
        // Set the player's turn state
        if (players.ContainsKey(playerActorNumber))
        {
            PlayerController player = players[playerActorNumber];
            player.StartTurn();
            
            Debug.Log($"Started turn for player {player.PlayerName}");
            
            // Update UI to show whose turn it is for all players
            if (battleUI != null)
            {
                // For each client, check if it's their turn
                bool isMyTurn = player.photonView.IsMine;
                battleUI.ShowPlayerTurn(isMyTurn);
                
                // Debug log to track turn status
                Debug.Log($"Battle UI updated: isMyTurn={isMyTurn} for player {PhotonNetwork.LocalPlayer.NickName}");
            }
        }
    }


    
    public void PlayerEndedTurn()
    {
        if (!PhotonNetwork.IsMasterClient || !isBattlePhaseActive) return;
        
        // After player's turn, the monster gets a turn
        int currentPlayerActor = activePlayers[currentPlayerTurn];
        
        if (playerMonsterPairs.ContainsKey(currentPlayerActor))
        {
            int monsterOwnerActor = playerMonsterPairs[currentPlayerActor];
            
            if (monsters.ContainsKey(monsterOwnerActor))
            {
                // Start monster turn
                StartMonsterTurn(monsterOwnerActor, currentPlayerActor);
            }
            else
            {
                // Monster not found, advance to next player
                AdvanceToNextPlayer();
            }
        }
        else
        {
            // No monster paired, advance to next player
            AdvanceToNextPlayer();
        }
    }

    
    private void StartMonsterTurn(int monsterOwnerActor, int targetPlayerActor)
    {
        // Notify all clients of monster turn
        photonView.RPC("RPC_StartMonsterTurn", RpcTarget.All, monsterOwnerActor, targetPlayerActor);
    }
    
    [PunRPC]
    private void RPC_StartMonsterTurn(int monsterOwnerActor, int targetPlayerActor)
    {
        if (monsters.ContainsKey(monsterOwnerActor))
        {
            MonsterController monster = monsters[monsterOwnerActor];
            monster.TakeTurn();
            
            Debug.Log($"Started turn for monster owned by {PhotonNetwork.CurrentRoom.GetPlayer(monsterOwnerActor).NickName}");
        }
    }
    
    public void MonsterEndedTurn()
    {
        // This is called by the MonsterController when they end their turn
        if (PhotonNetwork.IsMasterClient && isBattlePhaseActive)
        {
            // After monster's turn, advance to next player
            AdvanceToNextPlayer();
        }
    }
    
    private void AdvanceToNextPlayer()
    {
        currentPlayerTurn = (currentPlayerTurn + 1) % activePlayers.Count;
        
        // If we've gone through all players, check if we should end the round
        if (currentPlayerTurn == 0 && playersFinishedCombat.Count == activePlayers.Count)
        {
            // All players have finished combat, move to draft phase
            isBattlePhaseActive = false;
            SetGamePhase(GamePhase.Draft);
        }
        else
        {
            // Start the next player's turn
            StartPlayerTurn(activePlayers[currentPlayerTurn]);
        }
    }

    
    public void PlayerDefeated(int playerActorNumber)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Add player to finished combat list if not already there
            if (!playersFinishedCombat.Contains(playerActorNumber))
            {
                playersFinishedCombat.Add(playerActorNumber);
                
                // Check if all players have finished combat
                if (playersFinishedCombat.Count == activePlayers.Count)
                {
                    // All players have finished combat, move to draft phase
                    isBattlePhaseActive = false;
                    SetGamePhase(GamePhase.Draft);
                }
            }
        }
    }
    
    public void MonsterDefeated(int monsterOwnerActorNumber, int playerActorNumber)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Award a point to the player
            if (playerScores.ContainsKey(playerActorNumber))
            {
                // Increment score
                playerScores[playerActorNumber]++;
                
                // Notify all clients
                photonView.RPC("RPC_UpdatePlayerScore", RpcTarget.All, playerActorNumber, playerScores[playerActorNumber]);
                
                // Award victory to player
                if (players.ContainsKey(playerActorNumber))
                {
                    players[playerActorNumber].WinRound();
                }
            }
            
            // Add player to finished combat list if not already there
            if (!playersFinishedCombat.Contains(playerActorNumber))
            {
                playersFinishedCombat.Add(playerActorNumber);
                
                // Check if all players have finished combat
                if (playersFinishedCombat.Count == activePlayers.Count)
                {
                    // All players have finished combat, move to draft phase
                    isBattlePhaseActive = false;
                    SetGamePhase(GamePhase.Draft);
                }
            }
        }
    }
    
    [PunRPC]
    private void RPC_UpdatePlayerScore(int playerActorNumber, int newScore)
    {
        // Update local score tracking
        if (playerScores.ContainsKey(playerActorNumber))
        {
            playerScores[playerActorNumber] = newScore;
            
            string playerName = PhotonNetwork.CurrentRoom.GetPlayer(playerActorNumber).NickName;
            Debug.Log($"Updated score for {playerName}: {newScore}");
        }
    }
    
    public void InitializeDraftPhase()
    {
        // Reset players who have finished drafting
        playersFinishedDraft.Clear();
        
        // Hide battle UI
        if (battleUI != null)
        {
            battleUI.Hide();
        }
        
        // Hide battle overview if active
        if (battleUIManager != null)
        {
            battleUIManager.HideBattleOverview();
        }
        
        // Show phase announcement
        ShowPhaseAnnouncement($"Draft Phase - Round {currentRound}");
        
        if (PhotonNetwork.IsMasterClient)
        {
            // Generate upgrades for drafting
            GenerateUpgrades();
            
            // Wait for a moment then start the draft
            StartCoroutine(StartDraftAfterDelay());
        }
    }
    
    private IEnumerator StartDraftAfterDelay()
    {
        yield return new WaitForSeconds(2.0f);
        
        // Initialize draft queues for each player
        draftQueues.Clear();
        
        // Ensure we have active players
        if (activePlayers.Count > 0)
        {
            // Set initial draft options for the first player
            int firstPlayerIndex = 0;
            int firstPlayerActor = activePlayers[firstPlayerIndex];
            
            draftQueues.Add(firstPlayerActor, new List<Upgrade>(availableUpgrades));
            
            // Start draft for first player
            photonView.RPC("RPC_StartPlayerDraft", RpcTarget.All, firstPlayerActor);
        }
        else
        {
            // No active players, move to next round
            FinishRound();
        }
    }
    
    private void GenerateUpgrades()
    {
        // Clear previous upgrades
        availableUpgrades.Clear();
        
        // Number of upgrades = player count * 3
        int upgradeCount = activePlayers.Count * 3;
        
        // Generate random upgrades
        for (int i = 0; i < upgradeCount; i++)
        {
            Upgrade upgrade = GenerateRandomUpgrade();
            availableUpgrades.Add(upgrade);
        }
    }
    
    private Upgrade GenerateRandomUpgrade()
    {
        // Randomly select upgrade type
        UpgradeType upgradeType = (UpgradeType)Random.Range(0, System.Enum.GetValues(typeof(UpgradeType)).Length);
        
        Upgrade upgrade = new Upgrade();
        upgrade.UpgradeType = upgradeType;
        
        // Set appropriate values based on type
        switch (upgradeType)
        {
            case UpgradeType.PlayerMaxHealth:
                upgrade.Value = Random.Range(5, 16);
                upgrade.Description = $"Increase player max health by {upgrade.Value}";
                break;
                
            case UpgradeType.PlayerStrength:
                upgrade.Value = Random.Range(1, 4);
                upgrade.Description = $"Increase player strength by {upgrade.Value}";
                break;
                
            case UpgradeType.PlayerDexterity:
                upgrade.Value = Random.Range(1, 4);
                upgrade.Description = $"Increase player dexterity by {upgrade.Value}";
                break;
                
            case UpgradeType.MonsterMaxHealth:
                upgrade.Value = Random.Range(5, 16);
                upgrade.Description = $"Increase monster max health by {upgrade.Value}";
                break;
                
            case UpgradeType.MonsterAttack:
                upgrade.Value = Random.Range(1, 4);
                upgrade.Description = $"Increase monster attack by {upgrade.Value}";
                break;
                
            case UpgradeType.MonsterDefense:
                upgrade.Value = Random.Range(1, 4);
                upgrade.Description = $"Increase monster defense by {upgrade.Value}";
                break;
                
            case UpgradeType.MonsterAI:
                upgrade.Value = Random.Range(0, System.Enum.GetValues(typeof(MonsterAI)).Length);
                upgrade.Description = $"Upgrade monster AI to {(MonsterAI)upgrade.Value}";
                break;
                
            case UpgradeType.PlayerCardAdd:
                // Generate a random card
                upgrade.CardId = GetRandomCardId();
                upgrade.Description = $"Add {GetCardNameFromId(upgrade.CardId)} to player's deck";
                break;
                
            case UpgradeType.MonsterCardAdd:
                // Generate a random monster card
                upgrade.CardId = GetRandomMonsterCardId();
                upgrade.Description = $"Add {GetMonsterCardNameFromId(upgrade.CardId)} to monster's deck";
                break;
        }
        
        return upgrade;
    }
    
    private string GetRandomCardId()
    {
        string[] cardIds = {
            "strike_basic",
            "defend_basic",
            "pet_support",
            "pet_power",
            "energy_potion",
            "draw_card",
            "strength_potion"
        };
        
        return cardIds[Random.Range(0, cardIds.Length)];
    }
    
    private string GetCardNameFromId(string cardId)
    {
        switch (cardId)
        {
            case "strike_basic": return "Strike";
            case "defend_basic": return "Defend";
            case "pet_support": return "Pet Support";
            case "pet_power": return "Power Up";
            case "energy_potion": return "Energy Potion";
            case "draw_card": return "Card Draw";
            case "strength_potion": return "Strength Potion";
            default: return "Unknown Card";
        }
    }
    
    private string GetRandomMonsterCardId()
    {
        string[] cardIds = {
            "monster_attack_basic",
            "monster_defend_basic",
            "monster_buff_strength",
            "monster_buff_dexterity",
            "monster_heal"
        };
        
        return cardIds[Random.Range(0, cardIds.Length)];
    }
    
    private string GetMonsterCardNameFromId(string cardId)
    {
        switch (cardId)
        {
            case "monster_attack_basic": return "Monster Strike";
            case "monster_defend_basic": return "Monster Defend";
            case "monster_buff_strength": return "Monster Rage";
            case "monster_buff_dexterity": return "Monster Agility";
            case "monster_heal": return "Monster Heal";
            default: return "Unknown Monster Card";
        }
    }
    
    [PunRPC]
    private void RPC_StartPlayerDraft(int playerActorNumber)
    {
        // If this is our player, show draft UI
        if (playerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            if (draftQueues.ContainsKey(playerActorNumber) && draftQueues[playerActorNumber].Count > 0)
            {
                ShowDraftUI(draftQueues[playerActorNumber]);
            }
            else
            {
                // No upgrades to select, skip this player's draft
                if (PhotonNetwork.IsMasterClient)
                {
                    PlayerFinishedDraft(playerActorNumber, -1);
                }
                else
                {
                    photonView.RPC("RPC_PlayerNoUpgrades", RpcTarget.MasterClient, playerActorNumber);
                }
            }
        }
    }
    
    [PunRPC]
    private void RPC_PlayerNoUpgrades(int playerActorNumber)
    {
        // Master client handles a player with no upgrades
        if (PhotonNetwork.IsMasterClient)
        {
            PlayerFinishedDraft(playerActorNumber, -1);
        }
    }
    
    public void PlayerSelectedUpgrade(int upgradeIndex)
    {
        int localPlayerActor = PhotonNetwork.LocalPlayer.ActorNumber;
        
        if (draftQueues.ContainsKey(localPlayerActor) && upgradeIndex >= 0 && upgradeIndex < draftQueues[localPlayerActor].Count)
        {
            // Hide draft UI
            HideDraftUI();
            
            // Tell the server we've made our selection
            if (PhotonNetwork.IsMasterClient)
            {
                PlayerFinishedDraft(localPlayerActor, upgradeIndex);
            }
            else
            {
                photonView.RPC("RPC_PlayerMadeDraftSelection", RpcTarget.MasterClient, localPlayerActor, upgradeIndex);
            }
        }
    }
    
    [PunRPC]
    private void RPC_PlayerMadeDraftSelection(int playerActorNumber, int upgradeIndex)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PlayerFinishedDraft(playerActorNumber, upgradeIndex);
        }
    }
    
    private void PlayerFinishedDraft(int playerActorNumber, int selectedUpgradeIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Add player to finished draft list
        if (!playersFinishedDraft.Contains(playerActorNumber))
        {
            playersFinishedDraft.Add(playerActorNumber);
        }
        
        // Apply the selected upgrade
        if (draftQueues.ContainsKey(playerActorNumber) && selectedUpgradeIndex >= 0 && selectedUpgradeIndex < draftQueues[playerActorNumber].Count)
        {
            Upgrade selectedUpgrade = draftQueues[playerActorNumber][selectedUpgradeIndex];
            
            // Apply the upgrade
            ApplyUpgrade(playerActorNumber, selectedUpgrade);
            
            // Remove selected upgrade
            draftQueues[playerActorNumber].RemoveAt(selectedUpgradeIndex);
            
            // Pass remaining upgrades to next player
            PassUpgradesToNextPlayer(playerActorNumber);
        }
        else
        {
            // No upgrade selected or no upgrades available
            PassUpgradesToNextPlayer(playerActorNumber);
        }
        
        // Check if all players have finished drafting
        if (playersFinishedDraft.Count >= activePlayers.Count)
        {
            // All players have finished drafting, move to next round
            FinishRound();
        }
    }
    
    private void PassUpgradesToNextPlayer(int currentPlayerActor)
    {
        if (!draftQueues.ContainsKey(currentPlayerActor) || draftQueues[currentPlayerActor].Count == 0)
        {
            // No upgrades to pass
            return;
        }
        
        // Find the index of the current player in active players
        int currentPlayerIndex = activePlayers.IndexOf(currentPlayerActor);
        
        // Calculate next player index
        int nextPlayerIndex = (currentPlayerIndex + 1) % activePlayers.Count;
        int nextPlayerActor = activePlayers[nextPlayerIndex];
        
        // If next player already has a queue, append to it; otherwise create new queue
        if (draftQueues.ContainsKey(nextPlayerActor))
        {
            draftQueues[nextPlayerActor].AddRange(draftQueues[currentPlayerActor]);
        }
        else
        {
            draftQueues.Add(nextPlayerActor, new List<Upgrade>(draftQueues[currentPlayerActor]));
        }
        
        // Clear current player's queue
        draftQueues[currentPlayerActor].Clear();
        
        // Start draft for next player if they haven't finished yet
        if (!playersFinishedDraft.Contains(nextPlayerActor))
        {
            photonView.RPC("RPC_StartPlayerDraft", RpcTarget.All, nextPlayerActor);
        }
        else
        {
            // Next player already finished, pass upgrades again
            PassUpgradesToNextPlayer(nextPlayerActor);
        }
    }
    
    private void ApplyUpgrade(int playerActorNumber, Upgrade upgrade)
    {
        // Apply the upgrade effect based on type
        photonView.RPC("RPC_ApplyUpgrade", RpcTarget.All, playerActorNumber, 
            (int)upgrade.UpgradeType, upgrade.Value, upgrade.CardId);
    }
    
    [PunRPC]
    private void RPC_ApplyUpgrade(int playerActorNumber, int upgradeTypeInt, int value, string cardId)
    {
        UpgradeType upgradeType = (UpgradeType)upgradeTypeInt;
        
        // Get player and monster
        PlayerController player = null;
        MonsterController monster = null;
        
        if (players.ContainsKey(playerActorNumber))
        {
            player = players[playerActorNumber];
        }
        
        if (monsters.ContainsKey(playerActorNumber))
        {
            monster = monsters[playerActorNumber];
        }
        
        // Apply upgrade based on type
        switch (upgradeType)
        {
            case UpgradeType.PlayerMaxHealth:
                if (player != null)
                {
                    player.MaxHealth += value;
                    player.CurrentHealth += value;
                }
                break;
                
            case UpgradeType.PlayerStrength:
                if (player != null)
                {
                    player.StrengthModifier += value;
                }
                break;
                
            case UpgradeType.PlayerDexterity:
                if (player != null)
                {
                    player.DexterityModifier += value;
                }
                break;
                
            case UpgradeType.MonsterMaxHealth:
                if (monster != null)
                {
                    monster.MaxHealth += value;
                    monster.CurrentHealth += value;
                }
                break;
                
            case UpgradeType.MonsterAttack:
                if (monster != null)
                {
                    monster.AttackPower += value;
                }
                break;
                
            case UpgradeType.MonsterDefense:
                if (monster != null)
                {
                    monster.DefensePower += value;
                }
                break;
                
            case UpgradeType.MonsterAI:
                if (monster != null)
                {
                    MonsterUpgrade aiUpgrade = new MonsterUpgrade
                    {
                        UpgradeType = MonsterUpgradeType.AI,
                        Value = value,
                        Description = $"Upgrade AI to {(MonsterAI)value}"
                    };
                    monster.UpgradeMonster(aiUpgrade);
                }
                break;
                
            case UpgradeType.PlayerCardAdd:
                if (player != null && !string.IsNullOrEmpty(cardId))
                {
                    // Create a card based on cardId
                    Card card = CreateCardFromId(cardId);
                    if (card != null)
                    {
                        player.AddCardToDeck(card);
                    }
                }
                break;
                
            case UpgradeType.MonsterCardAdd:
                // This would need monster deck management
                // Not implemented in this version
                break;
        }
        
        // Update battle UI if this is for the local player
        if (player != null && player.photonView.IsMine && battleUI != null)
        {
            battleUI.UpdatePlayerStats(player);
        }
    }
    
    private Card CreateCardFromId(string cardId)
    {
        // This would be expanded with a proper card database
        // For now, just create some basic cards
        
        switch (cardId)
        {
            case "strike_basic":
                return new Card
                {
                    CardId = "strike_basic",
                    CardName = "Strike",
                    CardDescription = "Deal 6 damage.",
                    EnergyCost = 1,
                    CardType = CardType.Attack,
                    CardRarity = CardRarity.Basic,
                    DamageAmount = 6
                };
                
            case "defend_basic":
                return new Card
                {
                    CardId = "defend_basic",
                    CardName = "Defend",
                    CardDescription = "Gain 5 Block.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Basic,
                    BlockAmount = 5
                };
                
            case "pet_support":
                return new Card
                {
                    CardId = "pet_support",
                    CardName = "Pet Support",
                    CardDescription = "Give your pet 4 Block.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Basic,
                    BlockAmount = 4
                };
                
            case "pet_power":
                return new Card
                {
                    CardId = "pet_power",
                    CardName = "Power Up",
                    CardDescription = "Give your pet 2 Strength.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Basic,
                    StrengthModifier = 2
                };
                
            case "energy_potion":
                return new Card
                {
                    CardId = "energy_potion",
                    CardName = "Energy Potion",
                    CardDescription = "Gain 2 Energy.",
                    EnergyCost = 0,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Common,
                    // Energy gain would need special handling
                };
                
            case "draw_card":
                return new Card
                {
                    CardId = "draw_card",
                    CardName = "Card Draw",
                    CardDescription = "Draw 2 cards.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Common,
                    DrawAmount = 2
                };
                
            case "strength_potion":
                return new Card
                {
                    CardId = "strength_potion",
                    CardName = "Strength Potion",
                    CardDescription = "Gain 2 Strength.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Common,
                    StrengthModifier = 2
                };
                
            default:
                Debug.LogWarning("Unknown card ID: " + cardId);
                return null;
        }
    }
    
    private void FinishRound()
    {
        // Check if a player has won the game
        CheckForGameWinner();
        
        // Advance to next round if no winner
        if (currentPhase != GamePhase.GameOver)
        {
            currentRound++;
            
            if (currentRound > maxRounds)
            {
                // End game after max rounds
                SetGamePhase(GamePhase.GameOver);
                
                // Find player with highest score
                int highestScore = -1;
                int winnerActorNumber = -1;
                
                foreach (var score in playerScores)
                {
                    if (score.Value > highestScore)
                    {
                        highestScore = score.Value;
                        winnerActorNumber = score.Key;
                    }
                }
                
                if (winnerActorNumber != -1)
                {
                    string winnerName = PhotonNetwork.CurrentRoom.GetPlayer(winnerActorNumber).NickName;
                    ShowWinner(winnerName);
                }
            }
            else
            {
                // Start the next round
                ShowPhaseAnnouncement($"Round {currentRound} Starting");
                StartCoroutine(SetupRound());
            }
        }
    }
    
    public void CheckForGameWinner()
    {
        foreach (var score in playerScores)
        {
            if (score.Value >= pointsToWin)
            {
                // This player has won
                SetGamePhase(GamePhase.GameOver);
                
                string winnerName = PhotonNetwork.CurrentRoom.GetPlayer(score.Key).NickName;
                ShowWinner(winnerName);
                
                // Notify all players about the winner
                photonView.RPC("RPC_AnnounceWinner", RpcTarget.All, score.Key, winnerName);
                break;
            }
        }
    }
    
    [PunRPC]
    private void RPC_AnnounceWinner(int winnerActorNumber, string winnerName)
    {
        // Show game over UI with winner announcement
        if (winnerText != null)
        {
            winnerText.text = $"Winner: {winnerName} with {playerScores[winnerActorNumber]} points!";
        }
        
        // Show game over panel
        ShowGameOver();
    }
    
    private void ShowGameOver()
    {
        // Display game over UI
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        // Hide battle UI
        if (battleUI != null)
        {
            battleUI.Hide();
        }
        
        // Hide battle overview
        if (battleUIManager != null)
        {
            battleUIManager.HideBattleOverview();
        }
    }
    
    private void ShowWinner(string winnerName)
    {
        // Display winner text
        if (winnerText != null)
        {
            winnerText.text = "Winner: " + winnerName;
        }
        
        // Show game over panel
        ShowGameOver();
    }
    
    #endregion
    
    #region UI Methods
    
    private void CreateGameplayUI()
    {
        // Find the main canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;
        
        // Create phase announcement panel
        phaseAnnouncementPanel = new GameObject("PhaseAnnouncementPanel");
        phaseAnnouncementPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform announcementRect = phaseAnnouncementPanel.AddComponent<RectTransform>();
        announcementRect.anchorMin = new Vector2(0.5f, 0.5f);
        announcementRect.anchorMax = new Vector2(0.5f, 0.5f);
        announcementRect.sizeDelta = new Vector2(600, 100);
        
        Image announcementBg = phaseAnnouncementPanel.AddComponent<Image>();
        announcementBg.color = new Color(0, 0, 0, 0.8f);
        
        // Create announcement text
        GameObject textObj = new GameObject("AnnouncementText");
        textObj.transform.SetParent(phaseAnnouncementPanel.transform, false);
        
        phaseAnnouncementText = textObj.AddComponent<TextMeshProUGUI>();
        phaseAnnouncementText.fontSize = 36;
        phaseAnnouncementText.alignment = TextAlignmentOptions.Center;
        phaseAnnouncementText.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        // Create game over panel
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform gameOverRect = gameOverPanel.AddComponent<RectTransform>();
        gameOverRect.anchorMin = Vector2.zero;
        gameOverRect.anchorMax = Vector2.one;
        gameOverRect.sizeDelta = Vector2.zero;
        
        Image gameOverBg = gameOverPanel.AddComponent<Image>();
        gameOverBg.color = new Color(0, 0, 0, 0.9f);
        
        // Create game over text
        GameObject gameOverTextObj = new GameObject("GameOverText");
        gameOverTextObj.transform.SetParent(gameOverPanel.transform, false);
        
        TextMeshProUGUI gameOverText = gameOverTextObj.AddComponent<TextMeshProUGUI>();
        gameOverText.fontSize = 48;
        gameOverText.alignment = TextAlignmentOptions.Center;
        gameOverText.color = Color.white;
        gameOverText.text = "Game Over";
        
        RectTransform gameOverTextRect = gameOverTextObj.GetComponent<RectTransform>();
        gameOverTextRect.anchorMin = new Vector2(0.5f, 0.6f);
        gameOverTextRect.anchorMax = new Vector2(0.5f, 0.7f);
        gameOverTextRect.sizeDelta = new Vector2(400, 100);
        
        // Create winner text
        GameObject winnerTextObj = new GameObject("WinnerText");
        winnerTextObj.transform.SetParent(gameOverPanel.transform, false);
        
        winnerText = winnerTextObj.AddComponent<TextMeshProUGUI>();
        winnerText.fontSize = 36;
        winnerText.alignment = TextAlignmentOptions.Center;
        winnerText.color = Color.white;
        winnerText.text = "Winner: ";
        
       RectTransform winnerTextRect = winnerTextObj.GetComponent<RectTransform>();
        winnerTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        winnerTextRect.anchorMax = new Vector2(0.5f, 0.6f);
        winnerTextRect.sizeDelta = new Vector2(600, 80);
        
        // Create return to lobby button
        GameObject returnButtonObj = new GameObject("ReturnButton");
        returnButtonObj.transform.SetParent(gameOverPanel.transform, false);
        
        Button returnButton = returnButtonObj.AddComponent<Button>();
        Image returnButtonImage = returnButtonObj.AddComponent<Image>();
        returnButtonImage.color = new Color(0.2f, 0.4f, 0.8f, 1);
        
        RectTransform returnButtonRect = returnButtonObj.GetComponent<RectTransform>();
        returnButtonRect.anchorMin = new Vector2(0.5f, 0.35f);
        returnButtonRect.anchorMax = new Vector2(0.5f, 0.45f);
        returnButtonRect.sizeDelta = new Vector2(200, 60);
        
        GameObject returnTextObj = new GameObject("ReturnText");
        returnTextObj.transform.SetParent(returnButtonObj.transform, false);
        
        TextMeshProUGUI returnText = returnTextObj.AddComponent<TextMeshProUGUI>();
        returnText.fontSize = 24;
        returnText.alignment = TextAlignmentOptions.Center;
        returnText.color = Color.white;
        returnText.text = "Return to Lobby";
        
        RectTransform returnTextRect = returnTextObj.GetComponent<RectTransform>();
        returnTextRect.anchorMin = Vector2.zero;
        returnTextRect.anchorMax = Vector2.one;
        returnTextRect.sizeDelta = Vector2.zero;
        
        // Button action
        returnButton.onClick.AddListener(() => {
            if (networkManager != null)
            {
                networkManager.LeaveRoom();
            }
        });
        
        // Create draft panel
        draftPanel = new GameObject("DraftPanel");
        draftPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform draftRect = draftPanel.AddComponent<RectTransform>();
        draftRect.anchorMin = new Vector2(0.5f, 0.5f);
        draftRect.anchorMax = new Vector2(0.5f, 0.5f);
        draftRect.sizeDelta = new Vector2(800, 500);
        
        Image draftBg = draftPanel.AddComponent<Image>();
        draftBg.color = new Color(0, 0, 0, 0.9f);
        
        // Initially hide panels
        phaseAnnouncementPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        draftPanel.SetActive(false);
    }
    
    private void ShowPhaseAnnouncement(string text)
    {
        if (phaseAnnouncementPanel != null && phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = text;
            phaseAnnouncementPanel.SetActive(true);
            
            // Hide after delay
            StartCoroutine(HidePanelAfterDelay(phaseAnnouncementPanel, 2.0f));
        }
    }
    
    private IEnumerator HidePanelAfterDelay(GameObject panel, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
    
    private void ShowDraftUI(List<Upgrade> upgrades)
    {
        if (draftPanel == null) return;
        
        // Clear existing UI
        foreach (Transform child in draftPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        // Add title
        GameObject titleObj = new GameObject("DraftTitle");
        titleObj.transform.SetParent(draftPanel.transform, false);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 30;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.text = "Select an Upgrade";
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.85f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = Vector2.zero;
        
        // Add subtitle explaining draft mechanics
        GameObject subtitleObj = new GameObject("DraftSubtitle");
        subtitleObj.transform.SetParent(draftPanel.transform, false);
        
        TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = 16;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.8f, 0.8f, 0.8f, 1);
        subtitleText.text = "Choose one upgrade. Remaining options will be passed to the next player.";
        
        RectTransform subtitleRect = subtitleObj.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0, 0.8f);
        subtitleRect.anchorMax = new Vector2(1, 0.85f);
        subtitleRect.sizeDelta = Vector2.zero;
        
        // Create upgrade options container with scroll view for many options
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(draftPanel.transform, false);
        
        RectTransform scrollRect = scrollViewObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.05f, 0.1f);
        scrollRect.anchorMax = new Vector2(0.95f, 0.8f);
        scrollRect.sizeDelta = Vector2.zero;
        
        ScrollRect scrollView = scrollViewObj.AddComponent<ScrollRect>();
        
        // Create viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        
        Mask viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Create content container
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.sizeDelta = new Vector2(0, Mathf.Max(500, upgrades.Count * 80));
        
        // Add grid layout
        GridLayoutGroup gridLayout = contentObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(180, 220);
        gridLayout.spacing = new Vector2(20, 20);
        gridLayout.padding = new RectOffset(10, 10, 10, 10);
        gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
        
        // Add upgrade options
        for (int i = 0; i < upgrades.Count; i++)
        {
            CreateUpgradeOption(contentObj, upgrades[i], i);
        }
        
        // Configure scroll view
        scrollView.content = contentRect;
        scrollView.viewport = viewportRect;
        scrollView.horizontal = false;
        scrollView.vertical = true;
        
        // Show panel
        draftPanel.SetActive(true);
    }
    
    private void CreateUpgradeOption(GameObject parent, Upgrade upgrade, int index)
    {
        GameObject optionObj = new GameObject("Option_" + index);
        optionObj.transform.SetParent(parent.transform, false);
        
        Image optionBg = optionObj.AddComponent<Image>();
        optionBg.color = GetUpgradeColor(upgrade.UpgradeType);
        
        Button optionButton = optionObj.AddComponent<Button>();
        
        // Set button colors
        ColorBlock colors = optionButton.colors;
        colors.normalColor = GetUpgradeColor(upgrade.UpgradeType);
        colors.highlightedColor = new Color(
            colors.normalColor.r + 0.1f,
            colors.normalColor.g + 0.1f,
            colors.normalColor.b + 0.1f,
            colors.normalColor.a
        );
        colors.pressedColor = new Color(
            colors.normalColor.r - 0.1f,
            colors.normalColor.g - 0.1f,
            colors.normalColor.b - 0.1f,
            colors.normalColor.a
        );
        optionButton.colors = colors;
        
        // Set button action
        int optionIndex = index; // Local copy for closure
        optionButton.onClick.AddListener(() => PlayerSelectedUpgrade(optionIndex));
        
        // Create vertical layout
        VerticalLayoutGroup vertLayout = optionObj.AddComponent<VerticalLayoutGroup>();
        vertLayout.padding = new RectOffset(5, 5, 5, 5);
        vertLayout.spacing = 5;
        vertLayout.childAlignment = TextAnchor.UpperCenter;
        vertLayout.childForceExpandWidth = true;
        vertLayout.childForceExpandHeight = false;
        
        // Create icon
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(optionObj.transform, false);
        
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(1, 1, 1, 0.8f);
        
        // Set icon based on upgrade type
        iconImage.sprite = CreateCircleSprite(); // You could create different sprites based on upgrade type
        
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(50, 50);
        
        // Create title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(optionObj.transform, false);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 16;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.text = GetUpgradeTitle(upgrade.UpgradeType);
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 30);
        
        // Create description
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(optionObj.transform, false);
        
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 14;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        descText.text = upgrade.Description;
        
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(0, 80);
    }
    
    // Helper method to create a simple circle sprite for icons
    private Sprite CreateCircleSprite()
    {
        int width = 64;
        int height = 64;
        Texture2D texture = new Texture2D(width, height);
        
        Color[] colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distX = x - width / 2;
                float distY = y - height / 2;
                float dist = Mathf.Sqrt(distX * distX + distY * distY);
                float radius = width / 2;
                
                if (dist <= radius)
                {
                    colors[y * width + x] = Color.white;
                }
                else
                {
                    colors[y * width + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
    
    private Color GetUpgradeColor(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.PlayerMaxHealth:
            case UpgradeType.PlayerStrength:
            case UpgradeType.PlayerDexterity:
            case UpgradeType.PlayerCardAdd:
                return new Color(0.2f, 0.6f, 0.8f, 1); // Blue for player upgrades
                
            case UpgradeType.MonsterMaxHealth:
            case UpgradeType.MonsterAttack:
            case UpgradeType.MonsterDefense:
            case UpgradeType.MonsterAI:
            case UpgradeType.MonsterCardAdd:
                return new Color(0.8f, 0.2f, 0.2f, 1); // Red for monster upgrades
                
            default:
                return new Color(0.5f, 0.5f, 0.5f, 1); // Gray for unknown
        }
    }
    
    private string GetUpgradeTitle(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.PlayerMaxHealth:
                return "Player Health";
            case UpgradeType.PlayerStrength:
                return "Player Strength";
            case UpgradeType.PlayerDexterity:
                return "Player Dexterity";
            case UpgradeType.MonsterMaxHealth:
                return "Monster Health";
            case UpgradeType.MonsterAttack:
                return "Monster Attack";
            case UpgradeType.MonsterDefense:
                return "Monster Defense";
            case UpgradeType.MonsterAI:
                return "Monster AI";
            case UpgradeType.PlayerCardAdd:
                return "New Player Card";
            case UpgradeType.MonsterCardAdd:
                return "New Monster Card";
            default:
                return "Unknown";
        }
    }
    
    private void HideDraftUI()
    {
        if (draftPanel != null)
        {
            draftPanel.SetActive(false);
        }
    }
    
    // Public utility methods for access by other managers
    public PlayerController GetPlayerById(int actorNumber)
    {
        if (players.ContainsKey(actorNumber))
        {
            return players[actorNumber];
        }
        return null;
    }
    
    public MonsterController GetMonsterById(int actorNumber)
    {
        if (monsters.ContainsKey(actorNumber))
        {
            return monsters[actorNumber];
        }
        return null;
    }
    
    #endregion
}

#region Upgrade Related Classes

public enum UpgradeType
{
    PlayerMaxHealth,
    PlayerStrength,
    PlayerDexterity,
    MonsterMaxHealth,
    MonsterAttack,
    MonsterDefense,
    MonsterAI,
    PlayerCardAdd,
    MonsterCardAdd
}

[System.Serializable]
public class Upgrade
{
    public UpgradeType UpgradeType;
    public int Value;
    public string Description;
    public string CardId;
}

#endregion