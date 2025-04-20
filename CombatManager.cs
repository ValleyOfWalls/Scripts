using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq; // Needed for Find
using Newtonsoft.Json;

public class CombatManager
{
    private GameManager gameManager;
    private int startingPlayerHealth;
    private int startingPetHealth;
    private int startingEnergy;

    // --- ADDED: Hand Layout Parameters ---
    [Header("Hand Layout Settings")]
    [SerializeField] private float cardSpacing = 100f; // Horizontal space between card centers
    [SerializeField] private float tiltAnglePerCard = 5f; // Degrees to tilt each card away from center
    [SerializeField] private float offsetYPerCard = 10f; // Vertical offset downwards per card away from center
    [SerializeField] private float curveFactor = 0.5f; // 0 = straight line, 1 = more curve outwards
    // --- END ADDED ---

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

    // Added UI References for Deck View
    private Button viewPlayerDeckButton;
    private Button viewPetDeckButton;
    private Button viewOppPetDeckButton;
    private DeckViewController deckViewController;
    private HandPanelHoverManager handPanelHoverManager;

    private TextMeshProUGUI opponentPetDotText;

    // --- ADDED: Other Fights UI Fields ---
    private GameObject othersStatusArea;
    private TextMeshProUGUI otherPlayerStatusTemplate;
    private int currentTurnNumber = 0;
    // --- END ADDED ---

    // --- ADDED: State for Deck View Toggle ---
    private enum DeckViewType { None, Player, Pet, OpponentPet }
    private DeckViewType currentDeckViewType = DeckViewType.None;
    // --- END ADDED ---

    public void Initialize(GameManager gameManager, int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        this.gameManager = gameManager;
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingEnergy = startingEnergy;
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
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
        
        // --- ADDED: Find Other Fights UI Elements ---
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
        // --- END ADDED ---

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
        
        // --- ADDED: Get HandPanelHoverManager --- 
        if (playerHandPanel != null)
        {
            handPanelHoverManager = playerHandPanel.GetComponent<HandPanelHoverManager>();
            if (handPanelHoverManager == null)
            {
                 Debug.LogError("HandPanelHoverManager component not found on PlayerHandPanel!");
                 // Optionally add it if missing?
                 // handPanelHoverManager = playerHandPanel.AddComponent<HandPanelHoverManager>();
            }
        }
        // --- END ADDED ---
        
        Transform bottomBar = playerArea.Find("BottomBar");
        deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
        discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
        endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
        
        // --- ADDED: Find Deck View Buttons ---
        viewPlayerDeckButton = bottomBar?.Find("ViewPlayerDeckButton")?.GetComponent<Button>();
        viewPetDeckButton = bottomBar?.Find("ViewPetDeckButton")?.GetComponent<Button>();
        viewOppPetDeckButton = bottomBar?.Find("ViewOppPetDeckButton")?.GetComponent<Button>();

        // --- ADDED: Find DeckViewController ---
        // Assuming DeckViewerPanel is instantiated under the CombatCanvas or accessible via GameManager
        GameObject deckViewerPanelInstance = combatInstance.transform.Find("DeckViewerPanel")?.gameObject;
        if (deckViewerPanelInstance == null && gameManager.GetDeckViewerPanelPrefab() != null) // Fallback: Instantiate if not found
        {
            // Instantiate it under the combat canvas so it's part of the UI layer
            deckViewerPanelInstance = Object.Instantiate(gameManager.GetDeckViewerPanelPrefab(), combatInstance.transform);
            deckViewerPanelInstance.name = "DeckViewerPanel"; // Ensure consistent name
        }

        if (deckViewerPanelInstance != null)
        {
            deckViewController = deckViewerPanelInstance.GetComponent<DeckViewController>();
            if (deckViewController == null)
            {
                Debug.LogError("DeckViewController component not found on the DeckViewerPanel instance!");
            }
        }
        else
        {
            Debug.LogError("DeckViewerPanel instance could not be found or instantiated!");
        }

        // Assign listeners
        endTurnButton?.onClick.AddListener(EndTurn);
        // --- ADDED: Assign Deck View Listeners ---
        viewPlayerDeckButton?.onClick.AddListener(ShowPlayerDeck);
        viewPetDeckButton?.onClick.AddListener(ShowPetDeck);
        viewOppPetDeckButton?.onClick.AddListener(ShowOpponentPetDeck);

        // Validate critical findings 
        if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null)
            Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab!");
        else 
            Debug.Log("Successfully found critical combat UI elements.");
    }

    public void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        gameManager.GetPlayerManager().InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
        gameManager.GetCardManager().InitializeDecks();
        
        // --- ADDED: Reset Turn Number ---
        currentTurnNumber = 0; 
        // --- END ADDED ---
        
        // Setup Initial UI
        if (playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        UpdateHealthUI();
        UpdateStatusAndComboUI();
        
        if (opponentPetNameText)
        {
            Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
            opponentPetNameText.text = opponent != null ? $"{opponent.NickName}'s Pet" : "Opponent Pet";
        }
        
        if (ownPetNameText) ownPetNameText.text = gameManager.GetPlayerManager().GetLocalPetName();
        
        UpdateDeckCountUI();
        
        // Start the first turn
        StartTurn();
        
        // --- ADDED: Initial Other Fights UI Update ---
        UpdateOtherFightsUI();
        // --- END ADDED ---
    }

    public void StartTurn()
    {
        Debug.Log("Starting Player Turn");
        PlayerManager playerManager = gameManager.GetPlayerManager();

        // Reset combo counter at the start of the turn
        playerManager.ResetComboCount();

        // Reset block from previous turn
        playerManager.ResetAllBlock();

        // Process turn start effects (like DoT) and decrement buffs/debuffs
        playerManager.ProcessPlayerTurnStartEffects();
        playerManager.ProcessLocalPetTurnStartEffects();
        // Check for deaths after DoT
        if (gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer()) return; // End turn early if player/pet died

        // Reset energy
        playerManager.SetCurrentEnergy(startingEnergy);
        UpdateEnergyUI();

        // --- ADDED: Increment Turn and Publish Status ---
        currentTurnNumber++;
        PublishCombatStatus();
        // --- END ADDED ---

        // Draw cards
        gameManager.GetCardManager().DrawHand();
        UpdateHandUI();
        UpdateDeckCountUI();

        // Set opponent pet intent (Placeholder - should fetch from CardManager/PlayerManager later)
        // if (opponentPetIntentText) opponentPetIntentText.text = "Intent: Attack 5"; // Placeholder

        // Make cards playable and enable end turn button
        if (endTurnButton) endTurnButton.interactable = true;
        // Maybe update card interactability here based on energy etc.

        UpdateStatusAndComboUI();
        
        // --- ADDED: Update Other Fights UI ---
        UpdateOtherFightsUI();
        // --- END ADDED ---
    }

    public void EndTurn()
    {
        Debug.Log("Ending Player Turn");
        if (endTurnButton) endTurnButton.interactable = false; // Prevent double clicks

        // --- ADDED: Process End-of-Turn Hand Effects (Temp Upgrades, etc.) ---
        gameManager.GetCardManager().ProcessEndOfTurnHandEffects();
        // --- END ADDED ---

        // 1. Discard Hand
        gameManager.GetCardManager().DiscardHand();
        UpdateHandUI(); // Clear hand display
        UpdateDeckCountUI();

        // 2. Opponent Pet Acts
        gameManager.GetCardManager().ExecuteOpponentPetTurn(startingEnergy);
        UpdateStatusAndComboUI();

        // 3. Start Next Player Turn (if combat hasn't ended)
        if (!gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer())
        {
            StartTurn();
        }
    }

    // --- ADDED: Method to publish combat status to custom properties ---
    private void PublishCombatStatus()
    {
        if (!PhotonNetwork.InRoom) return; // Safety check

        int opponentPetHealth = gameManager.GetPlayerManager().GetOpponentPetHealth(); 
        int playerHealth = gameManager.GetPlayerManager().GetLocalPlayerHealth(); // Get player health
        
        ExitGames.Client.Photon.Hashtable combatProps = new ExitGames.Client.Photon.Hashtable
        {
            { CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, opponentPetHealth },
            { CombatStateManager.PLAYER_COMBAT_TURN_PROP, currentTurnNumber },
            { CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, playerHealth } // Add player health
        };
        
        PhotonNetwork.LocalPlayer.SetCustomProperties(combatProps);
        // Debug.Log($"Published Combat Status: Turn={currentTurnNumber}, OppPetHP={opponentPetHealth}");
    }
    // --- END ADDED ---

    // --- ADDED: Method to update the Other Fights UI panel ---
    public void UpdateOtherFightsUI()
    {
        if (othersStatusArea == null || otherPlayerStatusTemplate == null)
        {
            // Debug.LogWarning("Cannot update Other Fights UI - references missing.");
            return;
        }

        // Clear previous entries (but keep the template itself)
        foreach (Transform child in othersStatusArea.transform)
        {
            if (child.gameObject != otherPlayerStatusTemplate.gameObject && child.gameObject.name != "OtherStatusTitle") // Don't destroy template or title
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
            int playerHP = -1; // Variable for player health
            bool hpFound = false;
            bool turnFound = false;
            bool playerHpFound = false; // Flag for player health

            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, out object hpObj))
            {
                try { oppPetHP = (int)hpObj; hpFound = true; } catch { /* Ignore */ }
            }
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_TURN_PROP, out object turnObj))
            {
                 try { turnNum = (int)turnObj; turnFound = true; } catch { /* Ignore */ }
            }
            // Try to get player health
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, out object playerHpObj))
            {
                 try { playerHP = (int)playerHpObj; playerHpFound = true; } catch { /* Ignore */ }
            }

            // Only display if we have valid data for all fields now
            if (hpFound && turnFound && playerHpFound)
            {
                GameObject statusEntryGO = Object.Instantiate(otherPlayerStatusTemplate.gameObject, othersStatusArea.transform);
                statusEntryGO.name = $"Status_{player.NickName}"; // Easier debugging
                TextMeshProUGUI statusText = statusEntryGO.GetComponent<TextMeshProUGUI>();
                
                if (statusText != null)
                {
                    // Update text to include player HP
                    statusText.text = $"{player.NickName}: HP:{playerHP} | T{turnNum}, OppHP: {oppPetHP}";
                    statusEntryGO.SetActive(true); // Activate the instantiated entry
                }
                else
                {
                    Object.Destroy(statusEntryGO); // Clean up if component missing
                }
            }
            // Optional: Handle cases where data isn't found (e.g., show "Waiting..." or nothing)
        }
    }
    // --- END ADDED ---

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
        if (ownPetBlockText) ownPetBlockText.text = $"Block: {playerManager.GetLocalPetBlock()}";

        // Opponent Pet Health & Block
        int currentOpponentPetHealth = playerManager.GetOpponentPetHealth();
        int effectiveOpponentPetMaxHealth = playerManager.GetEffectiveOpponentPetMaxHealth();
        if (opponentPetHealthSlider) {
             opponentPetHealthSlider.value = (effectiveOpponentPetMaxHealth > 0) ? (float)currentOpponentPetHealth / effectiveOpponentPetMaxHealth : 0;
        }
        if (opponentPetHealthText) opponentPetHealthText.text = $"{currentOpponentPetHealth} / {effectiveOpponentPetMaxHealth}";
        if (opponentPetBlockText) opponentPetBlockText.text = $"Block: {playerManager.GetOpponentPetBlock()}";

        UpdateStatusAndComboUI();
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

        // --- ADDED: Apply Custom Layout ---
        int numCards = currentCardGOs.Count;
        if (numCards == 0) return; // No layout needed for empty hand

        float middleIndex = (numCards - 1) / 2.0f;

        for (int i = 0; i < numCards; i++)
        {
            GameObject cardGO = currentCardGOs[i];
            RectTransform cardRect = cardGO.GetComponent<RectTransform>();
            if (cardRect == null) continue;

            float offsetFromCenter = i - middleIndex;

            // Calculate Position
            float posX = offsetFromCenter * cardSpacing;
             // Apply outward curve based on distance from center
            posX += Mathf.Sign(offsetFromCenter) * Mathf.Pow(Mathf.Abs(offsetFromCenter), 2) * curveFactor * cardSpacing * 0.1f; // Adjust multiplier for curve strength
            float posY = -Mathf.Abs(offsetFromCenter) * offsetYPerCard; 
           
            // Apply slight curve to Y as well
            posY -= Mathf.Pow(offsetFromCenter, 2) * offsetYPerCard * 0.1f; // Make center higher


            // Calculate Rotation
            float angle = -offsetFromCenter * tiltAnglePerCard; // Negative tilt for Unity's Z rotation (clockwise positive)

            // Apply Transformations
            cardRect.localPosition = new Vector3(posX, posY, 0);
            cardRect.localRotation = Quaternion.Euler(0, 0, angle);
            
            // --- ADDED: Store original transform in handler ---
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.originalPosition = cardRect.localPosition;
                handler.originalRotation = cardRect.localRotation;
                handler.originalScale = cardRect.localScale; // Store original scale too
            }
            else
            {
                 Debug.LogError($"CardDragHandler missing on {cardGO.name} during layout!");
            }
            // --- END ADDED ---
             
            // Ensure pivot is reasonable for rotation (e.g., bottom center)
            // This should ideally be set on the prefab, but can be forced here if needed:
            // cardRect.pivot = new Vector2(0.5f, 0f); 
        }
        // --- END ADDED ---
        
        Debug.Log($"[UpdateHandUI] After layout. Final child count in PlayerHandPanel: {playerHandPanel.transform.childCount}");

        // --- ADDED: Update Hover Manager References ---
        if (handPanelHoverManager != null)
        {
            handPanelHoverManager.UpdateCardReferences();
        }
        // --- END ADDED ---
    }
    
    // --- ADDED: Helper function to update visuals ---
    private void UpdateCardVisuals(GameObject cardGO, CardData card, PlayerManager playerManager, CardManager cardManager)
    {
         bool isTempUpgrade = cardManager.IsCardTemporarilyUpgraded(card);

         Transform headerPanel = cardGO.transform.Find("HeaderPanel");
         Transform descPanel = cardGO.transform.Find("DescPanel");
         Transform artPanel = cardGO.transform.Find("ArtPanel"); // Assuming you might have art

         TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
         // Image artImage = artPanel?.GetComponent<Image>(); // Example if using art
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
             
             // Cost color logic (remains the same)
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
    // --- END ADDED ---

    public void UpdateDeckCountUI()
    {
        CardManager cardManager = gameManager.GetCardManager();
        if (deckCountText != null) deckCountText.text = $"Deck: {cardManager.GetDeckCount()}";
        if (discardCountText != null) discardCountText.text = $"Discard: {cardManager.GetDiscardCount()}";
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

    public bool IsPlayerTurn()
    {
        return !gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        return gameManager.GetCardManager().AttemptPlayCard(cardData, targetType);
    }

    public void DisableEndTurnButton()
    {
        if (endTurnButton) endTurnButton.interactable = false;
    }

    public GameObject GetPlayerHandPanel()
    {
        return playerHandPanel;
    }

    // --- ADDED Deck Viewing Methods ---
    private void ShowPlayerDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.Player)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        List<CardData> fullPlayerDeck = new List<CardData>(cardManager.GetDeck());
        fullPlayerDeck.AddRange(cardManager.GetDiscardPile());
        string title = $"{PhotonNetwork.LocalPlayer.NickName}'s Deck ({fullPlayerDeck.Count})";
        deckViewController.ShowDeck(title, fullPlayerDeck);
        currentDeckViewType = DeckViewType.Player;
    }

    private void ShowPetDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.Pet)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        List<CardData> petDeck = cardManager.GetLocalPetDeck() ?? new List<CardData>();
        string title = $"{gameManager.GetPlayerManager().GetLocalPetName()}'s Deck ({petDeck.Count})";
        deckViewController.ShowDeck(title, petDeck);
        currentDeckViewType = DeckViewType.Pet;
    }

    private void ShowOpponentPetDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.OpponentPet)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
        CardManager cardManager = gameManager.GetCardManager();
        if (opponent == null || cardManager == null) return;

        List<CardData> opponentPetDeck = cardManager.GetOpponentPetDeck() ?? new List<CardData>();
        string opponentPetName = opponent.NickName + "'s Pet";
        if (string.IsNullOrEmpty(opponent.NickName)) opponentPetName = "Opponent Pet";
        string title = $"{opponentPetName} Deck ({opponentPetDeck.Count})";

        deckViewController.ShowDeck(title, opponentPetDeck);
        currentDeckViewType = DeckViewType.OpponentPet;
    }
    // --- END ADDED Deck Viewing Methods ---

    // --- ADDED: Update Status Effects and Combo UI ---
    public void UpdateStatusAndComboUI()
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
    // --- END ADDED ---

    // New method to ensure all card GameObjects are properly destroyed
    private void ClearAllHandCardObjects()
    {
        if (playerHandPanel == null) return;
        
        // Destroy all card GameObjects except the template
        foreach (Transform child in playerHandPanel.transform)
        {
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
        }
        Debug.Log("ClearAllHandCardObjects: Destroyed all card GameObjects in hand panel");
    }
}