using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Controls the flow of combat, including turn management and card play resolution.
/// Simplifies and standardizes the combat flow for better readability and maintainability.
/// </summary>
public class CombatFlowController
{
    private GameManager gameManager;
    private int currentTurnNumber = 0;
    private int currentCombatTurn = 0;
    private CombatState currentState = CombatState.NotInCombat;
    
    // Reference to entities involved in combat
    private CombatEntity localPlayer;
    private CombatEntity localPet;
    private CombatEntity opponentPet;
    
    // Entity starting values
    private int startingPlayerEnergy;
    private int startingPetEnergy;
    
    public enum CombatState
    {
        NotInCombat,
        PlayerTurn,
        OpponentPetTurn,
        CombatEnded
    }
    
    public CombatFlowController(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(CombatEntity localPlayer, CombatEntity localPet, CombatEntity opponentPet, 
                            int startingPlayerEnergy, int startingPetEnergy)
    {
        this.localPlayer = localPlayer;
        this.localPet = localPet;
        this.opponentPet = opponentPet;
        this.startingPlayerEnergy = startingPlayerEnergy;
        this.startingPetEnergy = startingPetEnergy;
        
        currentTurnNumber = 0;
        currentCombatTurn = 0;
        currentState = CombatState.NotInCombat;
        
        Debug.Log("CombatFlowController initialized with entities and starting values");
    }
    
    public void StartCombat()
    {
        Debug.Log("Starting combat sequence");
        currentState = CombatState.NotInCombat; // Will be set to PlayerTurn in StartPlayerTurn
        StartPlayerTurn();
    }
    
    public void StartPlayerTurn()
    {
        Debug.Log("Starting Player Turn");
        
        // Update turn counters
        currentTurnNumber++;
        if (currentCombatTurn == 0) currentCombatTurn = 1;
        
        // Set state
        currentState = CombatState.PlayerTurn;
        
        // Reset combo counter
        gameManager.GetPlayerManager().ResetComboCount();
        
        // Process turn start effects for all entities
        localPlayer.ProcessTurnStartEffects();
        localPet.ProcessTurnStartEffects();
        
        // Check for deaths after effects
        if (gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer())
        {
            EndCombat();
            return;
        }
        
        // Reset energy
        localPlayer.ResetEnergy();
        
        // Reset opponent pet energy
        opponentPet.SetEnergy(startingPetEnergy);
        Debug.Log($"Opponent Pet Energy RESET to {startingPetEnergy} at start of player turn");
        
        // Notify opponent via RPC
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            gameManager.GetPhotonView()?.RPC("RpcResetMyPetEnergyProp", opponentPlayer, startingPetEnergy);
        }
        
        // Sync turn number to network
        PublishCombatStatus();
        
        // Draw opponent pet hand
        Debug.Log("Drawing opponent pet's hand");
        int petCardsToDraw = 5; // Assuming same as player
        gameManager.GetCardManager().GetPetDeckManager().DrawOpponentPetHand(petCardsToDraw);
        
        // Draw player hand
        gameManager.GetCardManager().DrawHand();
        
        // Enable end turn button
        gameManager.GetCombatUIManager().SetEndTurnButtonInteractable(true);
        
        // Update UI
        gameManager.UpdateHealthUI();
        gameManager.UpdateHandUI();
        gameManager.UpdateDeckCountUI();
        
        // Update other fights UI
        gameManager.GetCombatUIManager().UpdateOtherFightsUI();
    }
    
    public IEnumerator EndPlayerTurn()
    {
        Debug.Log("Ending Player Turn");
        
        // Disable end turn button to prevent double clicks
        gameManager.GetCombatUIManager().SetEndTurnButtonInteractable(false);
        
        // Increment combat turn counter
        currentCombatTurn++;
        
        // Process end-of-turn effects
        gameManager.GetCardManager().ProcessEndOfTurnHandEffects();
        
        // Discard hand
        gameManager.GetCardManager().DiscardHand();
        gameManager.UpdateHandUI();
        gameManager.UpdateDeckCountUI();
        
        // Reset opponent pet block before their turn
        opponentPet.ResetBlock();
        gameManager.UpdateHealthUI();
        
        // Decrement player crit buffs
        localPlayer.DecrementCritBuffs();
        gameManager.UpdateHealthUI();
        
        // Set state to opponent pet turn
        currentState = CombatState.OpponentPetTurn;
        
        // Start opponent pet turn
        Debug.Log("Starting Opponent Pet Turn");
        IEnumerator petTurnEnumerator = gameManager.GetCardManager().ExecuteOpponentPetTurn(startingPetEnergy);
        
        // Process the opponent pet's turn, yielding when needed
        while (petTurnEnumerator.MoveNext())
        {
            object currentYield = petTurnEnumerator.Current;
            
            if (currentYield is CardData cardPlayed)
            {
                // 1. Visualize the card
                Debug.Log($"Visualizing opponent pet card: {cardPlayed.cardName}");
                yield return gameManager.StartCoroutine(
                    gameManager.GetCombatUIManager().VisualizeOpponentPetCardPlay(cardPlayed));
                
                // 2. Notify pet owner about card play for energy decrement
                Player opponentPetOwner = gameManager.GetPlayerManager().GetOpponentPlayer();
                if (PhotonNetwork.InRoom && opponentPetOwner != null)
                {
                    gameManager.GetPhotonView()?.RPC("RpcOpponentPlayedCard", opponentPetOwner, cardPlayed.cost);
                }
                
                // 3. Apply card effects
                Debug.Log($"Applying effects for: {cardPlayed.cardName}");
                gameManager.GetCardManager().ProcessOpponentPetCardEffect(cardPlayed);
                
                // 4. Update UI
                gameManager.UpdateHealthUI();
            }
            else if (currentYield != null)
            {
                yield return currentYield;
            }
            else
            {
                yield return null;
            }
        }
        
        Debug.Log("Opponent Pet Turn Finished");
        
        // Reset player block after opponent's turn
        localPlayer.ResetBlock();
        
        // Check if combat has ended
        if (gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer())
        {
            EndCombat();
            yield break;
        }
        
        // Start next player turn
        StartPlayerTurn();
    }
    
    private void EndCombat()
    {
        Debug.Log("Combat has ended");
        currentState = CombatState.CombatEnded;
        
        // Additional cleanup or state changes if needed
    }
    
    public bool IsPlayerTurn()
    {
        return currentState == CombatState.PlayerTurn && !gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer();
    }
    
    private void PublishCombatStatus()
    {
        if (!PhotonNetwork.InRoom) return;
        
        // Get current status values
        int opponentPetHealth = opponentPet?.GetHealth() ?? gameManager.GetPlayerManager().GetOpponentPetHealth();
        int playerHealth = localPlayer?.GetHealth() ?? gameManager.GetPlayerManager().GetLocalPlayerHealth();
        
        // Create properties hashtable
        ExitGames.Client.Photon.Hashtable combatProps = new ExitGames.Client.Photon.Hashtable
        {
            { CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, opponentPetHealth },
            { CombatStateManager.PLAYER_COMBAT_TURN_PROP, currentTurnNumber },
            { CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, playerHealth }
        };
        
        // Set properties on local player
        PhotonNetwork.LocalPlayer.SetCustomProperties(combatProps);
    }
    
    // Getters
    public int GetCurrentTurnNumber() => currentTurnNumber;
    public int GetCurrentCombatTurn() => currentCombatTurn;
    public CombatState GetCurrentState() => currentState;
}