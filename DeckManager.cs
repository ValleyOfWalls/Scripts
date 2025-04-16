using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class DeckManager : MonoBehaviourPunCallbacks
{
    // References to related managers
    private GameplayManager gameplayManager;
    private CardUIManager cardUIManager;
    
    // Player deck tracking
    private Dictionary<int, List<Card>> playerDecks = new Dictionary<int, List<Card>>();
    
    private void Start()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
    }
    
    public void RegisterPlayerDeck(int playerId, List<Card> initialDeck)
    {
        if (!playerDecks.ContainsKey(playerId))
        {
            playerDecks.Add(playerId, new List<Card>(initialDeck));
        }
    }
    
    public List<Card> GetPlayerDeck(int playerId)
    {
        if (playerDecks.ContainsKey(playerId))
        {
            return playerDecks[playerId];
        }
        return new List<Card>();
    }
    
    public void AddCardToPlayerDeck(int playerId, Card card)
    {
        if (playerDecks.ContainsKey(playerId))
        {
            playerDecks[playerId].Add(card);
        }
    }
    
    public void SetupCardUIManager(CardUIManager cardUIMgr)
    {
        cardUIManager = cardUIMgr;
    }
}