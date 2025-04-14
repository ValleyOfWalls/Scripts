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
    private List<GameObject> cardObjects = new List<GameObject>();
    
    // Drag handling
    private GameObject currentDraggedCard;
    private Vector3 dragStartPosition;
    private int draggedCardIndex = -1;
    
    // Drop zones
    private GameObject enemyDropZone;
    private GameObject petDropZone;
    
    // Container for all battle UI elements
    private GameObject battleUIContainer;
    
    private void Start()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
        deckManager = FindObjectOfType<DeckManager>();
        
        // Wait a moment to ensure all managers are initialized
        StartCoroutine(DelayedInitialization());
    }
    
    private IEnumerator DelayedInitialization()
    {
        // Wait to make sure GameManager and other systems are fully initialized
        yield return new WaitForSeconds(0.5f);
        
        // Create a container for all battle UI elements
        CreateBattleUIContainer();
        
        // Create hand panel
        CreateHandPanel();
        
        // Create drop zones for targeting
        CreateDropZones();
        
        // Hide battle UI until needed
        HideBattleUI();
    }
    
    private void CreateBattleUIContainer()
    {
        // Find main canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;
        
        // Create a container for all battle UI elements
        battleUIContainer = new GameObject("BattleUIContainer");
        battleUIContainer.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform containerRect = battleUIContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;
        
        // Set a lower sort order to ensure it doesn't overlap with other UIs
        Canvas containerCanvas = battleUIContainer.AddComponent<Canvas>();
        containerCanvas.overrideSorting = true;
        containerCanvas.sortingOrder = 5; // Make sure this is below the lobby UI sorting order
        
        // Add a raycaster for UI interactions
        battleUIContainer.AddComponent<GraphicRaycaster>();
    }
    
    private void CreateHandPanel()
    {
        if (battleUIContainer == null) return;
        
        // Create hand panel
        handPanel = new GameObject("HandPanel");
        handPanel.transform.SetParent(battleUIContainer.transform, false);
        
        RectTransform handPanelRect = handPanel.AddComponent<RectTransform>();
        handPanelRect.anchorMin = new Vector2(0.2f, 0.01f);
        handPanelRect.anchorMax = new Vector2(0.8f, 0.25f);
        handPanelRect.sizeDelta = Vector2.zero;
        
        // Add background image
        Image bgImage = handPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);
        
        // Create a container for the cards with a horizontal layout
        GameObject cardsContainer = new GameObject("CardsContainer");
        cardsContainer.transform.SetParent(handPanel.transform, false);
        
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
    }
    
    private void CreateDropZones()
    {
        if (battleUIContainer == null) return;
        
        // Create enemy pet drop zone (upper right quadrant)
        enemyDropZone = new GameObject("EnemyDropZone");
        enemyDropZone.transform.SetParent(battleUIContainer.transform, false);
        
        RectTransform enemyRect = enemyDropZone.AddComponent<RectTransform>();
        enemyRect.anchorMin = new Vector2(0.75f, 0.75f);
        enemyRect.anchorMax = new Vector2(0.99f, 0.99f);
        enemyRect.sizeDelta = Vector2.zero;
        
        Image enemyZoneImage = enemyDropZone.AddComponent<Image>();
        enemyZoneImage.color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        
        // Add outline to make it more visible
        Outline enemyOutline = enemyDropZone.AddComponent<Outline>();
        enemyOutline.effectColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        enemyOutline.effectDistance = new Vector2(3, 3);
        
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
        petDropZone.transform.SetParent(battleUIContainer.transform, false);
        
        RectTransform petRect = petDropZone.AddComponent<RectTransform>();
        petRect.anchorMin = new Vector2(0.38f, 0.75f);
        petRect.anchorMax = new Vector2(0.62f, 0.99f);
        petRect.sizeDelta = Vector2.zero;
        
        Image petZoneImage = petDropZone.AddComponent<Image>();
        petZoneImage.color = new Color(0.2f, 0.6f, 0.8f, 0.3f);
        
        // Add outline to make it more visible
        Outline petOutline = petDropZone.AddComponent<Outline>();
        petOutline.effectColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        petOutline.effectDistance = new Vector2(3, 3);
        
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
    
    public void ShowBattleUI()
    {
        if (battleUIContainer != null)
            battleUIContainer.SetActive(true);
    }
    
    public void HideBattleUI()
    {
        if (battleUIContainer != null)
            battleUIContainer.SetActive(false);
    }
    
    // Subscribe to game state changes
    public void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            // Listen for game state changes
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            UpdateUIBasedOnGameState(currentState);
        }
    }
    
    // Update UI visibility based on game state
    private void UpdateUIBasedOnGameState(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Battle:
                ShowBattleUI();
                break;
            default:
                HideBattleUI();
                break;
        }
    }
    
    // Listen for game state changes
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);
        
        if (GameManager.Instance != null)
        {
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            UpdateUIBasedOnGameState(currentState);
        }
    }
    
    public void AddCardToHand(Card card)
    {
        // Find the cards container
        Transform cardsContainer = handPanel?.transform.Find("CardsContainer");
        if (cardsContainer == null) return;
        
        // Create card object
        GameObject cardObj = CreateCardObject(card);
        cardObj.transform.SetParent(cardsContainer, false);
        
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
        
        // Hide drop zones
        ShowDropZones(false);
        
        // Reset card scale
        if (cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            cardObjects[cardIndex].transform.localScale = Vector3.one;
        }
        
        // Check if dropped on a valid zone
        bool cardPlayed = false;
        
        if (IsPositionOverDropZone(position, enemyDropZone.GetComponent<RectTransform>()))
        {
            // Play card on enemy
            PlayerController localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null && localPlayer.photonView.IsMine)
            {
                localPlayer.PlayCard(cardIndex, -1);
                cardPlayed = true;
            }
        }
        else if (IsPositionOverDropZone(position, petDropZone.GetComponent<RectTransform>()))
        {
            // Play card on pet
            PlayerController localPlayer = FindObjectOfType<PlayerController>();
            if (localPlayer != null && localPlayer.photonView.IsMine)
            {
                localPlayer.PlayCardOnPet(cardIndex);
                cardPlayed = true;
            }
        }
        
        // If card wasn't played, return to original position
        if (!cardPlayed && cardIndex >= 0 && cardIndex < cardObjects.Count)
        {
            StartCoroutine(AnimateCardReturn(cardIndex));
        }
        
        draggedCardIndex = -1;
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
            }
        }
    }
    
    private void HighlightDropZones(Vector3 position)
    {
        // Highlight enemy drop zone if hovering over it
        if (enemyDropZone != null)
        {
            bool overEnemy = IsPositionOverDropZone(position, enemyDropZone.GetComponent<RectTransform>());
            enemyDropZone.GetComponent<Image>().color = overEnemy 
                ? new Color(1f, 0.3f, 0.3f, 0.5f) 
                : new Color(0.8f, 0.2f, 0.2f, 0.3f);
        }
        
        // Highlight pet drop zone if hovering over it
        if (petDropZone != null)
        {
            bool overPet = IsPositionOverDropZone(position, petDropZone.GetComponent<RectTransform>());
            petDropZone.GetComponent<Image>().color = overPet 
                ? new Color(0.3f, 0.8f, 1f, 0.5f) 
                : new Color(0.2f, 0.6f, 0.8f, 0.3f);
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
    
    // Helper method to create a rounded rectangle sprite
    private Sprite CreateRoundedRectSprite(int cornerRadius)
    {
        int width = 100;
        int height = 150;
        
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
    
    // Helper method to create a circle sprite for energy cost
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
}

// Class to handle card dragging
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CardUIManager cardUIManager;
    public int CardIndex { get; set; }
    private Canvas canvas;
    
    public void Initialize(CardUIManager manager, int index)
    {
        cardUIManager = manager;
        CardIndex = index;
        canvas = FindObjectOfType<Canvas>();
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
            Vector3 position;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                position = Input.mousePosition;
            }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    GetComponent<RectTransform>(), 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out position);
            }
            
            cardUIManager.OnCardDrag(CardIndex, position);
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