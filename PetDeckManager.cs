using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Realtime;
using Photon.Pun;
using ExitGames.Client.Photon;

public class PetDeckManager
{
    private GameManager gameManager;
    
    // Pet deck data
    private List<CardData> localPetDeck = new List<CardData>();
    private List<CardData> starterPetDeck = new List<CardData>();
    
    // Opponent pet state (for local simulation)
    private int opponentPetEnergy;
    private List<CardData> opponentPetDeck = new List<CardData>();
    private List<CardData> opponentPetHand = new List<CardData>();
    private List<CardData> opponentPetDiscard = new List<CardData>();
    
    // Card pool
    private List<CardData> allPetCardPool = new List<CardData>();
    
    public const string PLAYER_PET_DECK_PROP = "PetDeck";
    
    public PetDeckManager(GameManager gameManager, List<CardData> starterPetDeck, List<CardData> allPetCardPool)
    {
        this.gameManager = gameManager;
        this.starterPetDeck = new List<CardData>(starterPetDeck ?? new List<CardData>());
        this.allPetCardPool = new List<CardData>(allPetCardPool ?? new List<CardData>());
    }
    
    public void InitializeAndSyncLocalPetDeck()
    {
        if (localPetDeck == null || localPetDeck.Count == 0)
        {
            localPetDeck = new List<CardData>(starterPetDeck);
            Debug.Log($"InitializeAndSyncLocalPetDeck: Initialized localPetDeck from starter. Count: {localPetDeck.Count}");
            // Sync this initial state immediately
            SyncLocalPetDeckToCustomProperties();
        }
        else
        {
            Debug.Log("InitializeAndSyncLocalPetDeck: localPetDeck already initialized.");
        }
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
    
    // Opponent Pet Deck Methods
    public void InitializeOpponentPetDeck(List<string> cardNames)
    {
        opponentPetDeck.Clear(); // Clear previous round's deck

        if (cardNames == null || cardNames.Count == 0)
        {
            // No synced data found (first round? property missing?), use starter deck
            Debug.Log("InitializeOpponentPetDeck: No card names provided, using starter pet deck.");
            opponentPetDeck = new List<CardData>(starterPetDeck);
        }
        else
        {
            // Reconstruct the deck from the provided names
            Debug.Log($"InitializeOpponentPetDeck: Reconstructing deck from {cardNames.Count} names.");
            foreach (string cardName in cardNames)
            {
                CardData cardData = FindCardDataByName(cardName);
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
        }
    }
    
    public void DiscardRandomOpponentPetCards(int amount)
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
        
        // Process Turn Start Effects (handled by PlayerManager)
        gameManager.GetPlayerManager().ProcessOpponentPetTurnStartEffects();
        
        // Determine how many cards the pet should draw
        int petCardsToDraw = 3; // Example: Pet draws fewer cards
        DrawOpponentPetHand(petCardsToDraw);
        
        // --- ADDED: Find the target player for this pet's attacks ---
        Player targetPlayer = FindTargetPlayer();
        if (targetPlayer == null)
        {
            Debug.LogError("ExecuteOpponentPetTurn: Could not find the player being targeted by this pet! Aborting turn.");
            DiscardOpponentPetHand(); // Discard hand to prevent playing cards into the void
            return;
        }
        // --- END ADDED ---

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
                Debug.Log($"Opponent Pet playing card: {cardToPlay.cardName} (Cost: {cardToPlay.cost}) targeting {targetPlayer.NickName}");
                opponentPetEnergy -= cardToPlay.cost;
                
                // Apply effect locally for simulation (handled by CardEffectService)
                // gameManager.GetCardManager().ProcessOpponentPetCardEffect(cardToPlay);

                // --- MODIFIED: Send RPC to target player to apply effect and track card ---
                gameManager.GetPhotonView().RPC("RpcApplyOpponentPetCardEffect", targetPlayer, cardToPlay.name, cardToPlay.name);
                // --- END MODIFIED ---
                
                // Move card from hand to discard
                opponentPetHand.RemoveAt(cardIndex);
                opponentPetDiscard.Add(cardToPlay);
                cardPlayedThisLoop = true; // Indicate a card was played, loop again
                
                Debug.Log($"Opponent Pet energy remaining: {opponentPetEnergy}");
            }
            
        } while (cardPlayedThisLoop && opponentPetEnergy > 0); // Continue if a card was played and energy remains
        
        Debug.Log("Opponent Pet finished playing cards.");
        DiscardOpponentPetHand();
        Debug.Log("---> Ending Opponent Pet Turn <---");
    }

    // --- ADDED: Helper to find the player fighting this pet ---
    private Player FindTargetPlayer()
    {
        // The current player (running this code) owns the pet.
        // We need to find who is paired to fight *this* player's pet.
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PlayerManager.COMBAT_PAIRINGS_PROP, out object pairingsObj))
        {
            try
            {
                string pairingsJson = pairingsObj as string;
                var pairingsDict = JsonConvert.DeserializeObject<Dictionary<int, int>>(pairingsJson);
                
                // Find the player whose opponent pet owner actor number is MINE
                foreach (var kvp in pairingsDict)
                {
                    int playerActorNum = kvp.Key;
                    int opponentPetOwnerActorNum = kvp.Value;

                    if (opponentPetOwnerActorNum == myActorNumber)
                    {
                        // Found the player who is fighting my pet
                        Player target = PhotonNetwork.CurrentRoom.GetPlayer(playerActorNum);
                        if (target != null) 
                        {
                            Debug.Log($"FindTargetPlayer: Found player {target.NickName} ({playerActorNum}) is fighting my pet.");
                            return target;
                        }
                        else
                        {
                            Debug.LogError($"FindTargetPlayer: Found pairing where player {playerActorNum} fights my pet, but couldn't get Player object.");
                            return null;
                        }
                    }
                }
                Debug.LogWarning("FindTargetPlayer: Couldn't find any player paired against my pet in pairings property.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"FindTargetPlayer: Failed to deserialize combat pairings: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"FindTargetPlayer: Room property {PlayerManager.COMBAT_PAIRINGS_PROP} not found.");
        }

        return null; // Target player not found
    }
    // --- END ADDED ---

    private void SyncLocalPetDeckToCustomProperties()
    {
        // Convert List<CardData> to List<string> (card names)
        List<string> petDeckCardNames = localPetDeck.Select(card => card.cardName).ToList();

        // Serialize the list of names
        string petDeckJson = JsonConvert.SerializeObject(petDeckCardNames);

        // Set the custom property for the local player
        Hashtable props = new Hashtable { { PLAYER_PET_DECK_PROP, petDeckJson } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"Synced local pet deck to Player Properties. {petDeckCardNames.Count} cards. JSON: {petDeckJson}");
    }

    public void AddCardToPetDeck(CardData card)
    {
        if (card != null)
        {
            localPetDeck.Add(card);
            SyncLocalPetDeckToCustomProperties();
            Debug.Log($"Added {card.cardName} to pet deck. Current count: {localPetDeck.Count}");
        }
    }

    public bool UpgradePetCard(CardData cardToUpgrade, CardData upgradedVersion)
    {
        int index = localPetDeck.FindIndex(card => card == cardToUpgrade);
        if (index == -1)
        {
            // Fallback by name matching
            index = localPetDeck.FindIndex(card => card.cardName == cardToUpgrade.cardName && card.upgradedVersion != null);
        }
        
        if (index != -1 && upgradedVersion != null)
        {
            localPetDeck[index] = upgradedVersion;
            SyncLocalPetDeckToCustomProperties();
            return true;
        }
        return false;
    }

    public CardData FindCardDataByName(string cardName)
    {
        // First check pet card pools
        CardData found = allPetCardPool.FirstOrDefault(c => c.cardName == cardName);
        if (found == null)
        {
            found = starterPetDeck.FirstOrDefault(c => c.cardName == cardName);
        }
        
        // Check upgraded versions
        if (found == null)
        {
            foreach (var card in allPetCardPool.Concat(starterPetDeck))
            {
                if (card.upgradedVersion != null && card.upgradedVersion.cardName == cardName)
                {
                    return card.upgradedVersion;
                }
            }
        }
        
        return found;
    }

    // Getters
    public List<CardData> GetLocalPetDeck() => localPetDeck;
    public List<CardData> GetOpponentPetDeck() => opponentPetDeck;
    public List<CardData> GetOpponentPetHand() => opponentPetHand;
    public List<CardData> GetAllOwnedPetCards() => new List<CardData>(localPetDeck);
    public int GetOpponentPetEnergy() => opponentPetEnergy;
    public void SetOpponentPetEnergy(int energy) => opponentPetEnergy = energy;
}