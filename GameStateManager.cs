using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Newtonsoft.Json;
using System.Linq; // Added for Find

public class GameStateManager
{
    private GameManager gameManager;
    
    [Header("UI Prefabs")]
    private GameObject startScreenCanvasPrefab;
    private GameObject lobbyCanvasPrefab;
    private GameObject combatCanvasPrefab;
    private GameObject draftCanvasPrefab;
    
    // Instantiated Canvases
    private GameObject startScreenInstance;
    private GameObject lobbyInstance;
    private GameObject combatInstance;
    private GameObject draftInstance;
    
    // Lobby UI References
    private GameObject playerListPanel;
    private GameObject playerEntryTemplate;
    private Button readyButton;
    private Button leaveButton;
    private Button startGameButton;
    
    // Start Screen References
    private Button connectButton;
    private TMP_InputField playerNameInput;
    private TMP_InputField petNameInput;
    
    // Combat UI References
    private TextMeshProUGUI scoreText;
    
    // Draft Screen References
    private GameObject draftOptionsPanel;
    private TextMeshProUGUI draftTurnText;
    private GameObject draftOptionButtonTemplate;
    // --- ADDED: Draft Deck View Button References ---
    private Button viewDraftPlayerDeckButton;
    private Button viewDraftPetDeckButton;
    private DeckViewController deckViewController; // Reference to the controller
    
    // Game state
    private GameState currentState = GameState.Connecting;
    private bool userInitiatedConnection = false;
    private bool isWaitingToStartCombatRPC = false;
    
    // Constructor for dependency injection
    public GameStateManager(GameManager gameManager, GameObject startScreenCanvasPrefab, GameObject lobbyCanvasPrefab, 
                           GameObject combatCanvasPrefab, GameObject draftCanvasPrefab)
    {
        this.gameManager = gameManager;
        this.startScreenCanvasPrefab = startScreenCanvasPrefab;
        this.lobbyCanvasPrefab = lobbyCanvasPrefab;
        this.combatCanvasPrefab = combatCanvasPrefab;
        this.draftCanvasPrefab = draftCanvasPrefab;
    }
    
    public GameState GetCurrentState()
    {
        return currentState;
    }
    
    public void SetCurrentState(GameState state)
    {
        currentState = state;
    }
    
    public void Initialize()
    {
        currentState = GameState.Connecting;
        
        // Instantiate and setup Start Screen
        if (startScreenCanvasPrefab != null)
        {
            startScreenInstance = Object.Instantiate(startScreenCanvasPrefab);
            
            Transform centerPanel = startScreenInstance.transform.Find("CenterPanel");
            if (centerPanel != null)
            {
                connectButton = centerPanel.Find("ConnectButton")?.GetComponent<Button>();
                playerNameInput = centerPanel.Find("PlayerNameInput")?.GetComponent<TMP_InputField>();
                petNameInput = centerPanel.Find("PetNameInput")?.GetComponent<TMP_InputField>();
                
                if (connectButton != null)
                {
                    connectButton.onClick.AddListener(gameManager.ConnectToPhoton);
                }
                else Debug.LogError("ConnectButton not found within CenterPanel in StartScreenCanvasPrefab!");
                
                if (playerNameInput == null) Debug.LogError("PlayerNameInput not found within CenterPanel in StartScreenCanvasPrefab!");
                else playerNameInput.text = "Player_" + Random.Range(1000, 9999);
                
                if (petNameInput == null) Debug.LogError("PetNameInput not found within CenterPanel in StartScreenCanvasPrefab!");
                else petNameInput.text = "Buddy_" + Random.Range(100, 999);
            }
            else
            {
                Debug.LogError("CenterPanel not found in StartScreenCanvasPrefab!");
                connectButton = null;
                playerNameInput = null;
                petNameInput = null;
            }
        }
        else Debug.LogError("StartScreenCanvasPrefab is not assigned to GameManager!");
    }
    
    public void ShowStartScreen()
    {
        if (startScreenInstance != null)
        {
            startScreenInstance.SetActive(true);
            if (connectButton != null) connectButton.interactable = true;
        }
    }
    
    public void HideStartScreen()
    {
        if (startScreenInstance != null)
            startScreenInstance.SetActive(false);
    }
    
    public void ShowLobbyScreen()
    {
        if (lobbyInstance == null)
        {
            if (lobbyCanvasPrefab != null)
            {
                lobbyInstance = Object.Instantiate(lobbyCanvasPrefab);
                // Find Lobby elements
                Transform panelTransform = lobbyInstance.transform.Find("PlayerListPanel");
                playerListPanel = panelTransform?.gameObject;
                playerEntryTemplate = panelTransform?.Find("PlayerEntryTemplate")?.gameObject;
                readyButton = lobbyInstance.transform.Find("ReadyButton")?.GetComponent<Button>();
                leaveButton = lobbyInstance.transform.Find("LeaveButton")?.GetComponent<Button>();
                startGameButton = lobbyInstance.transform.Find("StartGameButton")?.GetComponent<Button>();
                
                // Assign listeners
                readyButton?.onClick.AddListener(gameManager.ToggleReadyStatus);
                leaveButton?.onClick.AddListener(gameManager.LeaveRoom);
                startGameButton?.onClick.AddListener(gameManager.StartGame);
                
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
        gameManager.UpdateLobbyControls();
    }
    
    public void HideLobbyScreen()
    {
        if (lobbyInstance != null)
            lobbyInstance.SetActive(false);
    }
    
    public void ShowCombatScreen()
    {
        if (combatInstance == null)
        {
            if (combatCanvasPrefab != null)
            {
                combatInstance = Object.Instantiate(combatCanvasPrefab);
                gameManager.InitializeCombatScreenReferences(combatInstance);
                
                // Find ScoreText
                Transform topArea = combatInstance.transform.Find("TopArea");
                if (topArea != null)
                {
                    scoreText = topArea.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
                }
            }
            else
            {
                Debug.LogError("CombatCanvasPrefab is not assigned!");
                return;
            }
        }
        combatInstance.SetActive(true);
    }
    
    public void HideCombatScreen()
    {
        Debug.Log("Hiding Combat Screen");
        if(combatInstance != null) combatInstance.SetActive(false);
    }
    
    public void ShowDraftScreen()
    {
        Debug.Log("Showing Draft Screen");
        if (draftInstance == null)
        {
            if (draftCanvasPrefab != null)
            {
                draftInstance = Object.Instantiate(draftCanvasPrefab);
                // Find Draft UI Elements
                draftOptionsPanel = draftInstance.transform.Find("DraftOptionsPanel")?.gameObject;
                draftTurnText = draftInstance.transform.Find("DraftTurnText")?.GetComponent<TextMeshProUGUI>();
                draftOptionButtonTemplate = draftOptionsPanel?.transform.Find("OptionButtonTemplate")?.gameObject;
                
                // --- ADDED: Find Draft Deck View Buttons & Controller ---
                Transform deckButtonsPanel = draftInstance.transform.Find("DeckButtonsPanel");
                viewDraftPlayerDeckButton = deckButtonsPanel?.Find("ViewPlayerDeckButton")?.GetComponent<Button>();
                viewDraftPetDeckButton = deckButtonsPanel?.Find("ViewPetDeckButton")?.GetComponent<Button>();
                
                // --- ADDED: Logging for button discovery ---
                if (viewDraftPlayerDeckButton != null)
                {
                    Debug.Log("Successfully found ViewPlayerDeckButton.");
                }
                else
                {
                     Debug.LogError("ViewPlayerDeckButton component NOT found on DeckButtonsPanel/ViewPlayerDeckButton object!");
                }
                // --- END ADDED Logging ---

                // Find or instantiate DeckViewController (similar to CombatManager)
                GameObject deckViewerPanelInstance = draftInstance.transform.Find("DeckViewerPanel")?.gameObject;
                if (deckViewerPanelInstance == null && gameManager.GetDeckViewerPanelPrefab() != null) 
                {
                    Debug.Log("DeckViewerPanel not found, attempting to instantiate from prefab.");
                    deckViewerPanelInstance = Object.Instantiate(gameManager.GetDeckViewerPanelPrefab(), draftInstance.transform);
                    deckViewerPanelInstance.name = "DeckViewerPanel"; 
                }
                
                if (deckViewerPanelInstance != null)
                {
                    deckViewController = deckViewerPanelInstance.GetComponent<DeckViewController>();
                    if (deckViewController == null) 
                    {
                        Debug.LogError("DeckViewController component NOT found on DeckViewerPanel instance in Draft screen!");
                    }
                    else
                    {
                        Debug.Log("Successfully found or instantiated DeckViewController."); // Added Log
                    }
                }
                else 
                {
                     Debug.LogError("DeckViewerPanel instance could NOT be found or instantiated in Draft screen!");
                }
                
                // Similar check/log for pet button can be added if needed
                 viewDraftPetDeckButton?.onClick.AddListener(ShowDraftPetDeck);
                // --- END ADDED SECTION ---

                if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null || viewDraftPlayerDeckButton == null || viewDraftPetDeckButton == null)
                {
                    Debug.LogError("One or more Draft UI elements (including deck view buttons) not found in DraftCanvasPrefab!");
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
    }
    
    public void HideDraftScreen()
    {
        Debug.Log("Hiding Draft Screen");
        if(draftInstance != null) draftInstance.SetActive(false);
    }
    
    public void TransitionToLobby()
    {
        currentState = GameState.Lobby;
        HideStartScreen();
        HideCombatScreen();
        HideDraftScreen();
        ShowLobbyScreen();
    }
    
    public void TransitionToCombat()
    {
        currentState = GameState.Combat;
        HideStartScreen();
        HideLobbyScreen();
        HideDraftScreen();
        ShowCombatScreen();
    }
    
    public void TransitionToDraft()
    {
        currentState = GameState.Drafting;
        HideStartScreen();
        HideLobbyScreen();
        HideCombatScreen();
        ShowDraftScreen();
    }
    
    public void TransitionToGameOver()
    {
        currentState = GameState.GameOver;
        HideStartScreen();
        HideLobbyScreen();
        HideCombatScreen();
        HideDraftScreen();
        // Show game over screen if you have one
    }
    
    public void OnDisconnected()
    {
        currentState = GameState.Connecting;
        if (lobbyInstance != null) Object.Destroy(lobbyInstance);
        if (combatInstance != null) Object.Destroy(combatInstance);
        if (draftInstance != null) Object.Destroy(draftInstance);
        ShowStartScreen();
    }
    
    public bool IsUserInitiatedConnection()
    {
        return userInitiatedConnection;
    }
    
    public void SetUserInitiatedConnection(bool value)
    {
        userInitiatedConnection = value;
    }
    
    public void SetWaitingToStartCombatRPC(bool value)
    {
        isWaitingToStartCombatRPC = value;
    }
    
    public bool IsWaitingToStartCombatRPC()
    {
        return isWaitingToStartCombatRPC;
    }
    
    public TMP_InputField GetPlayerNameInput()
    {
        return playerNameInput;
    }
    
    public TMP_InputField GetPetNameInput()
    {
        return petNameInput;
    }
    
    public Button GetConnectButton()
    {
        return connectButton;
    }
    
    public GameObject GetPlayerListPanel()
    {
        return playerListPanel;
    }
    
    public GameObject GetPlayerEntryTemplate()
    {
        return playerEntryTemplate;
    }
    
    public GameObject GetDraftOptionsPanel()
    {
        return draftOptionsPanel;
    }
    
    public TextMeshProUGUI GetDraftTurnText()
    {
        return draftTurnText;
    }
    
    public GameObject GetDraftOptionButtonTemplate()
    {
        return draftOptionButtonTemplate;
    }
    
    public GameObject GetLobbyInstance()
    {
        return lobbyInstance;
    }
    
    public Button GetStartGameButton()
    {
        return startGameButton;
    }
    
    public GameObject GetCombatInstance()
    {
        return combatInstance;
    }
    
    public TextMeshProUGUI GetScoreText()
    {
        return scoreText;
    }
    
    public void UpdateDraftUI(List<SerializableDraftOption> localCurrentPack, Dictionary<int, DraftOption> localActiveOptionMap, bool isWaitingForLocalPick)
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
                Object.Destroy(child.gameObject);
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
                    GameObject optionButtonGO = Object.Instantiate(draftOptionButtonTemplate, draftOptionsPanel.transform);
                    optionButtonGO.SetActive(true);
                    
                    TextMeshProUGUI buttonText = optionButtonGO.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null) buttonText.text = optionData.Description;
                    
                    Button optionButton = optionButtonGO.GetComponent<Button>();
                    if (optionButton != null)
                    {
                        int optionId = optionData.OptionId;
                        optionButton.onClick.RemoveAllListeners();
                        optionButton.onClick.AddListener(() => gameManager.HandleOptionSelected(optionId));
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

    // --- ADDED: Deck Viewing Methods for Draft Screen ---
    public void ShowDraftPlayerDeck()
    {
        Debug.Log("--- ShowDraftPlayerDeck method called! ---");
        if (deckViewController == null) 
        {
            Debug.LogError("DeckViewController is null in ShowDraftPlayerDeck");
            return;
        }
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) 
        {
             Debug.LogError("CardManager is null in ShowDraftPlayerDeck");
            return;
        }

        List<CardData> playerDeck = cardManager.GetDeck(); // Get the player's current full deck
        if (playerDeck != null)
        {
             deckViewController.ShowDeck("My Deck", playerDeck);
        }
        else
        {
            Debug.LogWarning("Player deck is null or empty, cannot show.");
        }
    }

    public void ShowDraftPetDeck()
    {
         if (deckViewController == null) 
        {
            Debug.LogError("DeckViewController is null in ShowDraftPetDeck");
            return;
        }
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) 
        {
            Debug.LogError("CardManager is null in ShowDraftPetDeck");
            return;
        }
        
        List<CardData> petDeck = cardManager.GetLocalPetDeck(); // Get the pet's current full deck
        if (petDeck != null)
        {
            deckViewController.ShowDeck("Pet Deck", petDeck);
        }
         else
        {
            Debug.LogWarning("Pet deck is null or empty, cannot show.");
        }
    }
    // --- END ADDED SECTION ---
}