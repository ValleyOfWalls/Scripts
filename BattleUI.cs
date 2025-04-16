using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class BattleUI : MonoBehaviourPunCallbacks
{
    private Canvas mainCanvas;
    private GameObject battlePanel;
    
    // Main UI panels
    private GameObject playerPanel;
    private GameObject opponentPetPanel;
    private GameObject playerPetPanel;
    private GameObject battleOverviewPanel;
    private GameObject cardHandPanel;
    private GameObject endTurnButton;
    
    // Text elements
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerEnergyText;
    private TextMeshProUGUI playerBlockText;
    private TextMeshProUGUI opponentPetNameText;
    private TextMeshProUGUI opponentPetHealthText;
    private TextMeshProUGUI playerPetNameText;
    private TextMeshProUGUI playerPetHealthText;
    
    // Progress bars
    private Slider playerHealthBar;
    private Slider opponentPetHealthBar;
    private Slider playerPetHealthBar;
    
    // References to game objects
    private PlayerController localPlayer;
    private MonsterController opponentPet;
    private MonsterController playerPet;
    
    public void Initialize()
    {
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("No canvas found for BattleUI");
            return;
        }
        
        CreateUI();
    }
    
    private void CreateUI()
    {
        // Create main battle panel - fills the entire screen
        battlePanel = CreatePanel("BattlePanel", new Vector2(0, 0), new Vector2(1, 1), new Color(0.1f, 0.1f, 0.15f, 1));
        
        // Create player stats panel (top left)
        playerPanel = CreatePanel("PlayerPanel", new Vector2(0.01f, 0.75f), new Vector2(0.25f, 0.99f), new Color(0.2f, 0.2f, 0.3f, 0.9f));
        playerPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create opponent pet panel (top right)
        opponentPetPanel = CreatePanel("OpponentPetPanel", new Vector2(0.75f, 0.75f), new Vector2(0.99f, 0.99f), new Color(0.4f, 0.2f, 0.2f, 0.9f));
        opponentPetPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create player's pet panel (top middle)
        playerPetPanel = CreatePanel("PlayerPetPanel", new Vector2(0.38f, 0.75f), new Vector2(0.62f, 0.99f), new Color(0.2f, 0.4f, 0.3f, 0.9f));
        playerPetPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create battle overview panel (middle right)
        battleOverviewPanel = CreatePanel("BattleOverviewPanel", new Vector2(0.75f, 0.3f), new Vector2(0.99f, 0.73f), new Color(0.25f, 0.25f, 0.3f, 0.8f));
        battleOverviewPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create card hand panel (bottom)
        cardHandPanel = CreatePanel("CardHandPanel", new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.25f), new Color(0.2f, 0.2f, 0.25f, 0.9f));
        cardHandPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create turn indicator and end turn button
        GameObject turnContainer = CreatePanel("TurnContainer", new Vector2(0.38f, 0.65f), new Vector2(0.62f, 0.73f), new Color(0.3f, 0.3f, 0.4f, 0.9f));
        turnContainer.transform.SetParent(battlePanel.transform, false);
        
        turnText = CreateTextElement(turnContainer, "TurnText", "Waiting for turn...", 24);
        RectTransform turnTextRect = turnText.GetComponent<RectTransform>();
        turnTextRect.anchorMin = new Vector2(0, 0);
        turnTextRect.anchorMax = new Vector2(1, 1);
        
        // Create end turn button
        endTurnButton = CreateButton("EndTurnButton", "End Turn", EndTurnClicked);
        endTurnButton.transform.SetParent(battlePanel.transform, false);
        RectTransform endTurnRect = endTurnButton.GetComponent<RectTransform>();
        endTurnRect.anchorMin = new Vector2(0.44f, 0.27f);
        endTurnRect.anchorMax = new Vector2(0.56f, 0.33f);
        endTurnButton.SetActive(false); // Initially disabled until player's turn
        
        // Set up player panel content
        SetupPlayerPanel();
        
        // Set up opponent pet panel content
        SetupOpponentPetPanel();
        
        // Set up player's pet panel content
        SetupPlayerPetPanel();
        
        // Set up battle overview panel content
        SetupBattleOverviewPanel();
        
        // Add battlePanel to mainCanvas
        battlePanel.transform.SetParent(mainCanvas.transform, false);
        
        // Initially hide the battle UI
        battlePanel.SetActive(false);
    }
    
    private void SetupPlayerPanel()
    {
        VerticalLayoutGroup layout = playerPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Player name
        playerNameText = CreateTextElement(playerPanel, "PlayerNameText", "Player Name", 20);
        RectTransform nameRect = playerNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 30);
        
        // Player health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(playerPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        playerHealthText = CreateTextElement(healthContainer, "HealthText", "HP: 0/0", 18);
        RectTransform healthTextRect = playerHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        
        playerHealthBar = CreateProgressBar(healthContainer, "HealthBar", Color.red);
        RectTransform healthBarRect = playerHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        
        // Player energy
        playerEnergyText = CreateTextElement(playerPanel, "EnergyText", "Energy: 0/0", 18);
        RectTransform energyRect = playerEnergyText.GetComponent<RectTransform>();
        energyRect.sizeDelta = new Vector2(0, 25);
        
        // Player block
        playerBlockText = CreateTextElement(playerPanel, "BlockText", "Block: 0", 18);
        RectTransform blockRect = playerBlockText.GetComponent<RectTransform>();
        blockRect.sizeDelta = new Vector2(0, 25);
    }
    
    private void SetupOpponentPetPanel()
    {
        VerticalLayoutGroup layout = opponentPetPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Add title
        TextMeshProUGUI titleText = CreateTextElement(opponentPetPanel, "TitleText", "OPPONENT PET", 16);
        titleText.color = new Color(1, 0.7f, 0.7f);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 20);
        
        // Pet name
        opponentPetNameText = CreateTextElement(opponentPetPanel, "PetNameText", "Unknown", 18);
        RectTransform nameRect = opponentPetNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 25);
        
        // Pet health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(opponentPetPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        opponentPetHealthText = CreateTextElement(healthContainer, "HealthText", "HP: 0/0", 18);
        RectTransform healthTextRect = opponentPetHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        
        opponentPetHealthBar = CreateProgressBar(healthContainer, "HealthBar", new Color(0.8f, 0.2f, 0.2f));
        RectTransform healthBarRect = opponentPetHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        
        // Add intent indicator
        GameObject intentObj = new GameObject("IntentIndicator");
        intentObj.transform.SetParent(opponentPetPanel.transform, false);
        RectTransform intentRect = intentObj.AddComponent<RectTransform>();
        intentRect.sizeDelta = new Vector2(0, 40);
        
        Image intentIcon = intentObj.AddComponent<Image>();
        intentIcon.color = new Color(0.8f, 0.3f, 0.3f);
        
        TextMeshProUGUI intentText = CreateTextElement(intentObj, "IntentText", "Intent: Attack", 16);
        RectTransform intentTextRect = intentText.GetComponent<RectTransform>();
        intentTextRect.anchorMin = Vector2.zero;
        intentTextRect.anchorMax = Vector2.one;
    }
    
    private void SetupPlayerPetPanel()
    {
        VerticalLayoutGroup layout = playerPetPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Add title
        TextMeshProUGUI titleText = CreateTextElement(playerPetPanel, "TitleText", "YOUR PET", 16);
        titleText.color = new Color(0.7f, 1f, 0.7f);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 20);
        
        // Pet name
        playerPetNameText = CreateTextElement(playerPetPanel, "PetNameText", "Unknown", 18);
        RectTransform nameRect = playerPetNameText.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 25);
        
        // Pet health
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(playerPetPanel.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        playerPetHealthText = CreateTextElement(healthContainer, "HealthText", "HP: 0/0", 18);
        RectTransform healthTextRect = playerPetHealthText.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        
        playerPetHealthBar = CreateProgressBar(healthContainer, "HealthBar", new Color(0.2f, 0.7f, 0.3f));
        RectTransform healthBarRect = playerPetHealthBar.GetComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0, 0);
        healthBarRect.anchorMax = new Vector2(1, 0.5f);
        
        // Add status indicators
        GameObject statusObj = new GameObject("StatusIndicators");
        statusObj.transform.SetParent(playerPetPanel.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(0, 30);
        
        TextMeshProUGUI statusText = CreateTextElement(statusObj, "StatusText", "Combat Status", 16);
        RectTransform statusTextRect = statusText.GetComponent<RectTransform>();
        statusTextRect.anchorMin = Vector2.zero;
        statusTextRect.anchorMax = Vector2.one;
    }
    
    private void SetupBattleOverviewPanel()
    {
        // Add title
        TextMeshProUGUI titleText = CreateTextElement(battleOverviewPanel, "TitleText", "BATTLE OVERVIEW", 18);
        titleText.color = new Color(0.9f, 0.9f, 0.6f);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.9f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = Vector2.zero;
        
        // Create scroll view for combat entries
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(battleOverviewPanel.transform, false);
        
        RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 0.9f);
        scrollViewRect.sizeDelta = Vector2.zero;
        
        ScrollRect scrollView = scrollViewObj.AddComponent<ScrollRect>();
        
        // Create viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);
        
        Mask viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Create content container
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 300); // Height will be controlled by content fitter
        
        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
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
        scrollView.scrollSensitivity = 20;
    }
    
    private GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panel = new GameObject(name);
        
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.sizeDelta = Vector2.zero;
        
        Image image = panel.AddComponent<Image>();
        image.color = color;
        
        // Add rounded corners using a sprite with 9-slice
        image.sprite = CreateRoundedRectSprite(20);
        image.type = Image.Type.Sliced;
        
        return panel;
    }
    
    private TextMeshProUGUI CreateTextElement(GameObject parent, string name, string text, int fontSize)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        return textComponent;
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
    
    private GameObject CreateButton(string name, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObj = new GameObject(name);
        
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = Vector2.zero;
        
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.6f, 0.8f, 1);
        buttonImage.sprite = CreateRoundedRectSprite(15);
        buttonImage.type = Image.Type.Sliced;
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Set colors for button states
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.3f, 0.6f, 0.8f, 1);
        colors.highlightedColor = new Color(0.4f, 0.7f, 0.9f, 1);
        colors.pressedColor = new Color(0.2f, 0.5f, 0.7f, 1);
        button.colors = colors;
        
        // Create text as child object
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 20;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        // Add click event
        button.onClick.AddListener(action);
        
        return buttonObj;
    }
    
    // Helper method to create a rounded rectangle sprite
    private Sprite CreateRoundedRectSprite(int cornerRadius)
    {
        int width = 100;
        int height = 100;
        
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Bilinear;
        
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
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.Tight, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
    }
    
    public void Show()
    {
        if (battlePanel != null)
        {
            battlePanel.SetActive(true);
            UpdateAllStats();
        }
    }
    
    public void Hide()
    {
        if (battlePanel != null)
        {
            battlePanel.SetActive(false);
        }
    }
    
    public void UpdatePlayerStats(PlayerController player)
    {
        if (player == null) return;
        
        localPlayer = player;
        
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
    
    // Method name needed by GameplayManager
    public void UpdateMonsterStats(MonsterController monster)
    {
        UpdateOpponentPetStats(monster);
    }
    
    public void UpdateOpponentPetStats(MonsterController pet)
    {
        if (pet == null) return;
        
        opponentPet = pet;
        
        if (opponentPetNameText != null)
            opponentPetNameText.text = pet.MonsterName;
            
        if (opponentPetHealthText != null)
            opponentPetHealthText.text = $"HP: {pet.CurrentHealth}/{pet.MaxHealth}";
            
        if (opponentPetHealthBar != null)
        {
            opponentPetHealthBar.maxValue = pet.MaxHealth;
            opponentPetHealthBar.value = pet.CurrentHealth;
        }
    }
    
    // Method name needed by GameplayManager
    public void UpdatePetStats(MonsterController pet)
    {
        UpdatePlayerPetStats(pet);
    }
    
    public void UpdatePlayerPetStats(MonsterController pet)
    {
        if (pet == null) return;
        
        playerPet = pet;
        
        if (playerPetNameText != null)
            playerPetNameText.text = pet.MonsterName;
            
        if (playerPetHealthText != null)
            playerPetHealthText.text = $"HP: {pet.CurrentHealth}/{pet.MaxHealth}";
            
        if (playerPetHealthBar != null)
        {
            playerPetHealthBar.maxValue = pet.MaxHealth;
            playerPetHealthBar.value = pet.CurrentHealth;
        }
    }
    
    public void AddBattleOverviewEntry(string playerName, string petName, int playerHealth, int petHealth)
    {
        Transform contentTransform = battleOverviewPanel.transform.Find("ScrollView/Viewport/Content");
        if (contentTransform == null) return;
        
        GameObject entryObj = new GameObject($"BattleEntry_{playerName}_{petName}");
        entryObj.transform.SetParent(contentTransform, false);
        
        RectTransform entryRect = entryObj.AddComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(0, 60);
        
        Image entryBg = entryObj.AddComponent<Image>();
        entryBg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        
        // Create layout group
        HorizontalLayoutGroup layoutGroup = entryObj.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 5;
        layoutGroup.padding = new RectOffset(5, 5, 5, 5);
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        
        // Create player info
        GameObject playerInfo = new GameObject("PlayerInfo");
        playerInfo.transform.SetParent(entryObj.transform, false);
        
        VerticalLayoutGroup playerLayout = playerInfo.AddComponent<VerticalLayoutGroup>();
        playerLayout.spacing = 2;
        playerLayout.childAlignment = TextAnchor.MiddleLeft;
        
        TextMeshProUGUI playerNameText = CreateTextElement(playerInfo, "PlayerName", playerName, 16);
        TextMeshProUGUI playerHealthText = CreateTextElement(playerInfo, "PlayerHealth", $"HP: {playerHealth}", 14);
        
        // Create VS text
        TextMeshProUGUI vsText = CreateTextElement(entryObj, "VS", "VS", 18);
        vsText.color = Color.yellow;
        
        // Create pet info
        GameObject petInfo = new GameObject("PetInfo");
        petInfo.transform.SetParent(entryObj.transform, false);
        
        VerticalLayoutGroup petLayout = petInfo.AddComponent<VerticalLayoutGroup>();
        petLayout.spacing = 2;
        petLayout.childAlignment = TextAnchor.MiddleRight;
        
        TextMeshProUGUI petNameText = CreateTextElement(petInfo, "PetName", petName, 16);
        TextMeshProUGUI petHealthText = CreateTextElement(petInfo, "PetHealth", $"HP: {petHealth}", 14);
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
    
    public void UpdateAllStats()
    {
        if (localPlayer != null)
        {
            UpdatePlayerStats(localPlayer);
        }
        
        if (opponentPet != null)
        {
            UpdateOpponentPetStats(opponentPet);
        }
        
        if (playerPet != null)
        {
            UpdatePlayerPetStats(playerPet);
        }
    }
    
    private void Update()
    {
        // Continuously update stats
        UpdateAllStats();
    }
}