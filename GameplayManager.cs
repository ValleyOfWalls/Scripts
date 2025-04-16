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
    
    // References to other managers
    [Header("Managers")]
    private UIManager uiManager;
    private NetworkManager networkManager;
    private DeckManager deckManager;
    
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
    }
    
    private void Start()
    {
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
        if (uiManager != null)
        {
            uiManager.ShowPhaseAnnouncement("Game Starting - Round 1");
        }
        
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
                    if (uiManager != null)
                    {
                        uiManager.ShowGameOver(winnerName);
                    }
                }
                else
                {
                    if (uiManager != null)
                    {
                        uiManager.ShowGameOver("Nobody - All players left!");
                    }
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
            
            // If this is the local player, set up managers
            if (player.photonView.IsMine)
            {
                player.SetupDeckManager(deckManager);
                player.SetupCardUIManager(uiManager);
                
                // Update UI with player stats
                if (uiManager != null)
                {
                    uiManager.UpdatePlayerStats(player);
                }
            }
            
            // Check if we should initialize battles
            if (currentPhase == GamePhase.Battle && hasSpawnedPrefabs)
            {
                // If we're already in battle phase, update UI and initialize relationships
                if (uiManager != null)
                {
                    uiManager.ShowBattleUI();
                }
                
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
                if (uiManager != null)
                {
                    uiManager.ShowBattleUI();
                }
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
        
        // Update Battle UI
        if (uiManager != null)
        {
            PlayerController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                uiManager.UpdatePlayerStats(localPlayer);
                
                // Find opponent monster based on pairings
                int playerActorNumber = localPlayer.photonView.Owner.ActorNumber;
                if (playerMonsterPairs.ContainsKey(playerActorNumber))
                {
                    int monsterOwnerActorNumber = playerMonsterPairs[playerActorNumber];
                    if (monsters.ContainsKey(monsterOwnerActorNumber))
                    {
                        MonsterController opponentMonster = monsters[monsterOwnerActorNumber];
                        uiManager.UpdateMonsterStats(opponentMonster);
                    }
                }
                
                // Find player's pet monster
                if (monsters.ContainsKey(playerActorNumber))
                {
                    MonsterController petMonster = monsters[playerActorNumber];
                    uiManager.UpdatePetStats(petMonster);
                }
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
                if (uiManager != null)
                {
                    uiManager.ShowGameOver(""); // Winner name will be set separately
                }
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
        
        // Assign each player to fight another player's pet
        // Player 1 fights Player 2's pet, Player 2 fights Player 3's pet, etc.
        // Last player fights Player 1's pet (round-robin)
        for (int i = 0; i < playerActors.Count; i++)
        {
            int playerActor = playerActors[i];
            int nextPlayerIndex = (i + 1) % playerActors.Count;
            int opponentPetOwner = playerActors[nextPlayerIndex];
            
            // Store the pairing: player -> opponent's pet owner
            playerMonsterPairs.Add(playerActor, opponentPetOwner);
            
            // Send RPC to set up the pairing
            photonView.RPC("RPC_SetOpponent", RpcTarget.All, playerActor, opponentPetOwner);
            
            Debug.Log($"Assigned player {playerActor} to fight pet owned by {opponentPetOwner}");
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
            if (player.photonView.IsMine && uiManager != null)
            {
                uiManager.UpdateMonsterStats(monster);
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
        
        // Force UIManager to hide lobby and show battle UI
        if (uiManager != null)
        {
            // Ensure all panels are hidden first
            uiManager.HideAllPanels();
            
            // Show gameplay UI panel
            uiManager.ShowGameplayUI();
            
            Debug.Log("Forced UI transition to battle UI");
            
            // Update player turn status
            PlayerController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                uiManager.ShowPlayerTurn(localPlayer.IsMyTurn);
            }
            
            // Show battle overview UI if we have pairings
            if (playerMonsterPairs.Count > 0)
            {
                uiManager.ShowBattleOverview(playerMonsterPairs);
            }
            
            // Show phase announcement
            uiManager.ShowPhaseAnnouncement($"Battle Phase - Round {currentRound}");
        }
        
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
            
            // Update player turn status
            PlayerController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                uiManager.ShowPlayerTurn(localPlayer.IsMyTurn);
            }
            
            // Show battle overview UI
            if (playerMonsterPairs.Count > 0)
            {
                uiManager.ShowBattleOverview(playerMonsterPairs);
            }
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
                
                // Update UI if this is the local player
                if (player.photonView.IsMine && uiManager != null)
                {
                    uiManager.UpdatePetStats(pet);
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
        
        // Make sure lobby UI is hidden and gameplay UI is shown
        if (uiManager != null)
        {
            // Hide all panels
            uiManager.HideAllPanels();
            
            // Show gameplay panel
            uiManager.ShowGameplayUI();
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
            if (uiManager != null)
            {
                // For each client, check if it's their turn
                bool isMyTurn = player.photonView.IsMine;
                uiManager.ShowPlayerTurn(isMyTurn);
                
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
        if (uiManager != null)
        {
            uiManager.HideBattleUI();
            
            // Show phase announcement
            uiManager.ShowPhaseAnnouncement($"Draft Phase - Round {currentRound}");
        }
        
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
                if (uiManager != null)
                {
                    uiManager.ShowDraftUI(draftQueues[playerActorNumber]);
                }
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
    
    [PunRPC]
    public void RPC_PlayerMadeDraftSelection(int playerActorNumber, int upgradeIndex)
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
        
        // Update UI if this is for the local player
        if (player != null && player.photonView.IsMine && uiManager != null)
        {
            uiManager.UpdatePlayerStats(player);
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
                    if (uiManager != null)
                    {
                        uiManager.ShowGameOver(winnerName);
                    }
                }
            }
            else
            {
                // Start the next round
                if (uiManager != null)
                {
                    uiManager.ShowPhaseAnnouncement($"Round {currentRound} Starting");
                }
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
                if (uiManager != null)
                {
                    uiManager.ShowGameOver(winnerName);
                }
                
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
        if (uiManager != null)
        {
            uiManager.ShowGameOver(winnerName);
        }
    }
    
    #endregion
    
    #region Public Utility Methods
    
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