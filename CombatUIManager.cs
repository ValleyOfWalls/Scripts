using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

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
        if (playerHandPanel == null || cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // Always destroy all existing card objects first
        foreach (Transform child in playerHandPanel.transform)
        {
            // Don't destroy the template
            if (child.gameObject.name != "CardTemplate") 
            {
                // Stop any coroutines before destroying
                CardDragHandler handler = child.GetComponent<CardDragHandler>();
                if (handler != null)
                {
                    handler.StopAllCoroutines();
                }
                Object.Destroy(child.gameObject);
            }
            else
            {
                child.gameObject.SetActive(false); // Keep template hidden
            }
        }

        PlayerManager playerManager = gameManager.GetPlayerManager();
        CardManager cardManager = gameManager.GetCardManager(); 
        List<CardData> currentHand = cardManager.GetHand();
        List<GameObject> currentCardGOs = new List<GameObject>(); // To hold the GOs for layout
        Debug.Log($"[UpdateHandUI] Creating {currentHand.Count} new card GameObjects");

        // Create fresh card objects for each card in hand
        for (int i = 0; i < currentHand.Count; i++)
        {
            CardData card = currentHand[i];
            GameObject cardGO = Object.Instantiate(cardPrefab, playerHandPanel.transform);
            cardGO.name = $"Card_{card.cardName}_{i}"; // Unique name helpful for debug
            Debug.Log($"[UpdateHandUI] Instantiating new GO '{cardGO.name}' for card '{card.cardName}' at index {i}.");
            UpdateCardVisuals(cardGO, card, playerManager, cardManager);
            
            // Store the card's index in hand
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null) {
                handler.cardHandIndex = i;
            }
            
            cardGO.SetActive(true);
            currentCardGOs.Add(cardGO);
        }

        // Apply Custom Layout
        ApplyHandLayout(currentCardGOs);

        Debug.Log($"[UpdateHandUI] After layout. Final child count in PlayerHandPanel: {playerHandPanel.transform.childCount}");

        // Update Hover Manager References
        if (handPanelHoverManager != null)
        {
            handPanelHoverManager.UpdateCardReferences();
        }
    }
    
    private void ApplyHandLayout(List<GameObject> cardGameObjects)
    {
        int numCards = cardGameObjects.Count;
        if (numCards == 0) return; // No layout needed for empty hand

        float middleIndex = (numCards - 1) / 2.0f;

        for (int i = 0; i < numCards; i++)
        {
            GameObject cardGO = cardGameObjects[i];
            RectTransform cardRect = cardGO.GetComponent<RectTransform>();
            if (cardRect == null) continue;

            float offsetFromCenter = i - middleIndex;

            // Calculate Position
            float posX = offsetFromCenter * cardSpacing;
            // Apply outward curve based on distance from center
            posX += Mathf.Sign(offsetFromCenter) * Mathf.Pow(Mathf.Abs(offsetFromCenter), 2) * curveFactor * cardSpacing * 0.1f;
            float posY = -Mathf.Abs(offsetFromCenter) * offsetYPerCard; 
           
            // Apply slight curve to Y as well
            posY -= Mathf.Pow(offsetFromCenter, 2) * offsetYPerCard * 0.1f; // Make center higher

            // Calculate Rotation
            float angle = -offsetFromCenter * tiltAnglePerCard; // Negative tilt for Unity's Z rotation

            // Apply Transformations
            cardRect.localPosition = new Vector3(posX, posY, 0);
            cardRect.localRotation = Quaternion.Euler(0, 0, angle);
            
            // Store original transform in handler
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.originalPosition = cardRect.localPosition;
                handler.originalRotation = cardRect.localRotation;
                handler.originalScale = cardRect.localScale;
            }
            else
            {
                 Debug.LogError($"CardDragHandler missing on {cardGO.name} during layout!");
            }
        }
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
                CardDragHandler handler = child.GetComponent<CardDragHandler>();
                if (handler != null)
                {
                    handler.StopAllCoroutines();
                }
                Object.Destroy(child.gameObject);
            }
        }
        Debug.Log("ClearAllHandCardObjects: Destroyed all card GameObjects in hand panel");
    }
    
    public GameObject GetPlayerHandPanel() => playerHandPanel;
    
    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton) endTurnButton.interactable = interactable;
    }
    
    public Button GetEndTurnButton() => endTurnButton;
} 