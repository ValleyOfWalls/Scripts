using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using DG.Tweening; // Added DOTween

public class CombatUIManager
{
    private GameManager gameManager;
    
    // UI References
    private TextMeshProUGUI playerNameText;
    private Slider playerHealthSlider;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerBlockText;
    private TextMeshProUGUI energyText;
    private TextMeshProUGUI opponentPetNameText;
    private Slider opponentPetHealthSlider;
    private TextMeshProUGUI opponentPetHealthText;
    private TextMeshProUGUI opponentPetBlockText;
    private TextMeshProUGUI opponentPetIntentText;
    private TextMeshProUGUI ownPetNameText;
    private Slider ownPetHealthSlider;
    private TextMeshProUGUI ownPetHealthText;
    private TextMeshProUGUI ownPetBlockText;
    private TextMeshProUGUI ownPetDotText;
    private GameObject playerHandPanel;
    private TextMeshProUGUI deckCountText;
    private TextMeshProUGUI discardCountText;
    private TextMeshProUGUI playerDotText;
    private TextMeshProUGUI comboCountText;
    private Button endTurnButton;
    private TextMeshProUGUI opponentPetDotText;
    private HandPanelHoverManager handPanelHoverManager;
    
    // Other Fights UI
    private GameObject othersStatusArea;
    private TextMeshProUGUI otherPlayerStatusTemplate;
    
    // Hand Layout Parameters
    private float cardSpacing = 100f;
    private float tiltAnglePerCard = 5f;
    private float offsetYPerCard = 10f;
    private float curveFactor = 0.5f;

    public CombatUIManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void InitializeUIReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;

        Transform topArea = combatInstance.transform.Find("TopArea");
        Transform playerArea = combatInstance.transform.Find("PlayerArea");

        if (topArea == null || playerArea == null)
        {
            Debug.LogError("Could not find TopArea or PlayerArea in CombatCanvas!");
            return;
        }

        // Find opponent pet area
        Transform opponentAreaContainer = topArea.Find("OpponentPetAreaContainer");
        Transform opponentArea = opponentAreaContainer?.Find("OpponentPetArea");
        opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
        opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
        opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
        opponentPetBlockText = opponentArea?.Find("OpponentPetBlockText")?.GetComponent<TextMeshProUGUI>();
        opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
        opponentPetDotText = opponentArea?.Find("OpponentPetDotText")?.GetComponent<TextMeshProUGUI>();
        
        // Hide the opponent intent text as it's currently unused
        if (opponentPetIntentText != null)
        {
            opponentPetIntentText.gameObject.SetActive(false); 
        }
        
        // Find own pet area
        Transform ownPetAreaContainer = topArea.Find("OwnPetAreaContainer");
        Transform ownPetArea = ownPetAreaContainer?.Find("OwnPetArea");
        ownPetNameText = ownPetArea?.Find("OwnPetNameText")?.GetComponent<TextMeshProUGUI>();
        ownPetHealthSlider = ownPetArea?.Find("OwnPetHealthSlider")?.GetComponent<Slider>();
        ownPetHealthText = ownPetArea?.Find("OwnPetHealthText")?.GetComponent<TextMeshProUGUI>();
        ownPetBlockText = ownPetArea?.Find("OwnPetBlockText")?.GetComponent<TextMeshProUGUI>();
        ownPetDotText = ownPetArea?.Find("OwnPetDotText")?.GetComponent<TextMeshProUGUI>();
        
        // Find Other Fights UI Elements
        Transform othersStatusAreaTransform = topArea.Find("OthersStatusArea");
        if (othersStatusAreaTransform != null)
        {
            othersStatusArea = othersStatusAreaTransform.gameObject;
            Transform templateTransform = othersStatusAreaTransform.Find("OtherPlayerStatusTemplate");
            if (templateTransform != null)
            {
                otherPlayerStatusTemplate = templateTransform.GetComponent<TextMeshProUGUI>();
                if (otherPlayerStatusTemplate != null)
                {
                    otherPlayerStatusTemplate.gameObject.SetActive(false); // Ensure template is initially hidden
                }
                else
                {
                     Debug.LogError("OtherPlayerStatusTemplate TextMeshProUGUI component not found!");
                }
            }
            else
            {
                Debug.LogError("OtherPlayerStatusTemplate GameObject not found under OthersStatusArea!");
            }
        }
        else
        {
            Debug.LogWarning("OthersStatusArea GameObject not found in TopArea.");
        }

        // Find elements within PlayerArea
        Transform statsRow = playerArea.Find("StatsRow");
        playerNameText = statsRow?.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        playerHealthSlider = statsRow?.Find("PlayerHealthSlider")?.GetComponent<Slider>();
        playerHealthText = statsRow?.Find("PlayerHealthText")?.GetComponent<TextMeshProUGUI>();
        playerBlockText = statsRow?.Find("PlayerBlockText")?.GetComponent<TextMeshProUGUI>();
        energyText = statsRow?.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        playerDotText = statsRow?.Find("PlayerDotText")?.GetComponent<TextMeshProUGUI>();
        comboCountText = statsRow?.Find("ComboCountText")?.GetComponent<TextMeshProUGUI>();
        
        Transform handPanelTransform = playerArea.Find("PlayerHandPanel");
        playerHandPanel = handPanelTransform?.gameObject;
        
        if (playerHandPanel != null)
        {
            handPanelHoverManager = playerHandPanel.GetComponent<HandPanelHoverManager>();
            if (handPanelHoverManager == null)
            {
                 Debug.LogError("HandPanelHoverManager component not found on PlayerHandPanel!");
            }
        }
        
        Transform bottomBar = playerArea.Find("BottomBar");
        deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
        discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
        endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
        
        // Validate critical findings 
        if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null)
            Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab!");
        else 
            Debug.Log("Successfully found critical combat UI elements.");
    }
    
    public void InitializeUIState()
    {
        if (playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        
        if (opponentPetNameText)
        {
            Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
            opponentPetNameText.text = opponent != null ? $"{opponent.NickName}'s Pet" : "Opponent Pet";
        }
        
        if (ownPetNameText) ownPetNameText.text = gameManager.GetPlayerManager().GetLocalPetName();
    }
    
    public void UpdateHealthUI()
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        // Player Health & Block
        int currentPlayerHealth = playerManager.GetLocalPlayerHealth();
        int effectivePlayerMaxHealth = playerManager.GetEffectivePlayerMaxHealth();
        if (playerHealthSlider) {
             // Ensure max health is not zero to avoid division errors
             playerHealthSlider.value = (effectivePlayerMaxHealth > 0) ? (float)currentPlayerHealth / effectivePlayerMaxHealth : 0;
        }
        if (playerHealthText) playerHealthText.text = $"{currentPlayerHealth} / {effectivePlayerMaxHealth}";
        if (playerBlockText) playerBlockText.text = $"Block: {playerManager.GetLocalPlayerBlock()}";

        // Own Pet Health & Block
        int currentPetHealth = playerManager.GetLocalPetHealth();
        int effectivePetMaxHealth = playerManager.GetEffectivePetMaxHealth();
        if (ownPetHealthSlider) {
            ownPetHealthSlider.value = (effectivePetMaxHealth > 0) ? (float)currentPetHealth / effectivePetMaxHealth : 0;
        }
        if (ownPetHealthText) ownPetHealthText.text = $"{currentPetHealth} / {effectivePetMaxHealth}";
        
        // Construct Own Pet Block & Energy Text
        if (ownPetBlockText)
        {
            int petBlock = playerManager.GetLocalPetBlock();
            int petEnergy = -1; // Default to invalid value
            
            // Always try to read the latest value from properties
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
            {
                try 
                { 
                    petEnergy = (int)energyObj; 
                }
                catch 
                { 
                    Debug.LogWarning($"Failed to cast {CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP} to int.");
                    petEnergy = 0;
                } 
            }
            
            if (petEnergy < 0) // If property doesn't exist yet, use default
            {
                PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                if (petDeckManager != null) {
                    petEnergy = gameManager.GetStartingPetEnergy();
                }
                else
                {
                    petEnergy = 0;
                }
            }
            
            ownPetBlockText.text = $"Block: {petBlock} | Energy: {petEnergy}";
        }

        // Opponent Pet Health & Block
        int currentOpponentPetHealth = playerManager.GetOpponentPetHealth();
        int effectiveOpponentPetMaxHealth = playerManager.GetEffectiveOpponentPetMaxHealth();
        if (opponentPetHealthSlider) {
             opponentPetHealthSlider.value = (effectiveOpponentPetMaxHealth > 0) ? (float)currentOpponentPetHealth / effectiveOpponentPetMaxHealth : 0;
        }
        if (opponentPetHealthText) opponentPetHealthText.text = $"{currentOpponentPetHealth} / {effectiveOpponentPetMaxHealth}";
        
        // Construct Opponent Pet Block & Energy Text
        if (opponentPetBlockText) 
        {
            int oppPetBlock = playerManager.GetOpponentPetBlock();
            int oppPetEnergy = 0;
            
            // Read energy from local simulation for live updates
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            if (petDeckManager != null) {
                oppPetEnergy = petDeckManager.GetOpponentPetEnergy();
            }
            
            opponentPetBlockText.text = $"Block: {oppPetBlock} | Energy: {oppPetEnergy}";
        }

        UpdateStatusEffectsUI();
    }
    
    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"Energy: {gameManager.GetPlayerManager().GetCurrentEnergy()} / {gameManager.GetPlayerManager().GetStartingEnergy()}";
        }
        else
        {
            Debug.LogWarning("UpdateEnergyUI: energyText reference is null!");
        }
    }
    
    public void UpdateDeckCountUI()
    {
        CardManager cardManager = gameManager.GetCardManager();
        if (deckCountText != null) deckCountText.text = $"Deck: {cardManager.GetDeckCount()}";
        if (discardCountText != null) discardCountText.text = $"Discard: {cardManager.GetDiscardCount()}";
    }
    
    public void UpdateStatusEffectsUI()
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        if (playerManager == null) return;

        // Player DoT
        if (playerDotText != null)
        {
            int dotTurns = playerManager.GetPlayerDotTurns();
            int dotDmg = playerManager.GetPlayerDotDamage();
            playerDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            playerDotText.gameObject.SetActive(dotTurns > 0);
        }

        // Combo Count
        if (comboCountText != null)
        {
            int combo = playerManager.GetCurrentComboCount();
            comboCountText.text = (combo > 0) ? $"Combo: {combo}" : "";
            comboCountText.gameObject.SetActive(combo > 0);
        }

        // Own Pet DoT
        if (ownPetDotText != null)
        {
            int dotTurns = playerManager.GetLocalPetDotTurns();
            int dotDmg = playerManager.GetLocalPetDotDamage();
            ownPetDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            ownPetDotText.gameObject.SetActive(dotTurns > 0);
        }

        // Opponent Pet DoT
        if (opponentPetDotText != null)
        {
            int dotTurns = playerManager.GetOpponentPetDotTurns();
            int dotDmg = playerManager.GetOpponentPetDotDamage();
            opponentPetDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            opponentPetDotText.gameObject.SetActive(dotTurns > 0);
        }
    }
    
    public void UpdateHandUI()
    {
        GameObject cardPrefab = gameManager.GetCardPrefab();
        PlayerManager playerManager = gameManager.GetPlayerManager();
        CardManager cardManager = gameManager.GetCardManager(); 
        List<CardData> currentHand = cardManager.GetHand();

        if (playerHandPanel == null || cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // --- Refactored Logic --- 

        // 1. Get Existing GOs currently in the panel
        List<GameObject> existingGOs = new List<GameObject>();
        Dictionary<GameObject, CardData> currentGOMap = new Dictionary<GameObject, CardData>();
        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                var handler = child.GetComponent<CardDragHandler>();
                if (handler != null && handler.cardData != null)
                {
                    existingGOs.Add(child.gameObject);
                    currentGOMap[child.gameObject] = handler.cardData;
                }
                else if (child.gameObject.activeSelf) // Only destroy if active & invalid
                {
                    Debug.LogWarning($"[UpdateHandUI] Destroying invalid/unexpected card GO: {child.name}");
                    Object.Destroy(child.gameObject);
                }
            }
            else
            {
                 child.gameObject.SetActive(false); // Keep template hidden
            }
        }

        // 2. Prepare for matching
        List<System.Tuple<Vector3, Quaternion, Vector3>> targetTransforms = CalculateLayoutTransforms(currentHand.Count);
        List<GameObject> finalGOs = new List<GameObject>(currentHand.Count); // GOs in the final layout order
        List<GameObject> availableGOs = new List<GameObject>(existingGOs); // Copy list of GOs available to be reused
        bool[] handCardMatched = new bool[currentHand.Count]; // Track which hand slots have been filled

        // 3. Match and Animate/Create GOs
        // Prioritize matching existing GOs to their corresponding CardData first
        List<GameObject> reusedGOs = new List<GameObject>();
        for (int i = 0; i < currentHand.Count; i++)
        {
            CardData cardData = currentHand[i];
            GameObject goForThisCard = null;

            // Try find a matching, available GO
            int foundIndex = -1;
            for (int j = 0; j < availableGOs.Count; j++)
            {
                // Check if the GO's data matches the current hand card AND it hasn't been claimed by an earlier slot
                if (currentGOMap.ContainsKey(availableGOs[j]) && currentGOMap[availableGOs[j]] == cardData)
                {
                    goForThisCard = availableGOs[j];
                    foundIndex = j;
                    break; // Found the first available match for this card data
                }
            }

            if (goForThisCard != null)
            {
                availableGOs.RemoveAt(foundIndex); // Mark as used for this update cycle
                UpdateCardVisuals(goForThisCard, cardData, playerManager, cardManager); // Update visuals

                // Animate to new position
                RectTransform cardRect = goForThisCard.GetComponent<RectTransform>();
                System.Tuple<Vector3, Quaternion, Vector3> target = targetTransforms[i];
                DOTween.Kill(cardRect); // Kill potential previous tweens (like discard fade)
                goForThisCard.SetActive(true); // Ensure it's active
                // If it was fading out via discard, reset alpha
                 CanvasGroup cg = goForThisCard.GetComponent<CanvasGroup>();
                 if (cg != null) { 
                     DOTween.Kill(cg); // Kill fade tween
                     cg.alpha = 1f; 
                     cg.blocksRaycasts = true;
                 }

                cardRect.DOLocalMove(target.Item1, 0.3f).SetEase(Ease.OutQuad);
                cardRect.DOLocalRotateQuaternion(target.Item2, 0.3f).SetEase(Ease.OutQuad);
                CardDragHandler handler = goForThisCard.GetComponent<CardDragHandler>();
                if (handler != null) { handler.originalPosition = target.Item1; handler.originalRotation = target.Item2; handler.originalScale = target.Item3; handler.cardData = cardData; /*Ensure data is current*/ }
                
                // Add to final list in the correct order
                 if (finalGOs.Count > i) finalGOs[i] = goForThisCard; else finalGOs.Add(goForThisCard);
                 reusedGOs.Add(goForThisCard);
                 handCardMatched[i] = true;
            }
        }

        // 4. Create new GOs for any hand cards that didn't find a match
        for (int i = 0; i < currentHand.Count; i++)
        {
            if (handCardMatched[i]) continue; // Already handled by reuse

            CardData cardData = currentHand[i];
            GameObject newGO = Object.Instantiate(cardPrefab, playerHandPanel.transform);
            newGO.name = $"Card_{cardData.cardName}_{System.Guid.NewGuid()}";
            UpdateCardVisuals(newGO, cardData, playerManager, cardManager);
            CardDragHandler handler = newGO.GetComponent<CardDragHandler>();
            if (handler != null) handler.cardData = cardData;
            else Debug.LogError($"CardDragHandler missing on {newGO.name}!");
            newGO.SetActive(true);

            // Animate In
            System.Tuple<Vector3, Quaternion, Vector3> target = targetTransforms[i];
            AnimateCardDraw(newGO, i, target.Item1, target.Item2, target.Item3);
            
            // Add to final list in the correct order
            if (finalGOs.Count > i) finalGOs[i] = newGO; else finalGOs.Add(newGO);
        }

        // 5. Destroy Unused Existing GOs
        foreach (GameObject unusedGO in availableGOs) // These GOs were in the panel but are not in the current hand
        {
            // Check if the GO is currently animating its discard
            CardDragHandler unusedHandler = unusedGO?.GetComponent<CardDragHandler>();
            bool isCurrentlyDiscarding = unusedHandler != null && unusedHandler.isDiscarding;

            if (!isCurrentlyDiscarding && unusedGO != null) // Only destroy if *not* discarding 
            { 
                Debug.LogWarning($"[UpdateHandUI] Destroying unused GO: {unusedGO.name} (Card: {currentGOMap.GetValueOrDefault(unusedGO)?.cardName ?? "Unknown"})");
                 DOTween.Kill(unusedGO.transform, true); // Kill tweens immediately
                 Object.Destroy(unusedGO);
            }
            else if (isCurrentlyDiscarding)
            {
                 Debug.Log($"[UpdateHandUI] Skipping destruction of {unusedGO.name} because it is animating discard.");
            }
        }

        // 6. Update Hover Manager
        if (handPanelHoverManager != null)
        { 
            handPanelHoverManager.UpdateCardReferences(finalGOs);
        }
    }
    
    private IEnumerator DelayedHoverManagerUpdate(List<GameObject> cardGOs)
    {
        yield return new WaitForSeconds(0.4f); // Wait for reposition animations
        if (handPanelHoverManager != null)
        {
             handPanelHoverManager.UpdateCardReferences(cardGOs);
        }
    }
    
    private List<System.Tuple<Vector3, Quaternion, Vector3>> CalculateLayoutTransforms(int numCards)
    {
        List<System.Tuple<Vector3, Quaternion, Vector3>> transforms = new List<System.Tuple<Vector3, Quaternion, Vector3>>();
        if (numCards == 0) return transforms;

        float middleIndex = (numCards - 1) / 2.0f;
        Vector3 baseScale = gameManager.GetCardPrefab()?.GetComponent<RectTransform>().localScale ?? Vector3.one;

        for (int i = 0; i < numCards; i++)
        {
            float offsetFromCenter = i - middleIndex;

            // Calculate Position
            float posX = offsetFromCenter * cardSpacing;
            posX += Mathf.Sign(offsetFromCenter) * Mathf.Pow(Mathf.Abs(offsetFromCenter), 2) * curveFactor * cardSpacing * 0.1f;
            float posY = -Mathf.Abs(offsetFromCenter) * offsetYPerCard; 
            posY -= Mathf.Pow(offsetFromCenter, 2) * offsetYPerCard * 0.1f; 

            // Calculate Rotation
            float angle = -offsetFromCenter * tiltAnglePerCard; 

            Vector3 position = new Vector3(posX, posY, 0);
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            Vector3 scale = baseScale; // Assuming uniform scale for now

            transforms.Add(System.Tuple.Create(position, rotation, scale));
        }
        return transforms;
    }
    
    private void AnimateCardDraw(GameObject cardGO, int index, Vector3 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        if (cardRect == null) return;

        float drawAnimDuration = 0.4f;
        float staggerDelay = index * 0.08f;

        // Initial state (off-screen below, slightly smaller, zero rotation)
        cardRect.localPosition = targetPos + Vector3.down * 400f;
        cardRect.localRotation = Quaternion.identity;
        cardRect.localScale = targetScale * 0.7f;
        
        // Kill existing tweens just in case
        DOTween.Kill(cardRect);

        // Create Sequence
        Sequence drawSequence = DOTween.Sequence();
        drawSequence.SetTarget(cardRect); // Associate tween with the RectTransform
        drawSequence.AppendInterval(staggerDelay);
        drawSequence.Append(cardRect.DOLocalMove(targetPos, drawAnimDuration).SetEase(Ease.OutBack));
        drawSequence.Join(cardRect.DOLocalRotateQuaternion(targetRot, drawAnimDuration).SetEase(Ease.OutBack));
        drawSequence.Join(cardRect.DOScale(targetScale, drawAnimDuration).SetEase(Ease.OutBack));
        
        // Update handler's original transform once animation is complete
        drawSequence.OnComplete(() => {
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.originalPosition = targetPos;
                handler.originalRotation = targetRot;
                handler.originalScale = targetScale;
            }
        });

        drawSequence.Play();
    }
    
    private void UpdateCardVisuals(GameObject cardGO, CardData card, PlayerManager playerManager, CardManager cardManager)
    {
         bool isTempUpgrade = cardManager.IsCardTemporarilyUpgraded(card);

         Transform headerPanel = cardGO.transform.Find("HeaderPanel");
         Transform descPanel = cardGO.transform.Find("DescPanel");
         Transform artPanel = cardGO.transform.Find("ArtPanel");

         TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
         Image cardBackground = cardGO.GetComponent<Image>(); 

         if (nameText != null) 
         {
             nameText.text = card.cardName;
             if (isTempUpgrade) nameText.text += " (T+)"; 
         }
         
         if (costText != null)
         {
             int cardSpecificModifier = playerManager.GetLocalHandCostModifier(card);
             int effectiveCost = Mathf.Max(0, card.cost + cardSpecificModifier);
             costText.text = effectiveCost.ToString();
             
             // Cost color logic
             if (cardSpecificModifier > 0) costText.color = Color.red; 
             else if (cardSpecificModifier < 0) costText.color = Color.green; 
             else costText.color = Color.white; 
         }

         if (descText != null) descText.text = card.description;

         if (cardBackground != null) // Reset background color first
         {
            cardBackground.color = Color.white; // Or your default card background color
            if (isTempUpgrade)
            {
                 cardBackground.color = new Color(0.8f, 0.7f, 1.0f, cardBackground.color.a); // Light purple tint for temp upgrade
            }
         }
        
         // Ensure CardDragHandler has the correct CardData
         CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
         if (handler != null)
         {
             handler.cardData = card;
         }
         else
         {
             Debug.LogError($"CardDragHandler component not found on card GameObject: {cardGO.name}");
         }
    }
    
    public IEnumerator VisualizeOpponentPetCardPlay(CardData card)
    {
        if (card == null) yield break;
        Debug.Log($"Visualizing Opponent Pet playing: {card.cardName}");

        // Brief delay after popup before card appears
        yield return new WaitForSeconds(0.2f); 

        // Instantiate and Prepare Card Visual
        GameObject cardPrefab = gameManager.GetCardPrefab();
        GameObject combatCanvas = gameManager.GetGameStateManager().GetCombatInstance();
        if (cardPrefab == null || combatCanvas == null) 
        { 
            Debug.LogError("Cannot visualize pet card: CardPrefab or CombatCanvas missing!");
            yield break; 
        }
        
        GameObject cardGO = Object.Instantiate(cardPrefab, combatCanvas.transform); // Instantiate under canvas
        cardGO.name = $"OpponentPlayedCard_{card.cardName}";
        
        // Configure visuals
        UpdateCardVisuals(cardGO, card, gameManager.GetPlayerManager(), gameManager.GetCardManager()); 
        
        // Disable interaction for opponent card
        CardDragHandler dragHandler = cardGO.GetComponent<CardDragHandler>();
        if(dragHandler != null) dragHandler.enabled = false;
        CanvasGroup canvasGroup = cardGO.GetComponent<CanvasGroup>() ?? cardGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;

        // Position and Animate Card
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        Vector3 startPosition = opponentPetBlockText != null ? opponentPetBlockText.transform.position : new Vector3(Screen.width * 0.8f, Screen.height * 0.8f, 0);
        Vector3 targetPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        float scaleFactor = 1.5f;
        float animDuration = 0.4f;

        cardRect.position = startPosition;
        cardRect.localScale = Vector3.one * 0.5f;
        canvasGroup.alpha = 0f;

        // Animation
        float timer = 0f;
        // Fade in and move to center
        while (timer < animDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / animDuration;
            cardRect.position = Vector3.Lerp(startPosition, targetPosition, progress);
            cardRect.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * scaleFactor, progress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }
        cardRect.position = targetPosition;
        cardRect.localScale = Vector3.one * scaleFactor;
        canvasGroup.alpha = 1f;

        // Pause at center
        yield return new WaitForSeconds(0.75f);

        // Fade out and Destroy
        timer = 0f;
        float fadeDuration = 0.3f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            yield return null;
        }

        Object.Destroy(cardGO);
        Debug.Log($"Finished visualizing: {card.cardName}");
    }
    
    public void UpdateOtherFightsUI()
    {
        if (othersStatusArea == null || otherPlayerStatusTemplate == null)
        {
            return;
        }

        // Clear previous entries (but keep the template itself)
        foreach (Transform child in othersStatusArea.transform)
        {
            if (child.gameObject != otherPlayerStatusTemplate.gameObject && child.gameObject.name != "OtherStatusTitle")
            {
                Object.Destroy(child.gameObject);
            }
        }

        // Loop through players and display their status
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsLocal) continue; // Skip self

            int oppPetHP = -1;
            int turnNum = -1;
            int playerHP = -1;
            bool hpFound = false;
            bool turnFound = false;
            bool playerHpFound = false;

            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, out object hpObj))
            {
                try { oppPetHP = (int)hpObj; hpFound = true; } catch { /* Ignore */ }
            }
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_TURN_PROP, out object turnObj))
            {
                 try { turnNum = (int)turnObj; turnFound = true; } catch { /* Ignore */ }
            }
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, out object playerHpObj))
            {
                 try { playerHP = (int)playerHpObj; playerHpFound = true; } catch { /* Ignore */ }
            }

            // Only display if we have valid data for all fields
            if (hpFound && turnFound && playerHpFound)
            {
                GameObject statusEntryGO = Object.Instantiate(otherPlayerStatusTemplate.gameObject, othersStatusArea.transform);
                statusEntryGO.name = $"Status_{player.NickName}";
                TextMeshProUGUI statusText = statusEntryGO.GetComponent<TextMeshProUGUI>();
                
                if (statusText != null)
                {
                    statusText.text = $"{player.NickName}: HP:{playerHP} | T{turnNum}, OppHP: {oppPetHP}";
                    statusEntryGO.SetActive(true);
                }
                else
                {
                    Object.Destroy(statusEntryGO);
                }
            }
        }
    }
    
    public void ClearAllHandCardObjects()
    {
        if (playerHandPanel == null) return;

        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                 if (child.gameObject != null) {
                    DOTween.Kill(child.transform, true); // Kill tweens
                    Object.Destroy(child.gameObject);
                 }
            } else { child.gameObject.SetActive(false); }
        }
        Debug.Log("ClearAllHandCardObjects: Destroyed all card GameObjects in hand panel");
    }
    
    public GameObject GetPlayerHandPanel() => playerHandPanel;
    
    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton) endTurnButton.interactable = interactable;
    }
    
    public Button GetEndTurnButton() => endTurnButton;

    /// <summary>
    /// Call this method *BEFORE* removing the card data from the CardManager's hand list.
    /// It finds the associated GameObject and starts the discard animation.
    /// </summary>
    public void TriggerDiscardAnimation(CardData cardData)
    {
        GameObject cardGOToDiscard = null;
        // Find the *first* active, non-animating GO matching the card data
        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name == "CardTemplate") continue;

            CardDragHandler handler = child.GetComponent<CardDragHandler>();
            CanvasGroup cg = child.GetComponent<CanvasGroup>();

            // Check if GO matches data, is active, and is not already fading out (alpha > 0)
            if (handler != null && handler.cardData == cardData && child.gameObject.activeSelf && (cg == null || cg.alpha > 0.1f))
            {
                // Check if it's already animating a discard (e.g., via DOTween ID or check active tweens)
                 // Simple check: If a tween is active on its RectTransform, assume it might be animating.
                 // This isn't perfect but avoids starting a second discard animation.
                 if (!DOTween.IsTweening(child.GetComponent<RectTransform>()))
                 {
                    cardGOToDiscard = child.gameObject;
                    break; // Found a suitable GO to animate
                 }
            }
        }

        if (cardGOToDiscard != null)
        {
            Debug.Log($"[TriggerDiscardAnimation] Found GO {cardGOToDiscard.name} for {cardData.cardName}. Starting animation.");
            // Start animation - AnimateCardDiscardAndRemove will destroy the GO
            gameManager.StartCoroutine(AnimateCardDiscardAndRemove(cardData, cardGOToDiscard));
        }
        else
        {
            Debug.LogWarning($"[TriggerDiscardAnimation] Card {cardData.cardName} not found or already animating discard in hand panel.");
        }
    }

    private IEnumerator AnimateCardDiscardAndRemove(CardData cardData, GameObject cardGO)
    {
        if (cardGO == null) yield break; // GO already destroyed

        // --- ADDED: Get handler and set discarding flag --- 
        CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
        if (handler != null) 
        {
            handler.isDiscarding = true;
        }
        else
        {
            Debug.LogWarning($"[AnimateCardDiscardAndRemove] Could not find CardDragHandler on {cardGO.name} to set isDiscarding flag.");
            // Proceed with animation anyway, but UpdateHandUI might destroy it early if called mid-animation.
        }
        // --- END ADDED ---

        // Ensure cardGO is still active before starting animation
        if (!cardGO.activeSelf)
        {
            Debug.LogWarning($"[AnimateCardDiscardAndRemove] CardGO {cardGO.name} for {cardData.cardName} is inactive. Destroying immediately.");
            if (cardGO != null) Object.Destroy(cardGO); // Destroy if somehow still exists
            yield break;
        }

        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = cardGO.GetComponent<CanvasGroup>() ?? cardGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; // Disable interaction during animation

        float discardAnimDuration = 0.5f;
        float moveDistance = 300f;
        float randomAngle = Random.Range(-90f, 90f);
        Vector3 targetPosOffset = Quaternion.Euler(0, 0, Random.Range(-30f, 30f)) * Vector3.up * moveDistance;

        // Kill existing tweens
        DOTween.Kill(cardRect); 
        DOTween.Kill(canvasGroup);

        Sequence discardSequence = DOTween.Sequence();
        discardSequence.SetTarget(cardRect); // Associate tween

        // Playful burst animation
        discardSequence.Append(cardRect.DOLocalMove(cardRect.localPosition + targetPosOffset, discardAnimDuration).SetEase(Ease.OutQuad));
        discardSequence.Join(cardRect.DOLocalRotate(new Vector3(0, 0, randomAngle), discardAnimDuration).SetEase(Ease.OutQuad));
        discardSequence.Join(cardRect.DOScale(cardRect.localScale * 1.2f, discardAnimDuration * 0.4f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo)); // Optional pulse
        discardSequence.Insert(discardAnimDuration * 0.5f, canvasGroup.DOFade(0f, discardAnimDuration * 0.5f).SetEase(Ease.InQuad)); // Fade out in the second half

        // Set OnComplete to destroy the GameObject
        discardSequence.OnComplete(() => {
            if (cardGO != null) // Check if not already destroyed
            { 
                Object.Destroy(cardGO);
            }
        });

        discardSequence.Play();

        // Wait for the animation duration before the coroutine finishes (optional, but good practice)
        yield return new WaitForSeconds(discardAnimDuration);
    }
} 