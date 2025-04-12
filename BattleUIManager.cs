using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class BattleUIManager : MonoBehaviourPunCallbacks
{
    private GameplayManager gameplayManager;
    private Canvas mainCanvas;
    
    private GameObject battleOverviewPanel;
    private Dictionary<int, GameObject> playerBattleCards = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> monsterBattleCards = new Dictionary<int, GameObject>();
    
    private void Start()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
        mainCanvas = FindObjectOfType<Canvas>();
        
        if (mainCanvas != null)
        {
            CreateBattleOverviewUI();
        }
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
            PlayerController player = gameplayManager.GetPlayerById(playerActorNumber);
            MonsterController monster = gameplayManager.GetMonsterById(monsterOwnerActorNumber);
            
            if (player != null && monster != null)
            {
                // Create battle card
                CreateBattleCard(contentTransform, playerActorNumber, monsterOwnerActorNumber, player, monster);
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
        
        // Store references for updating
        playerBattleCards[playerActorNumber] = playerHealthObj;
        monsterBattleCards[monsterOwnerActorNumber] = monsterHealthObj;
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
        
        // Clear references
        playerBattleCards.Clear();
        monsterBattleCards.Clear();
    }
    
    public void UpdateBattleCard(int actorNumber, bool isPlayer, int currentHealth, int maxHealth)
    {
        if (isPlayer && playerBattleCards.ContainsKey(actorNumber))
        {
            TextMeshProUGUI healthText = playerBattleCards[actorNumber].GetComponent<TextMeshProUGUI>();
            if (healthText != null)
            {
                healthText.text = $"HP: {currentHealth}/{maxHealth}";
            }
        }
        else if (!isPlayer && monsterBattleCards.ContainsKey(actorNumber))
        {
            TextMeshProUGUI healthText = monsterBattleCards[actorNumber].GetComponent<TextMeshProUGUI>();
            if (healthText != null)
            {
                healthText.text = $"HP: {currentHealth}/{maxHealth}";
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
}