using UnityEngine;
using System.Collections.Generic;

public class DeckManager
{
    private GameManager gameManager;
    
    // Core deck components
    private List<CardData> deck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    
    // Configuration
    private int cardsToDraw = 5;
    private List<CardData> starterDeck = new List<CardData>();
    
    public DeckManager(GameManager gameManager, List<CardData> starterDeck, int cardsToDraw)
    {
        this.gameManager = gameManager;
        this.starterDeck = new List<CardData>(starterDeck);
        this.cardsToDraw = cardsToDraw;
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
            // Discard triggers are handled by CardEffectService
        }
    }
    
    public bool DiscardCard(CardData card)
    {
        if (hand.Remove(card))
        {
            discardPile.Add(card);
            return true;
        }
        return false;
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
    
    public void DiscardRandomCards(int amount)
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
            
            // The discard trigger is handled by CardManager
            gameManager.GetCardManager().HandleDiscardTrigger(cardToDiscard);
        }
    }
    
    public void AddCardToPlayerDeck(CardData card)
    {
        if (card != null)
        {
            deck.Add(card);
            ShuffleDeck();
        }
    }
    
    // --- ADDED: Method to transform a card in hand ---
    public bool TransformCardInHand(CardData oldCard, CardData newCard)
    {
        int index = hand.IndexOf(oldCard);
        if (index != -1)
        {
            hand[index] = newCard;
            Debug.Log($"Transformed card '{oldCard?.name ?? "NULL"}' into '{newCard?.name ?? "NULL"}' in hand.");
            return true;
        }
        else
        {
            Debug.LogWarning($"TransformCardInHand: Could not find card '{oldCard?.name ?? "NULL"}' in hand.");
            return false;
        }
    }
    // --- END ADDED ---

    // --- ADDED: Add card directly to hand ---
    public void AddCardToHand(CardData card)
    {
        if (card != null)
        {
            hand.Add(card);
            Debug.Log($"Added card '{card.name}' directly to hand.");
        }
    }
    // --- END ADDED ---

    // --- ADDED: Add card directly to discard pile ---
    public void AddCardToDiscard(CardData card)
    {
        if (card != null)
        {
            discardPile.Add(card);
            Debug.Log($"Added card '{card.name}' directly to discard pile.");
        }
    }
    // --- END ADDED ---

    // Getters
    public List<CardData> GetDeck() => deck;
    public List<CardData> GetHand() => hand;
    public List<CardData> GetDiscardPile() => discardPile;
    public int GetDeckCount() => deck.Count;
    public int GetDiscardCount() => discardPile.Count;
    public int GetHandCount() => hand.Count;
    
    public List<CardData> GetAllOwnedPlayerCards()
    {
        // Creates a new list containing cards from deck, hand, and discard
        List<CardData> allCards = new List<CardData>(deck);
        allCards.AddRange(hand);
        allCards.AddRange(discardPile);
        return allCards;
    }
}