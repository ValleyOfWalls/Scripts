using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class UIManager : MonoBehaviour
{
    // UI Panels
    private GameObject connectingPanel;
    private GameObject mainMenuPanel;
    private GameObject lobbyPanel;
    private GameObject errorPanel;
    private GameObject gameplayPanel;

    // UI References
    private TextMeshProUGUI connectingText;
    private TextMeshProUGUI errorText;
    private Button errorCloseButton;
    private TMP_InputField nameInput;
    private TMP_InputField roomInput;

    // Canvas and CanvasScaler
    private Canvas mainCanvas;
    private CanvasScaler canvasScaler;
    
    // Reference to other managers
    private NetworkManager networkManager;
    
    // Random name generation
    private string[] firstNames = { "Magic", "Brave", "Swift", "Lucky", "Shadow", "Cosmic", "Mystic", "Fierce", "Speedy", "Nimble", "Wild", "Mighty" };
    private string[] lastNames = { "Wizard", "Knight", "Dragon", "Phoenix", "Tiger", "Falcon", "Unicorn", "Warrior", "Hunter", "Sprite", "Mage", "Hero" };

    private void Start()
    {
        // Find manager references
        FindManagers();
        
        CreateCanvas();
        CreateUIElements();
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
    }

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

    private void CreateUIElements()
    {
        // Create Connecting Panel
        connectingPanel = CreatePanel("ConnectingPanel");
        connectingText = CreateText(connectingPanel, "ConnectingText", "Connecting...");
        
        // Create MainMenu Panel
        mainMenuPanel = CreatePanel("MainMenuPanel");
        CreateMainMenuUI(mainMenuPanel);
        
        // Create Lobby Panel - IMPORTANT: Create with basic structure
        lobbyPanel = CreatePanel("LobbyPanel");
        // We'll let LobbyManager fill the content, but create the basic structure here
        
        // Create Error Panel
        errorPanel = CreatePanel("ErrorPanel");
        CreateErrorUI(errorPanel);
        
        // Create Gameplay Panel
        gameplayPanel = CreatePanel("GameplayPanel");
        CreateGameplayUI(gameplayPanel);
        
        Debug.Log("UI Elements created successfully");
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

    private void CreateMainMenuUI(GameObject panel)
    {
        // Create title
        TextMeshProUGUI titleText = CreateText(panel, "TitleText", "Card Battle Game");
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.9f);
        titleRect.sizeDelta = new Vector2(600, 100);
        titleText.fontSize = 60;
        
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
        
        // Set button actions with proper null checking
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
            
            networkManager.JoinRoom(roomCode, playerName);
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
            string roomCode = roomInput != null ? roomInput.text : "asd";
            
            networkManager.CreateRoom(roomCode, playerName);
        });
    }
    
    private void CreateGameplayUI(GameObject panel)
    {
        // Create a simple "Game Started" message for now
        TextMeshProUGUI gameStartedText = CreateText(panel, "GameStartedText", "Game Started!");
        RectTransform gameStartedRect = gameStartedText.GetComponent<RectTransform>();
        gameStartedRect.anchorMin = new Vector2(0.5f, 0.8f);
        gameStartedRect.anchorMax = new Vector2(0.5f, 0.9f);
        gameStartedRect.sizeDelta = new Vector2(600, 100);
        gameStartedText.fontSize = 60;
        
        // Add a placeholder for player and monster areas
        GameObject playerArea = new GameObject("PlayerArea");
        playerArea.transform.SetParent(panel.transform, false);
        
        RectTransform playerAreaRect = playerArea.AddComponent<RectTransform>();
        playerAreaRect.anchorMin = new Vector2(0.2f, 0.4f);
        playerAreaRect.anchorMax = new Vector2(0.8f, 0.7f);
        playerAreaRect.sizeDelta = Vector2.zero;
        
        Image playerAreaImage = playerArea.AddComponent<Image>();
        playerAreaImage.color = new Color(0.2f, 0.2f, 0.4f, 0.5f);
        
        TextMeshProUGUI playerAreaText = CreateText(playerArea, "PlayerAreaText", "Player Area");
        playerAreaText.fontSize = 36;
        
        GameObject monsterArea = new GameObject("MonsterArea");
        monsterArea.transform.SetParent(panel.transform, false);
        
        RectTransform monsterAreaRect = monsterArea.AddComponent<RectTransform>();
        monsterAreaRect.anchorMin = new Vector2(0.2f, 0.1f);
        monsterAreaRect.anchorMax = new Vector2(0.8f, 0.35f);
        monsterAreaRect.sizeDelta = Vector2.zero;
        
        Image monsterAreaImage = monsterArea.AddComponent<Image>();
        monsterAreaImage.color = new Color(0.4f, 0.2f, 0.2f, 0.5f);
        
        TextMeshProUGUI monsterAreaText = CreateText(monsterArea, "MonsterAreaText", "Monster Area");
        monsterAreaText.fontSize = 36;
    }

    private string GenerateRandomName()
    {
        // Generate a random fantasy-style name
        int firstNameIndex = UnityEngine.Random.Range(0, firstNames.Length);
        int lastNameIndex = UnityEngine.Random.Range(0, lastNames.Length);
        return firstNames[firstNameIndex] + lastNames[lastNameIndex];
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

    public Button CreateButton(GameObject parent, string name, string text)
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

    public void HideAllPanels()
    {
        if (connectingPanel != null) connectingPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
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

    public void ShowLobbyUI()
    {
        Debug.Log("Showing Lobby UI");
        HideAllPanels();
        
        // Make sure LobbyManager has initialized the lobby panel
        if (GameManager.Instance != null)
        {
            LobbyManager lobbyManager = GameManager.Instance.GetLobbyManager();
            if (lobbyManager != null)
            {
                lobbyManager.EnsureLobbyUICreated();
            }
        }
        
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Lobby panel is null in ShowLobbyUI");
        }
    }
    
    public void ShowGameplayUI()
    {
        Debug.Log("Showing Gameplay UI");
        HideAllPanels();
        
        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Gameplay panel is null in ShowGameplayUI");
        }
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
                    ShowMainMenuUI();
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

    public GameObject GetLobbyPanel()
    {
        if (lobbyPanel == null)
        {
            Debug.LogError("Lobby panel is null when requested by GetLobbyPanel");
            // Try to recreate it if it's missing
            lobbyPanel = CreatePanel("LobbyPanel");
        }
        
        Debug.Log("Returning lobby panel: " + (lobbyPanel != null));
        return lobbyPanel;
    }

    public GameObject GetMainCanvas()
    {
        return mainCanvas != null ? mainCanvas.gameObject : null;
    }
}