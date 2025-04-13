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
    private GameObject playerStatsPanel;
    private GameObject monsterStatsPanel;
    private GameObject petStatsPanel;
    private GameObject endTurnButton;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerEnergyText;
    private TextMeshProUGUI monsterHealthText;
    private TextMeshProUGUI petHealthText;
    
    private PlayerController localPlayer;
    private MonsterController opponentMonster;
    private MonsterController petMonster;
    
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
        // Create main battle panel
        battlePanel = CreatePanel("BattlePanel", new Vector2(0, 0), new Vector2(1, 1));
        
        // Create player stats panel (top left)
        playerStatsPanel = CreatePanel("PlayerStatsPanel", new Vector2(0, 0.8f), new Vector2(0.25f, 1));
        playerStatsPanel.transform.SetParent(battlePanel.transform, false);
        
        // Create player stats text
        GameObject playerNameObj = CreateTextObject("PlayerName", "Player: ", 20);
        playerNameObj.transform.SetParent(playerStatsPanel.transform, false);
        RectTransform playerNameRect = playerNameObj.GetComponent<RectTransform>();
        playerNameRect.anchorMin = new Vector2(0.1f, 0.7f);
        playerNameRect.anchorMax = new Vector2(0.9f, 0.9f);
        
        GameObject playerHealthObj = CreateTextObject("PlayerHealth", "HP: 0/0", 18);
        playerHealthObj.transform.SetParent(playerStatsPanel.transform, false);
        RectTransform playerHealthRect = playerHealthObj.GetComponent<RectTransform>();
        playerHealthRect.anchorMin = new Vector2(0.1f, 0.5f);
        playerHealthRect.anchorMax = new Vector2(0.9f, 0.7f);
        playerHealthText = playerHealthObj.GetComponent<TextMeshProUGUI>();
        
        GameObject playerEnergyObj = CreateTextObject("PlayerEnergy", "Energy: 0/0", 18);
        playerEnergyObj.transform.SetParent(playerStatsPanel.transform, false);
        RectTransform playerEnergyRect = playerEnergyObj.GetComponent<RectTransform>();
        playerEnergyRect.anchorMin = new Vector2(0.1f, 0.3f);
        playerEnergyRect.anchorMax = new Vector2(0.9f, 0.5f);
        playerEnergyText = playerEnergyObj.GetComponent<TextMeshProUGUI>();
        
        // Create opponent monster stats panel (top right)
        monsterStatsPanel = CreatePanel("MonsterStatsPanel", new Vector2(0.75f, 0.8f), new Vector2(1, 1));
        monsterStatsPanel.transform.SetParent(battlePanel.transform, false);
        
        GameObject monsterNameObj = CreateTextObject("MonsterName", "Monster: ", 20);
        monsterNameObj.transform.SetParent(monsterStatsPanel.transform, false);
        RectTransform monsterNameRect = monsterNameObj.GetComponent<RectTransform>();
        monsterNameRect.anchorMin = new Vector2(0.1f, 0.7f);
        monsterNameRect.anchorMax = new Vector2(0.9f, 0.9f);
        
        GameObject monsterHealthObj = CreateTextObject("MonsterHealth", "HP: 0/0", 18);
        monsterHealthObj.transform.SetParent(monsterStatsPanel.transform, false);
        RectTransform monsterHealthRect = monsterHealthObj.GetComponent<RectTransform>();
        monsterHealthRect.anchorMin = new Vector2(0.1f, 0.5f);
        monsterHealthRect.anchorMax = new Vector2(0.9f, 0.7f);
        monsterHealthText = monsterHealthObj.GetComponent<TextMeshProUGUI>();
        
        // Create pet stats panel (top middle)
        petStatsPanel = CreatePanel("PetStatsPanel", new Vector2(0.375f, 0.8f), new Vector2(0.625f, 1));
        petStatsPanel.transform.SetParent(battlePanel.transform, false);
        
        GameObject petNameObj = CreateTextObject("PetName", "Your Pet: ", 20);
        petNameObj.transform.SetParent(petStatsPanel.transform, false);
        RectTransform petNameRect = petNameObj.GetComponent<RectTransform>();
        petNameRect.anchorMin = new Vector2(0.1f, 0.7f);
        petNameRect.anchorMax = new Vector2(0.9f, 0.9f);
        
        GameObject petHealthObj = CreateTextObject("PetHealth", "HP: 0/0", 18);
        petHealthObj.transform.SetParent(petStatsPanel.transform, false);
        RectTransform petHealthRect = petHealthObj.GetComponent<RectTransform>();
        petHealthRect.anchorMin = new Vector2(0.1f, 0.5f);
        petHealthRect.anchorMax = new Vector2(0.9f, 0.7f);
        petHealthText = petHealthObj.GetComponent<TextMeshProUGUI>();
        
        // Create turn indicator
        GameObject turnObj = CreateTextObject("TurnIndicator", "Waiting for turn...", 24);
        turnObj.transform.SetParent(battlePanel.transform, false);
        RectTransform turnRect = turnObj.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.4f, 0.7f);
        turnRect.anchorMax = new Vector2(0.6f, 0.8f);
        turnText = turnObj.GetComponent<TextMeshProUGUI>();
        
        // Create end turn button
        endTurnButton = CreateButton("EndTurnButton", "End Turn", EndTurnClicked);
        endTurnButton.transform.SetParent(battlePanel.transform, false);
        RectTransform endTurnRect = endTurnButton.GetComponent<RectTransform>();
        endTurnRect.anchorMin = new Vector2(0.4f, 0.15f);
        endTurnRect.anchorMax = new Vector2(0.6f, 0.25f);
        endTurnButton.SetActive(false); // Initially disabled until player's turn
        
        // Add battlePanel to mainCanvas
        battlePanel.transform.SetParent(mainCanvas.transform, false);
        
        // Initially hide the battle UI
        battlePanel.SetActive(false);
    }
    
    private GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name);
        
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.sizeDelta = Vector2.zero;
        
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.5f);
        
        return panel;
    }
    
    private GameObject CreateTextObject(string name, string text, int fontSize)
    {
        GameObject textObj = new GameObject(name);
        
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
        
        return textObj;
    }
    
    private GameObject CreateButton(string name, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObj = new GameObject(name);
        
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.5f, 0.8f, 1);
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Create text as child object
        GameObject textObj = CreateTextObject(name + "Text", text, 20);
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        // Add click event
        button.onClick.AddListener(action);
        
        return buttonObj;
    }
    
    public void Show()
    {
        if (battlePanel != null)
        {
            battlePanel.SetActive(true);
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
        
        if (playerHealthText != null)
        {
            playerHealthText.text = $"HP: {player.CurrentHealth}/{player.MaxHealth}";
        }
        
        if (playerEnergyText != null)
        {
            playerEnergyText.text = $"Energy: {player.CurrentEnergy}/{player.MaxEnergy}";
        }
    }
    
    public void UpdateMonsterStats(MonsterController monster)
    {
        if (monster == null) return;
        
        opponentMonster = monster;
        
        if (monsterHealthText != null)
        {
            monsterHealthText.text = $"HP: {monster.CurrentHealth}/{monster.MaxHealth}";
        }
    }
    
    public void UpdatePetStats(MonsterController monster)
    {
        if (monster == null) return;
        
        petMonster = monster;
        
        if (petHealthText != null)
        {
            petHealthText.text = $"HP: {monster.CurrentHealth}/{monster.MaxHealth}";
        }
    }
    
    public void ShowPlayerTurn(bool isPlayerTurn)
    {
        if (turnText != null)
        {
            turnText.text = isPlayerTurn ? "Your Turn" : "Opponent's Turn";
            turnText.color = isPlayerTurn ? Color.green : Color.yellow;
        }
        
        if (endTurnButton != null)
        {
            endTurnButton.SetActive(isPlayerTurn);
        }
    }
    
    private void EndTurnClicked()
    {
        Debug.Log("End Turn button clicked");
        
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
        
        if (opponentMonster != null)
        {
            UpdateMonsterStats(opponentMonster);
        }
        
        if (petMonster != null)
        {
            UpdatePetStats(petMonster);
        }
    }
    
    private void Update()
    {
        // Continuously update stats
        UpdateAllStats();
    }
}