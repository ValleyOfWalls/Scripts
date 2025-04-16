using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.EventSystems; // Add this namespace for UI event interface

public class UIManager : MonoBehaviour
{
    // Main UI Canvas
    private Canvas mainCanvas;
    private CanvasScaler canvasScaler;
    
    // Core UI Panels
    private GameObject connectingPanel;
    private GameObject mainMenuPanel;
    private GameObject lobbyPanel;
    private GameObject errorPanel;
    private GameObject gameplayPanel;
    private GameObject gameOverPanel;
    private GameObject phaseAnnouncementPanel;
    private GameObject draftPanel;
    
    // References to other managers
    private NetworkManager networkManager;
    private GameplayManager gameplayManager;
    private DeckManager deckManager;
    
    // Random name generation
    private string[] firstNames = { "Magic", "Brave", "Swift", "Lucky", "Shadow", "Cosmic", "Mystic", "Fierce", "Speedy", "Nimble", "Wild", "Mighty" };
    private string[] lastNames = { "Wizard", "Knight", "Dragon", "Phoenix", "Tiger", "Falcon", "Unicorn", "Warrior", "Hunter", "Sprite", "Mage", "Hero" };

    // Main Menu UI elements
    private TextMeshProUGUI connectingText;
    private TextMeshProUGUI errorText;
    private Button errorCloseButton;
    private TMP_InputField nameInput;
    private TMP_InputField roomInput;

    // Lobby UI elements
    private TextMeshProUGUI roomNameText;
    private GameObject playerListContent;
    private Button leaveRoomButton;
    private Button readyButtonComponent;
    private GameObject readyButton;
    private bool isReady = false;

    // GamePlay UI elements
    private GameObject battlePanel;
    private GameObject cardHandPanel;
    private GameObject playerStatsPanel;
    private GameObject enemyMonsterPanel;
    private GameObject playerMonsterPanel;
    private GameObject battleOverviewPanel;
    private GameObject endTurnButton;
    
    // Battle UI elements
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerEnergyText;
    private TextMeshProUGUI playerBlockText;
    private Slider playerHealthBar;
    private TextMeshProUGUI enemyMonsterNameText;
    private TextMeshProUGUI enemyMonsterHealthText;
    private Slider enemyMonsterHealthBar;
    private TextMeshProUGUI playerMonsterNameText;
    private TextMeshProUGUI playerMonsterHealthText;
    private Slider playerMonsterHealthBar;
    
    // Draft UI elements
    private TextMeshProUGUI phaseAnnouncementText;
    private TextMeshProUGUI winnerText;
    
    // Card UI elements
    private List<GameObject> cardObjects = new List<GameObject>();
    private GameObject enemyDropZone;
    private GameObject petDropZone;
    private GameObject feedbackTextPrefab;
    private int draggedCardIndex = -1;
    private Vector3 dragStartPosition;
    
    // Game references
    private PlayerController localPlayer;
    private MonsterController enemyMonster;
    private MonsterController playerMonster;
    
    private void Start()
    {
        // Find manager references
        FindManagers();
        
        CreateCanvas();
        InitializeAllUI();
        HideAllPanels();
        
        Debug.Log("UIManager initialized");
    }
    
    private void FindManagers()
    {
        // Find NetworkManager reference
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null && GameManager.Instance != null)
            {
                networkManager = GameManager.Instance.GetNetworkManager();
            }
        }
        
        // Find other managers
        if (gameplayManager == null && GameManager.Instance != null)
        {
            gameplayManager = GameManager.Instance.GetGameplayManager();
        }
        
        if (deckManager == null)
        {
            deckManager = FindObjectOfType<DeckManager>();
        }
    }

    #region Core UI Setup
    
    private void CreateCanvas()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("MainCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Add CanvasScaler
        canvasScaler = canvasObj.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        // Add GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();

        // Make canvas persistent
        DontDestroyOnLoad(canvasObj);
    }
    
    private void InitializeAllUI()
    {
        // Initialize all UI elements
        InitializeGameStartUI();
        InitializeLobbyUI();
        InitializeCombatUI();
        InitializeDraftUI();
    }
    
    public void HideAllPanels()
    {
        Debug.Log("Hiding all UI panels");
        
        if (connectingPanel != null) connectingPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (phaseAnnouncementPanel != null) phaseAnnouncementPanel.SetActive(false);
        if (draftPanel != null) draftPanel.SetActive(false);
        if (battlePanel != null) battlePanel.SetActive(false);
    }

    private GameObject CreatePanel(string name)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.8f);
        
        return panel;
    }
    
    private TextMeshProUGUI CreateText(GameObject parent, string name, string text)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = 28;
        textComponent.color = Color.white;
        
        return textComponent;
    }

    private Button CreateButton(GameObject parent, string name, string text)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent.transform, false);
        
        // Add button image
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.4f, 0.8f, 1);
        
        // Add text component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        // Add button component
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Add color transition
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.4f, 0.8f, 1);
        colors.highlightedColor = new Color(0.3f, 0.5f, 0.9f, 1);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.7f, 1);
        colors.selectedColor = new Color(0.2f, 0.4f, 0.8f, 1);
        button.colors = colors;
        
        return button;
    }

    private TMP_InputField CreateInputField(GameObject parent, string name, string placeholder)
    {
        GameObject inputFieldObj = new GameObject(name);
        inputFieldObj.transform.SetParent(parent.transform, false);
        
        // Add background image
        Image backgroundImage = inputFieldObj.AddComponent<Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
        
        // Add text component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputFieldObj.transform, false);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.alignment = TextAlignmentOptions.Left;
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
        
        // Add placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputFieldObj.transform, false);
        TextMeshProUGUI placeholderComponent = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderComponent.text = placeholder;
        placeholderComponent.alignment = TextAlignmentOptions.Left;
        placeholderComponent.fontSize = 24;
        placeholderComponent.color = new Color(0.7f, 0.7f, 0.7f, 1);
        
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = new Vector2(0, 0);
        placeholderRect.anchorMax = new Vector2(1, 1);
        placeholderRect.offsetMin = new Vector2(10, 5);
        placeholderRect.offsetMax = new Vector2(-10, -5);
        
        // Add input field component
        TMP_InputField inputField = inputFieldObj.AddComponent<TMP_InputField>();
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholderComponent;
        inputField.text = "";
        
        return inputField;
    }
    
    private Slider CreateProgressBar(GameObject parent, string name, Color fillColor)
    {
        GameObject barObj = new GameObject(name);
        barObj.transform.SetParent(parent.transform, false);
        
        Slider slider = barObj.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;
        
        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(barObj.transform, false);
        
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        // Create fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(barObj.transform, false);
        
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(0, 0);
        fillAreaRect.offsetMax = new Vector2(0, 0);
        
        // Create fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor;
        
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        // Set up slider
        slider.fillRect = fillRect;
        
        return slider;
    }
    
    // Helper method to create a rounded rectangle sprite
    private Sprite CreateRoundedRectSprite(int cornerRadius)
    {
        int width = 100;
        int height = 100;
        
        Texture2D texture = new Texture2D(width, height);
        
        // Fill with transparent pixels initially
        Color[] colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // Draw a rounded rectangle
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Check if we're inside the rounded rectangle
                if (y < cornerRadius) // Bottom corners
                {
                    if (x < cornerRadius) // Bottom left
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                        if (distance <= cornerRadius)
                            texture.SetPixel(x, y, Color.white);
                    }
                    else if (x >= width - cornerRadius) // Bottom right
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius, cornerRadius));
                        if (distance <= cornerRadius)
                            texture.SetPixel(x, y, Color.white);
                    }
                    else // Bottom middle
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
                else if (y >= height - cornerRadius) // Top corners
                {
                    if (x < cornerRadius) // Top left
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, height - cornerRadius));
                        if (distance <= cornerRadius)
                            texture.SetPixel(x, y, Color.white);
                    }
                    else if (x >= width - cornerRadius) // Top right
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius, height - cornerRadius));
                        if (distance <= cornerRadius)
                            texture.SetPixel(x, y, Color.white);
                    }
                    else // Top middle
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
                else // Middle rows
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
    
    // Helper method to create a circle sprite
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        int radius = size / 2;
        
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Bilinear;
        
        // Fill with transparent pixels initially
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // Draw a circle
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                if (distance <= radius)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }
    
    #endregion

    #region Game Start UI

    private void InitializeGameStartUI()
    {
        // Create Connecting Panel
        connectingPanel = CreatePanel("ConnectingPanel");
        connectingText = CreateText(connectingPanel, "ConnectingText", "Connecting...");
        
        // Create Main Menu Panel
        mainMenuPanel = CreatePanel("MainMenuPanel");
        CreateMainMenuUI(mainMenuPanel);
        
        // Create Error Panel
        errorPanel = CreatePanel("ErrorPanel");
        CreateErrorUI(errorPanel);
    }
    
    private void CreateMainMenuUI(GameObject panel)
{
    // Create title
    TextMeshProUGUI titleText = CreateText(panel, "TitleText", "Card Battle Game");
    RectTransform titleRect = titleText.GetComponent<RectTransform>();
    titleRect.anchorMin = new Vector2(0.5f, 0.8f);
    titleRect.anchorMax = new Vector2(0.5f, 0.9f);
    titleRect.sizeDelta = new Vector2(600, 100);
    titleText.fontSize = 60;
    
    // Create subtitle
    TextMeshProUGUI subtitleText = CreateText(panel, "SubtitleText", "A Multiplayer Deck Builder");
    RectTransform subtitleRect = subtitleText.GetComponent<RectTransform>();
    subtitleRect.anchorMin = new Vector2(0.5f, 0.72f);
    subtitleRect.anchorMax = new Vector2(0.5f, 0.78f);
    subtitleRect.sizeDelta = new Vector2(400, 50);
    subtitleText.fontSize = 24;
    subtitleText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
    
    // Create input fields container
    GameObject inputContainer = new GameObject("InputContainer");
    inputContainer.transform.SetParent(panel.transform, false);
    RectTransform inputContainerRect = inputContainer.AddComponent<RectTransform>();
    inputContainerRect.anchorMin = new Vector2(0.5f, 0.4f);
    inputContainerRect.anchorMax = new Vector2(0.5f, 0.7f);
    inputContainerRect.sizeDelta = new Vector2(400, 300);
    
    // Create name input field
    nameInput = CreateInputField(inputContainer, "NameInput", "Your Name");
    RectTransform nameInputRect = nameInput.GetComponent<RectTransform>();
    nameInputRect.anchorMin = new Vector2(0, 0.7f);
    nameInputRect.anchorMax = new Vector2(1, 0.85f);
    nameInputRect.sizeDelta = new Vector2(0, 0);
    
    // Set a random name
    if (nameInput != null)
    {
        nameInput.text = GenerateRandomName();
    }
    
    // Create room code input field
    roomInput = CreateInputField(inputContainer, "RoomInput", "Room Code");
    RectTransform roomInputRect = roomInput.GetComponent<RectTransform>();
    roomInputRect.anchorMin = new Vector2(0, 0.4f);
    roomInputRect.anchorMax = new Vector2(1, 0.55f);
    roomInputRect.sizeDelta = new Vector2(0, 0);
    
    // Set default room code
    if (roomInput != null)
    {
        roomInput.text = "asd";
    }
    
    // Create button container
    GameObject buttonContainer = new GameObject("ButtonContainer");
    buttonContainer.transform.SetParent(panel.transform, false);
    RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
    buttonContainerRect.anchorMin = new Vector2(0.5f, 0.2f);
    buttonContainerRect.anchorMax = new Vector2(0.5f, 0.3f);
    buttonContainerRect.sizeDelta = new Vector2(400, 100);
    
    // Create join button
    Button joinButton = CreateButton(buttonContainer, "JoinButton", "Join Room");
    RectTransform joinButtonRect = joinButton.GetComponent<RectTransform>();
    joinButtonRect.anchorMin = new Vector2(0, 0);
    joinButtonRect.anchorMax = new Vector2(0.48f, 1);
    joinButtonRect.sizeDelta = new Vector2(0, 0);
    
    // Create create button
    Button createButton = CreateButton(buttonContainer, "CreateButton", "Create Room");
    RectTransform createButtonRect = createButton.GetComponent<RectTransform>();
    createButtonRect.anchorMin = new Vector2(0.52f, 0);
    createButtonRect.anchorMax = new Vector2(1, 1);
    createButtonRect.sizeDelta = new Vector2(0, 0);
    
    // Set button actions with proper null checking and connection check
    joinButton.onClick.AddListener(() => {
        FindManagers(); // Make sure we have the NetworkManager
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null when trying to join room");
            ShowErrorUI("Error: Cannot connect to network services");
            return;
        }
        
        string playerName = nameInput != null ? nameInput.text : GenerateRandomName();
        string roomCode = roomInput != null ? roomInput.text : "asd";
        
        // Check if connected first, if not, connect
        if (!networkManager.IsConnected())
        {
            // Start connection first, then join room when connected
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartConnection();
                StartCoroutine(JoinRoomWhenConnected(roomCode, playerName));
            }
        }
        else
        {
            // Already connected, join directly
            networkManager.JoinRoom(roomCode, playerName);
        }
    });
    
    createButton.onClick.AddListener(() => {
        FindManagers(); // Make sure we have the NetworkManager
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null when trying to create room");
            ShowErrorUI("Error: Cannot connect to network services");
            return;
        }
        
        string playerName = nameInput != null ? nameInput.text : GenerateRandomName();
        string roomCode = roomInput != null ? roomInput.text : ""; // Default to empty for CreateRoom
        
        // Directly call NetworkManager.CreateRoom
        // NetworkManager will handle the connection state check and deferred creation
        networkManager.CreateRoom(roomCode, playerName);
    });
}

    private IEnumerator JoinRoomWhenConnected(string roomCode, string playerName)
{
    // Wait until connected to Master Server specifically
    while (networkManager != null && 
          (!networkManager.IsConnected() || !PhotonNetwork.IsConnectedAndReady))
    {
        yield return new WaitForSeconds(0.5f);
    }
    
    // Join room when connected to Master
    if (networkManager != null && PhotonNetwork.IsConnectedAndReady)
    {
        networkManager.JoinRoom(roomCode, playerName);
    }
}

    private void CreateErrorUI(GameObject panel)
    {
        // Create error title
        TextMeshProUGUI errorTitle = CreateText(panel, "ErrorTitle", "Error");
        RectTransform errorTitleRect = errorTitle.GetComponent<RectTransform>();
        errorTitleRect.anchorMin = new Vector2(0.5f, 0.7f);
        errorTitleRect.anchorMax = new Vector2(0.5f, 0.8f);
        errorTitleRect.sizeDelta = new Vector2(400, 80);
        errorTitle.fontSize = 40;
        
        // Create error text
        errorText = CreateText(panel, "ErrorText", "An error occurred.");
        RectTransform errorTextRect = errorText.GetComponent<RectTransform>();
        errorTextRect.anchorMin = new Vector2(0.5f, 0.4f);
        errorTextRect.anchorMax = new Vector2(0.5f, 0.7f);
        errorTextRect.sizeDelta = new Vector2(600, 200);
        errorText.fontSize = 24;
        
        // Create close button
        errorCloseButton = CreateButton(panel, "CloseButton", "Close");
        RectTransform closeButtonRect = errorCloseButton.GetComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(0.5f, 0.2f);
        closeButtonRect.anchorMax = new Vector2(0.5f, 0.3f);
        closeButtonRect.sizeDelta = new Vector2(200, 60);
        
        errorCloseButton.onClick.AddListener(() => {
            HideErrorUI();
        });
    }
    
    public void ShowConnectingUI(string message)
    {
        HideAllPanels();
        if (connectingText != null) connectingText.text = message;
        if (connectingPanel != null) connectingPanel.SetActive(true);
    }

    public void ShowMainMenuUI()
    {
        HideAllPanels();
        
        // Generate a new random name each time we return to the menu
        if (nameInput != null)
        {
            nameInput.text = GenerateRandomName();
        }
        
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }
    
    public void ShowErrorUI(string message)
    {
        HideAllPanels();
        if (errorText != null) errorText.text = message;
        if (errorPanel != null) errorPanel.SetActive(true);
    }

    public void HideErrorUI()
    {
        if (errorPanel != null) errorPanel.SetActive(false);
        
        // Determine which panel to show based on the current game state
        if (GameManager.Instance != null)
        {
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            switch (currentState)
            {
                case GameManager.GameState.Connecting:
                    ShowConnectingUI("Connecting to server...");
                    break;
                case GameManager.GameState.Lobby:
                    ShowLobbyUI();
                    break;
                case GameManager.GameState.Battle:
                    ShowGameplayUI();
                    break;
                default:
                    ShowMainMenuUI();
                    break;
            }
        }
        else
        {
            ShowMainMenuUI();
        }
    }
    
    private string GenerateRandomName()
    {
        // Generate a random fantasy-style name
        int firstNameIndex = UnityEngine.Random.Range(0, firstNames.Length);
        int lastNameIndex = UnityEngine.Random.Range(0, lastNames.Length);
        return firstNames[firstNameIndex] + lastNames[lastNameIndex];
    }
    
    #endregion

    #region Lobby UI
    
    private void InitializeLobbyUI()
    {
        // Create Lobby Panel
        lobbyPanel = CreatePanel("LobbyPanel");
    }
    
    public void CreateLobbyUI()
    {
        // Clear existing UI elements in lobby panel
        if (lobbyPanel == null) return;
        
        foreach (Transform child in lobbyPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        // Create room info header
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(lobbyPanel.transform, false);
        
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 0.85f);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.sizeDelta = Vector2.zero;
        
        // Create room name text
        roomNameText = CreateText(headerObj, "RoomNameText", "Room: ");
        RectTransform roomNameRect = roomNameText.GetComponent<RectTransform>();
        roomNameRect.anchorMin = new Vector2(0.5f, 0.5f);
        roomNameRect.anchorMax = new Vector2(0.5f, 0.5f);
        roomNameRect.anchoredPosition = Vector2.zero;
        roomNameRect.sizeDelta = new Vector2(600, 80);
        roomNameText.fontSize = 36;
        
        // Create player list container
        GameObject playerListContainer = new GameObject("PlayerListContainer");
        playerListContainer.transform.SetParent(lobbyPanel.transform, false);
        
        RectTransform playerListContainerRect = playerListContainer.AddComponent<RectTransform>();
        playerListContainerRect.anchorMin = new Vector2(0.5f, 0.3f);
        playerListContainerRect.anchorMax = new Vector2(0.5f, 0.85f);
        playerListContainerRect.sizeDelta = new Vector2(600, 0);
        
        Image playerListBg = playerListContainer.AddComponent<Image>();
        playerListBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        // Create scrollview for player list
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(playerListContainer.transform, false);
        
        RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = Vector2.zero;
        scrollViewRect.anchorMax = Vector2.one;
        scrollViewRect.sizeDelta = new Vector2(-20, -20);
        scrollViewRect.anchoredPosition = Vector2.zero;
        
        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        
        // Create viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        
        Mask viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Create content for player list
        playerListContent = new GameObject("Content");
        playerListContent.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = playerListContent.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.pivot = new Vector2(0.5f, 1);
        
        VerticalLayoutGroup contentLayout = playerListContent.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        ContentSizeFitter contentSizeFitter = playerListContent.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Set up scroll view references
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20;
        scrollRect.inertia = true;
        
        // Create two separate containers for each button
        GameObject leaveButtonContainer = new GameObject("LeaveButtonContainer");
        leaveButtonContainer.transform.SetParent(lobbyPanel.transform, false);
        
        RectTransform leaveButtonContainerRect = leaveButtonContainer.AddComponent<RectTransform>();
        leaveButtonContainerRect.anchorMin = new Vector2(0.3f, 0.1f);
        leaveButtonContainerRect.anchorMax = new Vector2(0.3f, 0.1f);
        leaveButtonContainerRect.anchoredPosition = Vector2.zero;
        leaveButtonContainerRect.sizeDelta = new Vector2(180, 60);
        
        GameObject readyButtonContainer = new GameObject("ReadyButtonContainer");
        readyButtonContainer.transform.SetParent(lobbyPanel.transform, false);
        
        RectTransform readyButtonContainerRect = readyButtonContainer.AddComponent<RectTransform>();
        readyButtonContainerRect.anchorMin = new Vector2(0.7f, 0.1f);
        readyButtonContainerRect.anchorMax = new Vector2(0.7f, 0.1f);
        readyButtonContainerRect.anchoredPosition = Vector2.zero;
        readyButtonContainerRect.sizeDelta = new Vector2(180, 60);
        
        // Create leave button with new style
        GameObject leaveButtonObj = new GameObject("LeaveButton");
        leaveButtonObj.transform.SetParent(leaveButtonContainer.transform, false);
        
        RectTransform leaveButtonRect = leaveButtonObj.AddComponent<RectTransform>();
        leaveButtonRect.anchorMin = Vector2.zero;
        leaveButtonRect.anchorMax = Vector2.one;
        leaveButtonRect.sizeDelta = Vector2.zero;
        
        Image leaveButtonImage = leaveButtonObj.AddComponent<Image>();
        leaveButtonImage.color = new Color(0.85f, 0.2f, 0.2f, 1); // Red color for leave button
        
        // Add rounded corners to the leave button
        leaveButtonImage.sprite = CreateRoundedRectSprite(10); // 10 pixel radius for rounded corners
        
        // Create leave button text
        GameObject leaveTextObj = new GameObject("Text");
        leaveTextObj.transform.SetParent(leaveButtonObj.transform, false);
        
        TextMeshProUGUI leaveTextComponent = leaveTextObj.AddComponent<TextMeshProUGUI>();
        leaveTextComponent.text = "Leave Room";
        leaveTextComponent.alignment = TextAlignmentOptions.Center;
        leaveTextComponent.fontSize = 24;
        leaveTextComponent.color = Color.white;
        
        RectTransform leaveTextRect = leaveTextObj.GetComponent<RectTransform>();
        leaveTextRect.anchorMin = Vector2.zero;
        leaveTextRect.anchorMax = Vector2.one;
        leaveTextRect.sizeDelta = Vector2.zero;
        
        // Add button component
        leaveRoomButton = leaveButtonObj.AddComponent<Button>();
        leaveRoomButton.targetGraphic = leaveButtonImage;
        
        // Add color transition
        ColorBlock leaveColors = leaveRoomButton.colors;
        leaveColors.normalColor = new Color(0.85f, 0.2f, 0.2f, 1);
        leaveColors.highlightedColor = new Color(0.95f, 0.3f, 0.3f, 1);
        leaveColors.pressedColor = new Color(0.75f, 0.1f, 0.1f, 1);
        leaveColors.selectedColor = new Color(0.85f, 0.2f, 0.2f, 1);
        leaveRoomButton.colors = leaveColors;
        
        // Create ready button with new style
        GameObject readyButtonObj = new GameObject("ReadyButton");
        readyButtonObj.transform.SetParent(readyButtonContainer.transform, false);
        readyButton = readyButtonObj;
        
        RectTransform readyButtonRect = readyButtonObj.AddComponent<RectTransform>();
        readyButtonRect.anchorMin = Vector2.zero;
        readyButtonRect.anchorMax = Vector2.one;
        readyButtonRect.sizeDelta = Vector2.zero;
        
        Image readyButtonImage = readyButtonObj.AddComponent<Image>();
        readyButtonImage.color = new Color(0.2f, 0.7f, 0.3f, 1); // Green color for ready button
        
        // Add rounded corners to the ready button
        readyButtonImage.sprite = CreateRoundedRectSprite(10); // 10 pixel radius for rounded corners
        
        // Create ready button text
        GameObject readyTextObj = new GameObject("Text");
        readyTextObj.transform.SetParent(readyButtonObj.transform, false);
        
        TextMeshProUGUI readyTextComponent = readyTextObj.AddComponent<TextMeshProUGUI>();
        readyTextComponent.text = "Ready";
        readyTextComponent.alignment = TextAlignmentOptions.Center;
        readyTextComponent.fontSize = 24;
        readyTextComponent.color = Color.white;
        
        RectTransform readyTextRect = readyTextObj.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.sizeDelta = Vector2.zero;
        
        // Add button component
        readyButtonComponent = readyButtonObj.AddComponent<Button>();
        readyButtonComponent.targetGraphic = readyButtonImage;
        
        // Add color transition
        ColorBlock readyColors = readyButtonComponent.colors;
        readyColors.normalColor = new Color(0.2f, 0.7f, 0.3f, 1);
        readyColors.highlightedColor = new Color(0.3f, 0.8f, 0.4f, 1);
        readyColors.pressedColor = new Color(0.1f, 0.6f, 0.2f, 1);
        readyColors.selectedColor = new Color(0.2f, 0.7f, 0.3f, 1);
        readyButtonComponent.colors = readyColors;
        
        // Button listeners
        leaveRoomButton.onClick.AddListener(() => {
            Debug.Log("Leave button clicked");
            if (networkManager != null)
            {
                networkManager.LeaveRoom();
            }
            else
            {
                Debug.LogError("NetworkManager is null in button listener");
            }
        });
        
        readyButtonComponent.onClick.AddListener(() => {
            Debug.Log("Ready button clicked");
            ToggleReady();
        });
        
        // Set initial room name
        if (PhotonNetwork.InRoom)
        {
            roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
        }
        
        Debug.Log("Lobby UI created successfully");
    }
    
    private void ToggleReady()
    {
        isReady = !isReady;
        Debug.Log($"Ready status toggled to: {isReady}");
        
        if (networkManager != null)
        {
            networkManager.SetPlayerReady(isReady);
        }
        else
        {
            Debug.LogError("NetworkManager is null in ToggleReady");
            // Try to find it again
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager != null)
            {
                networkManager.SetPlayerReady(isReady);
            }
        }
        
        UpdateReadyButtonUI();
    }
    
    private void UpdateReadyButtonUI()
    {
        if (readyButtonComponent != null)
        {
            // Get the button image and text
            Image buttonImage = readyButtonComponent.GetComponent<Image>();
            TextMeshProUGUI buttonText = readyButtonComponent.GetComponentInChildren<TextMeshProUGUI>();
            
            if (isReady)
            {
                // Unready - Red color
                if (buttonImage != null)
                {
                    buttonImage.color = new Color(0.85f, 0.2f, 0.2f, 1);
                }
                
                // Update color transition
                ColorBlock colors = readyButtonComponent.colors;
                colors.normalColor = new Color(0.85f, 0.2f, 0.2f, 1);
                colors.highlightedColor = new Color(0.95f, 0.3f, 0.3f, 1);
                colors.pressedColor = new Color(0.75f, 0.1f, 0.1f, 1);
                colors.selectedColor = new Color(0.85f, 0.2f, 0.2f, 1);
                readyButtonComponent.colors = colors;
                
                if (buttonText != null)
                {
                    buttonText.text = "Unready";
                }
            }
            else
            {
                // Ready - Green color
                if (buttonImage != null)
                {
                    buttonImage.color = new Color(0.2f, 0.7f, 0.3f, 1);
                }
                
                // Update color transition
                ColorBlock colors = readyButtonComponent.colors;
                colors.normalColor = new Color(0.2f, 0.7f, 0.3f, 1);
                colors.highlightedColor = new Color(0.3f, 0.8f, 0.4f, 1);
                colors.pressedColor = new Color(0.1f, 0.6f, 0.2f, 1);
                colors.selectedColor = new Color(0.2f, 0.7f, 0.3f, 1);
                readyButtonComponent.colors = colors;
                
                if (buttonText != null)
                {
                    buttonText.text = "Ready";
                }
            }
        }
    }
    
    public void UpdateLobbyUI()
    {
        Debug.Log("Updating lobby UI");
        
        // First check if we're in a room
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("UpdateLobbyUI called but not in a room");
            return;
        }
        
        // Then check if playerListContent exists
        if (playerListContent == null)
        {
            Debug.LogWarning("UpdateLobbyUI called but playerListContent is null");
            
            // Try to recreate the UI if needed
            if (lobbyPanel != null)
            {
                CreateLobbyUI();
            }
            else
            {
                Debug.LogError("Lobby panel is null, cannot recreate UI");
                return;
            }
        }
        
        // Update room name
        if (roomNameText != null)
        {
            roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
        }
        
        // Clear existing player entries
        if (playerListContent != null)
        {
            foreach (Transform child in playerListContent.transform)
            {
                Destroy(child.gameObject);
            }
        }
        
        // Get player list
        List<PlayerInfo> players = null;
        if (GameManager.Instance != null)
        {
            players = GameManager.Instance.GetAllPlayers();
            Debug.Log($"Found {players.Count} players in the room");
        }
        else
        {
            Debug.LogError("GameManager.Instance is null in UpdateLobbyUI");
            return;
        }
        
        // Create player entries
        if (players != null && playerListContent != null)
        {
            foreach (PlayerInfo player in players)
            {
                CreatePlayerEntry(player);
            }
        }
        
        // Update ready button color based on status
        UpdateReadyButtonUI();
    }
    
    private void CreatePlayerEntry(PlayerInfo player)
    {
        GameObject playerEntryObj = new GameObject("PlayerEntry");
        playerEntryObj.transform.SetParent(playerListContent.transform, false);
        
        RectTransform playerEntryRect = playerEntryObj.AddComponent<RectTransform>();
        playerEntryRect.sizeDelta = new Vector2(0, 50);
        
        Image playerEntryBg = playerEntryObj.AddComponent<Image>();
        playerEntryBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Create horizontal layout
        HorizontalLayoutGroup entryLayout = playerEntryObj.AddComponent<HorizontalLayoutGroup>();
        entryLayout.spacing = 10;
        entryLayout.padding = new RectOffset(10, 10, 5, 5);
        entryLayout.childAlignment = TextAnchor.MiddleLeft;
        
        // Create player name text
        TextMeshProUGUI nameText = CreateText(playerEntryObj, "NameText", player.PlayerName);
        RectTransform nameTextRect = nameText.GetComponent<RectTransform>();
        nameTextRect.sizeDelta = new Vector2(350, 0);
        
        // Create ready status indicator
        GameObject statusObj = new GameObject("StatusIndicator");
        statusObj.transform.SetParent(playerEntryObj.transform, false);
        
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(100, 30);
        
        Image statusImage = statusObj.AddComponent<Image>();
        statusImage.color = player.IsReady ? new Color(0, 1, 0, 1) : new Color(1, 0, 0, 1);
        
        TextMeshProUGUI statusText = CreateText(statusObj, "StatusText", player.IsReady ? "Ready" : "Not Ready");
        statusText.fontSize = 16;
        
        // Is this the master client?
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players.Count > 0)
        {
            // Check if this player is the master client (first player)
            if (PhotonNetwork.CurrentRoom.GetPlayer(1) != null && 
                PhotonNetwork.CurrentRoom.GetPlayer(1).UserId == player.PlayerId)
            {
                GameObject hostIndicator = new GameObject("HostIndicator");
                hostIndicator.transform.SetParent(playerEntryObj.transform, false);
                
                RectTransform hostRect = hostIndicator.AddComponent<RectTransform>();
                hostRect.sizeDelta = new Vector2(80, 30);
                
                Image hostImage = hostIndicator.AddComponent<Image>();
                hostImage.color = new Color(1, 0.8f, 0, 1);
                
                TextMeshProUGUI hostText = CreateText(hostIndicator, "HostText", "Host");
                hostText.fontSize = 16;
            }
        }
    }
    
    public void ShowLobbyUI()
    {
        HideAllPanels();
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
            UpdateLobbyUI();

            // Ensure gameplay related canvases (if they exist and are known) are off
            // Assuming PlayerUICanvas and MonsterUICanvas are fields in UIManager or accessible
            // You might need to add references to these if they aren't already present
            // Example: Assuming 'gameplayPanel' holds these UI elements or they are separate GameObjects
            if (gameplayPanel != null) gameplayPanel.SetActive(false); // Hide the main gameplay container
            if (battlePanel != null) battlePanel.SetActive(false); // Hide the specific battle UI container
            // If PlayerUICanvas and MonsterUICanvas are separate top-level objects, find and disable them:
            // GameObject playerUICanvas = GameObject.Find("PlayerUICanvas"); // Example find logic
            // if (playerUICanvas != null) playerUICanvas.SetActive(false);
            // GameObject monsterUICanvas = GameObject.Find("MonsterUICanvas"); // Example find logic
            // if (monsterUICanvas != null) monsterUICanvas.SetActive(false);
        }
        else
        {
            Debug.LogError("Lobby panel is null in ShowLobbyUI");
        }
    }
    
    public void UpdatePlayerReadyStatus(string playerId, bool playerIsReady)
    {
        Debug.Log($"Updating player ready status: {playerId}, {playerIsReady}");
        UpdateLobbyUI();
    }
    
    #endregion

    #region Combat UI
    
    private void InitializeCombatUI()
    {
        // Create Gameplay Panel
        gameplayPanel = CreatePanel("GameplayPanel");
        CreateGameplayUI(gameplayPanel);
        
        // Create Battle UI
        CreateBattleUI();
        
        // Create Battle Overview UI
        CreateBattleOverviewUI();
        
        // Create Card Hand UI
        CreateCardHandUI();
        
        // Create dropzones
        CreateDropZones();
        
        // Create feedback text prefab
        CreateFeedbackTextPrefab();
    }
    
    private void CreateGameplayUI(GameObject panel)
    {
        // Create a title with game information
        TextMeshProUGUI titleText = CreateText(panel, "TitleText", "Card Battle Game");
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.93f);
        titleRect.anchorMax = new Vector2(0.5f, 0.98f);
        titleRect.sizeDelta = new Vector2(600, 40);
        titleText.fontSize = 32;
        
        // Create a round indicator
        TextMeshProUGUI roundText = CreateText(panel, "RoundText", "Round 1");
        RectTransform roundRect = roundText.GetComponent<RectTransform>();
        roundRect.anchorMin = new Vector2(0.5f, 0.9f);
        roundRect.anchorMax = new Vector2(0.5f, 0.93f);
        roundRect.sizeDelta = new Vector2(200, 30);
        roundText.fontSize = 24;
        
        // Add a note that this panel is just a container
        TextMeshProUGUI noteText = CreateText(panel, "NoteText", "Game UI is loading...");
        RectTransform noteRect = noteText.GetComponent<RectTransform>();
        noteRect.anchorMin = new Vector2(0.5f, 0.5f);
        noteRect.anchorMax = new Vector2(0.5f, 0.5f);
        noteRect.sizeDelta = new Vector2(400, 60);
        noteText.fontSize = 20;
    }
    
    private void CreateBattleUI()
    {
        // Create main battle panel
        battlePanel = new GameObject("BattlePanel");
        battlePanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform battlePanelRect = battlePanel.AddComponent<RectTransform>();
        battlePanelRect.anchorMin = new Vector2(0, 0);
        battlePanelRect.anchorMax = new Vector2(1, 1);
        battlePanelRect.sizeDelta = Vector2.zero;
        
        Image battlePanelImage = battlePanel.AddComponent<Image>();
        battlePanelImage.color = new Color(0.1f, 0.1f, 0.15f, 1);
        
        // Create player stats panel (top left)
        playerStatsPanel = new GameObject("PlayerStatsPanel");
        playerStatsPanel.transform.SetParent(battlePanel.transform, false);
        
        RectTransform playerPanelRect = playerStatsPanel.AddComponent<RectTransform>();
        playerPanelRect.anchorMin = new Vector2(0.01f, 0.75f);
        playerPanelRect.anchorMax = new Vector2(0.25f, 0.99f);
        playerPanelRect.sizeDelta = Vector2.zero;
        
        Image playerPanelImage = playerStatsPanel.AddComponent<Image>();
        playerPanelImage.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        
        // Create opponent pet panel (top right)
        enemyMonsterPanel = new GameObject("EnemyMonsterPanel");
        enemyMonsterPanel.transform.SetParent(battlePanel.transform, false);
        
        RectTransform enemyPanelRect = enemyMonsterPanel.AddComponent<RectTransform>();
        enemyPanelRect.anchorMin = new Vector2(0.75f, 0.75f);
        enemyPanelRect.anchorMax = new Vector2(0.99f, 0.99f);
        enemyPanelRect.sizeDelta = Vector2.zero;
        
        Image enemyPanelImage = enemyMonsterPanel.AddComponent<Image>();
        enemyPanelImage.color = new Color(0.4f, 0.2f, 0.2f, 0.9f);
        
        // Create player's pet panel (top middle)
        playerMonsterPanel = new GameObject("PlayerMonsterPanel");
        playerMonsterPanel.transform.SetParent(battlePanel.transform, false);
        
        RectTransform petPanelRect = playerMonsterPanel.AddComponent<RectTransform>();
        petPanelRect.anchorMin = new Vector2(0.38f, 0.75f);
        petPanelRect.anchorMax = new Vector2(0.62f, 0.99f);
        petPanelRect.sizeDelta = Vector2.zero;
        
        Image petPanelImage = playerMonsterPanel.AddComponent<Image>();
        petPanelImage.color = new Color(0.2f, 0.4f, 0.3f, 0.9f);
        
        // Set up player panel content
        SetupPlayerPanel();
        
        // Set up opponent pet panel content
        SetupEnemyMonsterPanel();
        
        // Set up player's pet panel content
        SetupPlayerMonsterPanel();
        
        // Create turn indicator and end turn button
        GameObject turnContainer = new GameObject("TurnContainer");
        turnContainer.transform.SetParent(battlePanel.transform, false);
        
        RectTransform turnContainerRect = turnContainer.AddComponent<RectTransform>();
        turnContainerRect.anchorMin = new Vector2(0.38f, 0.65f);
        turnContainerRect.anchorMax = new Vector2(0.62f, 0.73f);
        turnContainerRect.sizeDelta = Vector2.zero;
        
        Image turnContainerImage = turnContainer.AddComponent<Image>();
        turnContainerImage.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);
        
        turnText = CreateText(turnContainer, "TurnText", "Waiting for turn...");
        turnText.fontSize = 24;
        
        // Create end turn button
        endTurnButton = new GameObject("EndTurnButton");
        endTurnButton.transform.SetParent(battlePanel.transform, false);
        
        RectTransform endTurnRect = endTurnButton.AddComponent<RectTransform>();
        endTurnRect.anchorMin = new Vector2(0.44f, 0.27f);
        endTurnRect.anchorMax = new Vector2(0.56f, 0.33f);
        endTurnRect.sizeDelta = Vector2.zero;
        
        Image endTurnImage = endTurnButton.AddComponent<Image>();
        endTurnImage.color = new Color(0.3f, 0.6f, 0.8f, 1);
        
        // Add rounded corners using a sprite
        endTurnImage.sprite = CreateRoundedRectSprite(15);
        endTurnImage.type = Image.Type.Sliced;
        
        // End turn button text
        GameObject endTurnTextObj = new GameObject("Text");
        endTurnTextObj.transform.SetParent(endTurnButton.transform, false);
        
        TextMeshProUGUI endTurnTextComponent = endTurnTextObj.AddComponent<TextMeshProUGUI>();
        endTurnTextComponent.text = "End Turn";
        endTurnTextComponent.fontSize = 20;
        endTurnTextComponent.alignment = TextAlignmentOptions.Center;
        endTurnTextComponent.color = Color.white;
        
        RectTransform endTurnTextRect = endTurnTextObj.GetComponent<RectTransform>();
        endTurnTextRect.anchorMin = Vector2.zero;
        endTurnTextRect.anchorMax = Vector2.one;
        endTurnTextRect.sizeDelta = Vector2.zero;
        
        // Add button component
        Button endTurnButtonComponent = endTurnButton.AddComponent<Button>();
        endTurnButtonComponent.targetGraphic = endTurnImage;
        
        // Set colors for button states
        ColorBlock colors = endTurnButtonComponent.colors;
        colors.normalColor = new Color(0.3f, 0.6f, 0.8f, 1);
        colors.highlightedColor = new Color(0.4f, 0.7f, 0.9f, 1);
        colors.pressedColor = new Color(0.2f, 0.5f, 0.7f, 1);
        endTurnButtonComponent.colors = colors;
        
        // Add click handler
        endTurnButtonComponent.onClick.AddListener(EndTurnClicked);
        
        // Initially hide the end turn button
        endTurnButton.SetActive(false);
        
        // Initially hide the battle panel
        battlePanel.SetActive(false);
    }
    
    private void SetupPlayerPanel()
    {
        VerticalLayoutGroup layout = playerStatsPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Player name
        playerNameText = CreateText(playerStatsPanel, "PlayerNameText", "Player Name");
        playerNameText.fontSize = 20;
        playerNameText.alignment = TextAlignmentOptions.Left;
        RectTransform nameRect = playerNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 30);
        
        // Player health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(playerStatsPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        playerHealthText = CreateText(healthContainer, "HealthText", "HP: 0/0");
        playerHealthText.fontSize = 18;
        playerHealthText.alignment = TextAlignmentOptions.Left;
        RectTransform healthTextRect = playerHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        healthTextRect.sizeDelta = Vector2.zero;
        
        playerHealthBar = CreateProgressBar(healthContainer, "HealthBar", Color.red);
        RectTransform healthBarRect = playerHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        healthBarRect.sizeDelta = Vector2.zero;
        
        // Player energy
        playerEnergyText = CreateText(playerStatsPanel, "EnergyText", "Energy: 0/0");
        playerEnergyText.fontSize = 18;
        playerEnergyText.alignment = TextAlignmentOptions.Left;
        RectTransform energyRect = playerEnergyText.GetComponent<RectTransform>();
        energyRect.sizeDelta = new Vector2(0, 25);
        
        // Player block
        playerBlockText = CreateText(playerStatsPanel, "BlockText", "Block: 0");
        playerBlockText.fontSize = 18;
        playerBlockText.alignment = TextAlignmentOptions.Left;
        RectTransform blockRect = playerBlockText.GetComponent<RectTransform>();
        blockRect.sizeDelta = new Vector2(0, 25);
    }
    
    private void SetupEnemyMonsterPanel()
    {
        VerticalLayoutGroup layout = enemyMonsterPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Add title
        TextMeshProUGUI titleText = CreateText(enemyMonsterPanel, "TitleText", "OPPONENT PET");
        titleText.fontSize = 16;
        titleText.color = new Color(1, 0.7f, 0.7f);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 20);
        
        // Pet name
        enemyMonsterNameText = CreateText(enemyMonsterPanel, "PetNameText", "Unknown");
        enemyMonsterNameText.fontSize = 18;
        RectTransform nameRect = enemyMonsterNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 25);
        
        // Pet health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(enemyMonsterPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        enemyMonsterHealthText = CreateText(healthContainer, "HealthText", "HP: 0/0");
        enemyMonsterHealthText.fontSize = 18;
        RectTransform healthTextRect = enemyMonsterHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        healthTextRect.sizeDelta = Vector2.zero;
        
        enemyMonsterHealthBar = CreateProgressBar(healthContainer, "HealthBar", new Color(0.8f, 0.2f, 0.2f));
        RectTransform healthBarRect = enemyMonsterHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        healthBarRect.sizeDelta = Vector2.zero;
        
        // Add intent indicator
        GameObject intentObj = new GameObject("IntentIndicator");
        intentObj.transform.SetParent(enemyMonsterPanel.transform, false);
        RectTransform intentRect = intentObj.AddComponent<RectTransform>();
        intentRect.sizeDelta = new Vector2(0, 40);
        
        Image intentIcon = intentObj.AddComponent<Image>();
        intentIcon.color = new Color(0.8f, 0.3f, 0.3f);
        
        TextMeshProUGUI intentText = CreateText(intentObj, "IntentText", "Intent: Attack");
        intentText.fontSize = 16;
    }
    
    private void SetupPlayerMonsterPanel()
    {
        VerticalLayoutGroup layout = playerMonsterPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Add title
        TextMeshProUGUI titleText = CreateText(playerMonsterPanel, "TitleText", "YOUR PET");
        titleText.fontSize = 16;
        titleText.color = new Color(0.7f, 1f, 0.7f);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 20);
        
        // Pet name
        playerMonsterNameText = CreateText(playerMonsterPanel, "PetNameText", "Unknown");
        playerMonsterNameText.fontSize = 18;
        RectTransform nameRect = playerMonsterNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 25);
        
        // Pet health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(playerMonsterPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        playerMonsterHealthText = CreateText(healthContainer, "HealthText", "HP: 0/0");
        playerMonsterHealthText.fontSize = 18;
        RectTransform healthTextRect = playerMonsterHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        healthTextRect.sizeDelta = Vector2.zero;
        
        playerMonsterHealthBar = CreateProgressBar(healthContainer, "HealthBar", new Color(0.2f, 0.7f, 0.3f));
        RectTransform healthBarRect = playerMonsterHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        healthBarRect.sizeDelta = Vector2.zero;
        
        // Add status indicators
        GameObject statusObj = new GameObject("StatusIndicators");
        statusObj.transform.SetParent(playerMonsterPanel.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(0, 30);
        
        TextMeshProUGUI statusText = CreateText(statusObj, "StatusText", "Combat Status");
        statusText.fontSize = 16;
    }
    
    private void CreateBattleOverviewUI()
    {
        // Create battle overview panel
        battleOverviewPanel = new GameObject("BattleOverviewPanel");
        battleOverviewPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform panelRect = battleOverviewPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.7f, 0.6f);
        panelRect.anchorMax = new Vector2(0.98f, 0.95f);
        panelRect.sizeDelta = Vector2.zero;
        
        Image panelImage = battleOverviewPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);
        
        // Add title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(battleOverviewPanel.transform, false);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Active Battles";
        titleText.fontSize = 20;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.9f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = Vector2.zero;
        
        // Create scroll view for battle cards
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(battleOverviewPanel.transform, false);
        
        RectTransform scrollRect = scrollViewObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 0.9f);
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
        viewportImage.color = new Color(0, 0, 0, 0.5f);
        
        Mask viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Create content container
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        
        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Configure scroll view
        scrollView.content = contentRect;
        scrollView.viewport = viewportRect;
        scrollView.horizontal = false;
        scrollView.vertical = true;
        
        // Initially hide the panel until battle phase
        battleOverviewPanel.SetActive(false);
    }
    
    private void CreateCardHandUI()
    {
        // Create hand panel for cards
        cardHandPanel = new GameObject("CardHandPanel");
        cardHandPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform handPanelRect = cardHandPanel.AddComponent<RectTransform>();
        handPanelRect.anchorMin = new Vector2(0.2f, 0.01f);
        handPanelRect.anchorMax = new Vector2(0.8f, 0.25f);
        handPanelRect.sizeDelta = Vector2.zero;
        
        // Add background image
        Image bgImage = cardHandPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);
        
        // Create a container for the cards with a horizontal layout
        GameObject cardsContainer = new GameObject("CardsContainer");
        cardsContainer.transform.SetParent(cardHandPanel.transform, false);
        
        RectTransform containerRect = cardsContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, -10);
        
        // Add horizontal layout that doesn't force expand width
        HorizontalLayoutGroup handLayout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
        handLayout.spacing = 10;
        handLayout.childAlignment = TextAnchor.MiddleCenter;
        handLayout.childForceExpandWidth = false;
        handLayout.childForceExpandHeight = true;
        handLayout.childControlWidth = false;
        handLayout.childControlHeight = true;
        
        // Add content size fitter to make container adjust to content
        ContentSizeFitter contentFitter = cardsContainer.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Initially hide the card hand panel
        cardHandPanel.SetActive(false);
    }
    
    private void CreateDropZones()
    {
        // Create enemy pet drop zone (upper right quadrant)
        enemyDropZone = new GameObject("EnemyDropZone");
        enemyDropZone.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform enemyRect = enemyDropZone.AddComponent<RectTransform>();
        enemyRect.anchorMin = new Vector2(0.75f, 0.75f);
        enemyRect.anchorMax = new Vector2(0.99f, 0.99f);
        enemyRect.sizeDelta = Vector2.zero;
        
        Image enemyZoneImage = enemyDropZone.AddComponent<Image>();
        enemyZoneImage.color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        // Make it raycast target to detect drops
        enemyZoneImage.raycastTarget = true;
        
        // Add outline to make it more visible
        Outline enemyOutline = enemyDropZone.AddComponent<Outline>();
        enemyOutline.effectColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        enemyOutline.effectDistance = new Vector2(3, 3);
        
        // Add drop handler
        DropZoneHandler enemyDropHandler = enemyDropZone.AddComponent<DropZoneHandler>();
        enemyDropHandler.zoneType = DropZoneType.Enemy;
        
        // Add text that's more visible
        GameObject enemyTextObj = new GameObject("DropText");
        enemyTextObj.transform.SetParent(enemyDropZone.transform, false);
        
        TextMeshProUGUI enemyDropText = enemyTextObj.AddComponent<TextMeshProUGUI>();
        enemyDropText.text = "DROP TO ATTACK\nENEMY PET";
        enemyDropText.fontSize = 20;
        enemyDropText.alignment = TextAlignmentOptions.Center;
        enemyDropText.color = new Color(1f, 0.8f, 0.8f, 1f);
        enemyDropText.fontStyle = FontStyles.Bold;
        
        RectTransform enemyTextRect = enemyTextObj.GetComponent<RectTransform>();
        enemyTextRect.anchorMin = Vector2.zero;
        enemyTextRect.anchorMax = Vector2.one;
        enemyTextRect.sizeDelta = Vector2.zero;
        
        // Create pet drop zone (upper middle quadrant)
        petDropZone = new GameObject("PetDropZone");
        petDropZone.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform petRect = petDropZone.AddComponent<RectTransform>();
        petRect.anchorMin = new Vector2(0.38f, 0.75f);
        petRect.anchorMax = new Vector2(0.62f, 0.99f);
        petRect.sizeDelta = Vector2.zero;
        
        Image petZoneImage = petDropZone.AddComponent<Image>();
        petZoneImage.color = new Color(0.2f, 0.6f, 0.8f, 0.3f);
        // Make it raycast target to detect drops
        petZoneImage.raycastTarget = true;
        
        // Add outline to make it more visible
        Outline petOutline = petDropZone.AddComponent<Outline>();
        petOutline.effectColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        petOutline.effectDistance = new Vector2(3, 3);
        
        // Add drop handler
        DropZoneHandler petDropHandler = petDropZone.AddComponent<DropZoneHandler>();
        petDropHandler.zoneType = DropZoneType.Pet;
        
        // Add text that's more visible
        GameObject petTextObj = new GameObject("DropText");
        petTextObj.transform.SetParent(petDropZone.transform, false);
        
        TextMeshProUGUI petDropText = petTextObj.AddComponent<TextMeshProUGUI>();
        petDropText.text = "DROP TO SUPPORT\nYOUR PET";
        petDropText.fontSize = 20;
        petDropText.alignment = TextAlignmentOptions.Center;
        petDropText.color = new Color(0.8f, 1f, 1f, 1f);
        petDropText.fontStyle = FontStyles.Bold;
        
        RectTransform petTextRect = petTextObj.GetComponent<RectTransform>();
        petTextRect.anchorMin = Vector2.zero;
        petTextRect.anchorMax = Vector2.one;
        petTextRect.sizeDelta = Vector2.zero;
        
        // Initially hide drop zones
        enemyDropZone.SetActive(false);
        petDropZone.SetActive(false);
    }
    
    private void CreateFeedbackTextPrefab()
    {
        // Create a simple text object that can be instantiated for feedback
        feedbackTextPrefab = new GameObject("FeedbackTextPrefab");
        
        RectTransform rectTransform = feedbackTextPrefab.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        TextMeshProUGUI text = feedbackTextPrefab.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        
        // Add animation component
        FeedbackTextAnimation animation = feedbackTextPrefab.AddComponent<FeedbackTextAnimation>();
        
        // Deactivate the prefab
        feedbackTextPrefab.SetActive(false);
    }
    
    public void ShowGameplayUI()
    {
        Debug.Log("Showing Gameplay UI");
        
        // Make sure to hide all other panels first
        HideAllPanels();
        
        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Gameplay panel is null in ShowGameplayUI");
        }
        
        // Show battle UI components
        ShowBattleUI();
    }
    
    public void ShowBattleUI()
    {
        if (battlePanel != null)
        {
            battlePanel.SetActive(true);
        }
        
        if (cardHandPanel != null)
        {
            cardHandPanel.SetActive(true);
        }
        
        // Update localPlayer reference
        FindLocalPlayer();
        
        // Update all stats if local player is found
        UpdateAllStats();
    }
    
    public void HideBattleUI()
    {
        if (battlePanel != null)
        {
            battlePanel.SetActive(false);
        }
        
        if (cardHandPanel != null)
        {
            cardHandPanel.SetActive(false);
        }
        
        if (battleOverviewPanel != null)
        {
            battleOverviewPanel.SetActive(false);
        }
        
        if (enemyDropZone != null)
        {
            enemyDropZone.SetActive(false);
        }
        
        if (petDropZone != null)
        {
            petDropZone.SetActive(false);
        }
    }
    
    private void FindLocalPlayer()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (player.photonView.IsMine)
            {
                localPlayer = player;
                return;
            }
        }
    }
    
    public void UpdatePlayerStats(PlayerController player)
    {
        if (player == null) return;
        
        if (playerNameText != null)
            playerNameText.text = player.PlayerName;
            
        if (playerHealthText != null)
            playerHealthText.text = $"HP: {player.CurrentHealth}/{player.MaxHealth}";
            
        if (playerHealthBar != null)
        {
            playerHealthBar.maxValue = player.MaxHealth;
            playerHealthBar.value = player.CurrentHealth;
        }
        
        if (playerEnergyText != null)
            playerEnergyText.text = $"Energy: {player.CurrentEnergy}/{player.MaxEnergy}";
            
        if (playerBlockText != null)
            playerBlockText.text = $"Block: {player.Block}";
    }
    
    public void UpdateMonsterStats(MonsterController monster)
    {
        if (monster == null) return;
        
        enemyMonster = monster;
        
        if (enemyMonsterNameText != null)
            enemyMonsterNameText.text = monster.MonsterName;
            
        if (enemyMonsterHealthText != null)
            enemyMonsterHealthText.text = $"HP: {monster.CurrentHealth}/{monster.MaxHealth}";
            
        if (enemyMonsterHealthBar != null)
        {
            enemyMonsterHealthBar.maxValue = monster.MaxHealth;
            enemyMonsterHealthBar.value = monster.CurrentHealth;
        }
    }
    
    public void UpdatePetStats(MonsterController monster)
    {
        if (monster == null) return;
        
        playerMonster = monster;
        
        if (playerMonsterNameText != null)
            playerMonsterNameText.text = monster.MonsterName;
            
        if (playerMonsterHealthText != null)
            playerMonsterHealthText.text = $"HP: {monster.CurrentHealth}/{monster.MaxHealth}";
            
        if (playerMonsterHealthBar != null)
        {
            playerMonsterHealthBar.maxValue = monster.MaxHealth;
            playerMonsterHealthBar.value = monster.CurrentHealth;
        }
    }
    
    public void ShowBattleOverview(Dictionary<int, int> battlePairings)
    {
        // Clear existing battle cards
        ClearBattleCards();
        
        // Get content container
        Transform contentTransform = battleOverviewPanel.transform.Find("ScrollView/Viewport/Content");
        if (contentTransform == null) return;
        
        // Create battle cards for each pairing
        foreach (var pairing in battlePairings)
        {
            int playerActorNumber = pairing.Key;
            int monsterOwnerActorNumber = pairing.Value;
            
            // Only create cards if we have references to both the player and monster
            if (gameplayManager != null)
            {
                PlayerController player = gameplayManager.GetPlayerById(playerActorNumber);
                MonsterController monster = gameplayManager.GetMonsterById(monsterOwnerActorNumber);
                
                if (player != null && monster != null)
                {
                    // Create battle card
                    CreateBattleCard(contentTransform, playerActorNumber, monsterOwnerActorNumber, player, monster);
                }
            }
        }
        
        // Show the panel
        battleOverviewPanel.SetActive(true);
    }
    
    private void CreateBattleCard(Transform parent, int playerActorNumber, int monsterOwnerActorNumber, 
                               PlayerController player, MonsterController monster)
    {
        // Create battle card container
        GameObject cardObj = new GameObject($"BattleCard_{playerActorNumber}_vs_{monsterOwnerActorNumber}");
        cardObj.transform.SetParent(parent, false);
        
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(0, 80);
        
        Image cardImage = cardObj.AddComponent<Image>();
        cardImage.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        
        HorizontalLayoutGroup cardLayout = cardObj.AddComponent<HorizontalLayoutGroup>();
        cardLayout.spacing = 5;
        cardLayout.padding = new RectOffset(5, 5, 5, 5);
        cardLayout.childAlignment = TextAnchor.MiddleCenter;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = true;
        
        // Create player info
        GameObject playerInfoObj = new GameObject("PlayerInfo");
        playerInfoObj.transform.SetParent(cardObj.transform, false);
        
        VerticalLayoutGroup playerLayout = playerInfoObj.AddComponent<VerticalLayoutGroup>();
        playerLayout.spacing = 2;
        playerLayout.childAlignment = TextAnchor.MiddleLeft;
        
        // Player name
        GameObject playerNameObj = new GameObject("PlayerName");
        playerNameObj.transform.SetParent(playerInfoObj.transform, false);
        
        TextMeshProUGUI playerNameText = playerNameObj.AddComponent<TextMeshProUGUI>();
        playerNameText.text = player.PlayerName;
        playerNameText.fontSize = 16;
        playerNameText.color = Color.white;
        
        RectTransform playerNameRect = playerNameObj.GetComponent<RectTransform>();
        playerNameRect.sizeDelta = new Vector2(0, 20);
        
        // Player health
        GameObject playerHealthObj = new GameObject("PlayerHealth");
        playerHealthObj.transform.SetParent(playerInfoObj.transform, false);
        
        TextMeshProUGUI playerHealthText = playerHealthObj.AddComponent<TextMeshProUGUI>();
        playerHealthText.text = $"HP: {player.CurrentHealth}/{player.MaxHealth}";
        playerHealthText.fontSize = 14;
        playerHealthText.color = Color.white;
        
        RectTransform playerHealthRect = playerHealthObj.GetComponent<RectTransform>();
        playerHealthRect.sizeDelta = new Vector2(0, 18);
        
        // VS text
        GameObject vsObj = new GameObject("VS");
        vsObj.transform.SetParent(cardObj.transform, false);
        
        TextMeshProUGUI vsText = vsObj.AddComponent<TextMeshProUGUI>();
        vsText.text = "VS";
        vsText.fontSize = 18;
        vsText.alignment = TextAlignmentOptions.Center;
        vsText.color = Color.yellow;
        
        // Monster info
        GameObject monsterInfoObj = new GameObject("MonsterInfo");
        monsterInfoObj.transform.SetParent(cardObj.transform, false);
        
        VerticalLayoutGroup monsterLayout = monsterInfoObj.AddComponent<VerticalLayoutGroup>();
        monsterLayout.spacing = 2;
        monsterLayout.childAlignment = TextAnchor.MiddleRight;
        
        // Monster name
        GameObject monsterNameObj = new GameObject("MonsterName");
        monsterNameObj.transform.SetParent(monsterInfoObj.transform, false);
        
        TextMeshProUGUI monsterNameText = monsterNameObj.AddComponent<TextMeshProUGUI>();
        monsterNameText.text = monster.MonsterName;
        monsterNameText.fontSize = 16;
        monsterNameText.alignment = TextAlignmentOptions.Right;
        monsterNameText.color = Color.white;
        
        RectTransform monsterNameRect = monsterNameObj.GetComponent<RectTransform>();
        monsterNameRect.sizeDelta = new Vector2(0, 20);
        
        // Monster health
        GameObject monsterHealthObj = new GameObject("MonsterHealth");
        monsterHealthObj.transform.SetParent(monsterInfoObj.transform, false);
        
        TextMeshProUGUI monsterHealthText = monsterHealthObj.AddComponent<TextMeshProUGUI>();
        monsterHealthText.text = $"HP: {monster.CurrentHealth}/{monster.MaxHealth}";
        monsterHealthText.fontSize = 14;
        monsterHealthText.alignment = TextAlignmentOptions.Right;
        monsterHealthText.color = Color.white;
        
        RectTransform monsterHealthRect = monsterHealthObj.GetComponent<RectTransform>();
        monsterHealthRect.sizeDelta = new Vector2(0, 18);
    }
    
    private void ClearBattleCards()
    {
        Transform contentTransform = battleOverviewPanel.transform.Find("ScrollView/Viewport/Content");
        if (contentTransform == null) return;
        
        // Destroy all battle cards
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }
    }
    
    public void UpdateBattleCard(int actorNumber, bool isPlayer, int currentHealth, int maxHealth)
    {
        // Find and update the appropriate battle card
        Transform contentTransform = battleOverviewPanel.transform.Find("ScrollView/Viewport/Content");
        if (contentTransform == null) return;
        
        foreach (Transform child in contentTransform)
        {
            // Find cards that contain this actor
            string cardName = child.name;
            if ((isPlayer && cardName.Contains($"BattleCard_{actorNumber}_vs_")) ||
                (!isPlayer && cardName.Contains($"_vs_{actorNumber}")))
            {
                // Find the health text component
                Transform infoTransform = isPlayer ? child.Find("PlayerInfo/PlayerHealth") : child.Find("MonsterInfo/MonsterHealth");
                if (infoTransform != null)
                {
                    TextMeshProUGUI healthText = infoTransform.GetComponent<TextMeshProUGUI>();
                    if (healthText != null)
                    {
                        healthText.text = $"HP: {currentHealth}/{maxHealth}";
                    }
                }
            }
        }
    }
    
    public void HideBattleOverview()
    {
        if (battleOverviewPanel != null)
        {
            battleOverviewPanel.SetActive(false);
        }
    }
    
    public void ShowPlayerTurn(bool isPlayerTurn)
    {
        if (turnText != null)
        {
           turnText.text = isPlayerTurn ? "YOUR TURN" : "OPPONENT'S TURN";
            turnText.color = isPlayerTurn ? Color.green : Color.yellow;
        }
        
        if (endTurnButton != null)
        {
            endTurnButton.SetActive(isPlayerTurn);
        }
    }
    
    private void EndTurnClicked()
    {
        if (localPlayer != null)
        {
            localPlayer.EndTurn();
        }
    }
    
    private void UpdateAllStats()
    {
        if (localPlayer != null)
        {
            UpdatePlayerStats(localPlayer);
        }
        
        if (enemyMonster != null)
        {
            UpdateMonsterStats(enemyMonster);
        }
        
        if (playerMonster != null)
        {
            UpdatePetStats(playerMonster);
        }
    }
    
    #endregion

    #region Card UI Methods
    
    public void AddCardToHand(Card card)
    {
        // Find the cards container
        Transform cardsContainer = cardHandPanel?.transform.Find("CardsContainer");
        if (cardsContainer == null)
        {
            Debug.LogError("Cards container not found in hand panel!");
            return;
        }
        
        // Create card object
        GameObject cardObj = CreateCardObject(card);
        cardObj.transform.SetParent(cardsContainer, false);
        
        // Add to list
        cardObjects.Add(cardObj);
        
        // Add drag component
        CardDragHandler dragHandler = cardObj.AddComponent<CardDragHandler>();
        dragHandler.Initialize(this, cardObjects.Count - 1, card);
        
        Debug.Log("Added card to hand: " + card.CardName);
    }
    
    public void RemoveCardFromHand(int index)
    {
        if (index < 0 || index >= cardObjects.Count)
        {
            Debug.LogError("Invalid card index: " + index);
            return;
        }
        
        // Destroy card object
        Destroy(cardObjects[index]);
        cardObjects.RemoveAt(index);
        
        // Update indices for remaining drag handlers
        for (int i = 0; i < cardObjects.Count; i++)
        {
            CardDragHandler dragHandler = cardObjects[i].GetComponent<CardDragHandler>();
            if (dragHandler != null)
            {
                dragHandler.CardIndex = i;
            }
        }
        
        Debug.Log("Removed card from hand at index: " + index);
    }
    
    public void ClearHand()
    {
        foreach (GameObject cardObj in cardObjects)
        {
            Destroy(cardObj);
        }
        cardObjects.Clear();
        
        Debug.Log("Cleared all cards from hand");
    }
    
    private GameObject CreateCardObject(Card card)
    {
        // Create card game object
        GameObject cardObj = new GameObject("Card_" + card.CardName);
        
        // Add rect transform with fixed size
        RectTransform rectTransform = cardObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(120, 180);
        
        // Add layout element to prevent squishing
        LayoutElement layoutElement = cardObj.AddComponent<LayoutElement>();
        layoutElement.minWidth = 120;
        layoutElement.preferredWidth = 120;
        layoutElement.flexibleWidth = 0; // Don't allow width to be flexible
        
        // Add background with rounded corners if possible
        Image cardImage = cardObj.AddComponent<Image>();
        cardImage.color = GetCardTypeColor(card.CardType);
        // Enable raycasting on the card
        cardImage.raycastTarget = true;
        
        // Try to create a rounded rectangle sprite
        cardImage.sprite = CreateRoundedRectSprite(10);
        cardImage.type = Image.Type.Sliced;
        
        // Create card content container
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(cardObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(5, 5);
        contentRect.offsetMax = new Vector2(-5, -5);
        
        // Create layout group for content
        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
        contentLayout.padding = new RectOffset(5, 5, 5, 5);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        // Create card name text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(contentObj.transform, false);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = card.CardName;
        nameText.fontSize = 14;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 20);
        
        // Create energy cost with background
        GameObject costObj = new GameObject("CostCircle");
        costObj.transform.SetParent(cardObj.transform, false);
        
        RectTransform costRect = costObj.AddComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0, 1);
        costRect.anchorMax = new Vector2(0, 1);
        costRect.pivot = new Vector2(0, 1);
        costRect.anchoredPosition = new Vector2(5, -5);
        costRect.sizeDelta = new Vector2(25, 25);
        
        Image costBg = costObj.AddComponent<Image>();
        costBg.color = new Color(0.2f, 0.2f, 0.6f, 1);
        
        // Try to create a circle sprite
        costBg.sprite = CreateCircleSprite();
        
        // Create cost text
        GameObject costTextObj = new GameObject("CostText");
        costTextObj.transform.SetParent(costObj.transform, false);
        
        TextMeshProUGUI costText = costTextObj.AddComponent<TextMeshProUGUI>();
        costText.text = card.EnergyCost.ToString();
        costText.fontSize = 16;
        costText.alignment = TextAlignmentOptions.Center;
        costText.color = Color.white;
        
        RectTransform costTextRect = costTextObj.GetComponent<RectTransform>();
        costTextRect.anchorMin = Vector2.zero;
        costTextRect.anchorMax = Vector2.one;
        costTextRect.sizeDelta = Vector2.zero;
        
        // Create description text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(contentObj.transform, false);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = card.CardDescription;
        descText.fontSize = 12;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(0, 70);
        
        return cardObj;
    }
    
    private Color GetCardTypeColor(CardType type)
    {
        switch (type)
        {
            case CardType.Attack:
                return new Color(0.8f, 0.2f, 0.2f, 1); // Red
            case CardType.Skill:
                return new Color(0.2f, 0.6f, 0.8f, 1); // Blue
            case CardType.Power:
                return new Color(0.8f, 0.4f, 0.8f, 1); // Purple
            default:
                return new Color(0.5f, 0.5f, 0.5f, 1); // Gray
        }
    }
    
    // Handle card dragging started
    public void OnCardDragBegin(int cardIndex, Vector3 position)
    {
        draggedCardIndex = cardIndex;
        dragStartPosition = position;
        
        // Show drop zones
        ShowDropZones(true);
        
        // Scale up the card being dragged
        if (cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            cardObjects[cardIndex].transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            
            // Bring to front by setting as last child
            cardObjects[cardIndex].transform.SetAsLastSibling();
            
            // Log drag begin
            Debug.Log("Started dragging card #" + cardIndex);
        }
    }
    
    // Handle card dragging
    public void OnCardDrag(int cardIndex, Vector3 position)
    {
        if (cardIndex != draggedCardIndex) return;
        
        // Move the card
        if (cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            cardObjects[cardIndex].transform.position = position;
            
            // Highlight drop zones based on position
            HighlightDropZones(position);
        }
    }
    
    // Handle card drag end
    public void OnCardDragEnd(int cardIndex, Vector3 position)
    {
        if (cardIndex != draggedCardIndex) return;
        
        Debug.Log("Card drag ended at position: " + position);
        
        // Hide drop zones
        ShowDropZones(false);
        
        // Reset card scale
        if (cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            cardObjects[cardIndex].transform.localScale = Vector3.one;
        }
        
        // Check if a drop zone was hit using the drop zone handlers
        bool cardPlayed = false;
        DropZoneHandler hitHandler = null;
        
        // Check all active drop zones for hits
        if (enemyDropZone != null && enemyDropZone.activeInHierarchy)
        {
            DropZoneHandler enemyHandler = enemyDropZone.GetComponent<DropZoneHandler>();
            if (enemyHandler != null && enemyHandler.IsBeingHovered)
            {
                hitHandler = enemyHandler;
                Debug.Log("Enemy drop zone hit detected via handler!");
            }
        }
        
        if (hitHandler == null && petDropZone != null && petDropZone.activeInHierarchy)
        {
            DropZoneHandler petHandler = petDropZone.GetComponent<DropZoneHandler>();
            if (petHandler != null && petHandler.IsBeingHovered)
            {
                hitHandler = petHandler;
                Debug.Log("Pet drop zone hit detected via handler!");
            }
        }
        
        Debug.Log("Drop check - Over enemy: " + (hitHandler != null && hitHandler.zoneType == DropZoneType.Enemy) + 
                 ", Over pet: " + (hitHandler != null && hitHandler.zoneType == DropZoneType.Pet));
        
        // Make sure we have a local player to play cards with
        if (localPlayer == null)
        {
            FindLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogError("Cannot find local player to play card!");
                // Return card to hand
                StartCoroutine(AnimateCardReturn(cardIndex));
                draggedCardIndex = -1;
                return;
            }
        }
        
        // Debug the turn status
        Debug.Log("Turn status check - Player: " + localPlayer.PlayerName + 
                 ", IsMyTurn: " + localPlayer.IsMyTurn + 
                 ", IsInCombat: " + localPlayer.IsInCombat);
        
        // Process the drop if a handler was hit
        if (hitHandler != null)
        {
            // Get the card data for better feedback
            Card cardData = null;
            CardDragHandler dragHandler = cardObjects[cardIndex].GetComponent<CardDragHandler>();
            if (dragHandler != null)
            {
                cardData = dragHandler.CardData;
            }
            string cardName = cardData != null ? cardData.CardName : "Card";
            
            // Check if it's our turn
            if (!localPlayer.IsMyTurn)
            {
                Debug.LogWarning("Cannot play card - not your turn!");
                ShowFeedbackText("Not your turn!", position, Color.red);
                StartCoroutine(AnimateCardReturn(cardIndex));
                draggedCardIndex = -1;
                return;
            }
            
            // Check energy cost
            if (cardData != null && localPlayer.CurrentEnergy < cardData.EnergyCost)
            {
                Debug.LogWarning("Not enough energy to play card!");
                ShowFeedbackText("Not enough energy!", position, Color.red);
                StartCoroutine(AnimateCardReturn(cardIndex));
                draggedCardIndex = -1;
                return;
            }
            
            // Process based on zone type
            if (hitHandler.zoneType == DropZoneType.Enemy)
            {
                // Play card on enemy
                localPlayer.PlayCard(cardIndex, -1);
                ShowFeedbackText(cardName + " ➜ Enemy", position, Color.red);
                cardPlayed = true;
                Debug.Log("Card played on enemy successfully!");
            }
            else if (hitHandler.zoneType == DropZoneType.Pet)
            {
                // Make sure player has a pet
                if (localPlayer.PetMonster == null)
                {
                    Debug.LogWarning("Cannot play card on pet - No pet assigned!");
                    ShowFeedbackText("No pet available!", position, Color.red);
                    StartCoroutine(AnimateCardReturn(cardIndex));
                    draggedCardIndex = -1;
                    return;
                }
                
                // Play card on pet
                localPlayer.PlayCardOnPet(cardIndex);
                ShowFeedbackText(cardName + " ➜ Pet", position, Color.green);
                cardPlayed = true;
                Debug.Log("Card played on pet successfully!");
            }
        }
        
        // If card wasn't played, return to original position
        if (!cardPlayed && cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            Debug.Log("Card not played, returning to hand");
            StartCoroutine(AnimateCardReturn(cardIndex));
        }
        
        draggedCardIndex = -1;
    }
    
    // Handle zone hit directly from drop zone
    public void OnZoneHit(DropZoneType zoneType, int cardIndex, Vector3 position)
    {
        Debug.Log("Zone hit received: " + zoneType + " for card " + cardIndex);
        
        if (cardIndex < 0 || cardIndex >= cardObjects.Count) return;
        
        // Get the card data for better feedback
        Card cardData = null;
        CardDragHandler dragHandler = cardObjects[cardIndex].GetComponent<CardDragHandler>();
        if (dragHandler != null)
        {
            cardData = dragHandler.CardData;
        }
        string cardName = cardData != null ? cardData.CardName : "Card";
        
        // Make sure we have a local player to play cards with
        if (localPlayer == null)
        {
            FindLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogError("Cannot find local player to play card!");
                return;
            }
        }
        
        // Check if it's our turn
        if (!localPlayer.IsMyTurn)
        {
            Debug.LogWarning("Cannot play card - not your turn!");
            ShowFeedbackText("Not your turn!", position, Color.red);
            return;
        }
        
        // Check energy cost
        if (cardData != null && localPlayer.CurrentEnergy < cardData.EnergyCost)
        {
            Debug.LogWarning("Not enough energy to play card!");
            ShowFeedbackText("Not enough energy!", position, Color.red);
            return;
        }
        
        // Process based on zone type
        if (zoneType == DropZoneType.Enemy)
        {
            // Play card on enemy
            localPlayer.PlayCard(cardIndex, -1);
            ShowFeedbackText(cardName + " ➜ Enemy", position, Color.red);
            Debug.Log("Card played on enemy successfully!");
        }
        else if (zoneType == DropZoneType.Pet)
        {
            // Make sure player has a pet
            if (localPlayer.PetMonster == null)
            {
                Debug.LogWarning("Cannot play card on pet - No pet assigned!");
                ShowFeedbackText("No pet available!", position, Color.red);
                return;
            }
            
            // Play card on pet
            localPlayer.PlayCardOnPet(cardIndex);
            ShowFeedbackText(cardName + " ➜ Pet", position, Color.green);
            Debug.Log("Card played on pet successfully!");
        }
    }
    
    private void ShowFeedbackText(string message, Vector3 position, Color color)
    {
        if (feedbackTextPrefab == null || mainCanvas == null)
            return;
        
        // Instantiate feedback text
        GameObject feedbackObj = Instantiate(feedbackTextPrefab, mainCanvas.transform);
        feedbackObj.SetActive(true);
        
        // Set position
        feedbackObj.transform.position = position;
        
        // Set text
        TextMeshProUGUI text = feedbackObj.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.color = color;
        
        // Configure animation
        FeedbackTextAnimation anim = feedbackObj.GetComponent<FeedbackTextAnimation>();
        anim.Initialize();
        
        Debug.Log("Showing feedback: " + message);
    }
    
    private IEnumerator AnimateCardReturn(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= cardObjects.Count) yield break;
        
        GameObject card = cardObjects[cardIndex];
        Vector3 startPos = card.transform.position;
        float duration = 0.2f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(startPos, dragStartPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        card.transform.position = dragStartPosition;
    }
    
    private void ShowDropZones(bool show)
    {
        if (enemyDropZone != null && petDropZone != null)
        {
            enemyDropZone.SetActive(show);
            petDropZone.SetActive(show);
            
            // Reset colors
            if (show)
            {
                enemyDropZone.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
                petDropZone.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.8f, 0.3f);
                
                // Reset hover flags
                if (enemyDropZone.GetComponent<DropZoneHandler>() != null)
                    enemyDropZone.GetComponent<DropZoneHandler>().IsBeingHovered = false;
                
                if (petDropZone.GetComponent<DropZoneHandler>() != null)
                    petDropZone.GetComponent<DropZoneHandler>().IsBeingHovered = false;
            }
        }
    }
    
    private void HighlightDropZones(Vector3 position)
    {
        // Highlight enemy drop zone if hovering over it
        if (enemyDropZone != null)
        {
            DropZoneHandler enemyHandler = enemyDropZone.GetComponent<DropZoneHandler>();
            if (enemyHandler != null && enemyHandler.IsBeingHovered)
            {
                enemyDropZone.GetComponent<Image>().color = new Color(1f, 0.3f, 0.3f, 0.5f);
            }
            else
            {
                enemyDropZone.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
            }
        }
        
        // Highlight pet drop zone if hovering over it
        if (petDropZone != null)
        {
            DropZoneHandler petHandler = petDropZone.GetComponent<DropZoneHandler>();
            if (petHandler != null && petHandler.IsBeingHovered)
            {
                petDropZone.GetComponent<Image>().color = new Color(0.3f, 0.8f, 1f, 0.5f);
            }
            else
            {
                petDropZone.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.8f, 0.3f);
            }
        }
    }
    
    #endregion

    #region Draft UI
    
    private void InitializeDraftUI()
    {
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
    
    public void ShowPhaseAnnouncement(string text)
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
    
    public void ShowDraftUI(List<Upgrade> upgrades)
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
    
    public void PlayerSelectedUpgrade(int upgradeIndex)
    {
        // Notify the gameplay manager
        if (gameplayManager != null && localPlayer != null)
        {
            // Hide draft UI
            HideDraftUI();
            
            // Use the RPC call from the gameplay manager
            gameplayManager.photonView.RPC("RPC_PlayerMadeDraftSelection", RpcTarget.MasterClient, 
                                        localPlayer.photonView.Owner.ActorNumber, upgradeIndex);
        }
    }
    
    public void HideDraftUI()
    {
        if (draftPanel != null)
        {
            draftPanel.SetActive(false);
        }
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
    
    public void ShowGameOver(string winnerName)
    {
        // Display winner text
        if (winnerText != null)
        {
            winnerText.text = "Winner: " + winnerName;
        }
        
        // Show game over panel
        if (gameOverPanel != null)
        {
            HideAllPanels();
            gameOverPanel.SetActive(true);
        }
    }
    
    #endregion

    #region Utility Methods
    
    public GameObject GetLobbyPanel()
    {
        return lobbyPanel;
    }
    
    public GameObject GetGameplayPanel()
    {
        return gameplayPanel;
    }

    public GameObject GetMainCanvas()
    {
        return mainCanvas?.gameObject;
    }
    
    #endregion
}

// DropZoneHandler class for card target handling
public class DropZoneHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public DropZoneType zoneType;
    public bool IsBeingHovered { get; set; }
    
    private UIManager uiManager;
    
    private void Start()
    {
        uiManager = FindObjectOfType<UIManager>();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        IsBeingHovered = true;
        Debug.Log("Pointer entered " + zoneType + " drop zone");
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        IsBeingHovered = false;
        Debug.Log("Pointer exited " + zoneType + " drop zone");
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Drop detected on " + zoneType + " drop zone");
        
        // Try to get the card drag handler from the dropped object
        CardDragHandler dragHandler = eventData.pointerDrag?.GetComponent<CardDragHandler>();
        if (dragHandler != null && uiManager != null)
        {
            // Notify the UI manager directly that we had a hit
            uiManager.OnZoneHit(zoneType, dragHandler.CardIndex, transform.position);
        }
    }
}

// Enumeration for drop zone types
public enum DropZoneType
{
    Enemy,
    Pet
}

// Class to handle card dragging
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UIManager uiManager;
    public int CardIndex { get; set; }
    public Card CardData { get; private set; }
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    public void Initialize(UIManager manager, int index, Card cardData)
    {
        uiManager = manager;
        CardIndex = index;
        CardData = cardData;
        rectTransform = GetComponent<RectTransform>();
        
        // Add canvas group to control interaction during dragging
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Make slightly transparent during drag
        canvasGroup.alpha = 0.8f;
        
        // Set this to false to allow raycasts to hit objects underneath the card
        canvasGroup.blocksRaycasts = false;
        
        if (uiManager != null)
        {
            // Store the current position for returning if needed
            uiManager.OnCardDragBegin(CardIndex, transform.position);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        // Move the card with the mouse
        rectTransform.position = eventData.position;
        
        if (uiManager != null)
        {
            uiManager.OnCardDrag(CardIndex, eventData.position);
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore transparency and raycast blocking
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        if (uiManager != null)
        {
            uiManager.OnCardDragEnd(CardIndex, eventData.position);
        }
    }
}

// Class to animate feedback text
public class FeedbackTextAnimation : MonoBehaviour
{
    private float fadeSpeed = 1.5f;
    private float moveSpeed = 50f;
    private float lifetime = 1.5f;
    private float timer = 0f;
    private TextMeshProUGUI text;
    
    public void Initialize()
    {
        text = GetComponent<TextMeshProUGUI>();
        
        // Start auto-destroy coroutine
        StartCoroutine(DestroyAfterTime());
    }
    
    private void Update()
    {
        if (text == null) return;
        
        // Move upward
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        
        // Fade out
        timer += Time.deltaTime;
        float alpha = Mathf.Lerp(1f, 0f, timer / lifetime);
        Color newColor = text.color;
        newColor.a = alpha;
        text.color = newColor;
    }
    
    private IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
}