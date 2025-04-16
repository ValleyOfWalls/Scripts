using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon; // Required for Hashtable
using Newtonsoft.Json; // Using Newtonsoft.Json for easier serialization

public enum GameState
{
    Connecting,
    Lobby,
    Combat,
    Drafting,
    GameOver
}

public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [SerializeField] private int startingPlayerHealth = 1;
    [SerializeField] private int startingPetHealth = 1;
    [SerializeField] private int startingEnergy = 3;
    [SerializeField] private int cardsToDraw = 5;
    [SerializeField] private List<CardData> starterDeck = new List<CardData>(); // Assign starter cards in Inspector
    [SerializeField] private List<CardData> starterPetDeck = new List<CardData>(); // Assign pet starter cards in Inspector
    [SerializeField] private int optionsPerDraft = 3; // How many options presented at once

    [Header("Card Pools for Draft/Rewards")] // Assign these in Inspector
    [SerializeField] private List<CardData> allPlayerCardPool = new List<CardData>();
    [SerializeField] private List<CardData> allPetCardPool = new List<CardData>();

    [Header("UI Panels")][Tooltip("Assign from Assets/Prefabs/UI")]
    [SerializeField] private GameObject startScreenCanvasPrefab;
    [SerializeField] private GameObject lobbyCanvasPrefab;
    [SerializeField] private GameObject combatCanvasPrefab; // Assign this prefab
    [SerializeField] private GameObject draftCanvasPrefab; // TODO: Assign draft canvas prefab
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

    [Header("Draft Screen References")] // TODO: Populate in ShowDraftScreen
    private GameObject draftOptionsPanel; // Parent for option buttons
    private TextMeshProUGUI draftTurnText;
    private GameObject draftOptionButtonTemplate; // Template for option buttons

    // Instantiated Canvases
    private GameObject startScreenInstance;
    private GameObject lobbyInstance;
    private GameObject combatInstance;
    private GameObject draftInstance; // For Draft Canvas

    // Game State
    private GameState currentState = GameState.Connecting;

    // Player Ready Status
    private const string PLAYER_READY_PROPERTY = "IsReady";
    private const string COMBAT_FINISHED_PROPERTY = "CombatFinished";

    // Draft State Keys (Room Properties)
    private const string DRAFT_OPTIONS_PROP = "DraftOptions"; // Stores serialized List<SerializableDraftOption>
    private const string DRAFT_TURN_PROP = "DraftTurn";       // Stores ActorNumber of current picker
    private const string DRAFT_ORDER_PROP = "DraftOrder";    // Stores List<int> of ActorNumbers

    // Local Draft State Cache
    private List<SerializableDraftOption> currentDraftOptions = new List<SerializableDraftOption>();
    private List<int> draftPlayerOrder = new List<int>();
    private int currentPickerActorNumber = -1;
    private Dictionary<int, DraftOption> activeOptionMap = new Dictionary<int, DraftOption>(); // Maps OptionId to full DraftOption

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

    [Header("Local Pet State")] // State for the pet belonging to the local player
    private List<CardData> localPetDeck = new List<CardData>();
    // Note: We don't currently need localPetHand/Discard as the pet doesn't draw/play its own cards in this simulation.
    // Cards are played *on* the pet by the player, or the pet acts based on opponent actions.

    // <<--- ADDED PET STATE START --->>
    [Header("Opponent Pet Combat State (Local Simulation)")][Tooltip("Used to simulate the fight player has against opponent's pet")]
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

        currentState = GameState.Connecting; // Initial state

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
        currentState = GameState.Lobby; // Transition state
        if (startScreenInstance != null) startScreenInstance.SetActive(false);
        if (combatInstance != null) combatInstance.SetActive(false); // Ensure combat is hidden
        ShowLobbyScreen();
        UpdatePlayerList();
        SetPlayerReady(false);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        currentState = GameState.Connecting; // Transition state back
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

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        Debug.Log($"Player {targetPlayer.NickName} properties updated. CurrentState: {currentState}");
        if (currentState == GameState.Lobby && changedProps.ContainsKey(PLAYER_READY_PROPERTY))
        {
            if (lobbyInstance != null && lobbyInstance.activeSelf) UpdatePlayerList();
        }
        else if (currentState == GameState.Combat && changedProps.ContainsKey(COMBAT_FINISHED_PROPERTY))
        {
            Debug.Log($"Property update for {COMBAT_FINISHED_PROPERTY} received from {targetPlayer.NickName}");
            if (PhotonNetwork.IsMasterClient)
            {
                CheckForAllPlayersFinishedCombat();
            }
        }
        // Note: We now handle draft state changes in OnRoomPropertiesUpdate
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log($"Room properties updated. CurrentState: {currentState}");
        if (currentState == GameState.Drafting)
        {
            bool draftStateUpdated = false;
            if (propertiesThatChanged.ContainsKey(DRAFT_OPTIONS_PROP))
            {
                Debug.Log("Draft options updated in room properties.");
                UpdateLocalDraftOptionsFromRoomProps();
                draftStateUpdated = true;
            }
            if (propertiesThatChanged.ContainsKey(DRAFT_TURN_PROP))
            {
                Debug.Log("Draft turn updated in room properties.");
                UpdateLocalDraftTurnFromRoomProps();
                draftStateUpdated = true;
            }
            // Draft order typically set once at start, but handle if needed
            if (propertiesThatChanged.ContainsKey(DRAFT_ORDER_PROP))
            {
                 UpdateLocalDraftOrderFromRoomProps();
                 // May not need UI update just for this
            }

            if (draftStateUpdated)
            {
                Debug.Log("Updating Draft UI due to room property change.");
                UpdateDraftUI(); // Refresh UI based on new state

                // Check if draft ended
                if (currentDraftOptions.Count == 0 && currentPickerActorNumber != -1) // Ensure it wasn't just initialized empty
                {
                    Debug.Log("Draft options empty, ending draft phase.");
                    EndDraftPhase();
                }
            }
        }
        // Handle other room property updates if necessary
    }

    // Helper methods to update local state from Room Properties
    private void UpdateLocalDraftOptionsFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_OPTIONS_PROP, out object optionsObj))
        {
            try
            {
                string optionsJson = optionsObj as string;
                currentDraftOptions = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(optionsJson) ?? new List<SerializableDraftOption>();
                RebuildActiveOptionMap(); // Rebuild the lookup map
                 Debug.Log($"Successfully deserialized {currentDraftOptions.Count} draft options.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft options from room properties: {e.Message}");
                currentDraftOptions = new List<SerializableDraftOption>();
                activeOptionMap.Clear();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_OPTIONS_PROP} not found.");
            currentDraftOptions = new List<SerializableDraftOption>();
            activeOptionMap.Clear();
        }
    }

    private void UpdateLocalDraftTurnFromRoomProps()
    {
         if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_TURN_PROP, out object turnObj))
        {
            currentPickerActorNumber = (int)turnObj;
            Debug.Log($"Current draft picker ActorNumber: {currentPickerActorNumber}");
        }
         else
         {
             Debug.LogWarning($"Room property {DRAFT_TURN_PROP} not found.");
            currentPickerActorNumber = -1;
         }
    }

     private void UpdateLocalDraftOrderFromRoomProps()
    {
         if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_ORDER_PROP, out object orderObj))
        {
            try
            {
                string orderJson = orderObj as string;
                draftPlayerOrder = JsonConvert.DeserializeObject<List<int>>(orderJson) ?? new List<int>();
                Debug.Log($"Successfully deserialized draft order: {string.Join(", ", draftPlayerOrder)}");
            }
             catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft order: {e.Message}");
                draftPlayerOrder = new List<int>();
            }
        }
         else
         {
             Debug.LogWarning($"Room property {DRAFT_ORDER_PROP} not found.");
            draftPlayerOrder = new List<int>();
         }
    }

    // Rebuilds the Dict mapping OptionId to full DraftOption based on currentDraftOptions
    private void RebuildActiveOptionMap()
    {
        activeOptionMap.Clear();
        foreach (var serializableOption in currentDraftOptions)
        {
            DraftOption fullOption = serializableOption.ToDraftOption(allPlayerCardPool, allPetCardPool);
            if (fullOption != null)
            {
                activeOptionMap[fullOption.OptionId] = fullOption;
            }
            else
            {
                 Debug.LogWarning($"Could not fully deserialize option ID {serializableOption.OptionId}, Card maybe missing from pools?");
            }
        }
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
        currentState = GameState.Combat; // Transition state

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
            // Fallback or error handling? For now, continue, might be single player test?
        }

        // Initialize Health (Using TEST values)
        localPlayerHealth = startingPlayerHealth;
        localPetHealth = startingPetHealth;
        // Find the opponent's pet health property or use default if not set
        int opponentStartingPetHealth = startingPetHealth;
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue("PetHealth", out object oppPetHealth))
        {
            opponentStartingPetHealth = (int)oppPetHealth; // Use opponent's actual pet health if available later
        }
        else
        {
             // If opponent or property doesn't exist, use default (which is now 1 for testing)
             opponentStartingPetHealth = startingPetHealth;
        }
        opponentPetHealth = opponentStartingPetHealth;

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

        // --- Initialize Local Pet Deck ---
        if (starterPetDeck != null && starterPetDeck.Count > 0)
        {
            localPetDeck = new List<CardData>(starterPetDeck);
            ShuffleLocalPetDeck();
        }
        else
        {
            Debug.LogWarning("StarterPetDeck is not assigned or empty in GameManager inspector! Local Pet starts with no cards.");
            localPetDeck = new List<CardData>();
        }

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

    private bool combatEndedForLocalPlayer = false; // Flag to prevent multiple end calls

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
                    // Check player defeat condition
                    if (localPlayerHealth <= 0)
                    {
                        HandleCombatLoss(); // Player defeated
                        return; // End pet turn early if player is defeated
                    }
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
                 opponentPetHealth -= damageDealt;
                 Debug.Log($"Dealt {damageDealt} damage to Opponent Pet. New health: {opponentPetHealth}");
                 UpdateHealthUI();

                 if (opponentPlayer != null)
                 {
                     Debug.Log($"Sending RpcTakePetDamage({damageDealt}) to {opponentPlayer.NickName}");
                     photonView.RPC("RpcTakePetDamage", opponentPlayer, damageDealt);
                 }
                 else { /* Error logged previously */ }

                 // Check for opponent pet defeat
                 if (opponentPetHealth <= 0)
                 {
                     HandleCombatWin(); // Player wins
                     return true; // Stop processing further effects if combat ended
                 }
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
        if (localPetHealth < 0) localPetHealth = 0;

        UpdateHealthUI();

        // Check if pet health reached 0 and handle pet defeat logic for this player
        // NOTE: This doesn't necessarily mean the *local player* lost the combat round,
        // only that their pet was defeated in its own fight. We need a different mechanism
        // to determine the overall round winner based on objectives (e.g., points).
        // For now, we'll focus on detecting when THIS player's COMBAT INTERACTION is over.
        // Let's assume for now that if your pet dies, your fight interaction ends.
        // OR if the player dies (handled in ExecuteOpponentPetTurn).

        // Let's refine: Combat ends for a player if *either* they die *or* the opponent pet they are fighting dies.
        // RpcTakePetDamage handles *other* player damaging *my* pet. My pet dying doesn't end *my* fight with the opponent's pet.
        // Therefore, the check for pet death ending the combat belongs where the damage is applied:
        // - Player death check in ExecuteOpponentPetTurn (already added)
        // - Opponent pet death check in AttemptPlayCard (already added)

        // We still might want logic here if the pet dying has other consequences.
    }

    [PunRPC]
    private void RpcSelectDraftOption(int chosenOptionId, PhotonMessageInfo info)
    {
        // Primarily executed on Master Client, but could be RpcTarget.All if needed
        Debug.Log($"RPC Received: Player {info.Sender.ActorNumber} selected draft option ID {chosenOptionId}");

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Non-master client received RpcSelectDraftOption, ignoring.");
            return;
        }

        // --- Master Client Validation and Update --- 
        if (currentState != GameState.Drafting)
        {
            Debug.LogWarning("RpcSelectDraftOption received outside of Drafting state.");
            return;
        }

        // Validate Turn
        if (info.Sender.ActorNumber != currentPickerActorNumber)
        {
            Debug.LogWarning($"Player {info.Sender.ActorNumber} tried to pick out of turn (Current: {currentPickerActorNumber}).");
            // Maybe send an error back?
            return;
        }

        // Find the option in the *current* list from Room Properties
        // Important to use the synchronized state, not a potentially stale local cache
        UpdateLocalDraftOptionsFromRoomProps(); // Ensure we have the latest list
        SerializableDraftOption selectedOption = currentDraftOptions.FirstOrDefault(opt => opt.OptionId == chosenOptionId);

        if (selectedOption == null)
        {
            Debug.LogWarning($"Player {info.Sender.ActorNumber} tried to pick invalid/already taken option ID {chosenOptionId}.");
            // Option might have been taken by another player just before this RPC arrived
            return;
        }

        // --- Update Room Properties --- 

        // 1. Remove chosen option
        currentDraftOptions.Remove(selectedOption);
        string newOptionsJson = JsonConvert.SerializeObject(currentDraftOptions);

        // 2. Determine next player
        int currentPlayerIndex = draftPlayerOrder.IndexOf(currentPickerActorNumber);
        int nextPlayerIndex = (currentPlayerIndex + 1) % draftPlayerOrder.Count;
        int nextPickerActorNumber = draftPlayerOrder[nextPlayerIndex];

        // Prepare properties to set
        Hashtable roomPropsToUpdate = new Hashtable
        {
            { DRAFT_OPTIONS_PROP, newOptionsJson },
            { DRAFT_TURN_PROP, nextPickerActorNumber }
        };

        Debug.Log($"Master Client updating room props: Removed option {chosenOptionId}, next turn for {nextPickerActorNumber}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomPropsToUpdate);

        // Optionally, send an RPC confirm to the picker? Or just let OnRoomPropertiesUpdate handle it.

        // The Master Client doesn't apply the effect directly, that happens on the client
        // who made the pick, triggered by OnRoomPropertiesUpdate detecting the change.
    }

    #endregion

    #region Combat End Handling

    private void HandleCombatWin()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;

        Debug.Log("COMBAT WIN! Player defeated the opponent's pet.");
        // Award point (Placeholder)
        player1Score++; // Assuming local player is P1 for now
        UpdateScoreUI();

        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);

        // TODO: Show Win Message / Disable further actions
        if (endTurnButton) endTurnButton.interactable = false;
        // Consider disabling card dragging, etc.
    }

    private void HandleCombatLoss()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;

        Debug.Log("COMBAT LOSS! Player was defeated.");
        // Opponent gets point (Placeholder)
        player2Score++; // Assuming opponent is P2 for now
        UpdateScoreUI();

        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);

        // TODO: Show Loss Message / Disable further actions
        if (endTurnButton) endTurnButton.interactable = false;
        // Consider disabling card dragging, etc.
    }

    private void SetPlayerCombatFinished(bool finished)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { COMBAT_FINISHED_PROPERTY, finished }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"Set {COMBAT_FINISHED_PROPERTY} to {finished} for local player.");

        // Master client should check if all players are finished
        if (PhotonNetwork.IsMasterClient)
        {
            CheckForAllPlayersFinishedCombat();
        }
    }

    // Master Client checks if all players are done
    private void CheckForAllPlayersFinishedCombat()
    {
        if (!PhotonNetwork.IsMasterClient || currentState != GameState.Combat)
        {
            return; // Only Master Client checks, and only during combat
        }

        Debug.Log("Master Client checking if all players finished combat...");

        bool allFinished = true;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            object finishedStatus;
            if (p.CustomProperties.TryGetValue(COMBAT_FINISHED_PROPERTY, out finishedStatus))
            {
                if (!(bool)finishedStatus)
                {
                    allFinished = false;
                    Debug.Log($"Player {p.NickName} has not finished combat yet.");
                    break; // No need to check further
                }
            }
            else
            {
                // Player hasn't set the property yet
                allFinished = false;
                Debug.Log($"Player {p.NickName} property {COMBAT_FINISHED_PROPERTY} not found.");
                break;
            }
        }

        if (allFinished)
        {
            Debug.Log("All players have finished combat! Starting Draft Phase.");
            // Reset flags for next round before starting draft
            ResetCombatFinishedFlags();
            // Tell all clients to start the draft phase
            photonView.RPC("RpcStartDraft", RpcTarget.All);
        }
        else
        {
            Debug.Log("Not all players finished combat yet.");
        }
    }

    // Master Client calls this before starting draft to clear flags for next round
    private void ResetCombatFinishedFlags()
    {
         if (!PhotonNetwork.IsMasterClient) return;

         ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
         {
            { COMBAT_FINISHED_PROPERTY, false }
         };

         // Reset for all players
         foreach(Player p in PhotonNetwork.PlayerList)
         {
            p.SetCustomProperties(props);
         }
         Debug.Log("Reset CombatFinished flags for all players.");
    }


    [PunRPC]
    private void RpcStartDraft()
    {
        Debug.Log("RPC Received: Starting Draft Phase");
        currentState = GameState.Drafting;
        combatEndedForLocalPlayer = false; // Reset local flag for the next combat round

        // Hide Combat Screen
        if (combatInstance != null) combatInstance.SetActive(false);

        // Initialize local draft state cache before showing screen
        currentDraftOptions.Clear();
        activeOptionMap.Clear();
        draftPlayerOrder.Clear();
        currentPickerActorNumber = -1;

        // Master Client generates and distributes draft options / sets initial state
        if (PhotonNetwork.IsMasterClient)
        {
            GenerateAndDistributeDraftOptions();
        }
        else
        {
            // Non-master clients need to fetch initial state set by master
            UpdateLocalDraftOptionsFromRoomProps();
            UpdateLocalDraftTurnFromRoomProps();
            UpdateLocalDraftOrderFromRoomProps();
        }

        // Show Draft Screen (Needs Implementation)
        ShowDraftScreen();
         // Update UI after potentially getting initial state
        UpdateDraftUI();
    }

    #endregion

    #region Draft Phase Logic

    // --- Draft UI Methods ---
    private void ShowDraftScreen()
    {
        Debug.Log("Showing Draft Screen");
        if (draftInstance == null)
        {
            if (draftCanvasPrefab != null)
            {
                draftInstance = Instantiate(draftCanvasPrefab);
                // Find Draft UI Elements
                draftOptionsPanel = draftInstance.transform.Find("DraftOptionsPanel")?.gameObject;
                draftTurnText = draftInstance.transform.Find("DraftTurnText")?.GetComponent<TextMeshProUGUI>();
                draftOptionButtonTemplate = draftOptionsPanel?.transform.Find("OptionButtonTemplate")?.gameObject;

                if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null)
                {
                    Debug.LogError("One or more Draft UI elements not found in DraftCanvasPrefab!");
                }
                else
                {
                    draftOptionButtonTemplate.SetActive(false); // Hide template
                }
            }
            else
            {
                Debug.LogError("DraftCanvasPrefab is not assigned!");
                return;
            }
        }
        draftInstance.SetActive(true);
        // UpdateDraftUI(); // Called after state is potentially loaded in RpcStartDraft
    }

    private void HideDraftScreen()
    {
         Debug.Log("Hiding Draft Screen");
         if(draftInstance != null) draftInstance.SetActive(false);
    }

    private void UpdateDraftUI()
    {
        if (currentState != GameState.Drafting || draftInstance == null || !draftInstance.activeSelf)
        {
             // Don't update if not in draft or UI not ready
             // Debug.LogWarning("Skipping UpdateDraftUI - Not in Drafting state or UI inactive.");
             return;
        }

        if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null)
        {
            Debug.LogError("Cannot update Draft UI - references are missing.");
            return;
        }

        // Update Turn Text
        if (currentPickerActorNumber == -1)
        {
            draftTurnText.text = "Waiting for draft to start...";
        }
        else
        {
            Player picker = PhotonNetwork.CurrentRoom.GetPlayer(currentPickerActorNumber);
            if (picker != null)
            {
                draftTurnText.text = (picker.IsLocal ? "Your Turn to Pick!" : $"Waiting for {picker.NickName} to pick...");
            }
            else
            {
                 draftTurnText.text = "Waiting for player..."; // Player might have left
            }
        }

        // Clear previous option buttons
        foreach (Transform child in draftOptionsPanel.transform)
        {
            if (child.gameObject != draftOptionButtonTemplate) // Don't destroy template
            {
                Destroy(child.gameObject);
            }
        }

        bool isMyTurn = PhotonNetwork.LocalPlayer.ActorNumber == currentPickerActorNumber;

        // Populate with current options IF it's the local player's turn
        if (isMyTurn)
        {
            if (currentDraftOptions.Count == 0)
            {
                 draftTurnText.text = "No options remaining..."; // Or handle end draft
            }
            else
            {
                foreach (var serializableOption in currentDraftOptions)
                {
                    // Use the activeOptionMap which has richer data
                    if (activeOptionMap.TryGetValue(serializableOption.OptionId, out DraftOption optionData))
                    {
                         GameObject optionButtonGO = Instantiate(draftOptionButtonTemplate, draftOptionsPanel.transform);
                        optionButtonGO.SetActive(true);

                        TextMeshProUGUI buttonText = optionButtonGO.GetComponentInChildren<TextMeshProUGUI>();
                        if (buttonText != null) buttonText.text = optionData.Description;

                        Button optionButton = optionButtonGO.GetComponent<Button>();
                        if (optionButton != null)
                        {
                            // Make a local copy of the ID for the lambda closure
                            int optionId = optionData.OptionId; 
                            optionButton.onClick.RemoveAllListeners(); // Clear previous
                            optionButton.onClick.AddListener(() => HandleOptionSelected(optionId));
                            optionButton.interactable = true; // Enable button
                        }
                    }
                    else
                    {
                         Debug.LogWarning($"Option ID {serializableOption.OptionId} found in list but not in active map. UI cannot display.");
                    }
                }
            }
        }
        else
        {
            // Not my turn, maybe show options dimmed or just the turn text
            // Current implementation just shows empty panel if not my turn
            Debug.Log("Not local player's turn, not showing draft options.");
        }
    }

    private void HandleOptionSelected(int selectedOptionId)
    {
        Debug.Log($"Local player selected option ID: {selectedOptionId}");

        // Disable buttons immediately to prevent double-clicks
        if (draftOptionsPanel != null)
        {
            foreach (Transform child in draftOptionsPanel.transform)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }
        }
        draftTurnText.text = "Sending choice...";

        // Find the selected option data locally *before* sending RPC
        // We need this to apply the effect later
        if (activeOptionMap.TryGetValue(selectedOptionId, out DraftOption chosenOptionData))
        {   
            // Apply the effect locally immediately for responsiveness
            // The state change confirmation will come via OnRoomPropertiesUpdate
            Debug.Log("Applying draft choice locally BEFORE confirmation."); // Optional immediate apply
            ApplyDraftChoice(chosenOptionData);
        }
        else
        {
             Debug.LogError($"Selected option ID {selectedOptionId} not found in local active map when trying to apply! Cannot apply effect.");
             // Should not happen if UI was populated correctly
        }

        // Send RPC to Master Client to make the choice official
        photonView.RPC("RpcSelectDraftOption", RpcTarget.MasterClient, selectedOptionId);
    }

    // --- Draft Logic Methods ---
    private void GenerateAndDistributeDraftOptions()
    {
         if (!PhotonNetwork.IsMasterClient) return;
         Debug.Log("Master Client generating draft options...");

        List<SerializableDraftOption> generatedOptions = new List<SerializableDraftOption>();
        int optionIdCounter = 0; // Simple unique ID for this draft pool

        // Determine number of options to generate (e.g., based on player count)
        int totalOptionsToGenerate = PhotonNetwork.CurrentRoom.PlayerCount * optionsPerDraft; // Example: 3 options per player

        // Simple generation logic (replace with more sophisticated selection)
        for (int i = 0; i < totalOptionsToGenerate; i++)
        {
            optionIdCounter++;
            int choiceType = Random.Range(0, 4); // 0: Player Card, 1: Pet Card, 2: Player Stat, 3: Pet Stat

            if (choiceType <= 1 && (allPlayerCardPool.Count > 0 || allPetCardPool.Count > 0)) // Add Card
            {
                bool forPet = (choiceType == 1 && allPetCardPool.Count > 0) || allPlayerCardPool.Count == 0;
                List<CardData> pool = forPet ? allPetCardPool : allPlayerCardPool;
                if (pool.Count > 0)
                {
                    CardData randomCard = pool[Random.Range(0, pool.Count)];
                    generatedOptions.Add(SerializableDraftOption.FromDraftOption(DraftOption.CreateAddCardOption(optionIdCounter, randomCard, forPet)));
                }
                else i--; // Try again if selected pool was empty
            }
            else // Upgrade Stat
            {
                 bool forPet = (choiceType == 3); // 2=Player, 3=Pet
                 StatType stat = (StatType)Random.Range(0, System.Enum.GetValues(typeof(StatType)).Length);
                 int amount = 0;
                 if (stat == StatType.MaxHealth) amount = Random.Range(5, 11); // e.g., 5-10 health
                 else if (stat == StatType.StartingEnergy) amount = 1; // Always +1 energy?
                 // Add other stat amounts

                if (amount > 0)
                {
                    generatedOptions.Add(SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionIdCounter, stat, amount, forPet)));
                }
                 else i--; // Try again if amount was 0 (e.g., for unhandled stats)
            }
        }

        // Shuffle the generated options
        System.Random rng = new System.Random();
        generatedOptions = generatedOptions.OrderBy(a => rng.Next()).ToList();

        // Determine player order (simple example: based on ActorNumber)
        List<int> playerOrder = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(n => n).ToList();
        string playerOrderJson = JsonConvert.SerializeObject(playerOrder);

        // Serialize options for network
        string optionsJson = JsonConvert.SerializeObject(generatedOptions);

        // Set initial Room Properties
        Hashtable initialRoomProps = new Hashtable
        {
            { DRAFT_OPTIONS_PROP, optionsJson },
            { DRAFT_TURN_PROP, playerOrder[0] }, // First player in order starts
            { DRAFT_ORDER_PROP, playerOrderJson }
        };

        Debug.Log($"Setting initial draft properties: {generatedOptions.Count} options, first turn {playerOrder[0]}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(initialRoomProps);
    }

    private void ApplyDraftChoice(DraftOption choice)
    {
        Debug.Log($"Applying draft choice: {choice.Description}");
        switch (choice.Type)
        {
            case DraftOptionType.AddPlayerCard:
                if (choice.CardToAdd != null)
                {
                    deck.Add(choice.CardToAdd); // Add to deck (or maybe a temporary reward list?)
                    ShuffleDeck(); // Reshuffle after adding
                    UpdateDeckCountUI();
                    Debug.Log($"Added {choice.CardToAdd.cardName} to player deck.");
                }
                break;
            case DraftOptionType.AddPetCard:
                if (choice.CardToAdd != null)
                {
                    // TODO: Need a dedicated Pet Deck list for the local player's pet
                    // For now, adding to opponentPetDeck as placeholder, Needs Fix
                    // opponentPetDeck.Add(choice.CardToAdd); 
                    // ShuffleOpponentPetDeck();
                    localPetDeck.Add(choice.CardToAdd); // CORRECT: Add to the local player's pet deck
                    ShuffleLocalPetDeck(); // Shuffle after adding
                    // Debug.LogWarning("Added card to Pet Deck - Placeholder: Needs dedicated pet deck list");
                    Debug.Log($"Added {choice.CardToAdd.cardName} to local pet deck.");
                     // Update pet deck UI if exists
                }
                break;
            case DraftOptionType.UpgradePlayerStat:
                 if (choice.StatToUpgrade == StatType.MaxHealth)
                 {
                    startingPlayerHealth += choice.StatIncreaseAmount; // Modify the base for next round
                    localPlayerHealth += choice.StatIncreaseAmount; // Increase current health too
                    Debug.Log($"Upgraded Player Max Health by {choice.StatIncreaseAmount}. New base: {startingPlayerHealth}");
                 }
                 else if (choice.StatToUpgrade == StatType.StartingEnergy)
                 {
                     startingEnergy += choice.StatIncreaseAmount;
                     currentEnergy += choice.StatIncreaseAmount; // Optionally give energy now?
                     Debug.Log($"Upgraded Player Starting Energy by {choice.StatIncreaseAmount}. New base: {startingEnergy}");
                 }
                 // Update UI relevant to the stat
                 UpdateHealthUI();
                 UpdateEnergyUI(); 
                 break;
            case DraftOptionType.UpgradePetStat:
                 if (choice.StatToUpgrade == StatType.MaxHealth)
                 {
                    startingPetHealth += choice.StatIncreaseAmount; // Modify the base for next round
                    localPetHealth += choice.StatIncreaseAmount; // Increase current health too
                    Debug.Log($"Upgraded Pet Max Health by {choice.StatIncreaseAmount}. New base: {startingPetHealth}");
                 }
                 // TODO: Handle Pet Energy if pets have energy?
                 // Update UI relevant to the stat
                 UpdateHealthUI();
                 break;

            // TODO: Implement UpgradePlayerCard / UpgradePetCard
            case DraftOptionType.UpgradePlayerCard:
                 Debug.LogWarning("ApplyDraftChoice: UpgradePlayerCard not implemented.");
                 break;
            case DraftOptionType.UpgradePetCard:
                 Debug.LogWarning("ApplyDraftChoice: UpgradePetCard not implemented.");
                 break;
        }
    }

    private void EndDraftPhase()
    {
        Debug.Log("Ending Draft Phase. Preparing for next round...");
        HideDraftScreen();

        // Reset necessary combat states BUT keep upgraded stats/decks
        // This is similar to InitializeCombatState but without resetting health/energy bases
        
        // Maybe transition to a brief "Round Start" screen?
        // For now, go directly back to combat setup

        // TODO: Check win condition (e.g., score limit reached)
        int scoreLimit = 10; // Example
        if (player1Score >= scoreLimit || player2Score >= scoreLimit)
        { 
            HandleGameOver();
        }
        else
        {
             // Start next combat round via RPC
             // Make sure flags are reset before RpcStartCombat is called again
            // ResetCombatFinishedFlags(); // Already called by Master before RpcStartDraft
             photonView.RPC("RpcStartCombat", RpcTarget.All);
        }
    }

    private void HandleGameOver()
    {
        Debug.Log("GAME OVER!");
        currentState = GameState.GameOver;
        HideDraftScreen();
        HideCombatScreen(); // Make sure combat is hidden too

        // TODO: Show Game Over screen with winner/loser info
        // Example:
        // GameObject gameOverScreen = Instantiate(gameOverCanvasPrefab);
        // TextMeshProUGUI winnerText = gameOverScreen.transform.Find("WinnerText").GetComponent<TextMeshProUGUI>();
        // winnerText.text = (player1Score > player2Score) ? $"{PhotonNetwork.LocalPlayer.NickName} Wins!" : "Opponent Wins!"; // Basic 2 player
    }

    private void HideCombatScreen() // Helper added
    {
        Debug.Log("Hiding Combat Screen");
        if(combatInstance != null) combatInstance.SetActive(false);
    }

    private void ShuffleLocalPetDeck() // New method for local pet
    {
        System.Random rng = new System.Random();
        int n = localPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = localPetDeck[k];
            localPetDeck[k] = localPetDeck[n];
            localPetDeck[n] = value;
        }
        Debug.Log("Local Pet Deck shuffled.");
    }

    #endregion

} // End of GameManager Class


// Helper class for serializing draft options via JSON for Photon Properties
[System.Serializable]
public class SerializableDraftOption
{
    public int OptionId;
    public DraftOptionType Type;
    public string Description;

    // Store identifiers instead of direct references
    public string CardName;        // For Add/Upgrade Card
    public bool IsPetCard;
    public StatType StatToUpgrade; // For Upgrade Stat
    public int StatIncreaseAmount; // For Upgrade Stat
    public bool IsPetStat;

    // Convert from DraftOption to SerializableDraftOption
    public static SerializableDraftOption FromDraftOption(DraftOption option)
    {
        var serializable = new SerializableDraftOption
        {
            OptionId = option.OptionId,
            Type = option.Type,
            Description = option.Description,
            StatToUpgrade = option.StatToUpgrade,
            StatIncreaseAmount = option.StatIncreaseAmount,
            IsPetStat = (option.Type == DraftOptionType.UpgradePetStat)
        };

        if (option.Type == DraftOptionType.AddPlayerCard || option.Type == DraftOptionType.AddPetCard)
        {
            serializable.CardName = option.CardToAdd?.cardName;
            serializable.IsPetCard = (option.Type == DraftOptionType.AddPetCard);
        }
        // TODO: Handle UpgradePlayerCard / UpgradePetCard serialization (e.g., store CardName of card to upgrade)

        return serializable;
    }

    // Convert from SerializableDraftOption back to DraftOption
    public DraftOption ToDraftOption(List<CardData> playerCardPool, List<CardData> petCardPool)
    {
        DraftOption option = new DraftOption(this.OptionId)
        {
            Type = this.Type,
            Description = this.Description,
            StatToUpgrade = this.StatToUpgrade,
            StatIncreaseAmount = this.StatIncreaseAmount
        };

        if (this.Type == DraftOptionType.AddPlayerCard || this.Type == DraftOptionType.AddPetCard)
        {
            List<CardData> pool = this.IsPetCard ? petCardPool : playerCardPool;
            option.CardToAdd = pool?.FirstOrDefault(card => card.cardName == this.CardName);
            if (option.CardToAdd == null && !string.IsNullOrEmpty(this.CardName))
            {
                Debug.LogWarning($"Could not find card with name '{this.CardName}' in the corresponding pool during deserialization.");
                return null; // Indicate failure to deserialize fully
            }
        }
        // TODO: Handle UpgradePlayerCard / UpgradePetCard deserialization

        return option;
    }
} 