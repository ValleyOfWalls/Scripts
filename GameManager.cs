using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [SerializeField] private int startingPlayerHealth = 100;
    [SerializeField] private int startingPetHealth = 50;
    [SerializeField] private int startingEnergy = 3;
    [SerializeField] private int cardsToDraw = 5;
    [SerializeField] private List<CardData> starterDeck = new List<CardData>(); // Assign starter cards in Inspector
    [SerializeField] private List<CardData> starterPetDeck = new List<CardData>(); // Assign pet starter cards in Inspector

    [Header("UI Panels")][Tooltip("Assign from Assets/Prefabs/UI")]
    [SerializeField] private GameObject startScreenCanvasPrefab;
    [SerializeField] private GameObject lobbyCanvasPrefab;
    [SerializeField] private GameObject combatCanvasPrefab; // Assign this prefab
    // [SerializeField] private GameObject draftCanvasPrefab; // For later
    [SerializeField] private GameObject cardPrefab; // Assign this prefab via UISetup

    [Header("Start Screen References")]
    private Button connectButton;

    [Header("Lobby Screen References")]
    private GameObject playerListPanel; // Parent for player entries
    private GameObject playerEntryTemplate; // Template Text element
    private Button readyButton;
    private Button leaveButton;
    private Button startGameButton; // Master client only

    [Header("Combat Screen References")]
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI playerNameText;
    private Slider playerHealthSlider;
    private TextMeshProUGUI playerHealthText;
    private GameObject playerHandPanel;
    // private GameObject cardTemplate; // No longer needed - we use cardPrefab
    private TextMeshProUGUI deckCountText;
    private TextMeshProUGUI discardCountText;
    private Button endTurnButton;
    private TextMeshProUGUI opponentPetNameText;
    private Slider opponentPetHealthSlider;
    private TextMeshProUGUI opponentPetHealthText;
    private TextMeshProUGUI opponentPetIntentText;
    private TextMeshProUGUI ownPetNameText;
    private Slider ownPetHealthSlider;
    private TextMeshProUGUI ownPetHealthText;
    private GameObject othersStatusArea;
    private GameObject otherPlayerStatusTemplate; // Inside Others Status Area
    [SerializeField] private TextMeshProUGUI energyText; // Add Energy Text reference if you have one

    // Instantiated Canvases
    private GameObject startScreenInstance;
    private GameObject lobbyInstance;
    private GameObject combatInstance;

    // Player Ready Status
    private const string PLAYER_READY_PROPERTY = "IsReady";

    // Player List Management
    private List<GameObject> playerListEntries = new List<GameObject>();

    // Combat State
    [SerializeField] private int localPlayerHealth;
    [SerializeField] private int localPetHealth;
    [SerializeField] private int opponentPetHealth; // Health of the pet the local player is fighting
    [SerializeField] private int currentEnergy;
    private List<CardData> deck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    private Player opponentPlayer; // Reference to the opponent player whose pet we fight
    private int player1Score = 0;
    private int player2Score = 0; // Extend later for >2 players
    // <<--- ADDED PET STATE START --->>
    [Header("Opponent Pet Combat State (Local Simulation)")]
    [SerializeField] private int opponentPetEnergy;
    private List<CardData> opponentPetDeck = new List<CardData>();
    private List<CardData> opponentPetHand = new List<CardData>();
    private List<CardData> opponentPetDiscard = new List<CardData>();
    // <<--- ADDED PET STATE END --->>

    // --- Other combat state vars like buffs, debuffs, etc. --- 

    #region Unity Methods

    void Start()
    {
        // Ensure we have only one GameManager instance
        if (FindObjectsOfType<GameManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject); // Keep GameManager across scenes

        // Instantiate and setup Start Screen
        if (startScreenCanvasPrefab != null)
        {
            startScreenInstance = Instantiate(startScreenCanvasPrefab);
            connectButton = startScreenInstance.transform.Find("ConnectButton")?.GetComponent<Button>();
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(ConnectToPhoton);
            }
            else Debug.LogError("ConnectButton not found in StartScreenCanvasPrefab!");
        }
        else Debug.LogError("StartScreenCanvasPrefab is not assigned!");

        // Initially hide Lobby & Combat (will be instantiated when needed)
    }

    #endregion

    #region Photon Connection & Room Logic

    private void ConnectToPhoton()
    {
        Debug.Log("Connecting to Photon...");
        if(connectButton) connectButton.interactable = false;
        PhotonNetwork.NickName = "Player_" + Random.Range(1000, 9999);
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to Master Server. Player Nickname: {PhotonNetwork.NickName}");
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 }; // Allow up to 4 for future use
        PhotonNetwork.JoinOrCreateRoom("asd", roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        if (startScreenInstance != null) startScreenInstance.SetActive(false);
        if (combatInstance != null) combatInstance.SetActive(false); // Ensure combat is hidden
        ShowLobbyScreen();
        UpdatePlayerList();
        SetPlayerReady(false);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        if (lobbyInstance != null) Destroy(lobbyInstance); // Clean up instantiated lobby
        if (combatInstance != null) Destroy(combatInstance); // Clean up instantiated combat

        if (startScreenInstance != null)
        {
            startScreenInstance.SetActive(true);
            if (connectButton != null) connectButton.interactable = true;
        }
        // Reset state if necessary
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join Room Failed: ({returnCode}) {message}");
        if (connectButton != null) connectButton.interactable = true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: ({returnCode}) {message}");
        if (connectButton != null) connectButton.interactable = true;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered room.");
        if (lobbyInstance != null && lobbyInstance.activeSelf) // Only update if in lobby
        {
             UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left room.");
        if (lobbyInstance != null && lobbyInstance.activeSelf) // Only update if in lobby
        {
            UpdatePlayerList();
        }
        // Handle player leaving during combat later
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        Debug.Log($"Player {targetPlayer.NickName} properties updated.");
        if (lobbyInstance != null && lobbyInstance.activeSelf && changedProps.ContainsKey(PLAYER_READY_PROPERTY))
        {
            UpdatePlayerList();
        }
        // Handle property changes during combat later (e.g., health sync)
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"New Master Client: {newMasterClient.NickName}");
        if (lobbyInstance != null && lobbyInstance.activeSelf)
        {
             UpdateLobbyControls();
        }
    }

    #endregion

    #region Lobby UI Logic

    private void ShowLobbyScreen()
    {
        if (lobbyInstance == null)
        {
            if (lobbyCanvasPrefab != null)
            {
                lobbyInstance = Instantiate(lobbyCanvasPrefab);
                // Find Lobby elements
                Transform panelTransform = lobbyInstance.transform.Find("PlayerListPanel");
                playerListPanel = panelTransform?.gameObject;
                playerEntryTemplate = panelTransform?.Find("PlayerEntryTemplate")?.gameObject;
                readyButton = lobbyInstance.transform.Find("ReadyButton")?.GetComponent<Button>();
                leaveButton = lobbyInstance.transform.Find("LeaveButton")?.GetComponent<Button>();
                startGameButton = lobbyInstance.transform.Find("StartGameButton")?.GetComponent<Button>();

                // Assign listeners
                readyButton?.onClick.AddListener(ToggleReadyStatus);
                leaveButton?.onClick.AddListener(LeaveRoom);
                startGameButton?.onClick.AddListener(StartGame); // Master client calls this

                if (playerListPanel == null || playerEntryTemplate == null || readyButton == null || leaveButton == null || startGameButton == null)
                     Debug.LogError("One or more Lobby UI elements not found in LobbyCanvasPrefab!");
                
                playerEntryTemplate?.SetActive(false);
            }
            else
            {
                Debug.LogError("LobbyCanvasPrefab is not assigned!");
                return;
            }
        }
        lobbyInstance.SetActive(true);
        UpdateLobbyControls();
    }

    private void UpdatePlayerList()
    {
        if (playerListPanel == null || playerEntryTemplate == null) return;

        foreach (GameObject entry in playerListEntries) Destroy(entry);
        playerListEntries.Clear();

        foreach (Player player in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
        {
            GameObject newEntry = Instantiate(playerEntryTemplate, playerListPanel.transform);
            TextMeshProUGUI textComponent = newEntry.GetComponent<TextMeshProUGUI>();
            string readyStatus = "";
            if (player.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReadyStatus))
                readyStatus = (bool)isReadyStatus ? " <color=green>(Ready)</color>" : "";
            
            textComponent.text = $"{player.NickName}{(player.IsMasterClient ? " (Host)" : "")}{readyStatus}";
            newEntry.SetActive(true);
            playerListEntries.Add(newEntry);
        }
        UpdateLobbyControls();
    }

    private void UpdateLobbyControls()
    {
        if (lobbyInstance == null) return;
        bool allReady = PhotonNetwork.PlayerList.Length > 1 && // Need at least 2 players
                        PhotonNetwork.PlayerList.All(p => 
                            p.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReady) && (bool)isReady);
        
        startGameButton?.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        if (startGameButton != null) startGameButton.interactable = PhotonNetwork.IsMasterClient && allReady;
    }

    private void ToggleReadyStatus()
    {
        bool currentStatus = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReadyStatus))
            currentStatus = (bool)isReadyStatus;
        SetPlayerReady(!currentStatus);
    }

    private void SetPlayerReady(bool isReady)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable {{ PLAYER_READY_PROPERTY, isReady }};
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void LeaveRoom()
    {
        Debug.Log("Leave Button Clicked");
        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region Game Start & Combat Transition

    private void StartGame()
    {
        // Only Master Client can initiate
        if (!PhotonNetwork.IsMasterClient) return;

        // Check if all players are ready
        if (!PhotonNetwork.PlayerList.All(p => p.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReady) && (bool)isReady))
        {
            Debug.LogWarning("Cannot start game, not all players are ready.");
            return;
        }
        // Ensure minimum player count if desired (e.g., 2)
        if (PhotonNetwork.PlayerList.Length < 2)
        {
             Debug.LogWarning("Cannot start game, need at least 2 players.");
             return;
        }

        Debug.Log("Master Client is Starting Game...");

        // Prevent others from joining
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // Tell all clients to start combat setup via RPC
        photonView.RPC("RpcStartCombat", RpcTarget.All);
    }

    [PunRPC]
    private void RpcStartCombat()
    {
        Debug.Log($"RPC: Starting Combat Setup for {PhotonNetwork.LocalPlayer.NickName}");

        // Disable Lobby
        if (lobbyInstance != null) lobbyInstance.SetActive(false);

        // Enable Combat Screen
        ShowCombatScreen();

        // Initialize combat state for this player
        InitializeCombatState();
    }

    private void ShowCombatScreen()
    {
         if (combatInstance == null)
        {
            if (combatCanvasPrefab != null)
            {
                combatInstance = Instantiate(combatCanvasPrefab);
                // --- Find Combat UI Elements (Revised for more direct finding) --- 
                
                // Find Top-Level Containers
                Transform topArea = combatInstance.transform.Find("TopArea");
                Transform playerArea = combatInstance.transform.Find("PlayerArea");

                if (topArea == null || playerArea == null)
                {
                    Debug.LogError("Could not find TopArea or PlayerArea in CombatCanvas!");
                    return;
                }

                // Find elements within TopArea
                scoreText = topArea.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
                
                // <<--- MODIFIED FINDING LOGIC START --->>
                // Find the container first
                Transform opponentAreaContainer = topArea.Find("OpponentPetAreaContainer"); 
                // Then find the panel within the container
                Transform opponentArea = opponentAreaContainer?.Find("OpponentPetArea"); 
                opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
                opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
                opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
                opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
                
                // Find the container first
                Transform ownPetAreaContainer = topArea.Find("OwnPetAreaContainer"); 
                // Then find the panel within the container
                Transform ownPetArea = ownPetAreaContainer?.Find("OwnPetArea"); 
                ownPetNameText = ownPetArea?.Find("OwnPetNameText")?.GetComponent<TextMeshProUGUI>();
                ownPetHealthSlider = ownPetArea?.Find("OwnPetHealthSlider")?.GetComponent<Slider>();
                ownPetHealthText = ownPetArea?.Find("OwnPetHealthText")?.GetComponent<TextMeshProUGUI>();
                // <<--- MODIFIED FINDING LOGIC END --->>

                Transform othersAreaTransform = topArea.Find("OthersStatusArea");
                othersStatusArea = othersAreaTransform?.gameObject;
                otherPlayerStatusTemplate = othersAreaTransform?.Find("OtherPlayerStatusTemplate")?.gameObject;

                // Find elements within PlayerArea
                Transform statsRow = playerArea.Find("StatsRow");
                playerNameText = statsRow?.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
                playerHealthSlider = statsRow?.Find("PlayerHealthSlider")?.GetComponent<Slider>();
                playerHealthText = statsRow?.Find("PlayerHealthText")?.GetComponent<TextMeshProUGUI>();
                
                // Find Energy Text
                energyText = statsRow?.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
                
                Transform handPanelTransform = playerArea.Find("PlayerHandPanel");
                playerHandPanel = handPanelTransform?.gameObject;
                // cardTemplate = handPanelTransform?.Find("CardTemplate")?.gameObject; // Don't need to find template, use cardPrefab
                
                Transform bottomBar = playerArea.Find("BottomBar");
                deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
                discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
                endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
                
                // Assign listeners
                endTurnButton?.onClick.AddListener(EndTurn); 

                 // Validate critical findings 
                if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null /*|| cardTemplate == null*/ || scoreText == null)
                     Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab! PlayerHandPanel or OpponentPetName missing?");
                else 
                     Debug.Log("Successfully found critical combat UI elements.");

                
                // cardTemplate?.SetActive(false); // No longer need to deactivate template in scene
                otherPlayerStatusTemplate?.SetActive(false);
                othersStatusArea?.SetActive(PhotonNetwork.PlayerList.Length > 2); // Only show for >2 players
            }
            else
            {
                Debug.LogError("CombatCanvasPrefab is not assigned!");
                return;
            }
        }
        combatInstance.SetActive(true);
    }

    private void InitializeCombatState()
    {
        Debug.Log("Initializing Combat State...");
        
        // Reset scores (example)
        player1Score = 0;
        player2Score = 0;
        UpdateScoreUI();

        // Determine opponent (Simple 2-player logic)
        opponentPlayer = null;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p != PhotonNetwork.LocalPlayer)
            {
                opponentPlayer = p;
                break;
            }
        }

        if (opponentPlayer == null && PhotonNetwork.PlayerList.Length > 1)
        {
            Debug.LogError("Could not determine opponent!");
            // Fallback or error handling
        }

        // Initialize Health
        localPlayerHealth = startingPlayerHealth;
        localPetHealth = startingPetHealth;
        opponentPetHealth = startingPetHealth; // Assuming opponent pet has same starting health

        // Setup Initial UI
        if(playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        UpdateHealthUI(); // Call helper to update all health displays

        if (opponentPetNameText) opponentPetNameText.text = opponentPlayer != null ? $"{opponentPlayer.NickName}'s Pet" : "Opponent Pet";
        if (ownPetNameText) ownPetNameText.text = "Your Pet";

        // Initialize Player Deck
        deck = new List<CardData>(starterDeck); // Copy starter deck
        ShuffleDeck();
        hand.Clear();
        discardPile.Clear();
        UpdateDeckCountUI();

        // <<--- INITIALIZE PET DECK START --->>
        // Initialize Opponent Pet Deck (using local simulation)
        if (starterPetDeck != null && starterPetDeck.Count > 0)
        {
            opponentPetDeck = new List<CardData>(starterPetDeck);
            ShuffleOpponentPetDeck(); // Need to create this helper
        }
        else
        {
             Debug.LogWarning("StarterPetDeck is not assigned or empty in GameManager inspector!");
             opponentPetDeck = new List<CardData>(); // Start with empty if none assigned
        }
        opponentPetHand.Clear();
        opponentPetDiscard.Clear();
        // Don't necessarily need pet deck/discard count UI, but could add later
        // <<--- INITIALIZE PET DECK END --->>

        // Start the first turn
        StartTurn();
    }

    #endregion

    #region Combat Turn Logic

    private void StartTurn()
    {
        Debug.Log("Starting Player Turn");
        currentEnergy = startingEnergy;
        // TODO: Update Energy UI
        UpdateEnergyUI();

        DrawHand();
        UpdateHandUI();
        UpdateDeckCountUI();

        // TODO: Determine and display opponent pet's intent
        if(opponentPetIntentText) opponentPetIntentText.text = "Intent: Attack 5"; // Placeholder

        // Make cards playable, etc.
        if (endTurnButton) endTurnButton.interactable = true;
    }

    private void EndTurn()
    {
        Debug.Log("Ending Player Turn");
        if (endTurnButton) endTurnButton.interactable = false; // Prevent double clicks

        // 1. Discard Hand
        DiscardHand();
        UpdateHandUI(); // Clear hand display
        UpdateDeckCountUI();

        // 2. Opponent Pet Acts (Placeholder)
        // Debug.Log("Opponent Pet acts..."); // REMOVE Placeholder
        // --- TODO: Implement Pet AI based on intent --- 
        // Example: Apply damage to player
        // localPlayerHealth -= 5; 
        // UpdateHealthUI();
        ExecuteOpponentPetTurn(); // <<--- CALL PET TURN

        // 3. Check End Conditions (Placeholders)
        // if (opponentPetHealth <= 0) { HandleCombatVictory(); return; } 
        // if (localPlayerHealth <= 0) { HandleCombatDefeat(); return; }

        // 4. Start Next Player Turn
        StartTurn();
    }

    private void DrawHand()
    {
        hand.Clear();
        for (int i = 0; i < cardsToDraw; i++)
        {
            DrawCard();
        }
    }

    private void DrawCard()
    {
        if (deck.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("No cards left to draw!");
                return; // Out of cards
            }
            ReshuffleDiscardPile();
        }

        CardData drawnCard = deck[0];
        deck.RemoveAt(0);
        hand.Add(drawnCard);
    }

    private void DiscardHand()
    {
        discardPile.AddRange(hand);
        hand.Clear();
    }

    private void ReshuffleDiscardPile()
    {
        Debug.Log("Reshuffling discard pile into deck.");
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
    }

    private void ShuffleDeck()
    {
        System.Random rng = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = deck[k];
            deck[k] = deck[n];
            deck[n] = value;
        }
        Debug.Log("Deck shuffled.");
    }

    // <<--- OPPONENT PET CARD MANAGEMENT HELPERS START --->>
    // Based on Player versions

    private void ShuffleOpponentPetDeck()
    {
        System.Random rng = new System.Random();
        int n = opponentPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = opponentPetDeck[k];
            opponentPetDeck[k] = opponentPetDeck[n];
            opponentPetDeck[n] = value;
        }
        Debug.Log("Opponent Pet Deck shuffled.");
    }

    private void DrawOpponentPetHand(int amountToDraw)
    {
        opponentPetHand.Clear(); // Clear previous hand
        for (int i = 0; i < amountToDraw; i++)
        {
            DrawOpponentPetCard();
        }
        Debug.Log($"Opponent Pet drew {opponentPetHand.Count} cards.");
    }

    private void DrawOpponentPetCard()
    {
        if (opponentPetDeck.Count == 0)
        {
            if (opponentPetDiscard.Count == 0)
            {
                Debug.Log("Opponent Pet has no cards left to draw!");
                return; // Out of cards
            }
            ReshuffleOpponentPetDiscardPile();
        }
        
        if (opponentPetDeck.Count > 0) // Check again after potential reshuffle
        {
             CardData drawnCard = opponentPetDeck[0];
             opponentPetDeck.RemoveAt(0);
             opponentPetHand.Add(drawnCard);
        }
    }

    private void DiscardOpponentPetHand()
    {
        opponentPetDiscard.AddRange(opponentPetHand);
        opponentPetHand.Clear();
    }

    private void ReshuffleOpponentPetDiscardPile()
    {
        Debug.Log("Reshuffling Opponent Pet discard pile into deck.");
        opponentPetDeck.AddRange(opponentPetDiscard);
        opponentPetDiscard.Clear();
        ShuffleOpponentPetDeck();
    }

    // <<--- OPPONENT PET CARD MANAGEMENT HELPERS END --->>

    // --- TODO: Implement Card Playing Logic --- 
    // void PlayCard(CardData card) { ... }

    #endregion

    #region Opponent Pet Turn Logic (Local Simulation)

    private void ExecuteOpponentPetTurn()
    {
        Debug.Log("---> Starting Opponent Pet Turn <---");
        opponentPetEnergy = startingEnergy; // Use player starting energy for now
        
        // Determine how many cards the pet should draw (can be different from player)
        int petCardsToDraw = 3; // Example: Pet draws fewer cards
        DrawOpponentPetHand(petCardsToDraw);

        // Simple AI: Play cards until out of energy or no playable cards left
        bool cardPlayedThisLoop;
        do
        {
            cardPlayedThisLoop = false;
            CardData cardToPlay = null;
            int cardIndex = -1;

            // Find the first playable card in hand
            for(int i = 0; i < opponentPetHand.Count; i++)
            {
                if (opponentPetHand[i].cost <= opponentPetEnergy)
                {
                    cardToPlay = opponentPetHand[i];
                    cardIndex = i;
                    break; // Found one, stop looking
                }
            }

            // If a playable card was found
            if (cardToPlay != null)
            {
                Debug.Log($"Opponent Pet playing card: {cardToPlay.cardName} (Cost: {cardToPlay.cost})");
                opponentPetEnergy -= cardToPlay.cost;
                
                // Apply effect (Example: Damage to Player)
                if (cardToPlay.damage > 0)
                {
                    localPlayerHealth -= cardToPlay.damage;
                    Debug.Log($"Opponent Pet dealt {cardToPlay.damage} damage to Local Player. New health: {localPlayerHealth}");
                    UpdateHealthUI(); // Update player health bar
                    // TODO: Check player defeat condition
                }
                // TODO: Add other pet card effects (block self, apply buffs/debuffs)
                
                // Move card from hand to discard
                opponentPetHand.RemoveAt(cardIndex);
                opponentPetDiscard.Add(cardToPlay);
                cardPlayedThisLoop = true; // Indicate a card was played, loop again

                Debug.Log($"Opponent Pet energy remaining: {opponentPetEnergy}");
            }
            
        } while (cardPlayedThisLoop && opponentPetEnergy > 0); // Continue if a card was played and energy remains

        Debug.Log("Opponent Pet finished playing cards.");
        DiscardOpponentPetHand();
        Debug.Log("---> Ending Opponent Pet Turn <---");
    }

    #endregion

    #region Combat UI Updates

    private void UpdateHealthUI()
    {
        // Player Health
        if (playerHealthSlider) playerHealthSlider.value = (float)localPlayerHealth / startingPlayerHealth;
        if (playerHealthText) playerHealthText.text = $"{localPlayerHealth} / {startingPlayerHealth}";

        // Own Pet Health
        if (ownPetHealthSlider) ownPetHealthSlider.value = (float)localPetHealth / startingPetHealth;
        if (ownPetHealthText) ownPetHealthText.text = $"{localPetHealth} / {startingPetHealth}";

        // Opponent Pet Health
        if (opponentPetHealthSlider) opponentPetHealthSlider.value = (float)opponentPetHealth / startingPetHealth;
        if (opponentPetHealthText) opponentPetHealthText.text = $"{opponentPetHealth} / {startingPetHealth}";

        // --- TODO: Update Other Players Status UI (if > 2 players) --- 
    }

    private void UpdateScoreUI()
    {
        if (scoreText)
        {
            // Basic 2-player score display
            scoreText.text = $"Score: You: {player1Score} / Opp: {player2Score}"; // Adjust based on who is P1/P2
        }
    }

    private void UpdateHandUI()
    {
        if (playerHandPanel == null || cardPrefab == null) 
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // Clear existing card visuals (excluding the inactive placeholder if it somehow exists)
        foreach (Transform child in playerHandPanel.transform)
        {
            // Don't destroy the original placeholder if UISetup left one (it shouldn't)
            if (child.gameObject.name != "CardTemplate") 
            {
                Destroy(child.gameObject);
            }
            else
            {
                child.gameObject.SetActive(false); // Ensure placeholder remains inactive
            }
        }

        // Instantiate new card visuals for cards in hand using the prefab
        foreach (CardData card in hand)
        {
            GameObject cardGO = Instantiate(cardPrefab, playerHandPanel.transform);
            cardGO.name = $"Card_{card.cardName}"; // Give instantiated cards a useful name
            
            // --- Populate Card Visuals (Updated for new structure) --- 
            Transform headerPanel = cardGO.transform.Find("HeaderPanel");
            Transform descPanel = cardGO.transform.Find("DescPanel");
            Transform artPanel = cardGO.transform.Find("ArtPanel"); // Optional

            TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
            Image artImage = artPanel?.GetComponent<Image>(); // Optional

            if (nameText != null) nameText.text = card.cardName;
            if (costText != null) costText.text = card.cost.ToString();
            if (descText != null) descText.text = card.description;
            // if (artImage != null) { /* Load/assign art here... */ }
            
            // Get the handler and assign the data so it knows which card it represents
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null) 
            {
                handler.cardData = card; 
            }
            else
            {
                Debug.LogError($"CardDragHandler component not found on instantiated card prefab: {cardGO.name}");
            }

            // TODO: Add Button listener for playing the card (pass 'card' data)
            // Button cardButton = cardGO.GetComponent<Button>(); 
            // if (cardButton != null) cardButton.onClick.AddListener(() => PlayCard(card));

            cardGO.SetActive(true);
        }
    }

    private void UpdateDeckCountUI()
    {
        if (deckCountText != null) deckCountText.text = $"Deck: {deck.Count}";
        if (discardCountText != null) discardCountText.text = $"Discard: {discardPile.Count}";
    }

    private void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"Energy: {currentEnergy} / {startingEnergy}";
        }
        else
        {
             Debug.LogWarning("UpdateEnergyUI: energyText reference is null!");
        }
    }

    #endregion

    #region Combat UI & State Logic

    // TODO: Implement proper turn management logic
    public bool IsPlayerTurn()
    {
        Debug.Log("GameManager.IsPlayerTurn() called - Returning true for now.");
        // For now, assume it's always the player's turn for testing drag/drop
        return true; 
    }

    // TODO: Implement actual card playing logic (effects, costs, targeting, networking)
    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        Debug.Log($"GameManager.AttemptPlayCard called: Card '{cardData.cardName}', TargetType '{targetType}'");
        
        // Basic checks (add more like energy cost)
        if (cardData == null)
        {
             Debug.LogError("AttemptPlayCard: cardData is null!");
             return false;
        }

        // <<--- ENERGY CHECK START --->>
        if (cardData.cost > currentEnergy)
        {
            Debug.LogWarning($"AttemptPlayCard: Cannot play card '{cardData.cardName}' - Cost ({cardData.cost}) exceeds current energy ({currentEnergy}).");
            return false; // Not enough energy
        }
        // <<--- ENERGY CHECK END --->>

        // Attempt to remove the card from the hand list
        bool removed = hand.Remove(cardData);
        Debug.Log($"Attempting to remove card '{cardData.cardName}' from hand. Result: {removed}");

        // Placeholder: Consume the card (remove from hand) and move to discard
        if (removed) // Use the result from above
        {
             discardPile.Add(cardData);
             UpdateHandUI(); // Update visual hand
             UpdateDeckCountUI(); // Update discard count display

             // <<--- ENERGY CONSUMPTION START --->>
             currentEnergy -= cardData.cost;
             UpdateEnergyUI(); // Update UI after spending energy
             Debug.Log($"Played card '{cardData.cardName}'. Energy remaining: {currentEnergy}");
             // <<--- ENERGY CONSUMPTION END --->>

             // <<--- CARD EFFECT LOGIC START --->>
             // --- Apply card effects based on cardData and targetType --- 
             if (targetType == CardDropZone.TargetType.EnemyPet && cardData.damage > 0)
             {
                 int damageDealt = cardData.damage;
                 // Apply locally first for immediate feedback
                 opponentPetHealth -= damageDealt;
                 Debug.Log($"Dealt {damageDealt} damage to Opponent Pet. New health: {opponentPetHealth}");
                 UpdateHealthUI();

                 // <<--- SEND RPC START --->>
                 // Now, tell the actual opponent player to apply the damage to their pet
                 if (opponentPlayer != null)
                 {
                     Debug.Log($"Sending RpcTakePetDamage({damageDealt}) to {opponentPlayer.NickName}");
                     photonView.RPC("RpcTakePetDamage", opponentPlayer, damageDealt); 
                 }
                 else
                 {
                     Debug.LogError("Cannot send RpcTakePetDamage: opponentPlayer reference is null!");
                 }
                 // <<--- SEND RPC END --->>

                 // TODO: NETWORK - Sync opponentPetHealth change to the other player! // This RPC handles it
             }
             // --- Add other effects below (e.g., targeting OwnPet, PlayerSelf, applying block, buffs) --- 
             // else if (targetType == CardDropZone.TargetType.OwnPet && cardData.block > 0) { ... }
             
             // TODO: NETWORK - Send RPC to notify opponent about card play results (damage dealt, buffs applied, etc.) if needed for animations/logs
             // <<--- CARD EFFECT LOGIC END --->>

             // Log success AFTER effects are processed
             Debug.Log($"Successfully processed effects for card '{cardData.cardName}' on target '{targetType}'. Moved to discard.");

             return true; // Indicate success
         }
         else 
    {
        // --- IF REMOVE FAILS ---
        Debug.LogWarning($"AttemptPlayCard: Card '{cardData.cardName}' NOT found in hand list for removal (reference equality failed?).");
        // Check if a card with the same name exists, indicating a potential reference issue
        if (hand.Any(c => c != null && c.cardName == cardData.cardName)) {
            Debug.LogWarning($"--> NOTE: A card named '{cardData.cardName}' DOES exist in hand, but the reference passed from CardDragHandler doesn't match. Investigate CardData object lifetime/references.");
        }
        else
        {
            Debug.LogWarning($"--> NOTE: No card named '{cardData.cardName}' found in hand list at all.");
        }
        return false; // Card wasn't in hand (or reference didn't match)
    }
}

    #endregion

    #region Networking RPCs

    [PunRPC]
    private void RpcTakePetDamage(int damageAmount)
    {
        // This code executes on the client whose pet is taking damage
        if (damageAmount <= 0) return; // Don't process 0 or negative damage

        Debug.Log($"RPC Received: My Pet taking {damageAmount} damage.");
        localPetHealth -= damageAmount;
        // Ensure health doesn't go below zero visually if needed
        if (localPetHealth < 0) localPetHealth = 0; 

        UpdateHealthUI(); 

        // TODO: Potentially trigger visual effects or animations for taking damage
        // TODO: Check if pet health reached 0 and handle pet defeat logic for this player
    }

    #endregion
} 