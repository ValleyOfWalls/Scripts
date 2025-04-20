using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using ExitGames.Client.Photon;

public class GameManager : MonoBehaviourPunCallbacks
{
    // --- ADDED: Static instance for singleton access ---
    public static GameManager Instance { get; private set; }
    // --- END ADDED ---

    [Header("Settings")]
    [SerializeField] private int startingPlayerHealth = 50;
    [SerializeField] private int startingPetHealth = 30;
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
    [SerializeField] private GameObject deckViewerPanelPrefab;

    // Manager instances
    private GameStateManager gameStateManager;
    private PlayerManager playerManager;
    private CardManager cardManager;
    private PhotonManager photonManager;
    private CombatManager combatManager;
    private DraftManager draftManager;

    // PhotonView component reference
    private PhotonView photonViewComponent;

    #region Unity Lifecycle Methods

    void Awake()
    {
        Debug.Log("GameManager Awake");
        
        // --- MODIFIED: Singleton setup ---
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another GameManager instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this; 
        // --- END MODIFIED ---
        
        // Ensure only one GameManager instance exists (Kept original check just in case, but Instance check should handle it)
        // if (FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length > 1)
        // {
        //     Destroy(gameObject);
        //     return;
        // }
        DontDestroyOnLoad(gameObject);

        // Get PhotonView component
        photonViewComponent = GetComponent<PhotonView>();
        if (photonViewComponent == null)
        {
            Debug.LogError("GameManager requires a PhotonView component!");
            // Add it if missing? Or rely on prefab setup?
            // photonViewComponent = gameObject.AddComponent<PhotonView>();
            return; 
        }

        // Initialize Managers (Constructors)
        // Pass UI Prefabs to GameStateManager constructor
        gameStateManager = new GameStateManager(this, startScreenCanvasPrefab, lobbyCanvasPrefab, combatCanvasPrefab, draftCanvasPrefab);
        playerManager = new PlayerManager(this);
        cardManager = new CardManager(this, starterDeck, starterPetDeck, allPlayerCardPool, allPetCardPool, cardsToDraw);
        photonManager = gameObject.AddComponent<PhotonManager>(); // Add as component
        combatManager = new CombatManager(); // Default constructor
        draftManager = new DraftManager(); // Default constructor

        // ** Initialize and sync local pet deck early **
        if (cardManager != null)
        {
            cardManager.InitializeAndSyncLocalPetDeck();
        }
        else
        {
            Debug.LogError("CardManager is null after instantiation in Awake!");
        }
    }

    void Start()
    {
        // Initialize manager classes that require it after Awake
        gameStateManager.Initialize();
        playerManager.Initialize(startingPlayerHealth, startingPetHealth, startingEnergy);
        photonManager.Initialize(this); // Assuming PhotonManager needs Initialize
        combatManager.Initialize(this, startingPlayerHealth, startingPetHealth, startingEnergy); // Pass necessary refs/config
        draftManager.Initialize(this, optionsPerDraft); // Pass necessary refs/config
    }

    #endregion

    #region Photon Callbacks
    
    // These override methods forward to PhotonManager
    public override void OnConnectedToMaster() => photonManager.OnConnectedToMaster();
    public override void OnJoinedRoom() => photonManager.OnJoinedRoom();
    public override void OnLeftRoom() => photonManager.OnLeftRoom();
    public override void OnJoinRoomFailed(short returnCode, string message) => photonManager.OnJoinRoomFailed(returnCode, message);
    public override void OnCreateRoomFailed(short returnCode, string message) => photonManager.OnCreateRoomFailed(returnCode, message);
    public override void OnDisconnected(DisconnectCause cause) => photonManager.OnDisconnected(cause);
    public override void OnPlayerEnteredRoom(Player newPlayer) => photonManager.OnPlayerEnteredRoom(newPlayer);
    public override void OnPlayerLeftRoom(Player otherPlayer) => photonManager.OnPlayerLeftRoom(otherPlayer);
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        photonManager.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
        // Update Score UI if in Combat or Draft (score persists)
        if (gameStateManager.GetCurrentState() == GameState.Combat || gameStateManager.GetCurrentState() == GameState.Drafting)
        {
            playerManager.GetCombatStateManager().UpdateScoreUI(); // Use CombatStateManager via PlayerManager
             // --- ADDED: Update Other Fights UI on property change ---
             combatManager.UpdateOtherFightsUI();
             // --- END ADDED ---
        }
    }
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) => photonManager.OnRoomPropertiesUpdate(propertiesThatChanged);
    public override void OnMasterClientSwitched(Player newMasterClient) => photonManager.OnMasterClientSwitched(newMasterClient);
    
    #endregion

    #region Connection & Room Functions

    public void ConnectToPhoton()
    {
        string playerName = gameStateManager.GetPlayerNameInput()?.text;
        string petName = gameStateManager.GetPetNameInput()?.text;
        
        playerManager.SetLocalPetName(petName);
        photonManager.ConnectToPhoton(playerName, petName);
    }

    public void LeaveRoom()
    {
        photonManager.LeaveRoom();
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
                var pairingsDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, int>>(pairingsJson);
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
        combatManager.InitializeCombatState(opponentPetOwnerActorNum);
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        combatManager.InitializeCombatScreenReferences(combatInstance);
    }

    #endregion

    #region Card Play Functions

    public bool IsPlayerTurn()
    {
        return combatManager.IsPlayerTurn();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        return combatManager.AttemptPlayCard(cardData, targetType);
    }

    #endregion

    #region Draft Functions

    [PunRPC]
    private void RpcStartDraft()
    {
        draftManager.StartDraft();
    }

    public void HandleOptionSelected(int selectedOptionId)
    {
        draftManager.HandleOptionSelected(selectedOptionId);
    }

    [PunRPC]
    private void RpcPlayerPickedOption(int chosenOptionId, PhotonMessageInfo info)
    {
        draftManager.ProcessPlayerPickedOption(chosenOptionId, info.Sender);
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

    // --- ADDED: RPC for Hand Cost Modifier ---
    [PunRPC]
    private void RpcApplyHandCostModifier(int amount, int duration, int cardCount)
    {
        Debug.Log($"RPC Received: Apply Hand Cost Modifier Amount={amount}, Duration={duration}, Count={cardCount}");
        playerManager.ApplyCostModifierToLocalHand(amount, duration, cardCount);
        // The UI will update implicitly when hand is redrawn or cost is checked
    }
    // --- END ADDED ---

    // --- ADDED: RPC for Opponent's Pet taking damage (e.g., from player attacking their own pet) ---
    [PunRPC]
    private void RpcOpponentPetTookDamage(int damageAmount)
    {
        if (damageAmount <= 0) return;

        Debug.Log($"RPC Received: Opponent Pet (the one I'm fighting) taking {damageAmount} damage.");
        // Apply this damage to the pet we are fighting, WITHOUT triggering another RPC
        playerManager.GetHealthManager().ApplyDamageToOpponentPetLocally(damageAmount);
    }
    // --- END ADDED ---

    // --- ADDED: RPC Receiver for Local Pet Block Update (Sent by owner to others) ---
    [PunRPC]
    private void RpcUpdateLocalPetBlock(int newTotalBlock, PhotonMessageInfo info)
    {
        // We received an update about an opponent's pet block
        // 'info.Sender' is the owner of the pet whose block changed
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcUpdateLocalPetBlock({newTotalBlock}) from {info.Sender.NickName}. Updating their pet's block in our view.");
            // Find which opponent this corresponds to in *our* combat view
            Player opponentPlayer = playerManager.GetOpponentPlayer(); 
            if (opponentPlayer != null && opponentPlayer.ActorNumber == info.Sender.ActorNumber)
            {
                // This RPC came from the player whose pet we are currently fighting
                // We need to set the *opponentPetBlock* value in our HealthManager
                // Note: This RPC sends the *total* block, not an amount to add.
                playerManager.GetHealthManager().SetOpponentPetBlock(newTotalBlock); 
            }
            else
            {
                // This RPC came from a player whose pet we are *not* currently fighting
                // We still might want to store/update this info if we display other combats, 
                // but for now, we might just log it or ignore it if not directly relevant.
                // We need a way to map info.Sender to the correct 'other player' structure if needed.
                Debug.LogWarning($"Received RpcUpdateLocalPetBlock from player {info.Sender.NickName} who is not our current direct opponent.");
                // Potentially update a dictionary mapping player ID to their pet's block for the 'Other Fights UI'
                // playerManager.UpdateOtherPlayerPetBlock(info.Sender.ActorNumber, newTotalBlock);
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcUpdateLocalPetBlock from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Adding Block to Local Pet (Sent by opponent targeting our pet) ---
    [PunRPC]
    private void RpcAddBlockToLocalPet(int amount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that gives *our pet* block.
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
             Debug.Log($"RPC: Received RpcAddBlockToLocalPet({amount}) from {info.Sender.NickName}. Adding block to our local pet.");
             // Directly add the block amount to our local pet
             playerManager.AddBlockToLocalPet(amount);
        }
         else
        {
            Debug.LogWarning($"RPC: Received RpcAddBlockToLocalPet from self or null sender. Ignoring.");
        }
    }

    public PhotonView GetPhotonView()
    {
        return photonViewComponent;
    }

    #endregion

    #region UI Update Proxy Methods
    
    // These methods proxy UI update calls to the combat manager
    public void UpdateHandUI()
    {
        combatManager.UpdateHandUI();
    }

    public void UpdateDeckCountUI()
    {
        combatManager.UpdateDeckCountUI();
    }

    public void UpdateHealthUI()
    {
        combatManager.UpdateHealthUI();
    }

    public void UpdateEnergyUI()
    {
        combatManager.UpdateEnergyUI();
    }
    
    #endregion

    #region Public Helpers for Manager Classes

    public void DisableEndTurnButton()
    {
        combatManager.DisableEndTurnButton();
    }

    public TextMeshProUGUI GetScoreText()
    {
        return gameStateManager.GetScoreText();
    }

    public GameObject GetPlayerHandPanel()
    {
        return combatManager.GetPlayerHandPanel();
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
    
    public PhotonManager GetPhotonManager()
    {
        return photonManager;
    }
    
    public CombatManager GetCombatManager()
    {
        return combatManager;
    }
    
    public DraftManager GetDraftManager()
    {
        return draftManager;
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

    // --- ADDED Getter for Deck Viewer Prefab ---
    public GameObject GetDeckViewerPanelPrefab()
    {
        return deckViewerPanelPrefab;
    }
    // --- END ADDED Getter ---

    #endregion
}