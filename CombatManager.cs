using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq; // Needed for Find
using Newtonsoft.Json;

public class CombatManager
{
    private GameManager gameManager;
    private int startingPlayerHealth;
    private int startingPetHealth;
    private int startingEnergy;

    // UI References
    private TextMeshProUGUI playerNameText;
    private Slider playerHealthSlider;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerBlockText;
    private TextMeshProUGUI energyText;
    private TextMeshProUGUI opponentPetNameText;
    private Slider opponentPetHealthSlider;
    private TextMeshProUGUI opponentPetHealthText;
    private TextMeshProUGUI opponentPetBlockText;
    private TextMeshProUGUI opponentPetIntentText;
    private TextMeshProUGUI ownPetNameText;
    private Slider ownPetHealthSlider;
    private TextMeshProUGUI ownPetHealthText;
    private TextMeshProUGUI ownPetBlockText;
    private GameObject playerHandPanel;
    private TextMeshProUGUI deckCountText;
    private TextMeshProUGUI discardCountText;
    private Button endTurnButton;

    // Added UI References for Deck View
    private Button viewPlayerDeckButton;
    private Button viewPetDeckButton;
    private Button viewOppPetDeckButton;
    private DeckViewController deckViewController;

    public void Initialize(GameManager gameManager, int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        this.gameManager = gameManager;
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingEnergy = startingEnergy;
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;

        Transform topArea = combatInstance.transform.Find("TopArea");
        Transform playerArea = combatInstance.transform.Find("PlayerArea");

        if (topArea == null || playerArea == null)
        {
            Debug.LogError("Could not find TopArea or PlayerArea in CombatCanvas!");
            return;
        }

        // Find opponent pet area
        Transform opponentAreaContainer = topArea.Find("OpponentPetAreaContainer");
        Transform opponentArea = opponentAreaContainer?.Find("OpponentPetArea");
        opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
        opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
        opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
        opponentPetBlockText = opponentArea?.Find("OpponentPetBlockText")?.GetComponent<TextMeshProUGUI>();
        opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
        
        // Find own pet area
        Transform ownPetAreaContainer = topArea.Find("OwnPetAreaContainer");
        Transform ownPetArea = ownPetAreaContainer?.Find("OwnPetArea");
        ownPetNameText = ownPetArea?.Find("OwnPetNameText")?.GetComponent<TextMeshProUGUI>();
        ownPetHealthSlider = ownPetArea?.Find("OwnPetHealthSlider")?.GetComponent<Slider>();
        ownPetHealthText = ownPetArea?.Find("OwnPetHealthText")?.GetComponent<TextMeshProUGUI>();
        ownPetBlockText = ownPetArea?.Find("OwnPetBlockText")?.GetComponent<TextMeshProUGUI>();

        // Find elements within PlayerArea
        Transform statsRow = playerArea.Find("StatsRow");
        playerNameText = statsRow?.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        playerHealthSlider = statsRow?.Find("PlayerHealthSlider")?.GetComponent<Slider>();
        playerHealthText = statsRow?.Find("PlayerHealthText")?.GetComponent<TextMeshProUGUI>();
        playerBlockText = statsRow?.Find("PlayerBlockText")?.GetComponent<TextMeshProUGUI>();
        energyText = statsRow?.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        
        Transform handPanelTransform = playerArea.Find("PlayerHandPanel");
        playerHandPanel = handPanelTransform?.gameObject;
        
        Transform bottomBar = playerArea.Find("BottomBar");
        deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
        discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
        endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
        
        // --- ADDED: Find Deck View Buttons ---
        viewPlayerDeckButton = bottomBar?.Find("ViewPlayerDeckButton")?.GetComponent<Button>();
        viewPetDeckButton = bottomBar?.Find("ViewPetDeckButton")?.GetComponent<Button>();
        viewOppPetDeckButton = bottomBar?.Find("ViewOppPetDeckButton")?.GetComponent<Button>();

        // --- ADDED: Find DeckViewController ---
        // Assuming DeckViewerPanel is instantiated under the CombatCanvas or accessible via GameManager
        GameObject deckViewerPanelInstance = combatInstance.transform.Find("DeckViewerPanel")?.gameObject;
        if (deckViewerPanelInstance == null && gameManager.GetDeckViewerPanelPrefab() != null) // Fallback: Instantiate if not found
        {
            // Instantiate it under the combat canvas so it's part of the UI layer
            deckViewerPanelInstance = Object.Instantiate(gameManager.GetDeckViewerPanelPrefab(), combatInstance.transform);
            deckViewerPanelInstance.name = "DeckViewerPanel"; // Ensure consistent name
        }

        if (deckViewerPanelInstance != null)
        {
            deckViewController = deckViewerPanelInstance.GetComponent<DeckViewController>();
            if (deckViewController == null)
            {
                Debug.LogError("DeckViewController component not found on the DeckViewerPanel instance!");
            }
        }
        else
        {
            Debug.LogError("DeckViewerPanel instance could not be found or instantiated!");
        }

        // Assign listeners
        endTurnButton?.onClick.AddListener(EndTurn);
        // --- ADDED: Assign Deck View Listeners ---
        viewPlayerDeckButton?.onClick.AddListener(ShowPlayerDeck);
        viewPetDeckButton?.onClick.AddListener(ShowPetDeck);
        viewOppPetDeckButton?.onClick.AddListener(ShowOpponentPetDeck);

        // Validate critical findings 
        if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null)
            Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab!");
        else 
            Debug.Log("Successfully found critical combat UI elements.");
    }

    public void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        gameManager.GetPlayerManager().InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
        gameManager.GetCardManager().InitializeDecks();
        
        // Setup Initial UI
        if (playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        UpdateHealthUI();
        
        if (opponentPetNameText)
        {
            Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
            opponentPetNameText.text = opponent != null ? $"{opponent.NickName}'s Pet" : "Opponent Pet";
        }
        
        if (ownPetNameText) ownPetNameText.text = gameManager.GetPlayerManager().GetLocalPetName();
        
        UpdateDeckCountUI();
        
        // Start the first turn
        StartTurn();
    }

    public void StartTurn()
    {
        Debug.Log("Starting Player Turn");
        gameManager.GetPlayerManager().ResetAllBlock();
        gameManager.GetPlayerManager().SetCurrentEnergy(startingEnergy);
        UpdateEnergyUI();

        gameManager.GetCardManager().DrawHand();
        UpdateHandUI();
        UpdateDeckCountUI();

        // Set opponent pet intent
        if (opponentPetIntentText) opponentPetIntentText.text = "Intent: Attack 5"; // Placeholder

        // Make cards playable
        if (endTurnButton) endTurnButton.interactable = true;
    }

    public void EndTurn()
    {
        Debug.Log("Ending Player Turn");
        if (endTurnButton) endTurnButton.interactable = false; // Prevent double clicks

        // 1. Discard Hand
        gameManager.GetCardManager().DiscardHand();
        UpdateHandUI(); // Clear hand display
        UpdateDeckCountUI();

        // 2. Opponent Pet Acts
        gameManager.GetCardManager().ExecuteOpponentPetTurn(startingEnergy);

        // 3. Start Next Player Turn (if combat hasn't ended)
        if (!gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer())
        {
            StartTurn();
        }
    }

    public void UpdateHealthUI()
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        // Player Health & Block
        if (playerHealthSlider) playerHealthSlider.value = (float)playerManager.GetLocalPlayerHealth() / startingPlayerHealth;
        if (playerHealthText) playerHealthText.text = $"{playerManager.GetLocalPlayerHealth()} / {startingPlayerHealth}";
        if (playerBlockText) playerBlockText.text = $"Block: {playerManager.GetLocalPlayerBlock()}";

        // Own Pet Health & Block
        if (ownPetHealthSlider) ownPetHealthSlider.value = (float)playerManager.GetLocalPetHealth() / startingPetHealth;
        if (ownPetHealthText) ownPetHealthText.text = $"{playerManager.GetLocalPetHealth()} / {startingPetHealth}";
        if (ownPetBlockText) ownPetBlockText.text = $"Block: {playerManager.GetLocalPetBlock()}";

        // Opponent Pet Health & Block
        if (opponentPetHealthSlider) opponentPetHealthSlider.value = (float)playerManager.GetOpponentPetHealth() / startingPetHealth;
        if (opponentPetHealthText) opponentPetHealthText.text = $"{playerManager.GetOpponentPetHealth()} / {startingPetHealth}";
        if (opponentPetBlockText) opponentPetBlockText.text = $"Block: {playerManager.GetOpponentPetBlock()}";
    }

    public void UpdateHandUI()
    {
        GameObject cardPrefab = gameManager.GetCardPrefab();
        if (playerHandPanel == null || cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // Clear existing card visuals
        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        // Instantiate new card visuals for cards in hand
        foreach (CardData card in gameManager.GetCardManager().GetHand())
        {
            GameObject cardGO = Object.Instantiate(cardPrefab, playerHandPanel.transform);
            cardGO.name = $"Card_{card.cardName}";
            
            Transform headerPanel = cardGO.transform.Find("HeaderPanel");
            Transform descPanel = cardGO.transform.Find("DescPanel");
            Transform artPanel = cardGO.transform.Find("ArtPanel");

            TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
            Image artImage = artPanel?.GetComponent<Image>();

            if (nameText != null) nameText.text = card.cardName;
            if (costText != null) costText.text = card.cost.ToString();
            if (descText != null) descText.text = card.description;
            
            // Get the handler and assign the data
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.cardData = card;
            }
            else
            {
                Debug.LogError($"CardDragHandler component not found on instantiated card prefab: {cardGO.name}");
            }

            cardGO.SetActive(true);
        }
    }

    public void UpdateDeckCountUI()
    {
        CardManager cardManager = gameManager.GetCardManager();
        if (deckCountText != null) deckCountText.text = $"Deck: {cardManager.GetDeckCount()}";
        if (discardCountText != null) discardCountText.text = $"Discard: {cardManager.GetDiscardCount()}";
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"Energy: {gameManager.GetPlayerManager().GetCurrentEnergy()} / {gameManager.GetPlayerManager().GetStartingEnergy()}";
        }
        else
        {
            Debug.LogWarning("UpdateEnergyUI: energyText reference is null!");
        }
    }

    public bool IsPlayerTurn()
    {
        return !gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType)
    {
        return gameManager.GetCardManager().AttemptPlayCard(cardData, targetType);
    }

    public void DisableEndTurnButton()
    {
        if (endTurnButton) endTurnButton.interactable = false;
    }

    public GameObject GetPlayerHandPanel()
    {
        return playerHandPanel;
    }

    // --- ADDED Deck Viewing Methods ---
    private void ShowPlayerDeck()
    {
        if (deckViewController == null) return;
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        // Combine deck and discard for a full view
        List<CardData> fullPlayerDeck = new List<CardData>(cardManager.GetDeck());
        fullPlayerDeck.AddRange(cardManager.GetDiscardPile());
        // Optional: Sort the deck for display?
        // fullPlayerDeck = fullPlayerDeck.OrderBy(card => card.cost).ThenBy(card => card.cardName).ToList();

        deckViewController.ShowDeck($"{PhotonNetwork.LocalPlayer.NickName}'s Deck ({fullPlayerDeck.Count})", fullPlayerDeck);
    }

    private void ShowPetDeck()
    {
        if (deckViewController == null) return;
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        List<CardData> petDeck = cardManager.GetLocalPetDeck() ?? new List<CardData>();
        // Optional: Sort the deck for display?
        // petDeck = petDeck.OrderBy(card => card.cost).ThenBy(card => card.cardName).ToList();

        deckViewController.ShowDeck($"{gameManager.GetPlayerManager().GetLocalPetName()}'s Deck ({petDeck.Count})", petDeck);
    }

    private void ShowOpponentPetDeck()
    {
        if (deckViewController == null) return;
        Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
        CardManager cardManager = gameManager.GetCardManager();
        if (opponent == null || cardManager == null) return;

        List<CardData> opponentPetDeck = cardManager.GetOpponentPetDeck() ?? new List<CardData>(); // Get directly from CardManager state
        // Construct the pet name based on opponent's nickname
        string opponentPetName = opponent.NickName + "'s Pet"; // Simple construction
        // Fallback if opponent somehow becomes null after check (unlikely but safe)
        if (string.IsNullOrEmpty(opponent.NickName)) opponentPetName = "Opponent Pet";
        
        string title = $"{opponentPetName} Deck ({opponentPetDeck.Count})";

        // Optional: Sort the deck for display?
        // opponentPetDeck = opponentPetDeck.OrderBy(card => card.cost).ThenBy(card => card.cardName).ToList();

        deckViewController.ShowDeck(title, opponentPetDeck);
    }
    // --- END ADDED Deck Viewing Methods ---
}