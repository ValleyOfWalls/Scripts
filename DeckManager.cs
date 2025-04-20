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
        Debug.Log($"DrawHand START: Attempting to draw {cardsToDraw} cards. Current state - Deck: {deck.Count}, Discard: {discardPile.Count}");
        int cardsDrawnThisTurn = 0;
        for (int i = 0; i < cardsToDraw; i++)
        {
            if (!DrawCard()) // If DrawCard returns false, stop drawing
            {
                Debug.Log($"Could not draw card {i+1} of {cardsToDraw} (deck/discard likely empty). Stopping draw.");
                break;
            }
            cardsDrawnThisTurn++;
        }
        Debug.Log($"DrawHand END: Finished drawing. Hand size: {hand.Count}. Cards drawn this turn: {cardsDrawnThisTurn}. Final state - Deck: {deck.Count}, Discard: {discardPile.Count}");
    }
    
    /// <summary>
    /// Draws a single card from the deck into the hand.
    /// Handles reshuffling the discard pile if the deck is empty.
    /// </summary>
    /// <returns>True if a card was successfully drawn, false otherwise (deck and discard are empty).</returns>
    public bool DrawCard()
    {
        if (deck.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("DrawCard: Deck and discard pile are both empty. Cannot draw.");
                return false; // Out of cards entirely
            }
            ReshuffleDiscardPile();
             // After reshuffling, check if the deck is *still* empty (shouldn't happen if discard wasn't empty, but safety check)
            if (deck.Count == 0) 
            {
                 Debug.LogWarning("DrawCard: Deck is still empty after reshuffling non-empty discard pile. This should not happen.");
                 return false;
            }
        }
        
        CardData drawnCard = deck[0];
        deck.RemoveAt(0);
        hand.Add(drawnCard);
        Debug.Log($"DrawCard SUCCESS: Drew '{drawnCard.cardName}'. New state - Deck: {deck.Count}, Hand: {hand.Count}, Discard: {discardPile.Count}");
        return true;
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
        Debug.Log($"ReshuffleDiscardPile START: Reshuffling {discardPile.Count} cards from discard into deck (current deck size: {deck.Count}).");
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
        Debug.Log($"ReshuffleDiscardPile END: Deck size after reshuffle: {deck.Count}, Discard size: {discardPile.Count}");
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