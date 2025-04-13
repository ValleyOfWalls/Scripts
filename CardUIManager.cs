using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using UnityEngine.EventSystems;

public class CardUIManager : MonoBehaviourPunCallbacks
{
    // References
    private GameplayManager gameplayManager;
    private DeckManager deckManager;
    
    // UI References
    private GameObject handPanel;
    private GameObject cardPrefab;
    private List<GameObject> cardObjects = new List<GameObject>();
    
    // Drag handling
    private GameObject currentDraggedCard;
    private Vector3 dragStartPosition;
    private int draggedCardIndex = -1;
    
    // Drop zones
    private RectTransform enemyDropZone;
    private RectTransform petDropZone;
    
    private void Start()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
        deckManager = FindObjectOfType<DeckManager>();
        
        // Create hand panel
        CreateHandPanel();
        
        // Create drop zones for targeting
        CreateDropZones();
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
        
        // Add background image
        Image bgImage = handPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);
    }
    
    private void CreateDropZones()
    {
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;
        
        // Create enemy drop zone (upper right)
        GameObject enemyZone = new GameObject("EnemyDropZone");
        enemyZone.transform.SetParent(mainCanvas.transform, false);
        
        enemyDropZone = enemyZone.AddComponent<RectTransform>();
        enemyDropZone.anchorMin = new Vector2(0.7f, 0.5f);
        enemyDropZone.anchorMax = new Vector2(0.9f, 0.7f);
        enemyDropZone.sizeDelta = Vector2.zero;
        
        Image enemyZoneImage = enemyZone.AddComponent<Image>();
        enemyZoneImage.color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        
        TextMeshProUGUI enemyZoneText = CreateDropZoneText(enemyZone, "Drop to Attack Enemy");
        
        // Create pet drop zone (upper middle)
        GameObject petZone = new GameObject("PetDropZone");
        petZone.transform.SetParent(mainCanvas.transform, false);
        
        petDropZone = petZone.AddComponent<RectTransform>();
        petDropZone.anchorMin = new Vector2(0.4f, 0.5f);
        petDropZone.anchorMax = new Vector2(0.6f, 0.7f);
        petDropZone.sizeDelta = Vector2.zero;
        
        Image petZoneImage = petZone.AddComponent<Image>();
        petZoneImage.color = new Color(0.2f, 0.6f, 0.8f, 0.3f);
        
        TextMeshProUGUI petZoneText = CreateDropZoneText(petZone, "Drop to Support Pet");
        
        // Initially hide drop zones
        enemyZone.SetActive(false);
        petZone.SetActive(false);
    }
    
    private TextMeshProUGUI CreateDropZoneText(GameObject parent, string text)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 16;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        return textComponent;
    }
    
    public void AddCardToHand(Card card)
    {
        if (handPanel == null) return;
        
        // Create card object
        GameObject cardObj = CreateCardObject(card);
        cardObj.transform.SetParent(handPanel.transform, false);
        
        // Add to list
        cardObjects.Add(cardObj);
        
        // Add drag component
        CardDragHandler dragHandler = cardObj.AddComponent<CardDragHandler>();
        dragHandler.Initialize(this, cardObjects.Count - 1);
    }
    
    public void RemoveCardFromHand(int index)
    {
        if (index < 0 || index >= cardObjects.Count) return;
        
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
        
        return cardObj;
    }
    
    // Handle card dragging started
    public void OnCardDragBegin(int cardIndex, Vector3 position)
    {
        draggedCardIndex = cardIndex;
        dragStartPosition = position;
        
        // Show drop zones
        ShowDropZones(true);
    }
    
    // Handle card dragging
    public void OnCardDrag(int cardIndex, Vector3 position)
    {
        if (cardIndex != draggedCardIndex) return;
        
        // Move the card
        if (cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            cardObjects[cardIndex].transform.position = position;
        }
    }
    
    // Handle card drag end
    public void OnCardDragEnd(int cardIndex, Vector3 position)
    {
        if (cardIndex != draggedCardIndex) return;
        
        // Hide drop zones
        ShowDropZones(false);
        
        // Check if dropped on a valid zone
        if (IsPositionOverDropZone(position, enemyDropZone))
        {
            // Play card on enemy
            PlayerController localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null && localPlayer.photonView.IsMine)
            {
                localPlayer.PlayCard(cardIndex, -1);
            }
        }
        else if (IsPositionOverDropZone(position, petDropZone))
        {
            // Play card on pet
            PlayerController localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null && localPlayer.photonView.IsMine)
            {
                localPlayer.PlayCardOnPet(cardIndex);
            }
        }
        else
        {
            // Return card to original position
            if (cardIndex >= 0 && cardIndex < cardObjects.Count)
            {
                cardObjects[cardIndex].transform.position = dragStartPosition;
            }
        }
        
        draggedCardIndex = -1;
    }
    
    private void ShowDropZones(bool show)
    {
        if (enemyDropZone != null)
        {
            enemyDropZone.gameObject.SetActive(show);
        }
        
        if (petDropZone != null)
        {
            petDropZone.gameObject.SetActive(show);
        }
    }
    
    private bool IsPositionOverDropZone(Vector3 position, RectTransform dropZone)
    {
        if (dropZone == null) return false;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dropZone, position, null, out localPoint);
        
        Rect rect = dropZone.rect;
        return rect.Contains(localPoint);
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
    
    // Keep the other methods as they are
}

// Add this class to handle card dragging
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CardUIManager cardUIManager;
    public int CardIndex { get; set; }
    
    public void Initialize(CardUIManager manager, int index)
    {
        cardUIManager = manager;
        CardIndex = index;
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardUIManager != null)
        {
            cardUIManager.OnCardDragBegin(CardIndex, transform.position);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (cardUIManager != null)
        {
            cardUIManager.OnCardDrag(CardIndex, Input.mousePosition);
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (cardUIManager != null)
        {
            cardUIManager.OnCardDragEnd(CardIndex, Input.mousePosition);
        }
    }
}