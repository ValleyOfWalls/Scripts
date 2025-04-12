using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    private UIManager uiManager;
    private NetworkManager networkManager;

    private GameObject lobbyPanel;
    private GameObject playerListContent;
    private GameObject readyButton;
    private TextMeshProUGUI roomNameText;
    private Button leaveRoomButton;
    private Button readyButtonComponent;
    private bool isReady = false;

    private void Start()
    {
        // Find manager references
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null && GameManager.Instance != null)
            {
                uiManager = GameManager.Instance.GetUIManager();
            }
        }
        
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null && GameManager.Instance != null)
            {
                networkManager = GameManager.Instance.GetNetworkManager();
            }
        }
        
        Debug.Log("LobbyManager initialized");
    }

    public void EnsureLobbyUICreated()
    {
        Debug.Log("Ensuring lobby UI is created");
        
        // Make sure references are initialized
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null && GameManager.Instance != null)
            {
                uiManager = GameManager.Instance.GetUIManager();
            }
        }
        
        // Get the lobby panel
        if (lobbyPanel == null && uiManager != null)
        {
            lobbyPanel = uiManager.GetLobbyPanel();
        }
        
        // Create the UI elements if they don't exist
        if (lobbyPanel != null && playerListContent == null)
        {
            CreateLobbyUI();
        }
    }

    public void InitializeLobby()
    {
        // Make sure references are initialized
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null && GameManager.Instance != null)
            {
                uiManager = GameManager.Instance.GetUIManager();
            }
        }
        
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null && GameManager.Instance != null)
            {
                networkManager = GameManager.Instance.GetNetworkManager();
            }
        }
        
        // Ensure lobby UI is created
        EnsureLobbyUICreated();
        
        // If we're connected to a room, show the lobby UI; otherwise show the main menu
        if (PhotonNetwork.InRoom)
        {
            UpdateLobbyUI();
            
            if (uiManager != null)
            {
                uiManager.ShowLobbyUI();
            }
            else
            {
                Debug.LogError("UIManager is null in InitializeLobby");
            }
        }
        else
        {
            if (uiManager != null)
            {
                uiManager.ShowMainMenuUI();
            }
            else
            {
                Debug.LogError("UIManager is null in InitializeLobby");
            }
        }
    }

    private void CreateLobbyUI()
    {
        // Get reference to the lobby panel
        if (uiManager == null)
        {
            Debug.LogError("UIManager is null in CreateLobbyUI");
            return;
        }
        
        lobbyPanel = uiManager.GetLobbyPanel();
        if (lobbyPanel == null)
        {
            Debug.LogError("Lobby panel is null");
            return;
        }
        
        // Clear existing UI elements
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

    public void UpdatePlayerReadyStatus(string playerId, bool isReady)
    {
        Debug.Log($"Updating player ready status: {playerId}, {isReady}");
        UpdateLobbyUI();
    }

    private TextMeshProUGUI CreateText(GameObject parent, string name, string text)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        return textComponent;
    }
}