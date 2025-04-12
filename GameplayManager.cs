using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public class GameplayManager : MonoBehaviourPunCallbacks
{
    #region Variables and Properties

    // Game parameters
    [Header("Game Parameters")]
    [SerializeField] private int pointsToWin = 3;
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
        
        // Setup cross-references
        deckManager.SetupCardUIManager(cardUIManager);
        
        // Create UI elements
        CreateGameplayUI();
    }
    
    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Initialize the game when all players are ready
            if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
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
    
    #endregion
    
    #region Player and Monster Registration
    
    public void RegisterPlayer(PlayerController player)
    {
        int actorNumber = player.photonView.Owner.ActorNumber;
        
        if (!players.ContainsKey(actorNumber))
        {
            players.Add(actorNumber, player);
            playerScores.Add(actorNumber, 0);
            activePlayers.Add(actorNumber);
            
            Debug.Log($"Player registered: {player.photonView.Owner.NickName} (ActorNumber: {actorNumber})");
            
            // If this is the local player, set up UI managers
            if (player.photonView.IsMine)
            {
                player.SetupDeckManager(deckManager);
                player.SetupCardUIManager(cardUIManager);
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
        
        // Assign monsters to players for this round
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
        // Clear previous assignments
        playerMonsterPairs.Clear();
        
        // Get list of player actor numbers
        List<int> playerActors = new List<int>(players.Keys);
        
        // If odd number of players, one player will face their own monster
        bool oddNumberOfPlayers = playerActors.Count % 2 != 0;
        
        // Shuffle the player list
        for (int i = 0; i < playerActors.Count; i++)
        {
            int temp = playerActors[i];
            int randomIndex = Random.Range(i, playerActors.Count);
            playerActors[i] = playerActors[randomIndex];
            playerActors[randomIndex] = temp;
        }
        
        // Assign monsters - each player faces the next player's monster
        for (int i = 0; i < playerActors.Count; i++)
        {
            int playerActor = playerActors[i];
            int monsterOwner;
            
            if (i < playerActors.Count - 1)
            {
                // Player faces the next player's monster
                monsterOwner = playerActors[i + 1];
            }
            else
            {
                // Last player faces the first player's monster
                monsterOwner = playerActors[0];
            }
            
            // Store the pairing
            playerMonsterPairs.Add(playerActor, monsterOwner);
            
            // Send RPC to set up the pairing
            photonView.RPC("RPC_SetOpponent", RpcTarget.All, playerActor, monsterOwner);
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
        }
    }
    
    public void InitializeBattlePhase()
    {
        // Reset players who have finished combat
        playersFinishedCombat.Clear();
        
        // Set all players and monsters to combat mode
        foreach (PlayerController player in players.Values)
        {
            player.IsInCombat = true;
        }
        
        foreach (MonsterController monster in monsters.Values)
        {
            monster.IsInCombat = true;
        }
        
        // Show phase announcement
        ShowPhaseAnnouncement($"Battle Phase - Round {currentRound}");
        
        // Start combat for each player
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(StartCombatTurns());
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
            
            // Set first player's turn
            currentPlayerTurn = 0;
            StartPlayerTurn(activePlayers[currentPlayerTurn]);
        }
    }
    
    private void StartPlayerTurn(int playerActorNumber)
    {
        if (players.ContainsKey(playerActorNumber))
        {
            // Notify all clients of turn change
            photonView.RPC("RPC_StartPlayerTurn", RpcTarget.All, playerActorNumber);
        }
        else
        {
            // Player not found, move to next
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
            
            Debug.Log($"Started turn for player {player.photonView.Owner.NickName}");
        }
    }
    
    public void PlayerEndedTurn()
    {
        // This is called by the PlayerController when they end their turn
        if (PhotonNetwork.IsMasterClient && isBattlePhaseActive)
        {
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
        
        // If we've gone through all players, start a new round of turns
        if (currentPlayerTurn == 0)
        {
            // Check if all players have finished combat
            if (playersFinishedCombat.Count == activePlayers.Count)
            {
                // All players have finished combat, move to draft phase
                isBattlePhaseActive = false;
                SetGamePhase(GamePhase.Draft);
            }
            else
            {
                // Start a new round of turns
                StartPlayerTurn(activePlayers[currentPlayerTurn]);
            }
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
        
        // Set initial draft options for the first player
        int firstPlayerIndex = 0;
        int firstPlayerActor = activePlayers[firstPlayerIndex];
        
        draftQueues.Add(firstPlayerActor, new List<Upgrade>(availableUpgrades));
        
        // Start draft for first player
        photonView.RPC("RPC_StartPlayerDraft", RpcTarget.All, firstPlayerActor);
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
                // We'd need to generate a random card here
                upgrade.CardId = "basic_strike";  // Placeholder
                upgrade.Description = "Add a basic Strike card to player's deck";
                break;
                
            case UpgradeType.MonsterCardAdd:
                // We'd need to generate a random card here
                upgrade.CardId = "monster_attack";  // Placeholder
                upgrade.Description = "Add a basic Attack card to monster's deck";
                break;
        }
        
        return upgrade;
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
            }
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
    }
    
    private Card CreateCardFromId(string cardId)
    {
        // This would be expanded with a proper card database
        // For now, just create some basic cards
        
        switch (cardId)
        {
            case "basic_strike":
                return new Card
                {
                    CardId = "basic_strike",
                    CardName = "Strike",
                    CardDescription = "Deal 6 damage.",
                    EnergyCost = 1,
                    CardType = CardType.Attack,
                    CardRarity = CardRarity.Basic,
                    DamageAmount = 6
                };
                
            case "basic_defend":
                return new Card
                {
                    CardId = "basic_defend",
                    CardName = "Defend",
                    CardDescription = "Gain 5 Block.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Basic,
                    BlockAmount = 5
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
                break;
            }
        }
    }
    
    private void ShowGameOver()
    {
        // This would display game over UI
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
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
        draftRect.sizeDelta = new Vector2(800, 400);
        
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
        panel.SetActive(false);
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
        
        // Create upgrade options container
        GameObject optionsContainer = new GameObject("OptionsContainer");
        optionsContainer.transform.SetParent(draftPanel.transform, false);
        
        RectTransform optionsRect = optionsContainer.AddComponent<RectTransform>();
        optionsRect.anchorMin = new Vector2(0.1f, 0.2f);
        optionsRect.anchorMax = new Vector2(0.9f, 0.85f);
        optionsRect.sizeDelta = Vector2.zero;
        
        // Add grid layout
        GridLayoutGroup gridLayout = optionsContainer.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(180, 200);
        gridLayout.spacing = new Vector2(20, 20);
        gridLayout.padding = new RectOffset(10, 10, 10, 10);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 3;
        
        // Add upgrade options
        for (int i = 0; i < upgrades.Count; i++)
        {
            CreateUpgradeOption(optionsContainer, upgrades[i], i);
        }
        
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
        optionButton.onClick.AddListener(() => PlayerSelectedUpgrade(index));
        
        // Create vertical layout
        VerticalLayoutGroup vertLayout = optionObj.AddComponent<VerticalLayoutGroup>();
        vertLayout.padding = new RectOffset(5, 5, 5, 5);
        vertLayout.spacing = 5;
        vertLayout.childAlignment = TextAnchor.UpperCenter;
        vertLayout.childForceExpandWidth = true;
        vertLayout.childForceExpandHeight = false;
        
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
        descRect.sizeDelta = new Vector2(0, 60);
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