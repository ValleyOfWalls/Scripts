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

    public void Initialize(GameManager gameManager, int startingPlayerHealth, int startingPetHealth, int startingPlayerEnergy, int startingPetEnergy, CombatUIManager uiManager = null)
    {
        this.gameManager = gameManager;
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingPlayerEnergy = startingPlayerEnergy;
        this.startingPetEnergy = startingPetEnergy;
        
        // Use provided UIManager or create a new one if not provided
        this.uiManager = uiManager ?? new CombatUIManager(gameManager);
        turnManager = new CombatTurnManager(gameManager, this.uiManager, startingPlayerEnergy, startingPetEnergy);
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
        // Now just delegating to GameManager's implementation which handles the new entity system
        gameManager.InitializeCombatState(opponentPetOwnerActorNum);
    }

    public void StartTurn()
    {
        turnManager.StartTurn();
    }

    public IEnumerator EndTurn()
    {
        CombatFlowController flowController = gameManager.GetCombatFlowController();
        return flowController.EndPlayerTurn();
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
        CombatFlowController flowController = gameManager.GetCombatFlowController();
        return flowController.IsPlayerTurn();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType, GameObject cardGO = null)
    {
        return gameManager.GetCardManager().AttemptPlayCard(cardData, targetType, cardGO);
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