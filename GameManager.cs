using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Newtonsoft.Json;

public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [SerializeField] private int startingPlayerHealth = 1;
    [SerializeField] private int startingPetHealth = 1;
    [SerializeField] private int startingEnergy = 3;
    [SerializeField] private int cardsToDraw = 5;
    [SerializeField] private List<CardData> starterDeck = new List<CardData>();
    [SerializeField] private List<CardData> starterPetDeck = new List<CardData>();
    [SerializeField] private int optionsPerDraft = 3;

    [Header("Card Pools for Draft/Rewards")]
    [SerializeField] private List<CardData> allPlayerCardPool = new List<CardData>();
    [SerializeField] private List<CardData> allPetCardPool = new List<CardData>();

    [Header("UI Panels")]
    [SerializeField] private GameObject startScreenCanvasPrefab;
    [SerializeField] private GameObject lobbyCanvasPrefab;
    [SerializeField] private GameObject combatCanvasPrefab;
    [SerializeField] private GameObject draftCanvasPrefab;
    [SerializeField] private GameObject cardPrefab;

    // Combat UI References (assigned by InitializeCombatScreenReferences)
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI playerNameText;
    private Slider playerHealthSlider;
    private TextMeshProUGUI playerHealthText;
    private GameObject playerHandPanel;
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
    private GameObject otherPlayerStatusTemplate;
    [SerializeField] private TextMeshProUGUI energyText;

    // Manager instances
    private GameStateManager gameStateManager;
    private PlayerManager playerManager;
    private CardManager cardManager;

    // PhotonView component reference
    private PhotonView photonViewComponent;

    #region Unity Lifecycle Methods

    void Awake()
    {
        photonViewComponent = GetComponent<PhotonView>();
        if (photonViewComponent == null)
        {
            Debug.LogError("PhotonView component missing on GameManager!");
            photonViewComponent = gameObject.AddComponent<PhotonView>();
        }
    }

    void Start()
    {
        // Ensure we have only one GameManager instance
        if (FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        // Initialize manager classes
        gameStateManager = new GameStateManager(this, startScreenCanvasPrefab, lobbyCanvasPrefab, combatCanvasPrefab, draftCanvasPrefab);
        playerManager = new PlayerManager(this);
        cardManager = new CardManager(this, starterDeck, starterPetDeck, allPlayerCardPool, allPetCardPool, cardsToDraw);

        // Initialize state
        gameStateManager.Initialize();
        playerManager.Initialize(startingPlayerHealth, startingPetHealth, startingEnergy);
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to Master Server. Player Nickname: {PhotonNetwork.NickName}");
        if (gameStateManager.IsUserInitiatedConnection())
        {
            Debug.Log("User initiated connection - attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
            Debug.Log("Connected to Master, but not user-initiated. Waiting for user action.");
            Button connectBtn = gameStateManager.GetConnectButton();
            if (connectBtn != null) connectBtn.interactable = true;
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        gameStateManager.SetUserInitiatedConnection(false);
        gameStateManager.TransitionToLobby();
        UpdatePlayerList();
        playerManager.SetPlayerReady(false);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        gameStateManager.SetUserInitiatedConnection(false);
        gameStateManager.SetCurrentState(GameState.Connecting);
        gameStateManager.ShowStartScreen();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join Room Failed: ({returnCode}) {message}");
        gameStateManager.SetUserInitiatedConnection(false);
        Button connectBtn = gameStateManager.GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: ({returnCode}) {message}");
        gameStateManager.SetUserInitiatedConnection(false);
        Button connectBtn = gameStateManager.GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
        gameStateManager.OnDisconnected();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered room.");
        if (gameStateManager.GetLobbyInstance() != null && gameStateManager.GetLobbyInstance().activeSelf)
        {
            UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left room.");
        if (gameStateManager.GetLobbyInstance() != null && gameStateManager.GetLobbyInstance().activeSelf)
        {
            UpdatePlayerList();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        Debug.Log($"Player {targetPlayer.NickName} properties updated. CurrentState: {gameStateManager.GetCurrentState()}");
        
        if (gameStateManager.GetCurrentState() == GameState.Lobby && changedProps.ContainsKey(PlayerManager.PLAYER_READY_PROPERTY))
        {
            if (gameStateManager.GetLobbyInstance() != null && gameStateManager.GetLobbyInstance().activeSelf)
                UpdatePlayerList();
        }
        else if (gameStateManager.GetCurrentState() == GameState.Combat && changedProps.ContainsKey(PlayerManager.COMBAT_FINISHED_PROPERTY))
        {
            Debug.Log($"Property update for {PlayerManager.COMBAT_FINISHED_PROPERTY} received from {targetPlayer.NickName}");
            if (PhotonNetwork.IsMasterClient)
            {
                playerManager.CheckForAllPlayersFinishedCombat();
            }
        }
        else if (changedProps.ContainsKey(PlayerManager.PLAYER_SCORE_PROP))
        {
            Debug.Log($"Score property updated for {targetPlayer.NickName}. Refreshing Score UI.");
            playerManager.UpdateScoreUI();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log($"Room properties updated. CurrentState: {gameStateManager.GetCurrentState()}");

        // Master Client check: If waiting to send StartCombat RPC and pairings are now set
        if (PhotonNetwork.IsMasterClient && gameStateManager.IsWaitingToStartCombatRPC() && 
            propertiesThatChanged.ContainsKey(PlayerManager.COMBAT_PAIRINGS_PROP))
        {
            Debug.Log("Master Client confirms pairings property set. Sending RpcStartCombat.");
            gameStateManager.SetWaitingToStartCombatRPC(false);
            photonViewComponent.RPC("RpcStartCombat", RpcTarget.All);
        }

        if (gameStateManager.GetCurrentState() == GameState.Drafting)
        {
            bool draftStateUpdated = false;
            
            if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_PLAYER_QUEUES_PROP))
            {
                Debug.Log("Draft packs updated in room properties.");
                cardManager.UpdateLocalDraftStateFromRoomProps();
            }
            
            if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_PICKS_MADE_PROP))
            {
                Debug.Log("Draft picks made updated in room properties.");
                cardManager.UpdateLocalDraftPicksFromRoomProps();
            }
            
            if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_ORDER_PROP))
            {
                cardManager.UpdateLocalDraftOrderFromRoomProps();
            }

            // Check Draft End Condition
            object queuesProp = null;
            bool queuesExist = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PLAYER_QUEUES_PROP, out queuesProp);
            bool draftEnded = !queuesExist || queuesProp == null;
            string queuesJson = queuesProp as string;
            Debug.Log($"OnRoomPropertiesUpdate: Checking draft end. queuesExist={queuesExist}, queuesProp='{queuesJson}'");

            if (!draftEnded && !string.IsNullOrEmpty(queuesJson))
            {
                try
                {
                    var queuesDict = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson);
                    if (queuesDict == null)
                    {
                        Debug.LogWarning("End Check: Deserialized queuesDict is null.");
                        draftEnded = true;
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
                Debug.Log("End Check: queuesProp exists but is null/empty string.");
                draftEnded = true;
            }

            if (draftEnded)
            {
                // Prevent multiple calls if EndDraftPhase was already triggered
                if (gameStateManager.GetCurrentState() == GameState.Drafting)
                {
                    Debug.Log("Draft queues property indicates draft ended. Calling EndDraftPhase.");
                    EndDraftPhase();
                }
            }
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"New Master Client: {newMasterClient.NickName}");
        if (gameStateManager.GetLobbyInstance() != null && gameStateManager.GetLobbyInstance().activeSelf)
        {
            UpdateLobbyControls();
        }
    }

    #endregion

    #region Connection & Room Functions

    public void ConnectToPhoton()
    {
        Debug.Log("Connecting to Photon...");
        Button connectBtn = gameStateManager.GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = false;

        string playerName = gameStateManager.GetPlayerNameInput()?.text;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player_" + Random.Range(1000, 9999);
        }
        PhotonNetwork.NickName = playerName;

        string petName = gameStateManager.GetPetNameInput()?.text;
        if (string.IsNullOrWhiteSpace(petName))
        {
            petName = "Buddy_" + Random.Range(100, 999);
        }
        playerManager.SetLocalPetName(petName);
        
        Debug.Log($"Using Player Name: {PhotonNetwork.NickName}, Pet Name: {playerManager.GetLocalPetName()}");

        gameStateManager.SetUserInitiatedConnection(true);
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Already connected to Master, attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    private void TryJoinGameRoom()
    {
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("asd", roomOptions, TypedLobby.Default);
    }

    public void LeaveRoom()
    {
        Debug.Log("Leave Button Clicked");
        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region Lobby Functions

    public void UpdatePlayerList()
    {
        playerManager.UpdatePlayerList(gameStateManager.GetPlayerListPanel(), gameStateManager.GetPlayerEntryTemplate());
        UpdateLobbyControls();
    }

    public void UpdateLobbyControls()
    {
        GameObject lobbyInstance = gameStateManager.GetLobbyInstance();
        if (lobbyInstance == null) return;
        
        bool allReady = playerManager.CheckAllPlayersReady();
        
        Button startGameButton = gameStateManager.GetStartGameButton();
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            startGameButton.interactable = PhotonNetwork.IsMasterClient && allReady;
        }
    }

    public void ToggleReadyStatus()
    {
        bool currentStatus = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_READY_PROPERTY, out object isReadyStatus))
            currentStatus = (bool)isReadyStatus;
        
        playerManager.SetPlayerReady(!currentStatus);
    }

    #endregion

    #region Game Start & Combat Functions

    public void StartGame()
    {
        // Only Master Client can initiate
        if (!PhotonNetwork.IsMasterClient) return;

        // Check if all players are ready
        if (!playerManager.CheckAllPlayersReady())
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
        Hashtable initialScoreProps = new Hashtable { { PlayerManager.PLAYER_SCORE_PROP, 0 } };
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            p.SetCustomProperties(initialScoreProps);
            Debug.Log($"Set initial score for {p.NickName}");
        }

        // Prepare combat round
        playerManager.PrepareNextCombatRound();
    }

    [PunRPC]
    private void RpcStartCombat()
    {
        Debug.Log($"RPC: Starting Combat Setup for {PhotonNetwork.LocalPlayer.NickName}");
        gameStateManager.TransitionToCombat();

        // Fetch pairings from Room Properties
        int opponentPetOwnerActorNum = -1;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PlayerManager.COMBAT_PAIRINGS_PROP, out object pairingsObj))
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
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize combat pairings: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {PlayerManager.COMBAT_PAIRINGS_PROP} not found.");
        }

        // Initialize combat state for this player
        InitializeCombatState(opponentPetOwnerActorNum);
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;

        Transform topArea = combatInstance.transform.Find("TopArea");
        Transform playerArea = combatInstance.transform.Find("PlayerArea");

        if (topArea == null || playerArea == null)
        {
            Debug.LogError("Could not find TopArea or PlayerArea in CombatCanvas!");
            return;
        }

        // Find elements within TopArea
        scoreText = topArea.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        
        // Find opponent pet area
        Transform opponentAreaContainer = topArea.Find("OpponentPetAreaContainer");
        Transform opponentArea = opponentAreaContainer?.Find("OpponentPetArea");
        opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
        opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
        opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
        opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
        
        // Find own pet area
        Transform ownPetAreaContainer = topArea.Find("OwnPetAreaContainer");
        Transform ownPetArea = ownPetAreaContainer?.Find("OwnPetArea");
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
        
        // Find Energy Text
        energyText = statsRow?.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        
        Transform handPanelTransform = playerArea.Find("PlayerHandPanel");
        playerHandPanel = handPanelTransform?.gameObject;
        
        Transform bottomBar = playerArea.Find("BottomBar");
        deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
        discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
        endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
        
        // Assign listeners
        endTurnButton?.onClick.AddListener(EndTurn);

        // Validate critical findings 
        if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null || scoreText == null)
            Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab!");
        else 
            Debug.Log("Successfully found critical combat UI elements.");

        if (otherPlayerStatusTemplate != null) otherPlayerStatusTemplate.SetActive(false);
        if (othersStatusArea != null) othersStatusArea.SetActive(PhotonNetwork.PlayerList.Length > 2);
    }

    private void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        playerManager.InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
        cardManager.InitializeDecks();
        
        // Setup Initial UI
        if (playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        UpdateHealthUI();
        
        if (opponentPetNameText)
        {
            Player opponent = playerManager.GetOpponentPlayer();
            opponentPetNameText.text = opponent != null ? $"{opponent.NickName}'s Pet" : "Opponent Pet";
        }
        
        if (ownPetNameText) ownPetNameText.text = playerManager.GetLocalPetName();
        
        UpdateDeckCountUI();
        
        // Start the first turn
        StartTurn();
    }

    private void StartTurn()
    {
        Debug.Log("Starting Player Turn");
        playerManager.SetCurrentEnergy(startingEnergy);
        UpdateEnergyUI();

        cardManager.DrawHand();
        UpdateHandUI();
        UpdateDeckCountUI();

        // TODO: Determine and display opponent pet's intent
        if (opponentPetIntentText) opponentPetIntentText.text = "Intent: Attack 5"; // Placeholder

        // Make cards playable
        if (endTurnButton) endTurnButton.interactable = true;
    }

    public void EndTurn()
    {
        Debug.Log("Ending Player Turn");
        if (endTurnButton) endTurnButton.interactable = false; // Prevent double clicks

        // 1. Discard Hand
        cardManager.DiscardHand();
        UpdateHandUI(); // Clear hand display
        UpdateDeckCountUI();

        // 2. Opponent Pet Acts
        cardManager.ExecuteOpponentPetTurn(startingEnergy);

        // 3. Start Next Player Turn (if combat hasn't ended)
        if (!playerManager.IsCombatEndedForLocalPlayer())
        {
            StartTurn();
        }
    }

    #endregion

    #region Combat UI Functions

    public void UpdateHealthUI()
    {
        // Player Health
        if (playerHealthSlider) playerHealthSlider.value = (float)playerManager.GetLocalPlayerHealth() / startingPlayerHealth;
        if (playerHealthText) playerHealthText.text = $"{playerManager.GetLocalPlayerHealth()} / {startingPlayerHealth}";

        // Own Pet Health
        if (ownPetHealthSlider) ownPetHealthSlider.value = (float)playerManager.GetLocalPetHealth() / startingPetHealth;
        if (ownPetHealthText) ownPetHealthText.text = $"{playerManager.GetLocalPetHealth()} / {startingPetHealth}";

        // Opponent Pet Health
        if (opponentPetHealthSlider) opponentPetHealthSlider.value = (float)playerManager.GetOpponentPetHealth() / startingPetHealth;
        if (opponentPetHealthText) opponentPetHealthText.text = $"{playerManager.GetOpponentPetHealth()} / {startingPetHealth}";
    }

    public void UpdateHandUI()
    {
        if (playerHandPanel == null || cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // Clear existing card visuals
        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                Destroy(child.gameObject);
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        // Instantiate new card visuals for cards in hand
        foreach (CardData card in cardManager.GetHand())
        {
            GameObject cardGO = Instantiate(cardPrefab, playerHandPanel.transform);
            cardGO.name = $"Card_{card.cardName}";
            
            Transform headerPanel = cardGO.transform.Find("HeaderPanel");
            Transform descPanel = cardGO.transform.Find("DescPanel");
            Transform artPanel = cardGO.transform.Find("ArtPanel");

            TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
            Image artImage = artPanel?.GetComponent<Image>();

            if (nameText != null) nameText.text = card.cardName;
            if (costText != null) costText.text = card.cost.ToString();
            if (descText != null) descText.text = card.description;
            
            // Get the handler and assign the data
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.cardData = card;
            }
            else
            {
                Debug.LogError($"CardDragHandler component not found on instantiated card prefab: {cardGO.name}");
            }

            cardGO.SetActive(true);
        }
    }

    public void UpdateDeckCountUI()
    {
        if (deckCountText != null) deckCountText.text = $"Deck: {cardManager.GetDeckCount()}";
        if (discardCountText != null) discardCountText.text = $"Discard: {cardManager.GetDiscardCount()}";
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"Energy: {playerManager.GetCurrentEnergy()} / {playerManager.GetStartingEnergy()}";
        }
        else
        {
            Debug.LogWarning("UpdateEnergyUI: energyText reference is null!");
        }
    }

    #endregion

    #region Card Play Functions

    public bool IsPlayerTurn()
    {
        return !playerManager.IsCombatEndedForLocalPlayer();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        return cardManager.AttemptPlayCard(cardData, targetType);
    }

    #endregion

    #region Draft Functions

    [PunRPC]
    private void RpcStartDraft()
    {
        Debug.Log("RPC Received: Starting Draft Phase");
        gameStateManager.TransitionToDraft();
        playerManager.SetCombatEndedForLocalPlayer(false);

        // Master Client generates and distributes draft options
        if (PhotonNetwork.IsMasterClient)
        {
            cardManager.InitializeDraftState(optionsPerDraft);
        }
        else
        {
            cardManager.UpdateLocalDraftStateFromRoomProps();
            cardManager.UpdateLocalDraftPicksFromRoomProps();
            cardManager.UpdateLocalDraftOrderFromRoomProps();
        }
    }

    public void HandleOptionSelected(int selectedOptionId)
    {
        Debug.Log($"Local player selected option ID: {selectedOptionId}");
        cardManager.HandlePlayerDraftPick(selectedOptionId);
    }

    [PunRPC]
    private void RpcPlayerPickedOption(int chosenOptionId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        Debug.Log($"RPC Received: Player {info.Sender.NickName} ({info.Sender.ActorNumber}) picked Option ID {chosenOptionId}");

        if (gameStateManager.GetCurrentState() != GameState.Drafting)
        {
            Debug.LogWarning("RpcPlayerPickedOption received but not in Drafting state.");
            return;
        }

        Dictionary<int, List<string>> currentQueues = null;
        Dictionary<int, int> currentPicks = null;
        List<int> currentOrder = null;

        object queuesObj, picksObj, orderObj;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PLAYER_QUEUES_PROP, out queuesObj) || 
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PICKS_MADE_PROP, out picksObj) ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_ORDER_PROP, out orderObj))
        {
            Debug.LogError("RpcPlayerPickedOption: Failed to retrieve current draft state from Room Properties.");
            return;
        }

        try
        {
            currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>((string)queuesObj) ?? new Dictionary<int, List<string>>();
            currentPicks = JsonConvert.DeserializeObject<Dictionary<int, int>>((string)picksObj) ?? new Dictionary<int, int>();
            currentOrder = JsonConvert.DeserializeObject<List<int>>((string)orderObj) ?? new List<int>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RpcPlayerPickedOption: Failed to deserialize draft state: {e.Message}");
            return;
        }

        int pickerActorNum = info.Sender.ActorNumber;
        if (!currentQueues.ContainsKey(pickerActorNum) || currentQueues[pickerActorNum] == null || currentQueues[pickerActorNum].Count == 0)
        {
            Debug.LogWarning($"Player {pickerActorNum} sent RpcPlayerPickedOption but doesn't have a pack in their queue currently.");
            return;
        }

        List<string> pickerQueue = currentQueues[pickerActorNum];
        string packToProcessJson = pickerQueue[0];
        pickerQueue.RemoveAt(0);

        List<SerializableDraftOption> packToProcess = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(packToProcessJson) ?? new List<SerializableDraftOption>();
        SerializableDraftOption pickedOption = packToProcess.FirstOrDefault(opt => opt.OptionId == chosenOptionId);

        if (pickedOption == null)
        {
            Debug.LogWarning($"Player {pickerActorNum} tried to pick invalid Option ID {chosenOptionId} from their current pack.");
            return;
        }

        packToProcess.Remove(pickedOption);
        Debug.Log($"Removed option {chosenOptionId} from pack for player {pickerActorNum}. Remaining in pack: {packToProcess.Count}. Remaining in queue: {pickerQueue.Count}");

        currentPicks[pickerActorNum] = currentPicks.ContainsKey(pickerActorNum) ? currentPicks[pickerActorNum] + 1 : 1;

        Hashtable propsToUpdate = new Hashtable();

        if (packToProcess.Count > 0)
        {
            int pickerIndex = currentOrder.IndexOf(pickerActorNum);
            if (pickerIndex == -1)
            {
                Debug.LogError($"RpcPlayerPickedOption: Picker {pickerActorNum} not found in draft order!");
                return;
            }
            int nextPlayerIndex = (pickerIndex + 1) % currentOrder.Count;
            int nextPlayerActorNum = currentOrder[nextPlayerIndex];

            string remainingPackJson = JsonConvert.SerializeObject(packToProcess);
            
            List<string> nextPlayerQueue;
            if (!currentQueues.TryGetValue(nextPlayerActorNum, out nextPlayerQueue) || nextPlayerQueue == null)
            {
                nextPlayerQueue = new List<string>();
                currentQueues[nextPlayerActorNum] = nextPlayerQueue;
            }
            nextPlayerQueue.Add(remainingPackJson);
            
            Debug.Log($"Passing remaining {packToProcess.Count} options to player {nextPlayerActorNum}. Their queue size is now {nextPlayerQueue.Count}");
        }
        else
        {
            Debug.Log($"Pack processed for player {pickerActorNum} is now empty. Not passing.");
        }

        string finalQueuesJson = JsonConvert.SerializeObject(currentQueues);
        string finalPicksJson = JsonConvert.SerializeObject(currentPicks);
        propsToUpdate[CardManager.DRAFT_PLAYER_QUEUES_PROP] = finalQueuesJson;
        propsToUpdate[CardManager.DRAFT_PICKS_MADE_PROP] = finalPicksJson;
        
        Debug.Log($"Master Client setting properties. Queues JSON: {finalQueuesJson}, Picks JSON: {finalPicksJson}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(propsToUpdate);
    }

    private void EndDraftPhase()
    {
        Debug.Log("Ending Draft Phase. Preparing for next round...");
        gameStateManager.HideDraftScreen();

        // For now, go directly back to combat setup
        if (PhotonNetwork.IsMasterClient)
        {
            playerManager.PrepareNextCombatRound();
        }
    }

    #endregion

    #region RPCs and Network Functions

    [PunRPC]
    private void RpcTakePetDamage(int damageAmount)
    {
        if (damageAmount <= 0) return;

        Debug.Log($"RPC Received: My Pet taking {damageAmount} damage.");
        playerManager.DamageLocalPet(damageAmount);
    }

    public PhotonView GetPhotonView()
    {
        return photonViewComponent;
    }

    #endregion

    #region Public Helpers for Manager Classes

    public void DisableEndTurnButton()
    {
        if (endTurnButton) endTurnButton.interactable = false;
    }

    public TextMeshProUGUI GetScoreText()
    {
        return scoreText;
    }

    public GameObject GetPlayerHandPanel()
    {
        return playerHandPanel;
    }

    public GameObject GetCardPrefab()
    {
        return cardPrefab;
    }

    public GameStateManager GetGameStateManager()
    {
        return gameStateManager;
    }

    public PlayerManager GetPlayerManager()
    {
        return playerManager;
    }

    public CardManager GetCardManager()
    {
        return cardManager;
    }

    public int GetStartingPlayerHealth()
    {
        return startingPlayerHealth;
    }

    public void SetStartingPlayerHealth(int value)
    {
        startingPlayerHealth = value;
    }

    public int GetStartingPetHealth()
    {
        return startingPetHealth;
    }

    public void SetStartingPetHealth(int value)
    {
        startingPetHealth = value;
    }

    public int GetStartingEnergy()
    {
        return startingEnergy;
    }

    public void SetStartingEnergy(int value)
    {
        startingEnergy = value;
    }

    #endregion
}