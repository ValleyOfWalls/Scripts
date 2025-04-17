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
    [SerializeField] private TMP_InputField playerNameInput; // <<< ADDED
    [SerializeField] private TMP_InputField petNameInput; // <<< ADDED

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
    private const string PLAYER_SCORE_PROP = "PlayerScore";

    // Draft State Keys (Room Properties) - Queued Pack Draft Style
    // private const string DRAFT_PACKS_PROP = "DraftPacks";     // OLD: Dictionary<int ActorNum, string SerializedPack>
    private const string DRAFT_PLAYER_QUEUES_PROP = "DraftQueues"; // NEW: Dictionary<int ActorNum, List<string> SerializedPacks>
    private const string DRAFT_PICKS_MADE_PROP = "DraftPicks"; // NEW: Dictionary<int ActorNum, int Picks>
    private const string DRAFT_ORDER_PROP = "DraftOrder";    // Stores List<int> of ActorNumbers (Pass direction)

    // Local Draft State Cache - Pack Draft Style
    // private List<SerializableDraftOption> currentDraftOptions = new List<SerializableDraftOption>(); // OLD
    // private int currentPickerActorNumber = -1; // OLD
    private List<int> draftPlayerOrder = new List<int>();
    private List<SerializableDraftOption> localCurrentPack = new List<SerializableDraftOption>(); // Local player's current pack
    private Dictionary<int, DraftOption> localActiveOptionMap = new Dictionary<int, DraftOption>(); // Maps OptionId to full DraftOption for local pack
    private Dictionary<int, int> draftPicksMade = new Dictionary<int, int>(); // Local cache of picks made
    private bool isWaitingForLocalPick = false; // NEW FLAG: True if player is currently presented with a pack
    private bool isWaitingToStartCombatRPC = false; // Master client flag

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

    // Combat State Keys
    private const string COMBAT_PAIRINGS_PROP = "CombatPairings"; // NEW: Dictionary<int, int> PlayerActorNum -> OpponentPetOwnerActorNum
    private const string PLAYER_BASE_PET_HP_PROP = "BasePetHP"; // NEW: Player Custom Property

    // Local Player/Pet Info
    private string localPetName = "MyPet"; // <<< ADDED default pet name
    private bool userInitiatedConnection = false; // <<< ADDED flag for connection logic

    #region Unity Methods

    void Start()
    {
        // Ensure we have only one GameManager instance
        if (FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length > 1) // Recommended replacement
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

            // <<< MODIFIED: Find CenterPanel first >>>
            Transform centerPanel = startScreenInstance.transform.Find("CenterPanel");
            if (centerPanel != null)
            {
                connectButton = centerPanel.Find("ConnectButton")?.GetComponent<Button>();
                playerNameInput = centerPanel.Find("PlayerNameInput")?.GetComponent<TMP_InputField>();
                petNameInput = centerPanel.Find("PetNameInput")?.GetComponent<TMP_InputField>();

                if (connectButton != null)
                {
                    connectButton.onClick.AddListener(ConnectToPhoton);
                }
                else Debug.LogError("ConnectButton not found within CenterPanel in StartScreenCanvasPrefab!");

                if (playerNameInput == null) Debug.LogError("PlayerNameInput not found within CenterPanel in StartScreenCanvasPrefab!");
                else playerNameInput.text = "Player_" + Random.Range(1000, 9999); // Default Player Name

                if (petNameInput == null) Debug.LogError("PetNameInput not found within CenterPanel in StartScreenCanvasPrefab!");
                else petNameInput.text = "Buddy_" + Random.Range(100, 999); // Default Pet Name
            }
            else
            {
                 Debug.LogError("CenterPanel not found in StartScreenCanvasPrefab!");
                 // Assign null to prevent potential NullReferenceExceptions later if needed
                 connectButton = null;
                 playerNameInput = null;
                 petNameInput = null;
            }
        }
        else Debug.LogError("StartScreenCanvasPrefab is not assigned to GameManager!");

        // Initially hide Lobby & Combat (will be instantiated when needed)
    }

    #endregion

    #region Photon Connection & Room Logic

    private void ConnectToPhoton()
    {
        Debug.Log("Connecting to Photon...");
        if(connectButton) connectButton.interactable = false;

        // <<< ADDED: Read names from input, set NickName, store pet name >>>
        string playerName = playerNameInput?.text;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player_" + Random.Range(1000, 9999); // Fallback name
        }
        PhotonNetwork.NickName = playerName;

        localPetName = petNameInput?.text;
        if (string.IsNullOrWhiteSpace(localPetName))
        {
            localPetName = "Buddy_" + Random.Range(100, 999); // Fallback pet name
        }
        Debug.Log($"Using Player Name: {PhotonNetwork.NickName}, Pet Name: {localPetName}");

        userInitiatedConnection = true; // Mark that the user clicked connect
        if(PhotonNetwork.IsConnected)
        {
             // Already connected to master, maybe from a previous session or quick leave/rejoin? Try joining room directly.
             Debug.Log("Already connected to Master, attempting to join/create room...");
             TryJoinGameRoom();
        }
        else
        {
            // Not connected, establish connection first. OnConnectedToMaster will handle joining.
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to Master Server. Player Nickname: {PhotonNetwork.NickName}");
        // <<< MODIFIED: Only join if user initiated >>>
        if (userInitiatedConnection)
        {
            Debug.Log("User initiated connection - attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
             Debug.Log("Connected to Master, but not user-initiated. Waiting for user action.");
             // Make sure connect button is interactable if we somehow got here without user action
             if (startScreenInstance != null && startScreenInstance.activeSelf && connectButton != null)
             {
                 connectButton.interactable = true;
             }
        }
    }

    // <<< ADDED Helper Method >>>
    private void TryJoinGameRoom()
    {
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("asd", roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        userInitiatedConnection = false; // Reset flag once joined
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
        userInitiatedConnection = false; // <<< ADDED: Reset flag on leaving
        currentState = GameState.Connecting; // Transition state back
        if (lobbyInstance != null) Destroy(lobbyInstance); // Clean up instantiated lobby
        if (combatInstance != null) Destroy(combatInstance); // Clean up instantiated combat
        if (draftInstance != null) Destroy(draftInstance); // <<< ADDED: Clean up draft screen too

        // Destroy card instances if they are parented to the canvas root during drag/drop?
        // Might need more robust cleanup depending on drag/drop implementation.

        if (startScreenInstance != null)
        {
            startScreenInstance.SetActive(true);
            // <<< Update default names when returning? Optional >>>
            // if (playerNameInput != null) playerNameInput.text = "Player_" + Random.Range(1000, 9999);
            // if (petNameInput != null) petNameInput.text = "Buddy_" + Random.Range(100, 999);
            if (connectButton != null) connectButton.interactable = true;
        }
        // Reset state if necessary
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join Room Failed: ({returnCode}) {message}");
        userInitiatedConnection = false; // <<< ADDED: Reset flag on failure
        if (connectButton != null) connectButton.interactable = true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: ({returnCode}) {message}");
        userInitiatedConnection = false; // <<< ADDED: Reset flag on failure
        if (connectButton != null) connectButton.interactable = true;
    }

     public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
        userInitiatedConnection = false; // Reset flag on disconnect
        currentState = GameState.Connecting; // Go back to connecting state

        // Clean up UI instances
        if (lobbyInstance != null) Destroy(lobbyInstance);
        if (combatInstance != null) Destroy(combatInstance);
        if (draftInstance != null) Destroy(draftInstance);

        // Show Start Screen and enable button
        if (startScreenInstance != null)
        {
             startScreenInstance.SetActive(true);
             if (connectButton != null) connectButton.interactable = true;
             // Optionally clear/reset input fields here too
        }
        else
        {
             // If start screen was somehow destroyed, we might need to recreate it or handle differently
             Debug.LogError("Start screen instance not found after disconnect!");
        }
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
        else if (changedProps.ContainsKey(PLAYER_SCORE_PROP)) // Check for score changes
        {
            Debug.Log($"Score property updated for {targetPlayer.NickName}. Refreshing Score UI.");
            UpdateScoreUI(); // Update UI when score changes
        }
        // Note: We now handle draft state changes in OnRoomPropertiesUpdate
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log($"Room properties updated. CurrentState: {currentState}");

        // Master Client check: If waiting to send StartCombat RPC and pairings are now set
        if (PhotonNetwork.IsMasterClient && isWaitingToStartCombatRPC && propertiesThatChanged.ContainsKey(COMBAT_PAIRINGS_PROP))
        {
            Debug.Log("Master Client confirms pairings property set. Sending RpcStartCombat.");
            isWaitingToStartCombatRPC = false; // Reset flag
            photonView.RPC("RpcStartCombat", RpcTarget.All);
            // Return early? Or let subsequent checks run? Let subsequent checks run for now.
        }

        if (currentState == GameState.Drafting)
        {
            bool draftStateUpdated = false;
            // Check for changes in the packs or picks made
            if (propertiesThatChanged.ContainsKey(DRAFT_PLAYER_QUEUES_PROP))
            {
                Debug.Log("Draft packs updated in room properties.");
                UpdateLocalDraftStateFromRoomProps(); // Updated method name
                // draftStateUpdated = true; // UpdateLocalDraftStateFromRoomProps now handles UI update internally
            }
            if (propertiesThatChanged.ContainsKey(DRAFT_PICKS_MADE_PROP))
            {
                 Debug.Log("Draft picks made updated in room properties.");
                 UpdateLocalDraftPicksFromRoomProps(); // Separate update for picks cache
                 // May not trigger UI update directly, depends on if you display picks
            }
            if (propertiesThatChanged.ContainsKey(DRAFT_ORDER_PROP))
            {
                 UpdateLocalDraftOrderFromRoomProps();
            }

            if (draftStateUpdated)
            {
                // NOTE: Draft UI update is now handled within UpdateLocalDraftStateFromRoomProps
                // Debug.Log("Updating Draft UI due to room property change.");
                // UpdateDraftUI(); 
            }

            // --- Check Draft End Condition --- 
            object queuesProp = null;
            bool queuesExist = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PLAYER_QUEUES_PROP, out queuesProp);
            bool draftEnded = !queuesExist || queuesProp == null;
            string queuesJson = queuesProp as string; // Get JSON regardless of initial draftEnded state for logging
            Debug.Log($"OnRoomPropertiesUpdate: Checking draft end. queuesExist={queuesExist}, queuesProp='{queuesJson}'");

            if (!draftEnded && !string.IsNullOrEmpty(queuesJson))
            {
                try
                {
                    var queuesDict = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson);
                    if (queuesDict == null) 
                    {
                         Debug.LogWarning("End Check: Deserialized queuesDict is null.");
                         draftEnded = true; // Treat null dict as ended
                    }
                    else if (queuesDict.Count == 0)
                    {
                         Debug.Log("End Check: queuesDict count is 0.");
                         draftEnded = true; 
                    }
                    else
                    {
                        bool allEmpty = queuesDict.Values.All(q => q == null || q.Count == 0);
                        Debug.Log($"End Check: queuesDict count={queuesDict.Count}. All queues empty={allEmpty}");
                        if (allEmpty)
                        {
                            draftEnded = true;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error checking draft end condition: {e.Message}");
                    draftEnded = true; // Assume ended if state is corrupted
                }
            }
            else if (!draftEnded && string.IsNullOrEmpty(queuesJson))
            {
                 // If queuesExist was true but the string is null/empty, it means draft ended.
                 Debug.Log("End Check: queuesProp exists but is null/empty string.");
                 draftEnded = true;
            }

            if (draftEnded)
            {
                // Prevent multiple calls if EndDraftPhase was already triggered
                if (currentState == GameState.Drafting)
                {
                    Debug.Log("Draft queues property indicates draft ended. Calling EndDraftPhase.");
                    EndDraftPhase();
                }
            }
            // --- End Draft End Condition Check --- 
        }
        // Handle other room property updates if necessary
    }

    // Helper methods to update local state from Room Properties
    // --- UPDATED: Renamed and modified for Pack Draft --- 
    private void UpdateLocalDraftStateFromRoomProps()
    {
        // Don't process new packs if player hasn't finished with the current one
        if (isWaitingForLocalPick)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps: Skipping update, player is still picking (isWaitingForLocalPick=true).");
            return;
        }

        bool needsUIUpdate = false;
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        bool nowHasPack = false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PLAYER_QUEUES_PROP, out object queuesObj))
        {
            try
            {
                string queuesJson = queuesObj as string;
                Dictionary<int, List<string>> currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson) ?? new Dictionary<int, List<string>>();

                int localActorNum = PhotonNetwork.LocalPlayer.ActorNumber;
                if (currentQueues.ContainsKey(localActorNum) && currentQueues[localActorNum] != null && currentQueues[localActorNum].Count > 0)
                {
                    string nextPackJson = currentQueues[localActorNum][0]; // Peek at the first pack
                    List<SerializableDraftOption> serializablePack = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(nextPackJson) ?? new List<SerializableDraftOption>();
                    
                    // Deserialize into full DraftOption, linking actual CardData
                    foreach (var serializableOption in serializablePack)
                    {
                        // PASS LOCAL DECKS HERE
                        DraftOption fullOption = serializableOption.ToDraftOption(allPlayerCardPool, allPetCardPool, deck, localPetDeck); 
                        if (fullOption != null)
                        {
                            localCurrentPack.Add(serializableOption); // Still store the serializable version
                            localActiveOptionMap[serializableOption.OptionId] = fullOption; // Map ID to full data
                        }
                        else 
                        {
                            Debug.LogError($"Failed to deserialize draft option ID {serializableOption.OptionId} ({serializableOption.Description}) - skipping.");
                        }
                    }

                    if(localCurrentPack.Count > 0)
                    {
                        nowHasPack = true;
                        isWaitingForLocalPick = true; // We received a pack, now wait for player input
                        needsUIUpdate = true;
                         Debug.Log($"Deserialized pack for local player. {localCurrentPack.Count} options. Setting isWaitingForLocalPick=true.");
                    }
                    else
                    {
                        Debug.LogWarning("Deserialized pack for local player, but it resulted in zero valid options after deserialization.");
                        // Potentially try to dequeue this empty pack and check the next one?
                        // For now, just log.
                    }
                }
                else
                {
                    Debug.Log("No pack currently queued for local player.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft queues: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PLAYER_QUEUES_PROP} not found.");
        }

        if (needsUIUpdate)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps triggering Draft UI update.");
            UpdateDraftUI();
        }
        else if (!nowHasPack && isWaitingForLocalPick)
        {
            // If we were waiting, but now have no pack, reset waiting flag
            Debug.Log("Resetting isWaitingForLocalPick as no pack was found.");
            isWaitingForLocalPick = false; 
            UpdateDraftUI(); // Update UI to show waiting state
        }
    }

    private void UpdateLocalDraftPicksFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PICKS_MADE_PROP, out object picksObj))
        {
            try
            {
                 string picksJson = picksObj as string;
                 draftPicksMade = JsonConvert.DeserializeObject<Dictionary<int, int>>(picksJson) ?? new Dictionary<int, int>();
                 Debug.Log($"Successfully deserialized draft picks made: {draftPicksMade.Count} players.");
            }
            catch(System.Exception e)
            {
                 Debug.LogError($"Failed to deserialize draft picks: {e.Message}");
                 draftPicksMade = new Dictionary<int, int>();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PICKS_MADE_PROP} not found.");
            draftPicksMade = new Dictionary<int, int>();
        }
    }

    private void UpdateLocalDraftTurnFromRoomProps() // Kept for compatibility if needed elsewhere, but not primary logic anymore
    {
         /* // OLD LOGIC
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
         */
         // No longer relevant for pack draft turn logic
    }

    private void UpdateLocalDraftOrderFromRoomProps()
    {
         // ... (Keep existing logic as draft order is still used for passing) ...
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

    // Rebuilds the Dict mapping OptionId to full DraftOption *for the local pack*
    private void RebuildLocalActiveOptionMap()
    {
        localActiveOptionMap.Clear();
        foreach (var serializableOption in localCurrentPack)
        {
            // Pass local decks for accurate deserialization, especially for UpgradeCard options
            DraftOption fullOption = serializableOption.ToDraftOption(allPlayerCardPool, allPetCardPool, deck, localPetDeck);
            if (fullOption != null)
            {
                localActiveOptionMap[fullOption.OptionId] = fullOption;
            }
            else
            {
                 Debug.LogWarning($"Could not fully deserialize option ID {serializableOption.OptionId} in local pack, Card maybe missing from pools or deck?");
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
        // Ensure minimum player count
        if (PhotonNetwork.PlayerList.Length < 2)
        {
             Debug.LogWarning("Cannot start game, need at least 2 players.");
             return;
        }

        Debug.Log("Master Client is Starting Game...");

        // Prevent others from joining
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // Initialize scores for all players to 0
        Debug.Log("Initializing player scores...");
        Hashtable initialScoreProps = new Hashtable { { PLAYER_SCORE_PROP, 0 } };
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            p.SetCustomProperties(initialScoreProps);
            Debug.Log($"Set initial score for {p.NickName}");
        }

        // Call the preparation method which will set pairings and THEN call RpcStartCombat
        PrepareNextCombatRound(); 
    }

    [PunRPC]
    private void RpcStartCombat()
    {
        Debug.Log($"RPC: Starting Combat Setup for {PhotonNetwork.LocalPlayer.NickName}");
        currentState = GameState.Combat; // Transition state

        // Fetch pairings from Room Properties
        int opponentPetOwnerActorNum = -1;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(COMBAT_PAIRINGS_PROP, out object pairingsObj))
        {
            try
            {
                string pairingsJson = pairingsObj as string;
                var pairingsDict = JsonConvert.DeserializeObject<Dictionary<int, int>>(pairingsJson);
                if (pairingsDict != null && pairingsDict.TryGetValue(PhotonNetwork.LocalPlayer.ActorNumber, out int oppActorNum))
                {
                    opponentPetOwnerActorNum = oppActorNum;
                    Debug.Log($"Combat pairing found: Fighting pet of player {opponentPetOwnerActorNum}");
                }
                else
                {
                    Debug.LogWarning("Could not find local player's pairing in COMBAT_PAIRINGS_PROP.");
                }
            }
            catch(System.Exception e)
            {
                 Debug.LogError($"Failed to deserialize combat pairings: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {COMBAT_PAIRINGS_PROP} not found.");
        }

        // Disable Lobby
        if (lobbyInstance != null) lobbyInstance.SetActive(false);

        // Enable Combat Screen
        ShowCombatScreen();

        // Initialize combat state for this player
        // InitializeCombatState(); // OLD CALL
        InitializeCombatState(opponentPetOwnerActorNum); // NEW CALL with opponent info
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

    private void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        Debug.Log($"Initializing Combat State for round. Fighting pet of ActorNum {opponentPetOwnerActorNum}");
        
        combatEndedForLocalPlayer = false; // Reset combat end flag for the new round

        // Determine opponent player based on provided ActorNumber
        opponentPlayer = null; // Reset from previous round
        if (opponentPetOwnerActorNum > 0)
        {
            opponentPlayer = PhotonNetwork.CurrentRoom.GetPlayer(opponentPetOwnerActorNum);
        }

        if (opponentPlayer == null && opponentPetOwnerActorNum > 0)
        {
            Debug.LogError($"Could not find opponent player with ActorNum {opponentPetOwnerActorNum}!");
             // Handle this case? Maybe assign a default/dummy opponent?
        }
        else if (opponentPetOwnerActorNum <= 0)
        {
            Debug.LogWarning("InitializeCombatState called with invalid opponent ActorNum. Likely single player or pairing issue.");
        }

        // Initialize Health - Use potentially upgraded BASE values
        // Player health resets to their current startingPlayerHealth
        localPlayerHealth = startingPlayerHealth; 
        // Local pet health resets to its current startingPetHealth
        localPetHealth = startingPetHealth; 
        
        // Opponent pet health resets based on THEIR base value (from Player Property)
        int opponentBasePetHealth = startingPetHealth; // Default if property not found
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { opponentBasePetHealth = (int)oppBasePetHP; }
            catch { Debug.LogError($"Failed to cast {PLAYER_BASE_PET_HP_PROP} for player {opponentPlayer.NickName}"); }
        }
        opponentPetHealth = opponentBasePetHealth; // Refresh opponent pet health
        Debug.Log($"Set opponent pet health to {opponentPetHealth} (Base: {opponentBasePetHealth})");

        // Setup Initial UI
        if(playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        UpdateHealthUI(); // Update UI with refreshed health values
        if (opponentPetNameText) opponentPetNameText.text = opponentPlayer != null ? $"{opponentPlayer.NickName}'s Pet" : "Opponent Pet";
        if (ownPetNameText) ownPetNameText.text = $"{localPetName}"; // <<< MODIFIED

        // Initialize Player Deck (reshuffle existing deck)
        // Check if this is likely the first round (deck, hand, discard are empty)
        if (deck.Count == 0 && hand.Count == 0 && discardPile.Count == 0)
        {
            Debug.Log("First round detected: Initializing deck from starterDeck.");
            deck = new List<CardData>(starterDeck); 
            // Hand and discard are already clear
        }
        else
        {
            // Subsequent rounds: Combine hand/discard into deck
             Debug.Log("Subsequent round detected: Reshuffling existing deck/hand/discard.");
            discardPile.AddRange(hand); 
            hand.Clear();
            deck.AddRange(discardPile); 
            discardPile.Clear();
        }
        ShuffleDeck(); // Shuffle the full deck (either new or combined)
        UpdateDeckCountUI();

        // Re-initialize Opponent Pet Deck Simulation State
        // This simulation uses the STARTING pet deck defined in the inspector for simplicity.
        // A more complex simulation could try to mirror the opponent's actual pet deck state.
        if (starterPetDeck != null && starterPetDeck.Count > 0)
        {
            opponentPetDeck = new List<CardData>(starterPetDeck);
            ShuffleOpponentPetDeck();
        }
        else
        {
             opponentPetDeck = new List<CardData>();
        }
        opponentPetHand.Clear();
        opponentPetDiscard.Clear();

        // Initialize Local Pet Deck (reshuffle existing)
        // localPetDeck = new List<CardData>(starterPetDeck); // Don't reset to starter deck
        ShuffleLocalPetDeck(); // Just reshuffle the current pet deck

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
        if (!scoreText) return;

        // Fetch scores from Player Properties
        int localScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object localScoreVal))
        {
             try { localScore = (int)localScoreVal; } catch { /* Ignore cast error, keep 0 */ }
        }

        int opponentScore = 0;
        // Find the opponent player (simple 2-player logic assumption)
        Player currentOpponent = null; 
        foreach(Player p in PhotonNetwork.PlayerList)
        {
            if (p != PhotonNetwork.LocalPlayer) 
            {
                currentOpponent = p;
                break;
            }
        }
        
        if (currentOpponent != null && currentOpponent.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object oppScoreVal))
        {
             try { opponentScore = (int)oppScoreVal; } catch { /* Ignore cast error, keep 0 */ }
        }
        
        // Basic 2-player score display
        scoreText.text = $"Score: You: {localScore} / Opp: {opponentScore}"; 
        
        // --- FOR > 2 players, needs different display logic --- 
        /* Example:
        System.Text.StringBuilder scoreString = new System.Text.StringBuilder("Scores: ");
        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
        {
            int score = 0;
            if (p.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
            {
                 try { score = (int)scoreVal; } catch {}
            }
            scoreString.Append($"{p.NickName}: {score}  ");
        }
        scoreText.text = scoreString.ToString();
        */
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

    [PunRPC] // NEW RPC for Pack Draft
    private void RpcPlayerPickedOption(int chosenOptionId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            // This RPC should only be processed by the Master Client
            return;
        }

        Debug.Log($"RPC Received: Player {info.Sender.NickName} ({info.Sender.ActorNumber}) picked Option ID {chosenOptionId}");

        if (currentState != GameState.Drafting)
        {
            Debug.LogWarning("RpcPlayerPickedOption received but not in Drafting state.");
            return;
        }

        // --- Master Client: Process the Pick --- 

        // 1. Get current state from Room Properties
        // Dictionary<int, string> currentPacks = null; // OLD
        Dictionary<int, List<string>> currentQueues = null; // NEW
        Dictionary<int, int> currentPicks = null;
        List<int> currentOrder = null;

        object queuesObj, picksObj, orderObj; // Renamed packsObj to queuesObj
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PLAYER_QUEUES_PROP, out queuesObj) || // Use new key
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PICKS_MADE_PROP, out picksObj) ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_ORDER_PROP, out orderObj))
        {
            Debug.LogError("RpcPlayerPickedOption: Failed to retrieve current draft state from Room Properties.");
            return;
        }

        try
        {
            // currentPacks = JsonConvert.DeserializeObject<Dictionary<int, string>>((string)packsObj) ?? new Dictionary<int, string>(); // OLD
            currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>((string)queuesObj) ?? new Dictionary<int, List<string>>(); // NEW
            currentPicks = JsonConvert.DeserializeObject<Dictionary<int, int>>((string)picksObj) ?? new Dictionary<int, int>();
            currentOrder = JsonConvert.DeserializeObject<List<int>>((string)orderObj) ?? new List<int>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RpcPlayerPickedOption: Failed to deserialize draft state: {e.Message}");
            return;
        }

        // 2. Validate the pick
        int pickerActorNum = info.Sender.ActorNumber;
        // if (!currentPacks.ContainsKey(pickerActorNum)) // OLD
        if (!currentQueues.ContainsKey(pickerActorNum) || currentQueues[pickerActorNum] == null || currentQueues[pickerActorNum].Count == 0) // NEW
        {
            Debug.LogWarning($"Player {pickerActorNum} sent RpcPlayerPickedOption but doesn't have a pack in their queue currently.");
            // Maybe they already picked, or state is out of sync?
            return;
        }

        // 3. Process the pack (take the FIRST pack from the queue)
        List<string> pickerQueue = currentQueues[pickerActorNum];
        string packToProcessJson = pickerQueue[0]; // Get the first pack
        pickerQueue.RemoveAt(0); // Remove it from the picker's queue

        List<SerializableDraftOption> packToProcess = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(packToProcessJson) ?? new List<SerializableDraftOption>();
        SerializableDraftOption pickedOption = packToProcess.FirstOrDefault(opt => opt.OptionId == chosenOptionId);

        if (pickedOption == null)
        {
            Debug.LogWarning($"Player {pickerActorNum} tried to pick invalid Option ID {chosenOptionId} from their current pack.");
            // Put the pack back at the front of the queue? Or just drop the pick?
            // For now, just log and return, effectively dropping the pick.
            // Consider adding pickerQueue.Insert(0, packToProcessJson); to revert if needed.
            return; // Invalid option ID for this pack
        }

        packToProcess.Remove(pickedOption); // Remove the chosen option
        Debug.Log($"Removed option {chosenOptionId} from pack for player {pickerActorNum}. Remaining in pack: {packToProcess.Count}. Remaining in queue: {pickerQueue.Count}");

        // 4. Update Picks Made Count
        currentPicks[pickerActorNum] = currentPicks.ContainsKey(pickerActorNum) ? currentPicks[pickerActorNum] + 1 : 1;

        // 5. Prepare properties to update
        Hashtable propsToUpdate = new Hashtable();
        // currentPacks.Remove(pickerActorNum); // OLD: No need to remove entry, just modified the list

        // 6. Pass the remaining pack (if any options left)
        if (packToProcess.Count > 0)
        {
            int pickerIndex = currentOrder.IndexOf(pickerActorNum);
            if (pickerIndex == -1)
            {
                 Debug.LogError($"RpcPlayerPickedOption: Picker {pickerActorNum} not found in draft order!");
                 return; // Should not happen
            }
            int nextPlayerIndex = (pickerIndex + 1) % currentOrder.Count;
            int nextPlayerActorNum = currentOrder[nextPlayerIndex];

            string remainingPackJson = JsonConvert.SerializeObject(packToProcess);
            
            // Get or create the queue for the next player
            List<string> nextPlayerQueue;
            if (!currentQueues.TryGetValue(nextPlayerActorNum, out nextPlayerQueue) || nextPlayerQueue == null)
            {
                nextPlayerQueue = new List<string>();
                currentQueues[nextPlayerActorNum] = nextPlayerQueue;
            }
            // APPEND the passed pack to the END of the next player's queue
            nextPlayerQueue.Add(remainingPackJson);
            
            Debug.Log($"Passing remaining {packToProcess.Count} options to player {nextPlayerActorNum}. Their queue size is now {nextPlayerQueue.Count}");
        }
        else
        {
            Debug.Log($"Pack processed for player {pickerActorNum} is now empty. Not passing.");
        }

        // 7. Set updated properties
        string finalQueuesJson = JsonConvert.SerializeObject(currentQueues);
        string finalPicksJson = JsonConvert.SerializeObject(currentPicks);
        propsToUpdate[DRAFT_PLAYER_QUEUES_PROP] = finalQueuesJson; // Update the queues
        propsToUpdate[DRAFT_PICKS_MADE_PROP] = finalPicksJson;
        
        Debug.Log($"Master Client setting properties. Queues JSON: {finalQueuesJson}, Picks JSON: {finalPicksJson}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(propsToUpdate);
    }

    #endregion

    #region Combat End Handling

    private void HandleCombatWin()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;

        Debug.Log("COMBAT WIN! Player defeated the opponent's pet.");
        // Award point (Placeholder)
        // player1Score++; // OLD local increment
        // UpdateScoreUI(); // OLD call with local scores

        // Award point using Player Property
        int currentScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
        {
            currentScore = (int)scoreVal;
        }
        int newScore = currentScore + 1;
        Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newScore } };
        // Use Check-And-Swap for safety against race conditions
        Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentScore } }; 
        PhotonNetwork.LocalPlayer.SetCustomProperties(scoreUpdate, expectedScore);
        Debug.Log($"Attempted to set local player score to {newScore} (expected {currentScore}).");
        // UI will be updated via OnPlayerPropertiesUpdate

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
        // player2Score++; // OLD local increment
        // UpdateScoreUI(); // OLD call with local scores

        // Award point to opponent using Player Property
        if (opponentPlayer != null)
        {
            int currentOpponentScore = 0;
            if (opponentPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
            {
                currentOpponentScore = (int)scoreVal;
            }
            int newOpponentScore = currentOpponentScore + 1;
            Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newOpponentScore } };
            Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentOpponentScore } };
            opponentPlayer.SetCustomProperties(scoreUpdate, expectedScore);
            Debug.Log($"Attempted to set opponent ({opponentPlayer.NickName}) score to {newOpponentScore} (expected {currentOpponentScore}).");
        }
        else
        {
            Debug.LogError("HandleCombatLoss: Cannot award point, opponentPlayer is null!");
        }
         // UI will be updated via OnPlayerPropertiesUpdate

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
        combatEndedForLocalPlayer = false; // Reset local flag

        // Hide Combat Screen
        if (combatInstance != null) combatInstance.SetActive(false);

        // Initialize local draft state cache
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        draftPlayerOrder.Clear();
        draftPicksMade.Clear();
        // currentPickerActorNumber = -1; // No longer used

        // Master Client generates and distributes draft options / sets initial state
        if (PhotonNetwork.IsMasterClient)
        {
            // GenerateAndDistributeDraftOptions(); // OLD NAME
            InitializeDraftState(); // NEW NAME
        }
        else
        {
            // Non-master clients need to fetch initial state set by master
            // UpdateLocalDraftOptionsFromRoomProps(); // OLD NAME
            UpdateLocalDraftStateFromRoomProps(); // NEW NAME
            UpdateLocalDraftPicksFromRoomProps();
            UpdateLocalDraftOrderFromRoomProps();
        }

        // Show Draft Screen
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
             return;
        }

        if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null)
        {
            Debug.LogError("Cannot update Draft UI - references are missing.");
            return;
        }

        // Clear previous option buttons first
        foreach (Transform child in draftOptionsPanel.transform)
        {
            if (child.gameObject != draftOptionButtonTemplate) // Don't destroy template
            {
                Destroy(child.gameObject);
            }
        }

        // Determine UI state based on whether the local player has a pack
        if (localCurrentPack.Count > 0)
        {
            draftTurnText.text = "Your Turn to Pick!";
            // Populate buttons from the local pack
            foreach (var serializableOption in localCurrentPack)
            {
                if (localActiveOptionMap.TryGetValue(serializableOption.OptionId, out DraftOption optionData))
                {
                    GameObject optionButtonGO = Instantiate(draftOptionButtonTemplate, draftOptionsPanel.transform);
                    optionButtonGO.SetActive(true);

                    TextMeshProUGUI buttonText = optionButtonGO.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null) buttonText.text = optionData.Description;

                    Button optionButton = optionButtonGO.GetComponent<Button>();
                    if (optionButton != null)
                    {
                        int optionId = optionData.OptionId;
                        optionButton.onClick.RemoveAllListeners();
                        optionButton.onClick.AddListener(() => HandleOptionSelected(optionId));
                        optionButton.interactable = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"Option ID {serializableOption.OptionId} found in local pack list but not in active map. UI cannot display.");
                }
            }
        }
        else
        {
            // Local player doesn't have a pack right now
            draftTurnText.text = "Waiting for next pack...";
            // Buttons are already cleared
        }
    }

    private void HandleOptionSelected(int selectedOptionId)
    {
        Debug.Log($"Local player selected option ID: {selectedOptionId}");

        // Validate: Do we actually have this option locally?
        if (!localActiveOptionMap.ContainsKey(selectedOptionId))
        {
             Debug.LogError($"HandleOptionSelected: Clicked option {selectedOptionId} but it's not in the localActiveOptionMap! Maybe double-click or state issue?");
             return; // Avoid proceeding if state is inconsistent
        }
        
        // Prevent further interaction immediately
        isWaitingForLocalPick = false; // Mark as no longer waiting for local pick

        // Disable buttons immediately and show waiting text
        if (draftOptionsPanel != null)
        {
             // ... (disable/clear buttons as before) ...
             foreach (Transform child in draftOptionsPanel.transform)
            {
                 if (child.gameObject != draftOptionButtonTemplate)
                 {
                     Button btn = child.GetComponent<Button>();
                     if (btn != null) btn.interactable = false;
                     Destroy(child.gameObject);
                 }
            }
        }
        if(draftTurnText != null) draftTurnText.text = "Waiting for next pack...";

        // Find the selected option data locally
        DraftOption chosenOptionData = localActiveOptionMap[selectedOptionId];
       
        // Apply the effect locally immediately
        Debug.Log("Applying draft choice locally.");
        ApplyDraftChoice(chosenOptionData);
        
        // Clear the local pack state immediately AFTER applying choice
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();

        // Send RPC to Master Client to make the choice official and pass the pack
        Debug.Log($"Sending RpcPlayerPickedOption for ID {selectedOptionId} to Master Client.");
        photonView.RPC("RpcPlayerPickedOption", RpcTarget.MasterClient, selectedOptionId);

        // Now that the pick is sent, immediately check if a new pack is waiting in the room state
        // Debug.Log("HandleOptionSelected: Immediately re-checking room state for pending pack after sending RPC."); // REMOVE THIS BLOCK
        // UpdateLocalDraftStateFromRoomProps(); 
        // UpdateDraftUI(); // No need to call here, UpdateLocalDraftStateFromRoomProps calls it if state changed
    }

    // --- Draft Logic Methods ---
    // --- RENAMED from GenerateAndDistributeDraftOptions ---
    private void InitializeDraftState()
    {
         if (!PhotonNetwork.IsMasterClient) return;
         Debug.Log("Master Client initializing draft state (creating queues)... ");

        // Dictionary<int, string> initialPacks = new Dictionary<int, string>(); // OLD
        Dictionary<int, List<string>> initialQueues = new Dictionary<int, List<string>>(); // NEW
        Dictionary<int, int> initialPicks = new Dictionary<int, int>();
        int totalOptionsGenerated = 0;
        int optionIdCounter = 0; // Make IDs unique across all options this draft

        // Determine player order (simple example: based on ActorNumber)
        List<int> playerOrder = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(n => n).ToList();
        string playerOrderJson = JsonConvert.SerializeObject(playerOrder);

        int initialOptionsPerPack = optionsPerDraft; // Use the existing setting

        // Create one pack for each player
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            List<SerializableDraftOption> packForPlayer = new List<SerializableDraftOption>();
            Debug.Log($"Generating pack for player {player.NickName} ({player.ActorNumber})");

            for (int i = 0; i < initialOptionsPerPack; i++)
            {
                 optionIdCounter++;
                 SerializableDraftOption newOption = GenerateSingleDraftOption(optionIdCounter);
                 if (newOption != null) 
                 { 
                    packForPlayer.Add(newOption);
                    totalOptionsGenerated++;
                 }
                 else i--; // Try again if generation failed (e.g., pools empty)
            }

            // Shuffle the pack for this player
            System.Random packRng = new System.Random();
            packForPlayer = packForPlayer.OrderBy(a => packRng.Next()).ToList();

            // Serialize and add to the initial packs dictionary
            // initialPacks[player.ActorNumber] = JsonConvert.SerializeObject(packForPlayer); // OLD
            // NEW: Add the serialized pack as the first element in the player's queue
            initialQueues[player.ActorNumber] = new List<string> { JsonConvert.SerializeObject(packForPlayer) };

            initialPicks[player.ActorNumber] = 0; // Initialize picks made to 0
        }

        // Serialize dictionaries for network
        // string packsJson = JsonConvert.SerializeObject(initialPacks); // OLD
        string queuesJson = JsonConvert.SerializeObject(initialQueues); // NEW
        string picksJson = JsonConvert.SerializeObject(initialPicks);

        // Set initial Room Properties
        Hashtable initialRoomProps = new Hashtable
        {
            // { DRAFT_PACKS_PROP, packsJson }, // OLD
            { DRAFT_PLAYER_QUEUES_PROP, queuesJson }, // NEW
            { DRAFT_PICKS_MADE_PROP, picksJson },
            { DRAFT_ORDER_PROP, playerOrderJson }
            // { DRAFT_TURN_PROP, playerOrder[0] }, // No longer needed
        };

        Debug.Log($"Setting initial draft properties: {initialQueues.Count} queues generated ({totalOptionsGenerated} total options), Order: {string.Join(", ", playerOrder)}"); // NEW
        PhotonNetwork.CurrentRoom.SetCustomProperties(initialRoomProps);
    }

    // Helper to generate one option (extracted from old loop)
    private SerializableDraftOption GenerateSingleDraftOption(int optionId)
    {
        // Increased range to potentially include Upgrade Card options
        int choiceType = Random.Range(0, 6); // 0: Player Add, 1: Pet Add, 2: Player Stat, 3: Pet Stat, 4: Player Upgrade, 5: Pet Upgrade

        // --- Try Upgrade Card --- (if applicable deck has upgradable cards)
        if (choiceType >= 4) 
        {
            bool forPet = (choiceType == 5);
            List<CardData> deckToCheck = forPet ? localPetDeck : deck;
            // Find cards in the deck that HAVE an upgrade defined
            List<CardData> upgradableCards = deckToCheck.Where(card => card != null && card.upgradedVersion != null).ToList();

            if (upgradableCards.Count > 0)
            {
                CardData cardToUpgrade = upgradableCards[Random.Range(0, upgradableCards.Count)];
                DraftOption upgradeOption = DraftOption.CreateUpgradeCardOption(optionId, cardToUpgrade, forPet);
                if(upgradeOption != null) return SerializableDraftOption.FromDraftOption(upgradeOption);
                // else: generation failed (shouldn't happen if upgradableCards.Count > 0), fall through...
            }
            // else: No upgradable cards found in the target deck, fall through to other option types...
        }

        // --- Fallback / Original Logic: Try Add Card --- 
        if (choiceType <= 1 || choiceType >= 4) // Also try adding if upgrade failed
        {
             // Determine if adding for pet (either explicitly chosen or player pool empty)
             bool forPetAdd = (choiceType == 1 || choiceType == 5) ? // Prioritize pet if type 1 or 5
                                (allPetCardPool.Count > 0) :
                                (allPlayerCardPool.Count == 0 && allPetCardPool.Count > 0);

             List<CardData> pool = forPetAdd ? allPetCardPool : allPlayerCardPool;
             if (pool.Count > 0)
             {
                 CardData randomCard = pool[Random.Range(0, pool.Count)];
                 return SerializableDraftOption.FromDraftOption(DraftOption.CreateAddCardOption(optionId, randomCard, forPetAdd));
             }
             // else: Selected pool was empty, fall through...
        }

        // --- Fallback: Upgrade Stat ---
        // Only reach here if choice was Stat (2 or 3) OR Add Card failed
        bool forPetStat = (choiceType == 3 || choiceType == 5); // Upgrade Pet Stat if type 3 or 5 (and others failed)
        StatType stat = (StatType)Random.Range(0, System.Enum.GetValues(typeof(StatType)).Length);
        int amount = 0;
        if (stat == StatType.MaxHealth) amount = Random.Range(5, 11); // e.g., 5-10 health
        else if (stat == StatType.StartingEnergy) amount = 1; // Always +1 energy?
        // Add other stat amounts

        if (amount > 0)
        {
            // Ensure we don't upgrade pet energy if it doesn't exist
            if(forPetStat && stat == StatType.StartingEnergy) 
            {
                // Maybe default to pet health upgrade instead? Or try player stat?
                // For now, let's try player energy if pet energy was chosen
                if(startingEnergy < 10) { // Arbitrary cap
                     return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.StartingEnergy, amount, false));
                } else { // If player energy also invalid, try pet health
                     return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, Random.Range(5, 11), true));
                }
            }
            return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, stat, amount, forPetStat));
        }
        
        // --- Final Fallback: If everything else failed (e.g., pools empty, no stats to upgrade) ---
        // Return a simple player health upgrade as a last resort
        Debug.LogWarning("GenerateSingleDraftOption: Failed to generate desired option type, defaulting to Player Health upgrade.");
        return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, 5, false)); 
    }


    private void ApplyDraftChoice(DraftOption choice)
    {
        if (choice == null) 
        {
            Debug.LogError("ApplyDraftChoice received a null choice!");
            return;
        }

        Debug.Log($"Applying draft choice: {choice.Description}");
        switch (choice.Type)
        {
            case DraftOptionType.AddPlayerCard:
                if (choice.CardToAdd != null)
                {
                    deck.Add(choice.CardToAdd); // Add to deck
                    ShuffleDeck(); // Reshuffle after adding
                    UpdateDeckCountUI();
                    Debug.Log($"Added {choice.CardToAdd.cardName} to player deck.");
                }
                else { Debug.LogWarning("AddPlayerCard choice had null CardToAdd."); }
                break;
            case DraftOptionType.AddPetCard:
                if (choice.CardToAdd != null)
                {
                    localPetDeck.Add(choice.CardToAdd); // Add to the local player's pet deck
                    ShuffleLocalPetDeck(); // Shuffle after adding
                    Debug.Log($"Added {choice.CardToAdd.cardName} to local pet deck.");
                     // Update pet deck UI if exists
                }
                 else { Debug.LogWarning("AddPetCard choice had null CardToAdd."); }
                break;
            case DraftOptionType.UpgradePlayerStat:
                 if (choice.StatToUpgrade == StatType.MaxHealth)
                 {
                    startingPlayerHealth += choice.StatIncreaseAmount;
                    localPlayerHealth += choice.StatIncreaseAmount;
                    Debug.Log($"Upgraded Player Max Health by {choice.StatIncreaseAmount}. New base: {startingPlayerHealth}");
                 }
                 else if (choice.StatToUpgrade == StatType.StartingEnergy)
                 {
                     startingEnergy += choice.StatIncreaseAmount;
                     currentEnergy += choice.StatIncreaseAmount; // Give energy now as well
                     Debug.Log($"Upgraded Player Starting Energy by {choice.StatIncreaseAmount}. New base: {startingEnergy}");
                 }
                 UpdateHealthUI();
                 UpdateEnergyUI(); 
                 break;
            case DraftOptionType.UpgradePetStat:
                 if (choice.StatToUpgrade == StatType.MaxHealth)
                 {
                    startingPetHealth += choice.StatIncreaseAmount;
                    localPetHealth += choice.StatIncreaseAmount;
                    Debug.Log($"Upgraded Pet Max Health by {choice.StatIncreaseAmount}. New base: {startingPetHealth}");
                    Hashtable petProps = new Hashtable { { PLAYER_BASE_PET_HP_PROP, startingPetHealth } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(petProps);
                    Debug.Log($"Set {PLAYER_BASE_PET_HP_PROP} to {startingPetHealth} for local player.");
                 }
                 // TODO: Handle Pet Energy if pets have energy?
                 UpdateHealthUI(); // Updates both player and pet health bars
                 break;

            // --- IMPLEMENTED Upgrade Card --- 
            case DraftOptionType.UpgradePlayerCard:
                 if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                 {
                     // Find the first instance of the card to upgrade in the player's deck
                     int indexToUpgrade = deck.FindIndex(card => card == choice.CardToUpgrade);
                     if (indexToUpgrade != -1)
                     {
                         CardData upgradedCard = choice.CardToUpgrade.upgradedVersion;
                         deck[indexToUpgrade] = upgradedCard; // Replace the card
                         ShuffleDeck(); // Reshuffle maybe?
                         UpdateDeckCountUI();
                         Debug.Log($"Upgraded player card {choice.CardToUpgrade.cardName} to {upgradedCard.cardName} in deck.");
                     }
                     else
                     {
                         Debug.LogWarning($"Could not find player card '{choice.CardToUpgrade.cardName}' in deck to upgrade. It might have been removed or transformed already.");
                     }
                 }
                 else
                 {
                     Debug.LogError($"UpgradePlayerCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                 }
                 break;
            case DraftOptionType.UpgradePetCard:
                 if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                 {
                     // Find the first instance of the card to upgrade in the pet's deck
                     int indexToUpgrade = localPetDeck.FindIndex(card => card == choice.CardToUpgrade);
                     if (indexToUpgrade != -1)
                     {
                         CardData upgradedCard = choice.CardToUpgrade.upgradedVersion;
                         localPetDeck[indexToUpgrade] = upgradedCard; // Replace the card
                         ShuffleLocalPetDeck();
                         // Update pet deck UI if exists
                         Debug.Log($"Upgraded pet card {choice.CardToUpgrade.cardName} to {upgradedCard.cardName} in local pet deck.");
                     }
                     else
                     {
                         Debug.LogWarning($"Could not find pet card '{choice.CardToUpgrade.cardName}' in local pet deck to upgrade.");
                     }
                 }
                 else
                 {
                    Debug.LogError($"UpgradePetCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                 }
                 break;
        }
    }

    private void EndDraftPhase()
    {
        // ... (Keep existing logic, but maybe check picks made count?) ...
        Debug.Log("Ending Draft Phase. Preparing for next round...");
        HideDraftScreen();

        // Reset necessary combat states BUT keep upgraded stats/decks
        
        // Maybe transition to a brief "Round Start" screen?
        // For now, go directly back to combat setup

        // Check win condition (e.g., score limit reached)
        int scoreLimit = 10; // Example
        if (player1Score >= scoreLimit || player2Score >= scoreLimit)
        { 
            HandleGameOver();
        }
        else
        {
             // Start next combat round via RPC
             // photonView.RPC("RpcStartCombat", RpcTarget.All);
             if (PhotonNetwork.IsMasterClient)
             {
                 PrepareNextCombatRound();
             }
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

    private void PrepareNextCombatRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("Master Client preparing next combat round...");

        List<Player> players = PhotonNetwork.PlayerList.ToList();
        // Shuffle players for randomized pairings (optional but good)
        System.Random rng = new System.Random();
        players = players.OrderBy(p => rng.Next()).ToList();

        Dictionary<int, int> pairings = new Dictionary<int, int>();
        if (players.Count < 2)
        {
            Debug.LogWarning("Cannot create pairings with less than 2 players.");
            // Handle this case? End game? For now, just proceed without pairings.
        }
        else
        {
            for (int i = 0; i < players.Count; i++)
            {
                Player currentPlayer = players[i];
                Player opponentPetOwner = players[(i + 1) % players.Count]; // Cyclical pairing
                pairings[currentPlayer.ActorNumber] = opponentPetOwner.ActorNumber;
                Debug.Log($"Pairing: {currentPlayer.NickName} vs {opponentPetOwner.NickName}'s Pet");
            }
        }

        // Store pairings in room properties
        string pairingsJson = JsonConvert.SerializeObject(pairings);
        Hashtable roomProps = new Hashtable
        {
            { COMBAT_PAIRINGS_PROP, pairingsJson }
            // Optionally add a timestamp or round number here if needed
        };
        
        // Reset combat finished flags BEFORE setting properties that trigger the next step
        ResetCombatFinishedFlags();
        
        // Set properties
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        
        // Set flag to wait for OnRoomPropertiesUpdate confirmation
        isWaitingToStartCombatRPC = true;
        Debug.Log($"Master Client set pairings property and isWaitingToStartCombatRPC = true. Waiting for confirmation.");

        // Tell all clients to start combat -- REMOVED, now triggered by OnRoomPropertiesUpdate
        // Debug.Log("Pairings set. Calling RpcStartCombat.");
        // photonView.RPC("RpcStartCombat", RpcTarget.All);
    }

    #endregion

} // End of GameManager Class


