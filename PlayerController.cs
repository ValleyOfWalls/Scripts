using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Properties and Variables
    
    // References
    [Header("References")]
    private DeckManager deckManager;
    private CardUIManager cardUIManager;
    private GameplayManager gameplayManager;
    private GameObject playerUICanvas;
    
    // Player Stats
    [Header("Stats")]
    [SerializeField] private string playerName;
    [SerializeField] private int maxHealth = 80;
    [SerializeField] private int currentHealth;
    [SerializeField] private int maxEnergy = 3;
    [SerializeField] private int currentEnergy;
    [SerializeField] private int block = 0;
    [SerializeField] private int strengthModifier = 0;
    [SerializeField] private int dexterityModifier = 0;
    
    // UI Elements
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI energyText;
    private TextMeshProUGUI blockText;
    private TextMeshProUGUI strengthText;
    private TextMeshProUGUI dexterityText;
    private Slider healthSlider;
    
    // Game state
    [SerializeField] private bool isMyTurn = false;
    [SerializeField] private bool isInCombat = false;
    [SerializeField] private int roundsWon = 0;
    private MonsterController opponentMonster;
    
    // Card-related 
    [SerializeField] private List<Card> deck = new List<Card>();
    [SerializeField] private List<Card> hand = new List<Card>();
    [SerializeField] private List<Card> discardPile = new List<Card>();
    [SerializeField] private int handSize = 5;
    
    // Pet References
    [Header("Pet References")]
    [SerializeField] private MonsterController petMonster; // Reference to the player's own monster
    
    // Properties
    public string PlayerName { get => playerName; private set => playerName = value; }
    public int MaxHealth { get => maxHealth; set => maxHealth = value; }
    public int CurrentHealth { get => currentHealth; set => currentHealth = value; }
    public int MaxEnergy { get => maxEnergy; set => maxEnergy = value; }
    public int CurrentEnergy { get => currentEnergy; set => currentEnergy = value; }
    public int Block { get => block; set => block = value; }
    public int StrengthModifier { get => strengthModifier; set => strengthModifier = value; }
    public int DexterityModifier { get => dexterityModifier; set => dexterityModifier = value; }
    public int RoundsWon { get => roundsWon; set => roundsWon = value; }
    public bool IsMyTurn { get => isMyTurn; set => isMyTurn = value; }
    public bool IsInCombat { get => isInCombat; set => isInCombat = value; }
    public MonsterController PetMonster { get => petMonster; set => petMonster = value; }
    
    #endregion
    
    #region Unity and Photon Callbacks
    
    private void Awake()
    {
        // Set initial health and energy
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
    }
    
    private void Start()
    {
        // Try to find required objects with multiple fallbacks
        StartCoroutine(DelayedInitialization());
    }
    
    private IEnumerator DelayedInitialization()
    {
        // Wait a short time to ensure all objects are initialized
        yield return new WaitForSeconds(0.5f);
        
        // Get references
        TryGetGameplayManager();
        
        // Set player name from Photon nickname
        playerName = photonView.Owner.NickName;
        
        // Setup player visuals and UI
        if (photonView.IsMine)
        {
            SetupPlayerUI();
            InitializeDeck();
        }
        
        // Register with gameplay manager
        if (gameplayManager != null)
        {
            gameplayManager.RegisterPlayer(this);
            Debug.Log($"Player {playerName} registered with GameplayManager");
        }
        else
        {
            Debug.LogWarning($"Player {playerName} couldn't find GameplayManager to register with");
        }
    }
    
    private void TryGetGameplayManager()
    {
        // First try to find directly
        gameplayManager = FindObjectOfType<GameplayManager>();
        
        // If not found, wait a bit and try again with fallbacks
        if (gameplayManager == null)
        {
            StartCoroutine(RetryFindGameplayManager());
        }
    }
    
    private IEnumerator RetryFindGameplayManager()
    {
        int retryCount = 0;
        while (gameplayManager == null && retryCount < 5)
        {
            yield return new WaitForSeconds(0.5f);
            
            // Try direct find again
            gameplayManager = FindObjectOfType<GameplayManager>();
            
            // Try via GameManager as fallback
            if (gameplayManager == null && GameManager.Instance != null)
            {
                gameplayManager = GameManager.Instance.GetGameplayManager();
            }
            
            retryCount++;
        }
        
        if (gameplayManager == null)
        {
            Debug.LogWarning($"Player {playerName}: Failed to find GameplayManager after retries");
        }
        else
        {
            Debug.Log($"Player {playerName}: Found GameplayManager after retries");
            
            // Register with gameplay manager if found
            gameplayManager.RegisterPlayer(this);
        }
    }
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        // Only handle input if it's our turn and we're in combat
        if (isMyTurn && isInCombat)
        {
            // Could add any input controls here if needed
        }
        
        UpdateUI();
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Sync important variables over the network
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(playerName);
            stream.SendNext(currentHealth);
            stream.SendNext(maxHealth);
            stream.SendNext(currentEnergy);
            stream.SendNext(maxEnergy);
            stream.SendNext(block);
            stream.SendNext(strengthModifier);
            stream.SendNext(dexterityModifier);
            stream.SendNext(roundsWon);
            stream.SendNext(isMyTurn); // Make sure this syncs
            stream.SendNext(isInCombat);
        }
        else
        {
            // Receive data
            playerName = (string)stream.ReceiveNext();
            currentHealth = (int)stream.ReceiveNext();
            maxHealth = (int)stream.ReceiveNext();
            currentEnergy = (int)stream.ReceiveNext();
            maxEnergy = (int)stream.ReceiveNext();
            block = (int)stream.ReceiveNext();
            strengthModifier = (int)stream.ReceiveNext();
            dexterityModifier = (int)stream.ReceiveNext();
            roundsWon = (int)stream.ReceiveNext();
            isMyTurn = (bool)stream.ReceiveNext(); // Make sure this syncs
            isInCombat = (bool)stream.ReceiveNext();
        }
    }

    
    #endregion
    
    #region UI Methods
    
    private void SetupPlayerUI()
    {
        // Create player UI canvas
        playerUICanvas = new GameObject("PlayerUICanvas");
        Canvas canvas = playerUICanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = playerUICanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        playerUICanvas.AddComponent<GraphicRaycaster>();
        
        // Create player stats panel
        GameObject statsPanel = new GameObject("StatsPanel");
        statsPanel.transform.SetParent(playerUICanvas.transform, false);
        RectTransform statsPanelRect = statsPanel.AddComponent<RectTransform>();
        statsPanelRect.anchorMin = new Vector2(0, 1);
        statsPanelRect.anchorMax = new Vector2(0.2f, 1);
        statsPanelRect.pivot = new Vector2(0, 1);
        statsPanelRect.anchoredPosition = new Vector2(20, -20);
        statsPanelRect.sizeDelta = new Vector2(0, 200);
        Image statsPanelImage = statsPanel.AddComponent<Image>();
        statsPanelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Setup vertical layout group for stats
        VerticalLayoutGroup statsLayout = statsPanel.AddComponent<VerticalLayoutGroup>();
        statsLayout.padding = new RectOffset(10, 10, 10, 10);
        statsLayout.spacing = 5;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = false;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = false;
        statsLayout.childAlignment = TextAnchor.UpperLeft;
        
        // Create name text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(statsPanel.transform, false);
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 24;
        nameText.color = Color.white;
        nameText.text = "Name: " + playerName;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 30);
        
        // Create health text and slider
        GameObject healthObj = new GameObject("HealthContainer");
        healthObj.transform.SetParent(statsPanel.transform, false);
        RectTransform healthContainerRect = healthObj.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 50);
        
        GameObject healthTextObj = new GameObject("HealthText");
        healthTextObj.transform.SetParent(healthObj.transform, false);
        healthText = healthTextObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 20;
        healthText.color = Color.white;
        healthText.text = "HP: " + currentHealth + "/" + maxHealth;
        RectTransform healthTextRect = healthTextObj.GetComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(0, 0.6f);
        healthTextRect.anchorMax = new Vector2(1, 1);
        healthTextRect.sizeDelta = Vector2.zero;
        
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(healthObj.transform, false);
        healthSlider = sliderObj.AddComponent<Slider>();
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(1, 0.5f);
        sliderRect.sizeDelta = Vector2.zero;
        
        // Create slider background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        // Create slider fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(sliderObj.transform, false);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.8f, 0.2f, 0.2f, 1);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        // Configure slider
        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fillImage;
        healthSlider.minValue = 0;
        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
        
        // Create energy text
        GameObject energyObj = new GameObject("EnergyText");
        energyObj.transform.SetParent(statsPanel.transform, false);
        energyText = energyObj.AddComponent<TextMeshProUGUI>();
        energyText.fontSize = 20;
        energyText.color = new Color(0.2f, 0.6f, 1f);
        energyText.text = "Energy: " + currentEnergy + "/" + maxEnergy;
        RectTransform energyRect = energyObj.GetComponent<RectTransform>();
        energyRect.sizeDelta = new Vector2(0, 30);
        
        // Create block text
        GameObject blockObj = new GameObject("BlockText");
        blockObj.transform.SetParent(statsPanel.transform, false);
        blockText = blockObj.AddComponent<TextMeshProUGUI>();
        blockText.fontSize = 20;
        blockText.color = new Color(0.4f, 0.8f, 1f);
        blockText.text = "Block: " + block;
        RectTransform blockRect = blockObj.GetComponent<RectTransform>();
        blockRect.sizeDelta = new Vector2(0, 30);
        
        // Create strength text
        GameObject strengthObj = new GameObject("StrengthText");
        strengthObj.transform.SetParent(statsPanel.transform, false);
        strengthText = strengthObj.AddComponent<TextMeshProUGUI>();
        strengthText.fontSize = 20;
        strengthText.color = new Color(1f, 0.5f, 0.5f);
        strengthText.text = "Strength: " + strengthModifier;
        RectTransform strengthRect = strengthObj.GetComponent<RectTransform>();
        strengthRect.sizeDelta = new Vector2(0, 30);
        
        // Create dexterity text
        GameObject dexterityObj = new GameObject("DexterityText");
        dexterityObj.transform.SetParent(statsPanel.transform, false);
        dexterityText = dexterityObj.AddComponent<TextMeshProUGUI>();
        dexterityText.fontSize = 20;
        dexterityText.color = new Color(0.5f, 0.9f, 0.5f);
        dexterityText.text = "Dexterity: " + dexterityModifier;
        RectTransform dexterityRect = dexterityObj.GetComponent<RectTransform>();
        dexterityRect.sizeDelta = new Vector2(0, 30);
        
        // Create pet info panel
        CreatePetInfoPanel();
    }
    
    private void CreatePetInfoPanel()
    {
        // Create a panel to show basic info about the player's pet
        GameObject petPanel = new GameObject("PetInfoPanel");
        petPanel.transform.SetParent(playerUICanvas.transform, false);
        
        RectTransform petPanelRect = petPanel.AddComponent<RectTransform>();
        petPanelRect.anchorMin = new Vector2(0.8f, 1);
        petPanelRect.anchorMax = new Vector2(1, 1);
        petPanelRect.pivot = new Vector2(1, 1);
        petPanelRect.anchoredPosition = new Vector2(-20, -20);
        petPanelRect.sizeDelta = new Vector2(0, 100);
        
        Image petPanelImage = petPanel.AddComponent<Image>();
        petPanelImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        // Create title
        GameObject petTitleObj = new GameObject("PetTitle");
        petTitleObj.transform.SetParent(petPanel.transform, false);
        
        TextMeshProUGUI petTitleText = petTitleObj.AddComponent<TextMeshProUGUI>();
        petTitleText.text = "My Pet";
        petTitleText.fontSize = 18;
        petTitleText.alignment = TextAlignmentOptions.Center;
        petTitleText.color = Color.white;
        
        RectTransform petTitleRect = petTitleObj.GetComponent<RectTransform>();
        petTitleRect.anchorMin = new Vector2(0, 0.7f);
        petTitleRect.anchorMax = new Vector2(1, 1);
        petTitleRect.sizeDelta = Vector2.zero;
        
        // Pet status will be updated when the pet is assigned
    }
    
    private void UpdatePetInfoUI()
    {
        if (!photonView.IsMine || petMonster == null) return;
        
        // Update pet info in the UI
        Transform petPanel = playerUICanvas.transform.Find("PetInfoPanel");
        if (petPanel == null) return;
        
        // Check if pet info elements already exist
        Transform petNameObj = petPanel.Find("PetName");
        Transform petHealthObj = petPanel.Find("PetHealth");
        
        // Create or update pet name
        if (petNameObj == null)
        {
            // Create pet name
            GameObject nameObj = new GameObject("PetName");
            nameObj.transform.SetParent(petPanel, false);
            
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = petMonster.MonsterName;
            nameText.fontSize = 16;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.4f);
            nameRect.anchorMax = new Vector2(1, 0.7f);
            nameRect.sizeDelta = Vector2.zero;
        }
        else
        {
            TextMeshProUGUI nameText = petNameObj.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = petMonster.MonsterName;
            }
        }
        
        // Create or update pet health
        if (petHealthObj == null)
        {
            // Create pet health
            GameObject healthObj = new GameObject("PetHealth");
            healthObj.transform.SetParent(petPanel, false);
            
            TextMeshProUGUI healthText = healthObj.AddComponent<TextMeshProUGUI>();
            healthText.text = $"HP: {petMonster.CurrentHealth}/{petMonster.MaxHealth}";
            healthText.fontSize = 14;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.color = Color.white;
            
            RectTransform healthRect = healthObj.GetComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0, 0.1f);
            healthRect.anchorMax = new Vector2(1, 0.4f);
            healthRect.sizeDelta = Vector2.zero;
        }
        else
        {
            TextMeshProUGUI healthText = petHealthObj.GetComponent<TextMeshProUGUI>();
            if (healthText != null)
            {
                healthText.text = $"HP: {petMonster.CurrentHealth}/{petMonster.MaxHealth}";
            }
        }
    }
    
    private void UpdateUI()
    {
        if (nameText != null) 
            nameText.text = "Name: " + playerName;
        
        if (healthText != null)
            healthText.text = "HP: " + currentHealth + "/" + maxHealth;
        
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        
        if (energyText != null)
            energyText.text = "Energy: " + currentEnergy + "/" + maxEnergy;
        
        if (blockText != null)
            blockText.text = "Block: " + block;
        
        if (strengthText != null)
            strengthText.text = "Strength: " + strengthModifier;
        
        if (dexterityText != null)
            dexterityText.text = "Dexterity: " + dexterityModifier;
            
        // Update pet info if available
        if (petMonster != null)
        {
            UpdatePetInfoUI();
        }
    }
    
    #endregion
    
    #region Combat Methods
    
    public void StartTurn()
    {
        // Set turn state first to ensure it's correctly synced
        isMyTurn = photonView.IsMine;
        
        // Only the owner should perform these operations
        if (!photonView.IsMine) return;
        
        Debug.Log($"Player {PlayerName} starting turn");
        
        // Reset energy at start of turn
        currentEnergy = maxEnergy;
        
        // Draw cards
        DrawCards(handSize - hand.Count);
        
        // Update UI
        UpdateUI();
    }

    
    public void EndTurn()
    {
        if (!photonView.IsMine) return;
        
        // Discard hand
        DiscardHand();
        
        // Reset block at end of turn
        block = 0;
        
        // Set turn state
        isMyTurn = false;
        
        // Notify game manager that our turn is over
        if (gameplayManager != null)
            gameplayManager.PlayerEndedTurn();
        
        // Update UI
        UpdateUI();
    }
    
    [PunRPC]
    public void TakeDamage(int damage, string sourceId)
    {
        // Calculate actual damage after block and modifiers
        int actualDamage = Mathf.Max(0, damage - block);
        
        // Reduce block
        block = Mathf.Max(0, block - damage);
        
        // Apply damage to health
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);
        
        // Check if player is defeated
        if (currentHealth <= 0)
        {
            photonView.RPC("RPC_PlayerDefeated", RpcTarget.All);
        }
        
        // Update UI
        UpdateUI();
        
        // Update battle overview UI
        BattleUIManager battleUI = FindObjectOfType<BattleUIManager>();
        if (battleUI != null)
        {
            battleUI.UpdateBattleCard(photonView.Owner.ActorNumber, true, currentHealth, maxHealth);
        }
    }
    
    [PunRPC]
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
        
        // Update battle overview UI
        BattleUIManager battleUI = FindObjectOfType<BattleUIManager>();
        if (battleUI != null)
        {
            battleUI.UpdateBattleCard(photonView.Owner.ActorNumber, true, currentHealth, maxHealth);
        }
    }
    
    [PunRPC]
    public void AddBlock(int amount)
    {
        block += amount + dexterityModifier;
        UpdateUI();
    }
    
    [PunRPC]
    public void ModifyStrength(int amount)
    {
        strengthModifier += amount;
        UpdateUI();
    }
    
    [PunRPC]
    public void ModifyDexterity(int amount)
    {
        dexterityModifier += amount;
        UpdateUI();
    }
    
    [PunRPC]
    private void RPC_PlayerDefeated()
    {
        // Handle player defeat
        isInCombat = false;
        
        // Notify game manager
        if (gameplayManager != null)
            gameplayManager.PlayerDefeated(photonView.Owner.ActorNumber);
    }
    
    public void WinRound()
    {
        if (!photonView.IsMine) return;
        
        // Increment rounds won
        roundsWon++;
        
        // Check for game winner
        if (gameplayManager != null)
            gameplayManager.CheckForGameWinner();
    }
    
    public void SetOpponentMonster(MonsterController monster)
    {
        opponentMonster = monster;
    }
    
    public void SetPetMonster(MonsterController monster)
    {
        petMonster = monster;
        if (monster != null)
        {
            Debug.Log($"Set pet monster for player {playerName}: {monster.MonsterName}");
            UpdatePetInfoUI();
        }
    }
    
    public void PlayCardOnPet(int cardIndex)
    {
        if (!photonView.IsMine || !isMyTurn || cardIndex >= hand.Count) return;
        
        Card cardToPlay = hand[cardIndex];
        
        // Check if we have enough energy
        if (currentEnergy < cardToPlay.EnergyCost)
        {
            Debug.Log("Not enough energy to play this card!");
            return;
        }
        
        // Make sure we have a pet to support
        if (petMonster == null)
        {
            Debug.Log("No pet monster to support!");
            return;
        }
        
        // Spend energy
        currentEnergy -= cardToPlay.EnergyCost;
        
        // Apply card effects to pet based on type
        switch (cardToPlay.CardType)
        {
            case CardType.Attack:
                // For attack cards, add temporary strength to pet
                petMonster.ModifyStrength(cardToPlay.DamageAmount / 2);
                break;
                
            case CardType.Skill:
                // Apply block to pet
                if (cardToPlay.BlockAmount > 0)
                {
                    petMonster.AddBlock(cardToPlay.BlockAmount);
                }
                
                // Apply other effects
                if (cardToPlay.HealAmount > 0)
                {
                    petMonster.Heal(cardToPlay.HealAmount);
                }
                
                if (cardToPlay.StrengthModifier != 0)
                {
                    petMonster.ModifyStrength(cardToPlay.StrengthModifier);
                }
                
                if (cardToPlay.DexterityModifier != 0)
                {
                    petMonster.ModifyDexterity(cardToPlay.DexterityModifier);
                }
                break;
                
            case CardType.Power:
                // Handle persistent effects for pet
                break;
        }
        
        // Remove card from hand and add to discard
        hand.RemoveAt(cardIndex);
        
        if (cardToPlay.Exhaust)
        {
            // Exhausted cards don't go to discard
        }
        else if (cardToPlay.CardType != CardType.Power)
        {
            discardPile.Add(cardToPlay);
        }
        
        // Update UI
        if (cardUIManager != null)
            cardUIManager.RemoveCardFromHand(cardIndex);
            
        UpdateUI();
    }
    
    #endregion
    
    #region Card Methods
    
    private void InitializeDeck()
    {
        // This should be expanded later to load the player's actual deck
        // For now, just create a basic starter deck
        
        // Add basic attack cards
        for (int i = 0; i < 5; i++)
        {
            Card strikeCard = new Card
            {
                CardId = "strike_basic",
                CardName = "Strike",
                CardDescription = "Deal 6 damage.",
                EnergyCost = 1,
                CardType = CardType.Attack,
                CardRarity = CardRarity.Basic,
                DamageAmount = 6
            };
            deck.Add(strikeCard);
        }
        
        // Add basic defense cards
        for (int i = 0; i < 5; i++)
        {
            Card defendCard = new Card
            {
                CardId = "defend_basic",
                CardName = "Defend",
                CardDescription = "Gain 5 Block.",
                EnergyCost = 1,
                CardType = CardType.Skill,
                CardRarity = CardRarity.Basic,
                BlockAmount = 5
            };
            deck.Add(defendCard);
        }
        
        // Add pet support cards
        Card supportCard = new Card
        {
            CardId = "pet_support",
            CardName = "Pet Support",
            CardDescription = "Give your pet 4 Block.",
            EnergyCost = 1,
            CardType = CardType.Skill,
            CardRarity = CardRarity.Basic,
            BlockAmount = 4
        };
        deck.Add(supportCard);
        
        Card powerUpCard = new Card
        {
            CardId = "pet_power",
            CardName = "Power Up",
            CardDescription = "Give your pet 2 Strength.",
            EnergyCost = 1,
            CardType = CardType.Skill,
            CardRarity = CardRarity.Basic,
            StrengthModifier = 2
        };
        deck.Add(powerUpCard);
        
        // Shuffle the deck
        ShuffleDeck();
        
        Debug.Log($"Player {playerName} initialized deck with {deck.Count} cards");
    }
    
    public void ShuffleDeck()
    {
        // Fisher-Yates shuffle algorithm
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }
    
    public void DrawCards(int amount)
    {
        if (!photonView.IsMine) return;
        
        for (int i = 0; i < amount; i++)
        {
            // If deck is empty, shuffle discard pile into deck
            if (deck.Count == 0 && discardPile.Count > 0)
            {
                deck.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeck();
            }
            
            // If there are still cards to draw
            if (deck.Count > 0)
            {
                Card drawnCard = deck[0];
                deck.RemoveAt(0);
                hand.Add(drawnCard);
                
                // Update UI for the drawn card
                if (cardUIManager != null)
                    cardUIManager.AddCardToHand(drawnCard);
            }
        }
    }
    
    public void DiscardHand()
    {
        if (!photonView.IsMine) return;
        
        // Move all cards from hand to discard pile
        discardPile.AddRange(hand);
        
        // Clear UI
        if (cardUIManager != null)
            cardUIManager.ClearHand();
        
        // Clear hand list
        hand.Clear();
    }
    
    public void PlayCard(int cardIndex, int targetIndex)
    {
        if (!photonView.IsMine || !isMyTurn || cardIndex >= hand.Count) return;
        
        Card cardToPlay = hand[cardIndex];
        
        // Check if we have enough energy
        if (currentEnergy < cardToPlay.EnergyCost)
        {
            Debug.Log("Not enough energy to play this card!");
            return;
        }
        
        // Spend energy
        currentEnergy -= cardToPlay.EnergyCost;
        
        // Apply card effects based on type
        switch (cardToPlay.CardType)
        {
            case CardType.Attack:
                if (opponentMonster != null)
                {
                    // Apply strength modifier to damage
                    int totalDamage = cardToPlay.DamageAmount + strengthModifier;
                    opponentMonster.TakeDamage(totalDamage, photonView.Owner.ActorNumber.ToString());
                }
                break;
                
            case CardType.Skill:
                // Apply block
                if (cardToPlay.BlockAmount > 0)
                {
                    AddBlock(cardToPlay.BlockAmount);
                }
                
                // Apply other effects
                if (cardToPlay.HealAmount > 0)
                {
                    Heal(cardToPlay.HealAmount);
                }
                
                if (cardToPlay.StrengthModifier != 0)
                {
                    ModifyStrength(cardToPlay.StrengthModifier);
                }
                
                if (cardToPlay.DexterityModifier != 0)
                {
                    ModifyDexterity(cardToPlay.DexterityModifier);
                }
                
                // Add more effects as needed...
                
                break;
                
            case CardType.Power:
                // Handle persistent effects
                // This would be expanded based on your game design
                break;
        }
        
        // Remove card from hand and add to discard (unless it's a power or exhausts)
        hand.RemoveAt(cardIndex);
        
        if (cardToPlay.Exhaust)
        {
            // Exhausted cards don't go to discard
            Debug.Log("Card exhausted: " + cardToPlay.CardName);
        }
        else if (cardToPlay.CardType != CardType.Power)
        {
            discardPile.Add(cardToPlay);
        }
        
        // Update UI
        if (cardUIManager != null)
            cardUIManager.RemoveCardFromHand(cardIndex);
            
        UpdateUI();
    }
    
    public void AddCardToDeck(Card card)
    {
        if (!photonView.IsMine) return;
        
        deck.Add(card);
        ShuffleDeck();
    }
    
    #endregion
    
    #region Public Utility Methods
    
    public void SetupDeckManager(DeckManager deckMgr)
    {
        deckManager = deckMgr;
    }
    
    public void SetupCardUIManager(CardUIManager cardUIMgr)
    {
        cardUIManager = cardUIMgr;
    }
    
    public void ResetForNewRound()
    {
        // Reset health to max
        currentHealth = maxHealth;
        
        // Reset modifiers
        block = 0;
        strengthModifier = 0;
        dexterityModifier = 0;
        
        // Clear hand and reset deck
        hand.Clear();
        discardPile.Clear();
        InitializeDeck();  // Reset to starting deck
        
        // Reset combat state
        isInCombat = false;
        isMyTurn = false;
        
        // Reset UI
        UpdateUI();
    }
    
    #endregion
}

#region Card Related Classes

public enum CardType
{
    Attack,
    Skill,
    Power
}

public enum CardRarity
{
    Basic,
    Common,
    Uncommon,
    Rare
}

[System.Serializable]
public class Card
{
    // Card Info
    public string CardId;
    public string CardName;
    public string CardDescription;
    public int EnergyCost;
    public CardType CardType;
    public CardRarity CardRarity;
    
    // Card Effects
    public int DamageAmount;
    public int BlockAmount;
    public int HealAmount;
    public int DrawAmount;
    public int StrengthModifier;
    public int DexterityModifier;
    public bool Exhaust;
    
    // Additional effects could be added here based on your game design
}

#endregion