using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class CardUIManager : MonoBehaviourPunCallbacks
{
    // References
    private GameplayManager gameplayManager;
    private DeckManager deckManager;
    
    // UI References
    private GameObject handPanel;
    private GameObject cardPrefab;
    private List<GameObject> cardObjects = new List<GameObject>();
    
    private void Start()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
        deckManager = FindObjectOfType<DeckManager>();
        
        // Create hand panel
        CreateHandPanel();
    }
    
    private void CreateHandPanel()
    {
        // Find main canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;
        
        // Create hand panel
        handPanel = new GameObject("HandPanel");
        handPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform handPanelRect = handPanel.AddComponent<RectTransform>();
        handPanelRect.anchorMin = new Vector2(0.5f, 0);
        handPanelRect.anchorMax = new Vector2(0.5f, 0.2f);
        handPanelRect.sizeDelta = new Vector2(800, 0);
        
        // Add horizontal layout
        HorizontalLayoutGroup handLayout = handPanel.AddComponent<HorizontalLayoutGroup>();
        handLayout.spacing = 10;
        handLayout.childAlignment = TextAnchor.MiddleCenter;
        handLayout.childForceExpandWidth = false;
        handLayout.childForceExpandHeight = true;
    }
    
    public void AddCardToHand(Card card)
    {
        if (handPanel == null) return;
        
        // Create card object
        GameObject cardObj = CreateCardObject(card);
        cardObj.transform.SetParent(handPanel.transform, false);
        
        // Add to list
        cardObjects.Add(cardObj);
    }
    
    public void RemoveCardFromHand(int index)
    {
        if (index < 0 || index >= cardObjects.Count) return;
        
        // Destroy card object
        Destroy(cardObjects[index]);
        cardObjects.RemoveAt(index);
    }
    
    public void ClearHand()
    {
        foreach (GameObject cardObj in cardObjects)
        {
            Destroy(cardObj);
        }
        cardObjects.Clear();
    }
    
    private GameObject CreateCardObject(Card card)
    {
        // Create card game object
        GameObject cardObj = new GameObject("Card_" + card.CardName);
        
        // Add components
        RectTransform rectTransform = cardObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(120, 180);
        
        Image cardImage = cardObj.AddComponent<Image>();
        cardImage.color = GetCardTypeColor(card.CardType);
        
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
        
        // Create energy cost
        GameObject costObj = new GameObject("CostText");
        costObj.transform.SetParent(contentObj.transform, false);
        TextMeshProUGUI costText = costObj.AddComponent<TextMeshProUGUI>();
        costText.text = "Cost: " + card.EnergyCost;
        costText.fontSize = 12;
        costText.alignment = TextAlignmentOptions.Left;
        costText.color = Color.white;
        
        RectTransform costRect = costObj.GetComponent<RectTransform>();
        costRect.sizeDelta = new Vector2(0, 15);
        
        // Create description text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(contentObj.transform, false);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = card.CardDescription;
        descText.fontSize = 10;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(0, 70);
        
        // Add button component for interaction
        Button cardButton = cardObj.AddComponent<Button>();
        cardButton.onClick.AddListener(() => OnCardClicked(cardObjects.IndexOf(cardObj)));
        
        // Set button colors
        ColorBlock colors = cardButton.colors;
        colors.normalColor = GetCardTypeColor(card.CardType);
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
        cardButton.colors = colors;
        
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
    
    private void OnCardClicked(int cardIndex)
    {
        // Get local player
        PlayerController localPlayer = FindObjectOfType<PlayerController>();
        if (localPlayer == null || !localPlayer.photonView.IsMine || !localPlayer.IsMyTurn)
            return;
        
        // Show targeting UI to choose between opponent or pet
        ShowTargetingUI(cardIndex);
    }
    
    private void ShowTargetingUI(int cardIndex)
    {
        PlayerController localPlayer = FindObjectOfType<PlayerController>();
        if (localPlayer == null) return;
        
        // Create targeting popup
        GameObject targetingUI = new GameObject("TargetingUI");
        targetingUI.transform.SetParent(handPanel.transform.parent, false);
        
        RectTransform targetingRect = targetingUI.AddComponent<RectTransform>();
        targetingRect.anchorMin = new Vector2(0.5f, 0.3f);
        targetingRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetingRect.sizeDelta = new Vector2(300, 150);
        
        Image bgImage = targetingUI.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        // Create vertical layout
        VerticalLayoutGroup layout = targetingUI.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        
        // Create title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(targetingUI.transform, false);
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Select Target";
        titleText.fontSize = 20;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        // Create opponent button
        GameObject opponentBtn = new GameObject("OpponentButton");
        opponentBtn.transform.SetParent(targetingUI.transform, false);
        
        Image opponentBtnImage = opponentBtn.AddComponent<Image>();
        opponentBtnImage.color = new Color(0.8f, 0.2f, 0.2f, 1); // Red for opponent
        
        Button opponentButton = opponentBtn.AddComponent<Button>();
        opponentButton.targetGraphic = opponentBtnImage;
        
        GameObject opponentTextObj = new GameObject("Text");
        opponentTextObj.transform.SetParent(opponentBtn.transform, false);
        
        TextMeshProUGUI opponentText = opponentTextObj.AddComponent<TextMeshProUGUI>();
        opponentText.text = "Enemy Monster";
        opponentText.fontSize = 16;
        opponentText.alignment = TextAlignmentOptions.Center;
        opponentText.color = Color.white;
        
        RectTransform opponentTextRect = opponentTextObj.GetComponent<RectTransform>();
        opponentTextRect.anchorMin = Vector2.zero;
        opponentTextRect.anchorMax = Vector2.one;
        opponentTextRect.sizeDelta = Vector2.zero;
        
        // Create pet button
        GameObject petBtn = new GameObject("PetButton");
        petBtn.transform.SetParent(targetingUI.transform, false);
        
        Image petBtnImage = petBtn.AddComponent<Image>();
        petBtnImage.color = new Color(0.2f, 0.6f, 0.8f, 1); // Blue for pet
        
        Button petButton = petBtn.AddComponent<Button>();
        petButton.targetGraphic = petBtnImage;
        
        GameObject petTextObj = new GameObject("Text");
        petTextObj.transform.SetParent(petBtn.transform, false);
        
        TextMeshProUGUI petText = petTextObj.AddComponent<TextMeshProUGUI>();
        petText.text = "My Pet";
        petText.fontSize = 16;
        petText.alignment = TextAlignmentOptions.Center;
        petText.color = Color.white;
        
        RectTransform petTextRect = petTextObj.GetComponent<RectTransform>();
        petTextRect.anchorMin = Vector2.zero;
        petTextRect.anchorMax = Vector2.one;
        petTextRect.sizeDelta = Vector2.zero;
        
        // Create cancel button
        GameObject cancelBtn = new GameObject("CancelButton");
        cancelBtn.transform.SetParent(targetingUI.transform, false);
        
        Image cancelBtnImage = cancelBtn.AddComponent<Image>();
        cancelBtnImage.color = new Color(0.5f, 0.5f, 0.5f, 1); // Gray for cancel
        
        Button cancelButton = cancelBtn.AddComponent<Button>();
        cancelButton.targetGraphic = cancelBtnImage;
        
        GameObject cancelTextObj = new GameObject("Text");
        cancelTextObj.transform.SetParent(cancelBtn.transform, false);
        
        TextMeshProUGUI cancelText = cancelTextObj.AddComponent<TextMeshProUGUI>();
        cancelText.text = "Cancel";
        cancelText.fontSize = 16;
        cancelText.alignment = TextAlignmentOptions.Center;
        cancelText.color = Color.white;
        
        RectTransform cancelTextRect = cancelTextObj.GetComponent<RectTransform>();
        cancelTextRect.anchorMin = Vector2.zero;
        cancelTextRect.anchorMax = Vector2.one;
        cancelTextRect.sizeDelta = Vector2.zero;
        
        // Button actions
        opponentButton.onClick.AddListener(() => {
            localPlayer.PlayCard(cardIndex, -1); // -1 for opponent
            Destroy(targetingUI);
        });
        
        petButton.onClick.AddListener(() => {
            localPlayer.PlayCardOnPet(cardIndex);
            Destroy(targetingUI);
        });
        
        cancelButton.onClick.AddListener(() => {
            Destroy(targetingUI);
        });
    }
    
    // Add helper method to get a card's details if needed
    public Card GetCardDetails(string cardId)
    {
        // This would ideally be a lookup in a card database
        // For now, just return a basic card
        switch (cardId)
        {
            case "strike_basic":
                return new Card
                {
                    CardId = "strike_basic",
                    CardName = "Strike",
                    CardDescription = "Deal 6 damage.",
                    EnergyCost = 1,
                    CardType = CardType.Attack,
                    CardRarity = CardRarity.Basic,
                    DamageAmount = 6
                };
                
            case "defend_basic":
                return new Card
                {
                    CardId = "defend_basic",
                    CardName = "Defend",
                    CardDescription = "Gain 5 Block.",
                    EnergyCost = 1,
                    CardType = CardType.Skill,
                    CardRarity = CardRarity.Basic,
                    BlockAmount = 5
                };
                
            default:
                Debug.LogWarning("Unknown card ID: " + cardId);
                return null;
        }
    }
}