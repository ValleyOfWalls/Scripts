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
    // Add Energy Text reference if you have one

    // Instantiated Canvases
    private GameObject startScreenInstance;
    private GameObject lobbyInstance;
    private GameObject combatInstance;

    // Player Ready Status
    private const string PLAYER_READY_PROPERTY = "IsReady";

    // Player List Management
    private List<GameObject> playerListEntries = new List<GameObject>();

    // Combat State
    private int localPlayerHealth;
    private int localPetHealth;
    private int opponentPetHealth; // Health of the pet the local player is fighting
    private int currentEnergy;
    private List<CardData> deck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    private Player opponentPlayer; // Reference to the opponent player whose pet we fight
    private int player1Score = 0;
    private int player2Score = 0; // Extend later for >2 players

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
                Transform opponentArea = topArea.Find("OpponentPetArea");
                opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
                opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
                opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
                opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
                
                Transform ownPetArea = topArea.Find("OwnPetArea");
                ownPetNameText = ownPetArea?.Find("OwnPetNameText")?.GetComponent<TextMeshProUGUI>();
                ownPetHealthSlider = ownPetArea?.Find("OwnPetHealthSlider")?.GetComponent<Slider>();
                ownPetHealthText = ownPetArea?.Find("OwnPetHealthText")?.GetComponent<TextMeshProUGUI>();

                Transform othersAreaTransform = topArea.Find("OthersStatusArea");
                othersStatusArea = othersAreaTransform?.gameObject;
                otherPlayerStatusTemplate = othersAreaTransform?.Find("OtherPlayerStatusTemplate")?.gameObject;

                // Find elements within PlayerArea
                Transform statsRow = playerArea.Find("StatsRow");
                playerNameText = statsRow?.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
                playerHealthSlider = statsRow?.Find("PlayerHealthSlider")?.GetComponent<Slider>();
                playerHealthText = statsRow?.Find("PlayerHealthText")?.GetComponent<TextMeshProUGUI>();
                
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

        // Initialize Deck
        deck = new List<CardData>(starterDeck); // Copy starter deck
        ShuffleDeck();
        hand.Clear();
        discardPile.Clear();
        UpdateDeckCountUI();

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
        Debug.Log("Opponent Pet acts...");
        // --- TODO: Implement Pet AI based on intent --- 
        // Example: Apply damage to player
        // localPlayerHealth -= 5; 
        // UpdateHealthUI();

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

    // --- TODO: Implement Card Playing Logic --- 
    // void PlayCard(CardData card) { ... }

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
            
            // TODO: Add Button listener for playing the card (pass 'card' data)
            // Button cardButton = cardGO.GetComponent<Button>(); 
            // if (cardButton != null) cardButton.onClick.AddListener(() => PlayCard(card));

            cardGO.SetActive(true);
        }
    }

    private void UpdateDeckCountUI()
    {
         if(deckCountText) deckCountText.text = $"Deck: {deck.Count}";
         if(discardCountText) discardCountText.text = $"Discard: {discardPile.Count}";
    }

    #endregion
} 