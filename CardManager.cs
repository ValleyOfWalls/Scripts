using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class CardManager
{
    private GameManager gameManager;
    
    // Card data
    private List<CardData> starterDeck = new List<CardData>();
    private List<CardData> starterPetDeck = new List<CardData>();
    private List<CardData> allPlayerCardPool = new List<CardData>();
    private List<CardData> allPetCardPool = new List<CardData>();
    
    // Player deck state
    private List<CardData> deck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    
    // Pet deck state
    private List<CardData> localPetDeck = new List<CardData>();
    
    // Opponent pet state (for local simulation)
    private int opponentPetEnergy;
    private List<CardData> opponentPetDeck = new List<CardData>();
    private List<CardData> opponentPetHand = new List<CardData>();
    private List<CardData> opponentPetDiscard = new List<CardData>();
    
    // Draft state
    private List<SerializableDraftOption> localCurrentPack = new List<SerializableDraftOption>();
    private Dictionary<int, DraftOption> localActiveOptionMap = new Dictionary<int, DraftOption>();
    private List<int> draftPlayerOrder = new List<int>();
    private Dictionary<int, int> draftPicksMade = new Dictionary<int, int>();
    private bool isWaitingForLocalPick = false;
    
    // Draft constants
    public const string DRAFT_PLAYER_QUEUES_PROP = "DraftQueues";
    public const string DRAFT_PICKS_MADE_PROP = "DraftPicks";
    public const string DRAFT_ORDER_PROP = "DraftOrder";
    public const string PLAYER_PET_DECK_PROP = "PetDeck"; // New property key
    
    // Card configuration
    private int cardsToDraw = 5;
    
    public CardManager(GameManager gameManager, List<CardData> starterDeck, List<CardData> starterPetDeck,
                      List<CardData> allPlayerCardPool, List<CardData> allPetCardPool, int cardsToDraw)
    {
        this.gameManager = gameManager;
        this.starterDeck = new List<CardData>(starterDeck);
        this.starterPetDeck = new List<CardData>(starterPetDeck);
        this.allPlayerCardPool = new List<CardData>(allPlayerCardPool);
        this.allPetCardPool = new List<CardData>(allPetCardPool);
        this.cardsToDraw = cardsToDraw;
    }
    
    // Call this ONCE early in GameManager.Awake
    public void InitializeAndSyncLocalPetDeck()
    {
        if (localPetDeck == null || localPetDeck.Count == 0)
        {
            localPetDeck = new List<CardData>(starterPetDeck ?? new List<CardData>());
            Debug.Log($"InitializeAndSyncLocalPetDeck: Initialized localPetDeck from starter. Count: {localPetDeck.Count}");
            // Sync this initial state immediately
            SyncLocalPetDeckToCustomProperties();
        }
        else
        {
            Debug.Log("InitializeAndSyncLocalPetDeck: localPetDeck already initialized.");
            // Optional: Re-sync if needed, but likely not necessary here.
            // SyncLocalPetDeckToCustomProperties();
        }
    }

    public void InitializeDecks()
    {
        // Check if this is likely the first round (deck, hand, discard are empty)
        bool isFirstRound = (deck.Count == 0 && hand.Count == 0 && discardPile.Count == 0);
        if (isFirstRound)
        {
            Debug.Log("First round detected: Initializing player deck from starterDeck.");
            deck = new List<CardData>(starterDeck);
        }
        else
        {
            // Subsequent rounds: Combine hand/discard into deck
            Debug.Log("Subsequent round detected: Reshuffling existing player deck/hand/discard.");
            discardPile.AddRange(hand);
            hand.Clear();
            deck.AddRange(discardPile);
            discardPile.Clear();
        }
        ShuffleDeck(); // Shuffle the full player deck (either new or combined)

        // Ensure Local Pet Deck exists (should have been initialized in Awake)
        if (localPetDeck == null) // Add null check just in case
        {
            Debug.LogWarning("InitializeDecks: localPetDeck was null! Re-initializing from starter. This might indicate an issue.");
            localPetDeck = new List<CardData>(starterPetDeck ?? new List<CardData>());
        }
        // We only need to shuffle the local pet deck here, not re-initialize or sync it.
        ShuffleLocalPetDeck();

        // NOTE: Opponent Pet Deck initialization is now handled by InitializeOpponentPetDeck
        // which is called AFTER this method by PlayerManager during combat setup.
        // We still need to clear the opponent's hand/discard from the previous round here.
        opponentPetHand.Clear();
        opponentPetDiscard.Clear();
    }
    
    public void DrawHand()
    {
        hand.Clear();
        for (int i = 0; i < cardsToDraw; i++)
        {
            DrawCard();
        }
    }
    
    public void DrawCard()
    {
        if (deck.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("No cards left to draw!");
                return; // Out of cards
            }
            ReshuffleDiscardPile();
        }
        
        CardData drawnCard = deck[0];
        deck.RemoveAt(0);
        hand.Add(drawnCard);
    }
    
    public void DiscardHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(hand); // Create a copy to iterate over
        hand.Clear();
        foreach(CardData card in cardsToDiscard)
        {
            discardPile.Add(card);
            HandleDiscardTrigger(card); // ADDED: Trigger discard effect
        }
        // UI update likely happens in CombatManager after calling this
    }
    
    public void ReshuffleDiscardPile()
    {
        Debug.Log("Reshuffling discard pile into deck.");
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
    }
    
    public void ShuffleDeck()
    {
        System.Random rng = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = deck[k];
            deck[k] = deck[n];
            deck[n] = value;
        }
        Debug.Log("Deck shuffled.");
    }
    
    public void ShuffleLocalPetDeck()
    {
        System.Random rng = new System.Random();
        int n = localPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = localPetDeck[k];
            localPetDeck[k] = localPetDeck[n];
            localPetDeck[n] = value;
        }
        Debug.Log("Local Pet Deck shuffled.");
    }
    
    public void ShuffleOpponentPetDeck()
    {
        System.Random rng = new System.Random();
        int n = opponentPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = opponentPetDeck[k];
            opponentPetDeck[k] = opponentPetDeck[n];
            opponentPetDeck[n] = value;
        }
        Debug.Log("Opponent Pet Deck shuffled.");
    }
    
    public void DrawOpponentPetHand(int amountToDraw)
    {
        opponentPetHand.Clear(); // Clear previous hand
        for (int i = 0; i < amountToDraw; i++)
        {
            DrawOpponentPetCard();
        }
        Debug.Log($"Opponent Pet drew {opponentPetHand.Count} cards.");
    }
    
    public void DrawOpponentPetCard()
    {
        if (opponentPetDeck.Count == 0)
        {
            if (opponentPetDiscard.Count == 0)
            {
                Debug.Log("Opponent Pet has no cards left to draw!");
                return; // Out of cards
            }
            ReshuffleOpponentPetDiscardPile();
        }
        
        if (opponentPetDeck.Count > 0) // Check again after potential reshuffle
        {
            CardData drawnCard = opponentPetDeck[0];
            opponentPetDeck.RemoveAt(0);
            opponentPetHand.Add(drawnCard);
        }
    }
    
    public void DiscardOpponentPetHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(opponentPetHand); // Create a copy
        opponentPetHand.Clear();
        foreach(CardData card in cardsToDiscard)
        {
            opponentPetDiscard.Add(card);
            // Note: We generally don't trigger opponent discard effects on the local client
            // HandleDiscardTrigger(card); // Maybe only for specific effects or debugging?
        }
    }
    
    public void ReshuffleOpponentPetDiscardPile()
    {
        Debug.Log("Reshuffling Opponent Pet discard pile into deck.");
        opponentPetDeck.AddRange(opponentPetDiscard);
        opponentPetDiscard.Clear();
        ShuffleOpponentPetDeck();
    }
    
    public void ExecuteOpponentPetTurn(int startingEnergy)
    {
        Debug.Log("---> Starting Opponent Pet Turn <---");
        opponentPetEnergy = startingEnergy;
        
        // --- REVISED: Process Turn Start Effects (includes status decrement) ---
        gameManager.GetPlayerManager().ProcessOpponentPetTurnStartEffects();
        // --- END REVISED ---
        
        // Determine how many cards the pet should draw (can be different from player)
        int petCardsToDraw = 3; // Example: Pet draws fewer cards
        DrawOpponentPetHand(petCardsToDraw);
        
        // Simple AI: Play cards until out of energy or no playable cards left
        bool cardPlayedThisLoop;
        do
        {
            cardPlayedThisLoop = false;
            CardData cardToPlay = null;
            int cardIndex = -1;
            
            // Find the first playable card in hand
            for(int i = 0; i < opponentPetHand.Count; i++)
            {
                if (opponentPetHand[i].cost <= opponentPetEnergy)
                {
                    cardToPlay = opponentPetHand[i];
                    cardIndex = i;
                    break; // Found one, stop looking
                }
            }
            
            // If a playable card was found
            if (cardToPlay != null)
            {
                Debug.Log($"Opponent Pet playing card: {cardToPlay.cardName} (Cost: {cardToPlay.cost})");
                opponentPetEnergy -= cardToPlay.cost;
                
                // Apply effect (Example: Damage to Player)
                if (cardToPlay.damage > 0)
                {
                    // --- ADDED: Check Attacker (Opponent Pet) Weakness --- 
                    int actualDamage = cardToPlay.damage;
                    if (gameManager.GetPlayerManager().IsOpponentPetWeak()) {
                        // Example: 25% reduction, rounded down
                        int reduction = Mathf.FloorToInt(actualDamage * 0.25f); 
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Opponent Pet is Weak! Reducing damage from {cardToPlay.damage} to {actualDamage}");
                    }
                    // --- END ADDED ---
                    
                    gameManager.GetPlayerManager().DamageLocalPlayer(actualDamage); // Use potentially reduced damage
                    Debug.Log($"Opponent Pet dealt {actualDamage} damage to Local Player. New health: {gameManager.GetPlayerManager().GetLocalPlayerHealth()}, Block: {gameManager.GetPlayerManager().GetLocalPlayerBlock()}");
                    // Player defeat check is handled in DamageLocalPlayer
                }
                
                // --- ADDED: Apply Opponent Pet Block --- 
                if (cardToPlay.block > 0)
                {
                    // Apply block to the opponent's pet (from the local player's perspective)
                    gameManager.GetPlayerManager().AddBlockToOpponentPet(cardToPlay.block);
                    Debug.Log($"Opponent Pet gained {cardToPlay.block} block. New block (local sim): {gameManager.GetPlayerManager().GetOpponentPetBlock()}");
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Opponent Pet Energy Gain --- 
                if (cardToPlay.energyGain > 0)
                {
                    // Note: This currently only affects the opponent's simulated energy for THIS turn's card playing.
                    // If energy carries over, more complex tracking might be needed.
                    opponentPetEnergy += cardToPlay.energyGain;
                    Debug.Log($"Opponent Pet gained {cardToPlay.energyGain} energy. New energy: {opponentPetEnergy}");
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Opponent Pet Draw --- 
                if (cardToPlay.drawAmount > 0)
                {
                    Debug.Log($"Opponent Pet drawing {cardToPlay.drawAmount} cards.");
                    for(int d=0; d < cardToPlay.drawAmount; d++)
                    {
                        DrawOpponentPetCard();
                    }
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Opponent Pet Discard --- 
                if (cardToPlay.discardRandomAmount > 0)
                {
                    Debug.Log($"Opponent Pet discarding {cardToPlay.discardRandomAmount} random cards.");
                    DiscardRandomOpponentPetCards(cardToPlay.discardRandomAmount);
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Opponent Pet Healing --- 
                if (cardToPlay.healingAmount > 0)
                {
                    gameManager.GetPlayerManager().HealOpponentPet(cardToPlay.healingAmount);
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Opponent Pet Temp Max HP --- 
                if (cardToPlay.tempMaxHealthChange != 0)
                {
                    gameManager.GetPlayerManager().ApplyTempMaxHealthOpponentPet(cardToPlay.tempMaxHealthChange);
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply Status Effects (Opponent Pet applying to Player) --- 
                if (cardToPlay.statusToApply != StatusEffectType.None && cardToPlay.statusDuration > 0)
                {
                     // Opponent pet usually applies status to the player
                     gameManager.GetPlayerManager().ApplyStatusEffectLocalPlayer(cardToPlay.statusToApply, cardToPlay.statusDuration);
                }
                // --- END ADDED ---
                
                // --- ADDED: Apply DoT (Opponent Pet applying to Player) --- 
                if (cardToPlay.dotDamageAmount > 0 && cardToPlay.dotDuration > 0)
                {
                    gameManager.GetPlayerManager().ApplyDotLocalPlayer(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
                }
                // --- END ADDED ---
                
                // Move card from hand to discard
                opponentPetHand.RemoveAt(cardIndex);
                opponentPetDiscard.Add(cardToPlay);
                // Don't trigger discard effect for the played card itself
                cardPlayedThisLoop = true; // Indicate a card was played, loop again
                
                Debug.Log($"Opponent Pet energy remaining: {opponentPetEnergy}");
            }
            
        } while (cardPlayedThisLoop && opponentPetEnergy > 0); // Continue if a card was played and energy remains
        
        Debug.Log("Opponent Pet finished playing cards.");
        DiscardOpponentPetHand();
        Debug.Log("---> Ending Opponent Pet Turn <---");
    }
    
    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        Debug.Log($"CardManager.AttemptPlayCard called: Card '{cardData.cardName}', TargetType '{targetType}'");
        
        // Basic checks (add more like energy cost)
        if (cardData == null)
        {
            Debug.LogError("AttemptPlayCard: cardData is null!");
            return false;
        }
        
        // Energy check
        if (cardData.cost > gameManager.GetPlayerManager().GetCurrentEnergy())
        {
            Debug.LogWarning($"AttemptPlayCard: Cannot play card '{cardData.cardName}' - Cost ({cardData.cost}) exceeds current energy ({gameManager.GetPlayerManager().GetCurrentEnergy()}).");
            return false; // Not enough energy
        }
        
        // Attempt to remove the card from the hand list
        bool removed = hand.Remove(cardData);
        Debug.Log($"Attempting to remove card '{cardData.cardName}' from hand. Result: {removed}");
        
        if (removed)
        {
            discardPile.Add(cardData);
            gameManager.UpdateHandUI(); // Update visual hand
            gameManager.UpdateDeckCountUI(); // Update discard count display
            
            // Consume energy
            gameManager.GetPlayerManager().ConsumeEnergy(cardData.cost);
            Debug.Log($"Played card '{cardData.cardName}'. Energy remaining: {gameManager.GetPlayerManager().GetCurrentEnergy()}");
            
            // Apply card effects based on cardData and targetType
            // --- REVISED: Apply effects based on target --- 
            switch (targetType)
            {
                case CardDropZone.TargetType.EnemyPet:
                    if (cardData.damage > 0)
                    {
                        // --- ADDED: Check Attacker (Player) Weakness --- 
                        int actualDamage = cardData.damage;
                        if (gameManager.GetPlayerManager().IsPlayerWeak()) {
                            int reduction = Mathf.FloorToInt(actualDamage * 0.25f);
                            actualDamage = Mathf.Max(0, actualDamage - reduction);
                             Debug.Log($"Player is Weak! Reducing damage from {cardData.damage} to {actualDamage}");
                        }
                        // --- END ADDED ---
                        
                        gameManager.GetPlayerManager().DamageOpponentPet(actualDamage); // Use potentially reduced damage
                        Debug.Log($"Dealt {actualDamage} damage to Opponent Pet. New health: {gameManager.GetPlayerManager().GetOpponentPetHealth()}, Est. Block: {gameManager.GetPlayerManager().GetOpponentPetBlock()}");
                        // Combat win check is handled in DamageOpponentPet
                    }
                    // --- ADDED: Healing & Temp Max HP for Enemy Pet --- 
                    if (cardData.healingAmount > 0)
                    {
                         gameManager.GetPlayerManager().HealOpponentPet(cardData.healingAmount);
                    }
                     if (cardData.tempMaxHealthChange != 0)
                    {
                        gameManager.GetPlayerManager().ApplyTempMaxHealthOpponentPet(cardData.tempMaxHealthChange);
                    }
                    // --- ADDED: Apply Status Effect to Enemy Pet --- 
                    if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyStatusEffectOpponentPet(cardData.statusToApply, cardData.statusDuration);
                    }
                    // --- ADDED: Apply DoT to Enemy Pet --- 
                    if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyDotOpponentPet(cardData.dotDamageAmount, cardData.dotDuration);
                    }
                    break;
                    
                case CardDropZone.TargetType.PlayerSelf:
                    if (cardData.block > 0)
                    {
                        gameManager.GetPlayerManager().AddBlockToLocalPlayer(cardData.block);
                    }
                    // --- ADDED: Healing & Temp Max HP for Player --- 
                    if (cardData.healingAmount > 0)
                    {
                        gameManager.GetPlayerManager().HealLocalPlayer(cardData.healingAmount);
                    }
                    if (cardData.tempMaxHealthChange != 0)
                    {
                        gameManager.GetPlayerManager().ApplyTempMaxHealthPlayer(cardData.tempMaxHealthChange);
                    }
                    // --- ADDED: Apply Status Effect to Player --- 
                    if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyStatusEffectLocalPlayer(cardData.statusToApply, cardData.statusDuration);
                    }
                    // --- ADDED: Apply DoT to Player --- 
                    if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyDotLocalPlayer(cardData.dotDamageAmount, cardData.dotDuration);
                    }
                    break;
                    
                case CardDropZone.TargetType.OwnPet:
                    if (cardData.block > 0)
                    {
                        gameManager.GetPlayerManager().AddBlockToLocalPet(cardData.block);
                    }
                    // --- ADDED: Healing & Temp Max HP for Own Pet --- 
                    if (cardData.healingAmount > 0)
                    {
                        gameManager.GetPlayerManager().HealLocalPet(cardData.healingAmount);
                    }
                    if (cardData.tempMaxHealthChange != 0)
                    {
                        gameManager.GetPlayerManager().ApplyTempMaxHealthPet(cardData.tempMaxHealthChange);
                    }
                    // --- ADDED: Apply Status Effect to Own Pet --- 
                    if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyStatusEffectLocalPet(cardData.statusToApply, cardData.statusDuration);
                    }
                    // --- ADDED: Apply DoT to Own Pet --- 
                    if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                    {
                        gameManager.GetPlayerManager().ApplyDotLocalPet(cardData.dotDamageAmount, cardData.dotDuration);
                    }
                    break;
                    
                // Add more cases if needed (e.g., TargetType.Self for non-combat effects)
            }
            
            // --- ADDED: Apply energy gain (target independent) --- 
            if (cardData.energyGain > 0)
            {
                gameManager.GetPlayerManager().GainEnergy(cardData.energyGain);
            }
            // --- END ADDED ---
            
            // --- ADDED: Apply Draw Card --- 
            if (cardData.drawAmount > 0)
            {
                Debug.Log($"Drawing {cardData.drawAmount} cards.");
                for(int d=0; d < cardData.drawAmount; d++)
                {
                    DrawCard();
                }
                gameManager.UpdateHandUI(); // Update hand after drawing
                gameManager.UpdateDeckCountUI();
            }
            // --- END ADDED ---
            
            // --- ADDED: Apply Discard Random --- 
            if (cardData.discardRandomAmount > 0)
            {
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from hand.");
                DiscardRandomPlayerCards(cardData.discardRandomAmount);
                gameManager.UpdateHandUI(); // Update hand after discarding
                gameManager.UpdateDeckCountUI();
            }
            // --- END ADDED ---
            
            // --- ADDED: Combo Check --- 
            if (cardData.isComboStarter)
            {
                PlayerManager playerManager = gameManager.GetPlayerManager();
                playerManager.IncrementComboCount();
                if (playerManager.GetCurrentComboCount() >= cardData.comboTriggerValue)
                {
                    Debug.Log($"COMBO TRIGGERED! ({playerManager.GetCurrentComboCount()}/{cardData.comboTriggerValue}) Applying effect: {cardData.comboEffectType}");
                    ApplyComboEffect(cardData);
                    // Optional: Reset combo count immediately after trigger?
                    // playerManager.ResetComboCount(); 
                }
            }
            // --- END ADDED ---
            
            Debug.Log($"Successfully processed effects for card '{cardData.cardName}' on target '{targetType}'. Moved to discard.");
            // Note: Discard trigger for the PLAYED card is NOT activated here.
            return true; // Indicate success
        }
        else
        {
            Debug.LogWarning($"AttemptPlayCard: Card '{cardData.cardName}' NOT found in hand list for removal (reference equality failed?).");
            if (hand.Any(c => c != null && c.cardName == cardData.cardName)) {
                Debug.LogWarning($"--> NOTE: A card named '{cardData.cardName}' DOES exist in hand, but the reference passed from CardDragHandler doesn't match.");
            }
            else
            {
                Debug.LogWarning($"--> NOTE: No card named '{cardData.cardName}' found in hand list at all.");
            }
            return false; // Card wasn't in hand (or reference didn't match)
        }
    }
    
    public void InitializeDraftState(int optionsPerDraft)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log("Master Client initializing draft state (creating queues)... ");
        
        Dictionary<int, List<string>> initialQueues = new Dictionary<int, List<string>>();
        Dictionary<int, int> initialPicks = new Dictionary<int, int>();
        int totalOptionsGenerated = 0;
        int optionIdCounter = 0;
        
        // Determine player order (simple example: based on ActorNumber)
        List<int> playerOrder = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(n => n).ToList();
        string playerOrderJson = JsonConvert.SerializeObject(playerOrder);
        
        int initialOptionsPerPack = optionsPerDraft;
        
        // Create one pack for each player
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            List<SerializableDraftOption> packForPlayer = new List<SerializableDraftOption>();
            Debug.Log($"Generating pack for player {player.NickName} ({player.ActorNumber})");
            
            for (int i = 0; i < initialOptionsPerPack; i++)
            {
                optionIdCounter++;
                SerializableDraftOption newOption = GenerateSingleDraftOption(optionIdCounter);
                if (newOption != null)
                {
                    packForPlayer.Add(newOption);
                    totalOptionsGenerated++;
                }
                else i--; // Try again if generation failed (e.g., pools empty)
            }
            
            // Shuffle the pack for this player
            System.Random packRng = new System.Random();
            packForPlayer = packForPlayer.OrderBy(a => packRng.Next()).ToList();
            
            // Add the serialized pack as the first element in the player's queue
            initialQueues[player.ActorNumber] = new List<string> { JsonConvert.SerializeObject(packForPlayer) };
            
            initialPicks[player.ActorNumber] = 0; // Initialize picks made to 0
        }
        
        // Serialize dictionaries for network
        string queuesJson = JsonConvert.SerializeObject(initialQueues);
        string picksJson = JsonConvert.SerializeObject(initialPicks);
        
        // Set initial Room Properties
        Hashtable initialRoomProps = new Hashtable
        {
            { DRAFT_PLAYER_QUEUES_PROP, queuesJson },
            { DRAFT_PICKS_MADE_PROP, picksJson },
            { DRAFT_ORDER_PROP, playerOrderJson }
        };
        
        Debug.Log($"Setting initial draft properties: {initialQueues.Count} queues generated ({totalOptionsGenerated} total options), Order: {string.Join(", ", playerOrder)}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(initialRoomProps);
    }
    
    private SerializableDraftOption GenerateSingleDraftOption(int optionId)
    {
        // Increased range to potentially include Upgrade Card options
        int choiceType = Random.Range(0, 6); // 0: Player Add, 1: Pet Add, 2: Player Stat, 3: Pet Stat, 4: Player Upgrade, 5: Pet Upgrade
        
        // Try Upgrade Card (if applicable deck has upgradable cards)
        if (choiceType >= 4)
        {
            bool forPet = (choiceType == 5);
            List<CardData> deckToCheck = forPet ? localPetDeck : deck;
            // Find cards in the deck that HAVE an upgrade defined
            List<CardData> upgradableCards = deckToCheck.Where(card => card != null && card.upgradedVersion != null).ToList();
            
            if (upgradableCards.Count > 0)
            {
                CardData cardToUpgrade = upgradableCards[Random.Range(0, upgradableCards.Count)];
                DraftOption upgradeOption = DraftOption.CreateUpgradeCardOption(optionId, cardToUpgrade, forPet);
                if(upgradeOption != null) return SerializableDraftOption.FromDraftOption(upgradeOption);
            }
        }
        
        // Fallback / Original Logic: Try Add Card
        if (choiceType <= 1 || choiceType >= 4) // Also try adding if upgrade failed
        {
            // Determine if adding for pet (either explicitly chosen or player pool empty)
            bool forPetAdd = (choiceType == 1 || choiceType == 5) ?
                              (allPetCardPool.Count > 0) :
                              (allPlayerCardPool.Count == 0 && allPetCardPool.Count > 0);
            
            List<CardData> pool = forPetAdd ? allPetCardPool : allPlayerCardPool;
            if (pool.Count > 0)
            {
                CardData randomCard = pool[Random.Range(0, pool.Count)];
                return SerializableDraftOption.FromDraftOption(DraftOption.CreateAddCardOption(optionId, randomCard, forPetAdd));
            }
        }
        
        // Fallback: Upgrade Stat
        bool forPetStat = (choiceType == 3 || choiceType == 5);
        StatType stat = (StatType)Random.Range(0, System.Enum.GetValues(typeof(StatType)).Length);
        int amount = 0;
        if (stat == StatType.MaxHealth) amount = Random.Range(5, 11);
        else if (stat == StatType.StartingEnergy) amount = 1;
        
        if (amount > 0)
        {
            if(forPetStat && stat == StatType.StartingEnergy)
            {
                if(gameManager.GetPlayerManager().GetStartingEnergy() < 10) {
                    return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.StartingEnergy, amount, false));
                } else {
                    return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, Random.Range(5, 11), true));
                }
            }
            return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, stat, amount, forPetStat));
        }
        
        // Final Fallback
        Debug.LogWarning("GenerateSingleDraftOption: Failed to generate desired option type, defaulting to Player Health upgrade.");
        return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, 5, false));
    }
    
    public void ApplyDraftChoice(DraftOption choice)
    {
        if (choice == null)
        {
            Debug.LogError("ApplyDraftChoice received a null choice!");
            return;
        }
        
        Debug.Log($"Applying draft choice: {choice.Description}");
        bool petDeckChanged = false; // Flag to check if we need to update properties
        
        switch (choice.Type)
        {
            case DraftOptionType.AddPlayerCard:
                if (choice.CardToAdd != null)
                {
                    deck.Add(choice.CardToAdd);
                    ShuffleDeck();
                    gameManager.UpdateDeckCountUI();
                    Debug.Log($"Added {choice.CardToAdd.cardName} to player deck.");
                }
                else { Debug.LogWarning("AddPlayerCard choice had null CardToAdd."); }
                break;
                
            case DraftOptionType.AddPetCard:
                if (choice.CardToAdd != null)
                {
                    localPetDeck.Add(choice.CardToAdd);
                    ShuffleLocalPetDeck(); // Maybe shuffle later or not at all if just adding?
                    Debug.Log($"Added {choice.CardToAdd.cardName} to local pet deck. Current count: {localPetDeck.Count}");
                    petDeckChanged = true;
                }
                else { Debug.LogWarning("AddPetCard choice had null CardToAdd."); }
                break;
                
            case DraftOptionType.UpgradePlayerStat:
                int startingPlayerHealth = gameManager.GetStartingPlayerHealth();
                int localPlayerHealth = gameManager.GetPlayerManager().GetLocalPlayerHealth();
                
                if (choice.StatToUpgrade == StatType.MaxHealth)
                {
                    gameManager.SetStartingPlayerHealth(startingPlayerHealth + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().DamageLocalPlayer(-choice.StatIncreaseAmount); // Negative damage = healing
                    Debug.Log($"Upgraded Player Max Health by {choice.StatIncreaseAmount}. New base: {gameManager.GetStartingPlayerHealth()}");
                }
                else if (choice.StatToUpgrade == StatType.StartingEnergy)
                {
                    gameManager.SetStartingEnergy(gameManager.GetPlayerManager().GetStartingEnergy() + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().SetCurrentEnergy(gameManager.GetPlayerManager().GetCurrentEnergy() + choice.StatIncreaseAmount);
                    Debug.Log($"Upgraded Player Starting Energy by {choice.StatIncreaseAmount}. New base: {gameManager.GetPlayerManager().GetStartingEnergy()}");
                }
                gameManager.UpdateHealthUI();
                gameManager.UpdateEnergyUI();
                break;
                
            case DraftOptionType.UpgradePetStat:
                int startingPetHealth = gameManager.GetStartingPetHealth();
                int localPetHealth = gameManager.GetPlayerManager().GetLocalPetHealth();
                
                if (choice.StatToUpgrade == StatType.MaxHealth)
                {
                    gameManager.SetStartingPetHealth(startingPetHealth + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().DamageLocalPet(-choice.StatIncreaseAmount); // Negative damage = healing
                    Debug.Log($"Upgraded Pet Max Health by {choice.StatIncreaseAmount}. New base: {gameManager.GetStartingPetHealth()}");
                    Hashtable petProps = new Hashtable { { PlayerManager.PLAYER_BASE_PET_HP_PROP, gameManager.GetStartingPetHealth() } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(petProps);
                    Debug.Log($"Set {PlayerManager.PLAYER_BASE_PET_HP_PROP} to {gameManager.GetStartingPetHealth()} for local player.");
                }
                gameManager.UpdateHealthUI();
                break;
                
            case DraftOptionType.UpgradePlayerCard:
                if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                {
                    int indexToUpgrade = deck.FindIndex(card => card == choice.CardToUpgrade);
                    if (indexToUpgrade != -1)
                    {
                        CardData upgradedCard = choice.CardToUpgrade.upgradedVersion;
                        deck[indexToUpgrade] = upgradedCard;
                        ShuffleDeck();
                        gameManager.UpdateDeckCountUI();
                        Debug.Log($"Upgraded player card {choice.CardToUpgrade.cardName} to {upgradedCard.cardName} in deck.");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find player card '{choice.CardToUpgrade.cardName}' in deck to upgrade.");
                    }
                }
                else
                {
                    Debug.LogError($"UpgradePlayerCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                }
                break;
                
            case DraftOptionType.UpgradePetCard:
                if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                {
                    // Find the specific instance to upgrade if multiple copies exist
                    // Using FindIndex might just find the first one. If CardData had unique IDs this would be better.
                    int indexToUpgrade = localPetDeck.FindIndex(card => card == choice.CardToUpgrade);
                    if (indexToUpgrade == -1)
                    {
                         // Fallback: If reference match failed (e.g., due to deserialization differences), try matching by name
                         indexToUpgrade = localPetDeck.FindIndex(card => card.cardName == choice.CardToUpgrade.cardName && card.upgradedVersion != null);
                         if(indexToUpgrade != -1) Debug.LogWarning($"UpgradePetCard: Reference match failed for {choice.CardToUpgrade.cardName}, but found by name.");
                    }

                    if (indexToUpgrade != -1)
                    {
                        CardData upgradedCard = choice.CardToUpgrade.upgradedVersion;
                        Debug.Log($"Upgrading pet card {localPetDeck[indexToUpgrade].cardName} (index {indexToUpgrade}) to {upgradedCard.cardName} in local pet deck.");
                        localPetDeck[indexToUpgrade] = upgradedCard;
                        // Don't necessarily shuffle immediately after upgrade? Depends on game design.
                        // ShuffleLocalPetDeck();
                        petDeckChanged = true;
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find upgradable pet card '{choice.CardToUpgrade.cardName}' in local pet deck to upgrade (Size: {localPetDeck.Count}).");
                        // Log the current pet deck contents for debugging
                        string deckContents = string.Join(", ", localPetDeck.Select(c => c.cardName + (c.upgradedVersion != null ? "(U)" : "")));
                        Debug.LogWarning($"Current Pet Deck: [{deckContents}]");
                    }
                }
                else
                {
                    Debug.LogError($"UpgradePetCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                }
                break;
        }

        // If the pet deck was modified, update the player's custom properties
        if (petDeckChanged)
        {
            SyncLocalPetDeckToCustomProperties();
        }
    }
    
    // New method to sync the current localPetDeck to Photon Player Properties
    private void SyncLocalPetDeckToCustomProperties()
    {
        // Convert List<CardData> to List<string> (card names)
        List<string> petDeckCardNames = localPetDeck.Select(card => card.cardName).ToList();

        // Serialize the list of names (JSON is convenient)
        string petDeckJson = JsonConvert.SerializeObject(petDeckCardNames);

        // Set the custom property for the local player
        Hashtable props = new Hashtable { { PLAYER_PET_DECK_PROP, petDeckJson } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"Synced local pet deck to Player Properties. {petDeckCardNames.Count} cards. JSON: {petDeckJson}");
    }

    // Helper method to get all CardData (could be optimized later, e.g., dictionary lookup)
    public CardData FindCardDataByName(string cardName, bool findInPetPool)
    {
        List<CardData> pool = findInPetPool ? allPetCardPool : allPlayerCardPool;
        // Also check starter decks as they might contain base versions not in pools?
        CardData found = pool.FirstOrDefault(c => c.cardName == cardName);
        if (found == null)
        {
            found = starterDeck.FirstOrDefault(c => c.cardName == cardName);
        }
        if (found == null)
        {
            found = starterPetDeck.FirstOrDefault(c => c.cardName == cardName);
        }
        // Check upgraded versions too (less efficient, better if CardData had unique IDs)
        if (found == null)
        {
            foreach (var card in pool.Concat(starterDeck).Concat(starterPetDeck))
            {
                 if (card.upgradedVersion != null && card.upgradedVersion.cardName == cardName)
                 {
                    return card.upgradedVersion;
                 }
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"FindCardDataByName: Could not find CardData for name '{cardName}'");
        }
        return found;
    }
    
    public void UpdateLocalDraftStateFromRoomProps()
    {
        if (isWaitingForLocalPick)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps: Skipping update, player is still picking (isWaitingForLocalPick=true).");
            return;
        }
        
        bool needsUIUpdate = false;
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        bool nowHasPack = false;
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PLAYER_QUEUES_PROP, out object queuesObj))
        {
            try
            {
                string queuesJson = queuesObj as string;
                Dictionary<int, List<string>> currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson) ?? new Dictionary<int, List<string>>();
                
                int localActorNum = PhotonNetwork.LocalPlayer.ActorNumber;
                if (currentQueues.ContainsKey(localActorNum) && currentQueues[localActorNum] != null && currentQueues[localActorNum].Count > 0)
                {
                    string nextPackJson = currentQueues[localActorNum][0]; // Peek at the first pack
                    List<SerializableDraftOption> serializablePack = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(nextPackJson) ?? new List<SerializableDraftOption>();
                    
                    // Deserialize into full DraftOption, linking actual CardData
                    foreach (var serializableOption in serializablePack)
                    {
                        DraftOption fullOption = serializableOption.ToDraftOption(allPlayerCardPool, allPetCardPool, deck, localPetDeck);
                        if (fullOption != null)
                        {
                            localCurrentPack.Add(serializableOption);
                            localActiveOptionMap[serializableOption.OptionId] = fullOption;
                        }
                        else
                        {
                            Debug.LogError($"Failed to deserialize draft option ID {serializableOption.OptionId} ({serializableOption.Description}) - skipping.");
                        }
                    }
                    
                    if(localCurrentPack.Count > 0)
                    {
                        nowHasPack = true;
                        isWaitingForLocalPick = true;
                        needsUIUpdate = true;
                        Debug.Log($"Deserialized pack for local player. {localCurrentPack.Count} options. Setting isWaitingForLocalPick=true.");
                    }
                    else
                    {
                        Debug.LogWarning("Deserialized pack for local player, but it resulted in zero valid options after deserialization.");
                    }
                }
                else
                {
                    Debug.Log("No pack currently queued for local player.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft queues: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PLAYER_QUEUES_PROP} not found.");
        }
        
        if (needsUIUpdate)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps triggering Draft UI update.");
            gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        }
        else if (!nowHasPack && isWaitingForLocalPick)
        {
            // If we were waiting, but now have no pack, reset waiting flag
            Debug.Log("Resetting isWaitingForLocalPick as no pack was found.");
            isWaitingForLocalPick = false;
            gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        }
    }
    
    public void UpdateLocalDraftPicksFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PICKS_MADE_PROP, out object picksObj))
        {
            try
            {
                string picksJson = picksObj as string;
                draftPicksMade = JsonConvert.DeserializeObject<Dictionary<int, int>>(picksJson) ?? new Dictionary<int, int>();
                Debug.Log($"Successfully deserialized draft picks made: {draftPicksMade.Count} players.");
            }
            catch(System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft picks: {e.Message}");
                draftPicksMade = new Dictionary<int, int>();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PICKS_MADE_PROP} not found.");
            draftPicksMade = new Dictionary<int, int>();
        }
    }
    
    public void UpdateLocalDraftOrderFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_ORDER_PROP, out object orderObj))
        {
            try
            {
                string orderJson = orderObj as string;
                draftPlayerOrder = JsonConvert.DeserializeObject<List<int>>(orderJson) ?? new List<int>();
                Debug.Log($"Successfully deserialized draft order: {string.Join(", ", draftPlayerOrder)}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft order: {e.Message}");
                draftPlayerOrder = new List<int>();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_ORDER_PROP} not found.");
            draftPlayerOrder = new List<int>();
        }
    }
    
    public void HandlePlayerDraftPick(int optionId)
    {
        Debug.Log("Handling player pick, sending to Master Client via RPC");
        isWaitingForLocalPick = false;
        
        // Apply the selection locally
        if (localActiveOptionMap.TryGetValue(optionId, out DraftOption chosenOption))
        {
            ApplyDraftChoice(chosenOption);
        }
        else
        {
            Debug.LogError($"Selected option ID {optionId} not found in localActiveOptionMap!");
        }
        
        // Clear local state
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        
        // Update UI to show waiting state
        gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        
        // Send RPC to Master Client
        gameManager.GetPhotonView().RPC("RpcPlayerPickedOption", RpcTarget.MasterClient, optionId);
    }
    
    public List<CardData> GetDeck()
    {
        return deck;
    }
    
    public List<CardData> GetHand()
    {
        return hand;
    }
    
    public List<CardData> GetDiscardPile()
    {
        return discardPile;
    }
    
    public List<CardData> GetLocalPetDeck()
    {
        return localPetDeck;
    }
    
    public int GetDeckCount()
    {
        return deck.Count;
    }
    
    public int GetDiscardCount()
    {
        return discardPile.Count;
    }
    
    public int GetHandCount()
    {
        return hand.Count;
    }
    
    public bool IsWaitingForLocalPick()
    {
        return isWaitingForLocalPick;
    }
    
    public void SetWaitingForLocalPick(bool value)
    {
        isWaitingForLocalPick = value;
    }
    
    public Dictionary<int, DraftOption> GetLocalActiveOptionMap()
    {
        return localActiveOptionMap;
    }
    
    public List<SerializableDraftOption> GetLocalCurrentPack()
    {
        return localCurrentPack;
    }

    // New method called by PlayerManager to set up the opponent's deck based on synced data
    public void InitializeOpponentPetDeck(List<string> cardNames)
    {
        opponentPetDeck.Clear(); // Clear previous round's deck

        if (cardNames == null || cardNames.Count == 0)
        {
            // No synced data found (first round? property missing?), use starter deck
            Debug.Log("InitializeOpponentPetDeck: No card names provided, using starter pet deck.");
            opponentPetDeck = new List<CardData>(starterPetDeck ?? new List<CardData>());
        }
        else
        {
            // Reconstruct the deck from the provided names
            Debug.Log($"InitializeOpponentPetDeck: Reconstructing deck from {cardNames.Count} names.");
            foreach (string cardName in cardNames)
            {
                CardData cardData = FindCardDataByName(cardName, true); // Find in pet pools/starters
                if (cardData != null)
                {
                    opponentPetDeck.Add(cardData);
                }
                else
                {
                    Debug.LogWarning($"InitializeOpponentPetDeck: Could not find CardData for card name '{cardName}' while reconstructing opponent deck.");
                }
            }
        }

        // Ensure hand/discard are clear and shuffle the newly constructed deck
        opponentPetHand.Clear();
        opponentPetDiscard.Clear();
        ShuffleOpponentPetDeck(); // Shuffle the reconstructed or starter deck

        Debug.Log($"Opponent Pet Deck initialized with {opponentPetDeck.Count} cards.");
    }

    // --- ADDED: Get Opponent Pet Deck ---
    public List<CardData> GetOpponentPetDeck()
    {
        // This list is populated by InitializeOpponentPetDeck
        return opponentPetDeck;
    }
    // --- END ADDED ---

    // --- ADDED: Methods to get all owned cards ---
    public List<CardData> GetAllOwnedPlayerCards()
    {
        // Creates a new list containing cards from deck, hand, and discard
        List<CardData> allCards = new List<CardData>(deck);
        allCards.AddRange(hand);
        allCards.AddRange(discardPile);
        // Optional: Sort the combined list for display consistency
        // allCards = allCards.OrderBy(card => card.cost).ThenBy(card => card.cardName).ToList();
        return allCards;
    }
    
    public List<CardData> GetAllOwnedPetCards()
    {
        // Assuming pet only has the 'localPetDeck' list and no separate hand/discard
        // If pet had hand/discard, combine them similarly to the player method above.
        List<CardData> allCards = new List<CardData>(localPetDeck);
        // Optional: Sort the list
        // allCards = allCards.OrderBy(card => card.cost).ThenBy(card => card.cardName).ToList();
        return allCards;
    }
    // --- END ADDED ---

    // --- ADDED: Helper for Random Player Discard ---
    private void DiscardRandomPlayerCards(int amount)
    {
        System.Random rng = new System.Random();
        int cardsToDiscardCount = Mathf.Min(amount, hand.Count); // Can't discard more than in hand
        
        for (int i = 0; i < cardsToDiscardCount; i++)
        {
            if (hand.Count == 0) break; // Safety check
            
            int randomIndex = rng.Next(hand.Count);
            CardData cardToDiscard = hand[randomIndex];
            hand.RemoveAt(randomIndex);
            discardPile.Add(cardToDiscard);
            Debug.Log($"Randomly discarded player card: {cardToDiscard.cardName}");
            HandleDiscardTrigger(cardToDiscard); // Check for discard trigger
        }
    }
    // --- END ADDED ---
    
    // --- ADDED: Helper for Random Opponent Pet Discard ---
    private void DiscardRandomOpponentPetCards(int amount)
    {
        System.Random rng = new System.Random();
        int cardsToDiscardCount = Mathf.Min(amount, opponentPetHand.Count); 
        
        for (int i = 0; i < cardsToDiscardCount; i++)
        {
            if (opponentPetHand.Count == 0) break; 
            
            int randomIndex = rng.Next(opponentPetHand.Count);
            CardData cardToDiscard = opponentPetHand[randomIndex];
            opponentPetHand.RemoveAt(randomIndex);
            opponentPetDiscard.Add(cardToDiscard);
            Debug.Log($"Randomly discarded opponent pet card: {cardToDiscard.cardName}");
            // HandleDiscardTrigger(cardToDiscard); // Usually don't trigger opponent effects here
        }
    }
    // --- END ADDED ---
    
    // --- ADDED: Handle Discard Trigger Effects ---
    private void HandleDiscardTrigger(CardData discardedCard)
    {
        if (discardedCard == null || discardedCard.discardEffectType == DiscardEffectType.None)
        {
            return; // No effect to trigger
        }

        Debug.Log($"Handling discard trigger for {discardedCard.cardName}: {discardedCard.discardEffectType}, Value: {discardedCard.discardEffectValue}");

        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = discardedCard.discardEffectValue;

        switch (discardedCard.discardEffectType)
        {
            case DiscardEffectType.DealDamageToOpponentPet:
                if (value > 0) playerManager.DamageOpponentPet(value);
                break;
            case DiscardEffectType.GainBlockPlayer:
                if (value > 0) playerManager.AddBlockToLocalPlayer(value);
                break;
            case DiscardEffectType.GainBlockPet:
                 if (value > 0) playerManager.AddBlockToLocalPet(value);
                break;
            case DiscardEffectType.DrawCard:
                if (value > 0) {
                    for(int i=0; i < value; i++) DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                break;
            case DiscardEffectType.GainEnergy:
                if (value > 0) playerManager.GainEnergy(value);
                break;
            // Add cases for other effects
        }
    }
    // --- END ADDED ---

    // --- ADDED: Handle Combo Effect --- 
    private void ApplyComboEffect(CardData triggeringCard)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = triggeringCard.comboEffectValue;
        CardDropZone.TargetType comboTarget = triggeringCard.comboEffectTarget;

        switch (triggeringCard.comboEffectType)
        {
            case ComboEffectType.DealDamageToOpponentPet:
                 if (value > 0) playerManager.DamageOpponentPet(value);
                break;
            case ComboEffectType.GainBlockPlayer:
                if (value > 0) playerManager.AddBlockToLocalPlayer(value);
                break;
            case ComboEffectType.GainBlockPet:
                 if (value > 0) playerManager.AddBlockToLocalPet(value);
                break;
            case ComboEffectType.DrawCard:
                 if (value > 0) {
                    for(int i=0; i < value; i++) DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                break;
            case ComboEffectType.GainEnergy:
                if (value > 0) playerManager.GainEnergy(value);
                break;
            // TODO: Add more complex combo effects (e.g., apply status, heal based on target)
            // Need to potentially check comboTarget here if effects are target-dependent.
        }
    }
    // --- END ADDED ---
}