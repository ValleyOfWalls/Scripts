using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime; // Add this for Player type

public class EnergyManager
{
    private GameManager gameManager;
    
    private int currentEnergy;
    private int startingEnergy;
    
    // Card Cost Modification
    private struct CostModifierInfo
    {
        public int amount;
        public int durationTurns;
    }
    
    private Dictionary<CardData, CostModifierInfo> cardCostModifiers = new Dictionary<CardData, CostModifierInfo>();
    private int opponentHandCostModifierTurns = 0;
    private int opponentHandCostModifierAmount = 0;
    
    public EnergyManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingEnergy)
    {
        this.startingEnergy = startingEnergy;
        this.currentEnergy = startingEnergy;
        
        // Reset cost modifiers
        cardCostModifiers.Clear();
        opponentHandCostModifierTurns = 0;
        opponentHandCostModifierAmount = 0;
    }
    
    #region Energy Methods
    
    public void ConsumeEnergy(int amount)
    {
        currentEnergy -= amount;
        if (currentEnergy < 0) currentEnergy = 0;
        gameManager.UpdateEnergyUI();
    }
    
    public void GainEnergy(int amount)
    {
        if (amount <= 0) return;
        currentEnergy += amount;
        Debug.Log($"Gained {amount} energy. New total: {currentEnergy}");
        gameManager.UpdateEnergyUI();
    }
    
    #endregion
    
    #region Cost Modifier Methods
    
    public void ApplyCostModifierToOpponentHand(int amount, int duration, int cardCount)
    {
        opponentHandCostModifierAmount = amount;
        opponentHandCostModifierTurns = duration; 
        Debug.Log($"Applying Cost Modifier to Opponent Hand: Amount={amount}, Duration={duration}, Count={cardCount}. (Local state set)");
        Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (opponent != null)
        {
            gameManager.GetPhotonView().RPC("RpcApplyHandCostModifier", opponent, amount, duration, cardCount);
            Debug.Log($"Sent RpcApplyHandCostModifier to {opponent.NickName}");
        }
        else
        {
            Debug.LogError("ApplyCostModifierToOpponentHand: Cannot send RPC, opponentPlayer is null!");
        }
    }
    
    public void ApplyCostModifierToLocalHand(int amount, int duration, int cardCount)
    {
        Debug.Log($"ApplyCostModifierToLocalHand called: Amount={amount}, Duration={duration}, Count={cardCount}");
        List<CardData> currentHand = gameManager.GetCardManager().GetHand();
        if (currentHand.Count == 0) return;

        List<CardData> cardsToModify = new List<CardData>();

        if (cardCount <= 0 || cardCount >= currentHand.Count) // Affect all cards
        {
            cardsToModify.AddRange(currentHand);
            Debug.Log("Applying cost modifier to all cards in hand.");
        }
        else // Affect specific number of random cards
        {
            System.Random rng = new System.Random();
            cardsToModify = currentHand.OrderBy(x => rng.Next()).Take(cardCount).ToList();
            Debug.Log($"Applying cost modifier to {cardCount} random cards in hand.");
        }

        foreach (CardData card in cardsToModify)
        {
            // Overwrite existing modifier or add new one
            cardCostModifiers[card] = new CostModifierInfo { amount = amount, durationTurns = duration };
            Debug.Log($" -> Set cost modifier for '{card.cardName}': Amount={amount}, Duration={duration}");
        }

        // Trigger UI update as costs might have changed
        gameManager.UpdateHandUI(); 
    }
    
    public int GetLocalHandCostModifier(CardData card)
    {
        if (card != null && cardCostModifiers.TryGetValue(card, out CostModifierInfo info))
        {
            return info.amount;
        }
        return 0; // No modifier for this specific card
    }
    
    public void RemoveCostModifierForCard(CardData card)
    {
        if (card != null && cardCostModifiers.Remove(card))
        {
            Debug.Log($"Removed cost modifier tracking for card: {card.cardName}");
        }
    }
    
    public void DecrementCostModifiers()
    {
        List<CardData> expiredModifiers = new List<CardData>();
        List<CardData> currentModKeys = new List<CardData>(cardCostModifiers.Keys);
        foreach (CardData card in currentModKeys)
        {
            if (cardCostModifiers.TryGetValue(card, out CostModifierInfo info))
            {
                info.durationTurns--;
                if (info.durationTurns <= 0)
                {
                    expiredModifiers.Add(card);
                    Debug.Log($"Cost modifier expired for card: {card.cardName}");
                }
                else
                {
                    cardCostModifiers[card] = info; // Update duration
                }
            }
        }
        foreach (CardData cardToRemove in expiredModifiers)
        {
            cardCostModifiers.Remove(cardToRemove);
        }
        
        // Opponent hand cost modifiers (for tracking)
        if (opponentHandCostModifierTurns > 0) opponentHandCostModifierTurns--;
    }
    
    #endregion
    
    #region Getters and Setters
    
    public int GetCurrentEnergy() => currentEnergy;
    public void SetCurrentEnergy(int energy) => currentEnergy = energy;
    public int GetStartingEnergy() => startingEnergy;
    public void SetStartingEnergy(int energy) => startingEnergy = energy;
    
    #endregion
}