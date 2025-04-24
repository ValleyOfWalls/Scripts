using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections;

public class CardManager
{
    private GameManager gameManager;
    
    // Card data and configuration
    private List<CardData> starterDeck = new List<CardData>();
    private List<CardData> starterPetDeck = new List<CardData>();
    private List<CardData> allPlayerCardPool = new List<CardData>();
    private List<CardData> allPetCardPool = new List<CardData>();
    private int cardsToDraw = 5;
    
    // Manager instances
    private DeckManager deckManager;
    private PetDeckManager petDeckManager;
    private CardEffectService cardEffectService;
    private CardModificationService cardModificationService;
    private DraftService draftService;
    
    // --- ADDED: Play Count Tracking ---
    private Dictionary<CardData, int> cardPlayCountsThisCombat = new Dictionary<CardData, int>();
    // --- END ADDED ---
    
    // Constants (maintaining the original API for other classes)
    // Draft constants
    public const string DRAFT_PLAYER_QUEUES_PROP = "DraftQueues";
    public const string DRAFT_PICKS_MADE_PROP = "DraftPicks";
    public const string DRAFT_ORDER_PROP = "DraftOrder";
    public const string PLAYER_PET_DECK_PROP = "PetDeck";
    
    public CardManager(GameManager gameManager, List<CardData> starterDeck, List<CardData> starterPetDeck,
                      List<CardData> allPlayerCardPool, List<CardData> allPetCardPool, int cardsToDraw)
    {
        this.gameManager = gameManager;
        this.starterDeck = new List<CardData>(starterDeck);
        this.starterPetDeck = new List<CardData>(starterPetDeck);
        this.allPlayerCardPool = new List<CardData>(allPlayerCardPool);
        this.allPetCardPool = new List<CardData>(allPetCardPool);
        this.cardsToDraw = cardsToDraw;
        
        // Initialize sub-managers
        this.deckManager = new DeckManager(gameManager, starterDeck, cardsToDraw);
        this.petDeckManager = new PetDeckManager(gameManager, starterPetDeck, allPetCardPool);
        this.cardEffectService = new CardEffectService(gameManager);
        this.cardModificationService = new CardModificationService(gameManager);
        this.draftService = new DraftService(gameManager, allPlayerCardPool, allPetCardPool);
    }
    
    public void InitializeAndSyncLocalPetDeck()
    {
        petDeckManager.InitializeAndSyncLocalPetDeck();
    }
    
    public void InitializeDecks()
    {
        deckManager.InitializeDecks();
        petDeckManager.ShuffleLocalPetDeck();
        ResetCombatCardPlayCounts(); // Reset play counts when decks are initialized for combat
    }
    
    // --- ADDED: Reset Play Counts ---
    public void ResetCombatCardPlayCounts()
    {
        cardPlayCountsThisCombat.Clear();
        Debug.Log("Combat card play counts reset.");
    }
    // --- END ADDED ---
    
    public void DrawHand()
    {
        // Before drawing, ensure no lingering temp upgrades from previous states
        cardModificationService.RevertExpiredHandUpgrades(true); 
        deckManager.DrawHand();
    }
    
    public void DrawCard()
    {
        deckManager.DrawCard();
    }
    
    public void DiscardHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(deckManager.GetHand());

        // Trigger discard animations *before* actually discarding
        CombatUIManager combatUIManager = gameManager.GetCombatUIManager();
        if (combatUIManager != null)
        {
            foreach(CardData card in cardsToDiscard)
            {
                combatUIManager.TriggerDiscardAnimation(card);
            }
        }
        else
        {
            Debug.LogWarning("CombatUIManager not found, cannot trigger discard animations.");
        }

        deckManager.DiscardHand();
        
        // Process discard triggers for each card (after they are moved to discard pile)
        foreach(CardData card in cardsToDiscard)
        {
            // Remove any temporary upgrade tracking
            // This is now handled by CardModificationService during end of turn processing
            
            // Remove cost modifier tracking
            gameManager.GetPlayerManager().RemoveCostModifierForCard(card);
            
            // Process discard trigger
            HandleDiscardTrigger(card, CardDropZone.TargetType.PlayerSelf);
        }
    }
    
    public void HandleDiscardTrigger(CardData card, CardDropZone.TargetType targetType = CardDropZone.TargetType.PlayerSelf)
    {
        cardEffectService.HandleDiscardTrigger(card, targetType);
    }
    
    // --- MODIFIED: Accept optional GameObject --- 
    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType, GameObject cardGO = null)
    {
        Debug.Log($"CardManager.AttemptPlayCard called: Card '{cardData.cardName}', TargetType '{targetType}'");
        
        // Basic checks
        if (cardData == null)
        {
            Debug.LogError("AttemptPlayCard: cardData is null!");
            return false;
        }
        
        // Energy Check with Modifier
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int cardSpecificModifier = playerManager.GetLocalHandCostModifier(cardData);
        int effectiveCost = Mathf.Max(0, cardData.cost + cardSpecificModifier);
        
        if (effectiveCost > playerManager.GetCurrentEnergy())
        {
            Debug.LogWarning($"AttemptPlayCard: Cannot play card '{cardData.cardName}' - Effective Cost ({effectiveCost}) exceeds current energy ({playerManager.GetCurrentEnergy()}).");
            return false; // Not enough energy
        }
        
        // --- ADDED: Check for debug card reuse mode ---
        bool debugCardReuseMode = gameManager.IsDebugCardReuseMode();
        bool removed = false;
        
        if (!debugCardReuseMode)
        {
            // Normal behavior - Attempt to remove the card from the hand list
            // --- MODIFIED: Pass cardGO to DiscardCard ---
            removed = deckManager.DiscardCard(cardData, cardGO); 
            Debug.Log($"Attempting to remove card '{cardData.cardName}' from hand. Result: {removed}");
        }
        else
        {
            // Debug mode - Skip removing card from hand
            Debug.Log($"Debug Card Reuse Mode: Card '{cardData.cardName}' will NOT be discarded when played");
            removed = true; // Pretend the card was successfully removed
        }
        // --- END ADDED ---
        
        if (removed)
        {
            // Remove cost modifier tracking for played card
            playerManager.RemoveCostModifierForCard(cardData);
            
            // --- ADDED: Only update UI if not in debug reuse mode ---
            if (!debugCardReuseMode)
            {
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
            }
            // --- END ADDED ---
            
            // Consume energy (using effective cost)
            // --- MODIFIED: Skip energy consumption in debug mode ---
            if (!debugCardReuseMode)
            {
                playerManager.ConsumeEnergy(effectiveCost);
                Debug.Log($"Played card '{cardData.cardName}'. Energy remaining: {playerManager.GetCurrentEnergy()}");
            }
            else
            {
                Debug.Log($"Debug Card Reuse Mode: Energy NOT consumed for '{cardData.cardName}'");
            }
            // --- END MODIFIED ---
            
            // --- MODIFIED: Get play count BEFORE processing effects --- 
            int previousPlays = GetCardPlayCountThisCombat(cardData);
            // --- ADDED: Get copy count BEFORE processing effects ---
            int totalCopies = GetTotalCardCopies(cardData);
            
            // Apply card effects via CardEffectService
            // --- MODIFIED: Pass totalCopies --- 
            bool effectsProcessed = cardEffectService.ProcessCardEffect(cardData, targetType, previousPlays, totalCopies);

            // Update health/status UI AFTER effects are processed
            if (effectsProcessed)
            {
                gameManager.UpdateHealthUI(); // This will also update status effects like combo
                
                // --- MODIFIED: Increment play count AFTER successful processing --- 
                cardPlayCountsThisCombat[cardData] = previousPlays + 1;
                Debug.Log($"Incremented play count for {cardData.cardName} to {previousPlays + 1}");
            }
            
            Debug.Log($"Successfully processed effects for card '{cardData.cardName}' on target '{targetType}'.");
            return effectsProcessed;
        }
        else
        {
            Debug.LogWarning($"AttemptPlayCard: Card '{cardData.cardName}' NOT found in hand list for removal (reference equality failed?).");
            return false;
        }
    }
    
    // --- ADDED: Get Play Count ---
    public int GetCardPlayCountThisCombat(CardData cardData)
    {
        if (cardData == null) return 0;
        return cardPlayCountsThisCombat.GetValueOrDefault(cardData, 0);
    }
    // --- END ADDED ---
    
    // --- ADDED: Get Total Card Copies ---
    public int GetTotalCardCopies(CardData cardData)
    {
        if (cardData == null || string.IsNullOrEmpty(cardData.cardFamilyName))
        {
            Debug.LogWarning($"GetTotalCardCopies: Invalid CardData or missing cardFamilyName for '{cardData?.cardName}'. Returning 0.");
            return 0;
        }

        string familyNameToCount = cardData.cardFamilyName;
        List<CardData> allCards = deckManager.GetAllOwnedPlayerCards(); // Includes deck, hand, discard
        int count = allCards.Count(card => card != null && card.cardFamilyName == familyNameToCount);
        
        // Debug.Log($"GetTotalCardCopies for family '{familyNameToCount}': Found {count} copies.");
        return count;
    }
    // --- END ADDED ---
    
    public void ProcessOpponentPetCardEffect(CardData cardToPlay)
    {
        cardEffectService.ProcessOpponentPetCardEffect(cardToPlay);
    }
    
    public void ProcessEndOfTurnHandEffects()
    {
        cardModificationService.ProcessEndOfTurnHandEffects();
    }
    
    public IEnumerator ExecuteOpponentPetTurn(int startingPetEnergy)
    {
        Debug.Log("CardManager forwarding Opponent Pet Turn execution.");
        IEnumerator petTurnEnumerator = petDeckManager.ExecuteOpponentPetTurn(startingPetEnergy);
        while (petTurnEnumerator.MoveNext()) {
            // Explicitly yield whatever the PetDeckManager yields (should be CardData or null)
            yield return petTurnEnumerator.Current;
        }
        Debug.Log("CardManager finished forwarding Opponent Pet Turn execution.");
    }
    
    public void InitializeOpponentPetDeck(List<string> cardNames)
    {
        petDeckManager.InitializeOpponentPetDeck(cardNames);
    }
    
    // ----- Draft Methods -----
    
    public void InitializeDraftState(int optionsPerDraft)
    {
        draftService.InitializeDraftState(optionsPerDraft);
    }
    
    public void HandlePlayerDraftPick(int optionId)
    {
        draftService.HandlePlayerDraftPick(optionId);
    }
    
    public void UpdateLocalDraftStateFromRoomProps()
    {
        draftService.UpdateLocalDraftStateFromRoomProps();
    }
    
    public void UpdateLocalDraftPicksFromRoomProps()
    {
        draftService.UpdateLocalDraftPicksFromRoomProps();
    }
    
    public void UpdateLocalDraftOrderFromRoomProps()
    {
        draftService.UpdateLocalDraftOrderFromRoomProps();
    }
    
    // ----- Getters -----
    
    // Core hand/deck accessors
    public List<CardData> GetDeck() => deckManager.GetDeck();
    public List<CardData> GetHand() => deckManager.GetHand();
    public List<CardData> GetDiscardPile() => deckManager.GetDiscardPile();
    public List<CardData> GetLocalPetDeck() => petDeckManager.GetLocalPetDeck();
    public List<CardData> GetOpponentPetDeck() => petDeckManager.GetOpponentPetDeck();
    public int GetDeckCount() => deckManager.GetDeckCount();
    public int GetDiscardCount() => deckManager.GetDiscardCount();
    public int GetHandCount() => deckManager.GetHandCount();
    
    // Deck view helpers
    public List<CardData> GetAllOwnedPlayerCards() => deckManager.GetAllOwnedPlayerCards();
    public List<CardData> GetAllOwnedPetCards() => petDeckManager.GetAllOwnedPetCards();
    
    // Draft state access
    public bool IsWaitingForLocalPick() => draftService.IsWaitingForLocalPick();
    public void SetWaitingForLocalPick(bool value) => draftService.SetWaitingForLocalPick(value);
    public Dictionary<int, DraftOption> GetLocalActiveOptionMap() => draftService.GetLocalActiveOptionMap();
    public List<SerializableDraftOption> GetLocalCurrentPack() => draftService.GetLocalCurrentPack();
    
    // Card modification checks
    public bool IsCardTemporarilyUpgraded(CardData card) => cardModificationService.IsCardTemporarilyUpgraded(card);
    
    // Manager accessors
    public DeckManager GetDeckManager() => deckManager;
    public PetDeckManager GetPetDeckManager() => petDeckManager;
    public CardEffectService GetCardEffectService() => cardEffectService;
    public CardModificationService GetCardModificationService() => cardModificationService;
    public DraftService GetDraftService() => draftService;
}