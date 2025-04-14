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
    private PlayerController localPlayer;
    
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
    
    // Visual feedback
    private GameObject feedbackTextPrefab;
    
    [SerializeField] private bool debugMode = true;
    
    private void Start()
    {
        Debug.Log("CardUIManager starting");
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
        
        // Create feedback text prefab
        CreateFeedbackTextPrefab();
        
        // Try to find local player
        localPlayer = FindLocalPlayer();
        
        // Hide battle UI until needed
        HideBattleUI();
        
        Debug.Log("CardUIManager delayed initialization complete");
        
        // Explicitly check game state to ensure proper UI visibility
        if (GameManager.Instance != null)
        {
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            Debug.Log("Current game state: " + currentState);
            UpdateUIBasedOnGameState(currentState);
        }
    }
    
    private PlayerController FindLocalPlayer()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (player.photonView.IsMine)
            {
                Debug.Log("Found local player: " + player.PlayerName);
                return player;
            }
        }
        Debug.LogWarning("Could not find local player!");
        return null;
    }
    
    private void CreateFeedbackTextPrefab()
    {
        // Create a simple text object that can be instantiated for feedback
        GameObject textObj = new GameObject("FeedbackTextPrefab");
        
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        
        // Add animation component
        FeedbackTextAnimation animation = textObj.AddComponent<FeedbackTextAnimation>();
        
        // Save as prefab
        feedbackTextPrefab = textObj;
        textObj.SetActive(false);
    }
    
    private void CreateBattleUIContainer()
    {
        // Find main canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("Main Canvas not found!");
            return;
        }
        
        Debug.Log("Creating BattleUIContainer inside " + mainCanvas.name);
        
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
        
        // Add a CanvasGroup to control visibility easily
        CanvasGroup canvasGroup = battleUIContainer.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
    
    private void CreateHandPanel()
    {
        if (battleUIContainer == null)
        {
            Debug.LogError("BattleUIContainer is null when creating hand panel!");
            return;
        }
        
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
        
        Debug.Log("Hand panel created successfully");
    }
    
    private void CreateDropZones()
    {
        if (battleUIContainer == null)
        {
            Debug.LogError("BattleUIContainer is null when creating drop zones!");
            return;
        }
        
        // Create enemy pet drop zone (upper right quadrant)
        enemyDropZone = new GameObject("EnemyDropZone");
        enemyDropZone.transform.SetParent(battleUIContainer.transform, false);
        
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
        petDropZone.transform.SetParent(battleUIContainer.transform, false);
        
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
        
        Debug.Log("Drop zones created successfully");
    }
    
    public void ShowBattleUI()
    {
        if (battleUIContainer != null)
        {
            battleUIContainer.SetActive(true);
            Debug.Log("BattleUIContainer is now active");
            
            // Update local player reference
            if (localPlayer == null)
            {
                localPlayer = FindLocalPlayer();
            }
        }
        else
        {
            Debug.LogError("BattleUIContainer is null when trying to show it!");
            
            // Try to reinitialize
            StartCoroutine(DelayedInitialization());
        }
    }
    
    public void HideBattleUI()
    {
        if (battleUIContainer != null)
        {
            battleUIContainer.SetActive(false);
            Debug.Log("BattleUIContainer is now inactive");
        }
    }
    
    // Subscribe to game state changes
    public void OnEnable()
    {
        Debug.Log("CardUIManager OnEnable called");
        if (GameManager.Instance != null)
        {
            // Listen for game state changes
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            Debug.Log("Current game state in OnEnable: " + currentState);
            UpdateUIBasedOnGameState(currentState);
        }
    }
    
    // Update UI visibility based on game state
    private void UpdateUIBasedOnGameState(GameManager.GameState state)
    {
        Debug.Log("Updating UI based on game state: " + state);
        switch (state)
        {
            case GameManager.GameState.Battle:
                ShowBattleUI();
                break;
            case GameManager.GameState.Draft:
                ShowBattleUI(); // Keep UI visible during draft phase
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
        
        Debug.Log("Room properties updated");
        
        if (GameManager.Instance != null)
        {
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            Debug.Log("Current game state after room update: " + currentState);
            UpdateUIBasedOnGameState(currentState);
        }
    }
    
    private void Update()
    {
        // Debug check to ensure local player reference is valid
        if (debugMode && localPlayer == null)
        {
            localPlayer = FindLocalPlayer();
        }
        
        // Debug check to force UI visibility if it should be visible
        if (debugMode && GameManager.Instance != null)
        {
            GameManager.GameState currentState = GameManager.Instance.GetCurrentState();
            if ((currentState == GameManager.GameState.Battle || 
                 currentState == GameManager.GameState.Draft) && 
                (battleUIContainer == null || !battleUIContainer.activeSelf))
            {
                Debug.LogWarning("BattleUIContainer should be visible but isn't! Forcing visibility...");
                ShowBattleUI();
            }
        }
    }
    
    public void AddCardToHand(Card card)
    {
        // Find the cards container
        Transform cardsContainer = handPanel?.transform.Find("CardsContainer");
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
            localPlayer = FindLocalPlayer();
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
            localPlayer = FindLocalPlayer();
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
        if (feedbackTextPrefab == null || battleUIContainer == null)
            return;
        
        // Instantiate feedback text
        GameObject feedbackObj = Instantiate(feedbackTextPrefab, battleUIContainer.transform);
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

// Enumeration for drop zone types
public enum DropZoneType
{
    Enemy,
    Pet
}

// Component to handle drop zone events
public class DropZoneHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public DropZoneType zoneType;
    public bool IsBeingHovered { get; set; }
    
    private CardUIManager cardUIManager;
    
    private void Start()
    {
        cardUIManager = FindObjectOfType<CardUIManager>();
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
        if (dragHandler != null)
        {
            // Notify the card UI manager directly that we had a hit
            cardUIManager.OnZoneHit(zoneType, dragHandler.CardIndex, transform.position);
        }
    }
}

// Class to handle card dragging
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CardUIManager cardUIManager;
    public int CardIndex { get; set; }
    public Card CardData { get; private set; }
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    public void Initialize(CardUIManager manager, int index, Card cardData)
    {
        cardUIManager = manager;
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
        
        if (cardUIManager != null)
        {
            // Store the current position for returning if needed
            cardUIManager.OnCardDragBegin(CardIndex, transform.position);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        // Move the card with the mouse
        rectTransform.position = eventData.position;
        
        if (cardUIManager != null)
        {
            cardUIManager.OnCardDrag(CardIndex, eventData.position);
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore transparency and raycast blocking
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        if (cardUIManager != null)
        {
            cardUIManager.OnCardDragEnd(CardIndex, eventData.position);
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