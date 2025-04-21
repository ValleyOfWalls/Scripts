using UnityEngine;
using System.Collections;
using Photon.Pun;

public class CombatManager
{
    private GameManager gameManager;
    private int startingPlayerHealth;
    private int startingPetHealth;
    private int startingPlayerEnergy;
    private int startingPetEnergy;

    // Helper managers
    private CombatUIManager uiManager;
    private CombatTurnManager turnManager;
    private DeckViewManager deckViewManager;

    public void Initialize(GameManager gameManager, int startingPlayerHealth, int startingPetHealth, int startingPlayerEnergy, int startingPetEnergy)
    {
        this.gameManager = gameManager;
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingPlayerEnergy = startingPlayerEnergy;
        this.startingPetEnergy = startingPetEnergy;
        
        // Initialize helper managers
        uiManager = new CombatUIManager(gameManager);
        turnManager = new CombatTurnManager(gameManager, uiManager, startingPlayerEnergy, startingPetEnergy);
        deckViewManager = new DeckViewManager(gameManager);
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;

        // Initialize UI references in helper managers
        uiManager.InitializeUIReferences(combatInstance);
        deckViewManager.InitializeReferences(combatInstance);
        
        // Assign listeners for end turn button
        uiManager.GetEndTurnButton()?.onClick.RemoveAllListeners();
        uiManager.GetEndTurnButton()?.onClick.AddListener(() => gameManager.StartCoroutine(EndTurn()));
    }

    public void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        gameManager.GetPlayerManager().InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
        gameManager.GetCardManager().InitializeDecks();
        
        // Set opponent pet energy for local simulation
        gameManager.GetCardManager().GetPetDeckManager().SetOpponentPetEnergy(startingPetEnergy);
        
        // Setup Initial UI
        uiManager.InitializeUIState();
        uiManager.UpdateHealthUI();
        uiManager.UpdateDeckCountUI();
        
        // Start the first turn
        StartTurn();
        
        // Update other UI elements
        uiManager.UpdateOtherFightsUI();
        uiManager.UpdateHealthUI();
    }

    public void StartTurn()
    {
        turnManager.StartTurn();
        
        // --- ADDED: Draw Opponent Pet's Hand at Player Turn Start ---
        // Debug.Log("CombatManager.StartTurn: Drawing opponent pet's hand.");
        // int petCardsToDraw = 5; // Assuming same draw count as player
        // gameManager.GetCardManager().GetPetDeckManager().DrawOpponentPetHand(petCardsToDraw);
        // UpdateOpponentPetHandUI() is called inside DrawOpponentPetHand
        // --- END ADDED ---
        // --- MOVED to CombatTurnManager.StartTurn ---
    }

    public IEnumerator EndTurn()
    {
        return turnManager.EndTurn();
    }

    public void UpdateHealthUI()
    {
        uiManager.UpdateHealthUI();
    }

    public void UpdateHandUI()
    {
        uiManager.UpdateHandUI();
    }

    public void UpdateDeckCountUI()
    {
        uiManager.UpdateDeckCountUI();
    }

    public void UpdateEnergyUI()
    {
        uiManager.UpdateEnergyUI();
    }

    public bool IsPlayerTurn()
    {
        return turnManager.IsPlayerTurn();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        return gameManager.GetCardManager().AttemptPlayCard(cardData, targetType);
    }

    public void DisableEndTurnButton()
    {
        uiManager.SetEndTurnButtonInteractable(false);
    }

    public GameObject GetPlayerHandPanel()
    {
        return uiManager.GetPlayerHandPanel();
    }

    public CombatUIManager GetCombatUIManager()
    {
        return uiManager;
    }

    public CombatTurnManager GetCombatTurnManager()
    {
        return turnManager;
    }
}