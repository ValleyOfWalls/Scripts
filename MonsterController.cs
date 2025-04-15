using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class MonsterController : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Properties and Variables
    
    // References
    [Header("References")]
    private GameplayManager gameplayManager;
    private GameObject monsterUICanvas;
    private PlayerController owningPlayer;
    
    // Monster Stats
    [Header("Stats")]
    [SerializeField] private string monsterName = "Pet Monster";
    [SerializeField] private int maxHealth = 70;
    [SerializeField] private int currentHealth;
    [SerializeField] private int attackPower = 5;
    [SerializeField] private int defensePower = 3;
    [SerializeField] private int block = 0;
    [SerializeField] private int strengthModifier = 0;
    [SerializeField] private int dexterityModifier = 0;
    
    // UI Elements
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI intentText;
    private Slider healthSlider;
    private Image intentIcon;
    
    // Game state
    [SerializeField] private bool isInCombat = false;
    [SerializeField] private MonsterIntent currentIntent = MonsterIntent.Attack;
    private PlayerController opponentPlayer;
    
    // AI Decision Making
    [SerializeField] private List<MonsterAction> possibleActions = new List<MonsterAction>();
    [SerializeField] private MonsterAction nextAction;
    [SerializeField] private MonsterAI aiType = MonsterAI.Basic;
    
    // Card-related 
    [SerializeField] private List<Card> monsterDeck = new List<Card>();
    
    // Properties
    public string MonsterName { get => monsterName; private set => monsterName = value; }
    public int MaxHealth { get => maxHealth; set => maxHealth = value; }
    public int CurrentHealth { get => currentHealth; set => currentHealth = value; }
    public int AttackPower { get => attackPower; set => attackPower = value; }
    public int DefensePower { get => defensePower; set => defensePower = value; }
    public int Block { get => block; set => block = value; }
    public int StrengthModifier { get => strengthModifier; set => strengthModifier = value; }
    public int DexterityModifier { get => dexterityModifier; set => dexterityModifier = value; }
    public bool IsInCombat { get => isInCombat; set => isInCombat = value; }
    public MonsterIntent CurrentIntent { get => currentIntent; private set => currentIntent = value; }
    public PlayerController OwningPlayer { get => owningPlayer; set => owningPlayer = value; }
    
    #endregion
    
    #region Unity and Photon Callbacks
    
    private void Awake()
    {
        // Set initial health
        currentHealth = maxHealth;
        
        // Initialize possible actions
        InitializeActions();
    }
    
    private void Start()
    {
        // Start delayed initialization
        StartCoroutine(DelayedInitialization());
    }
    
    private IEnumerator DelayedInitialization()
    {
        // Wait a short time to ensure all objects are initialized
        yield return new WaitForSeconds(0.5f);
        
        // Try to find required objects with multiple fallbacks
        TryGetGameplayManager();
        
        // Generate a unique name if not set
        if (string.IsNullOrEmpty(monsterName) || monsterName == "Pet Monster")
        {
            monsterName = GenerateMonsterName();
        }
        
        // Setup monster visuals and UI
        SetupMonsterUI();
        
        // Initialize deck if needed
        InitializeMonsterDeck();
        
        // Decide on the first action
        DecideNextAction();
        
        // Register with gameplay manager
        if (gameplayManager != null)
        {
            gameplayManager.RegisterMonster(this);
            Debug.Log($"Monster {monsterName} registered with GameplayManager");
        }
        else
        {
            Debug.LogWarning($"Monster {monsterName} couldn't find GameplayManager to register with");
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
            Debug.LogWarning($"Monster {monsterName}: Failed to find GameplayManager after retries");
        }
        else
        {
            Debug.Log($"Monster {monsterName}: Found GameplayManager after retries");
            
            // Register with gameplay manager if found
            gameplayManager.RegisterMonster(this);
        }
    }
    
    private string GenerateMonsterName()
    {
        string[] prefixes = { "Mighty", "Shadow", "Mystic", "Fierce", "Crimson", "Dark", "Ancient", "Frozen", "Flaming", "Thunder" };
        string[] types = { "Dragon", "Goblin", "Troll", "Gryphon", "Phoenix", "Beast", "Golem", "Wyrm", "Elemental", "Demon" };
        
        string prefix = prefixes[Random.Range(0, prefixes.Length)];
        string type = types[Random.Range(0, types.Length)];
        
        return $"{prefix} {type}";
    }
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        UpdateUI();
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Sync important variables over the network
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(monsterName);
            stream.SendNext(currentHealth);
            stream.SendNext(maxHealth);
            stream.SendNext(attackPower);
            stream.SendNext(defensePower);
            stream.SendNext(block);
            stream.SendNext(strengthModifier);
            stream.SendNext(dexterityModifier);
            stream.SendNext(isInCombat);
            stream.SendNext((int)currentIntent);
        }
        else
        {
            // Receive data
            monsterName = (string)stream.ReceiveNext();
            currentHealth = (int)stream.ReceiveNext();
            maxHealth = (int)stream.ReceiveNext();
            attackPower = (int)stream.ReceiveNext();
            defensePower = (int)stream.ReceiveNext();
            block = (int)stream.ReceiveNext();
            strengthModifier = (int)stream.ReceiveNext();
            dexterityModifier = (int)stream.ReceiveNext();
            isInCombat = (bool)stream.ReceiveNext();
            currentIntent = (MonsterIntent)(int)stream.ReceiveNext();
        }
    }
    
    #endregion
    
    #region UI Methods
    
    private void SetupMonsterUI()
    {
        // Create monster UI canvas
        monsterUICanvas = new GameObject("MonsterUICanvas");
        Canvas canvas = monsterUICanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = monsterUICanvas.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;
        monsterUICanvas.AddComponent<GraphicRaycaster>();
        
        // Position the canvas
        canvas.transform.position = transform.position + Vector3.up * 2;
        canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - Camera.main.transform.position);
        canvas.transform.localScale = Vector3.one * 0.01f;
        
        // Create monster stats panel
        GameObject statsPanel = new GameObject("StatsPanel");
        statsPanel.transform.SetParent(monsterUICanvas.transform, false);
        RectTransform statsPanelRect = statsPanel.AddComponent<RectTransform>();
        statsPanelRect.sizeDelta = new Vector2(200, 100);
        Image statsPanelImage = statsPanel.AddComponent<Image>();
        statsPanelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Setup vertical layout group for stats
        VerticalLayoutGroup statsLayout = statsPanel.AddComponent<VerticalLayoutGroup>();
        statsLayout.padding = new RectOffset(5, 5, 5, 5);
        statsLayout.spacing = 5;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = false;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = false;
        statsLayout.childAlignment = TextAnchor.UpperCenter;
        
        // Create name text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(statsPanel.transform, false);
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 16;
        nameText.color = Color.white;
        nameText.text = monsterName;
        nameText.alignment = TextAlignmentOptions.Center;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 20);
        
        // Create health text and slider
        GameObject healthObj = new GameObject("HealthContainer");
        healthObj.transform.SetParent(statsPanel.transform, false);
        RectTransform healthContainerRect = healthObj.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 30);
        
        GameObject healthTextObj = new GameObject("HealthText");
        healthTextObj.transform.SetParent(healthObj.transform, false);
        healthText = healthTextObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 14;
        healthText.color = Color.white;
        healthText.text = "HP: " + currentHealth + "/" + maxHealth;
        healthText.alignment = TextAlignmentOptions.Center;
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
        
        // Create intent container
        GameObject intentContainer = new GameObject("IntentContainer");
        intentContainer.transform.SetParent(statsPanel.transform, false);
        RectTransform intentContainerRect = intentContainer.AddComponent<RectTransform>();
        intentContainerRect.sizeDelta = new Vector2(0, 30);
        
        // Create intent icon
        GameObject intentIconObj = new GameObject("IntentIcon");
        intentIconObj.transform.SetParent(intentContainer.transform, false);
        intentIcon = intentIconObj.AddComponent<Image>();
        intentIcon.color = Color.white;
        RectTransform intentIconRect = intentIconObj.GetComponent<RectTransform>();
        intentIconRect.anchorMin = new Vector2(0, 0);
        intentIconRect.anchorMax = new Vector2(0.3f, 1);
        intentIconRect.sizeDelta = Vector2.zero;
        
        // Create intent text
        GameObject intentTextObj = new GameObject("IntentText");
        intentTextObj.transform.SetParent(intentContainer.transform, false);
        intentText = intentTextObj.AddComponent<TextMeshProUGUI>();
        intentText.fontSize = 14;
        intentText.color = Color.white;
        intentText.alignment = TextAlignmentOptions.Left;
        RectTransform intentTextRect = intentTextObj.GetComponent<RectTransform>();
        intentTextRect.anchorMin = new Vector2(0.33f, 0);
        intentTextRect.anchorMax = new Vector2(1, 1);
        intentTextRect.sizeDelta = Vector2.zero;
        
        // Set initial intent icon and text
        UpdateIntentDisplay();
    }
    
    private void UpdateUI()
    {
        if (nameText != null) 
            nameText.text = monsterName;
        
        if (healthText != null)
            healthText.text = "HP: " + currentHealth + "/" + maxHealth;
        
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        
        UpdateIntentDisplay();
    }
    
    private void UpdateIntentDisplay()
    {
        if (intentText == null || intentIcon == null) return;
        
        // Update intent icon based on current intent
        switch (currentIntent)
        {
            case MonsterIntent.Attack:
                intentIcon.color = new Color(0.8f, 0.2f, 0.2f, 1); // Red for attack
                intentText.text = "Attack: " + (attackPower + strengthModifier);
                break;
                
            case MonsterIntent.Defend:
                intentIcon.color = new Color(0.2f, 0.6f, 0.8f, 1); // Blue for defense
                intentText.text = "Block: " + (defensePower + dexterityModifier);
                break;
                
            case MonsterIntent.Buff:
                intentIcon.color = new Color(0.8f, 0.8f, 0.2f, 1); // Yellow for buff
                intentText.text = "Buff";
                break;
                
            case MonsterIntent.Debuff:
                intentIcon.color = new Color(0.8f, 0.4f, 0.8f, 1); // Purple for debuff
                intentText.text = "Debuff";
                break;
                
            case MonsterIntent.Mixed:
                intentIcon.color = new Color(0.5f, 0.5f, 0.5f, 1); // Gray for mixed
                intentText.text = "Complex";
                break;
        }
    }
    
    #endregion
    
    #region Combat Methods
    
    public void SetOpponentPlayer(PlayerController player)
    {
        opponentPlayer = player;
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
    
    // Check if monster is defeated
    if (currentHealth <= 0)
    {
        photonView.RPC("RPC_MonsterDefeated", RpcTarget.All);
    }
    
    // Update UI
    UpdateUI();
    
    // Force UI update across the network for battle overview
    BattleUIManager battleUIManager = FindObjectOfType<BattleUIManager>();
    if (battleUIManager != null)
    {
        battleUIManager.UpdateBattleCard(photonView.Owner.ActorNumber, false, currentHealth, maxHealth);
    }
    
    // Also update BattleUI if it exists
    BattleUI battleUI = FindObjectOfType<BattleUI>();
    if (battleUI != null)
    {
        // This will update UI for all players who have this monster as opponent
        photonView.RPC("RPC_UpdateMonsterUI", RpcTarget.All, currentHealth, maxHealth);
    }
}
    
[PunRPC]
public void RPC_UpdateMonsterUI(int health, int maxHealth)
{
    // This ensures all clients update their UI representation of this monster
    BattleUI battleUI = FindObjectOfType<BattleUI>();
    if (battleUI != null)
    {
        // Check if this is the opponent monster for the local player
        PlayerController localPlayer = null;
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (player.photonView.IsMine)
            {
                localPlayer = player;
                break;
            }
        }
        
        if (localPlayer != null && localPlayer.GetOpponentMonster() == this)
        {
            battleUI.UpdateMonsterStats(this);
        }
        else if (localPlayer != null && localPlayer.GetPetMonster() == this)
        {
            battleUI.UpdatePetStats(this);
        }
    }
}

    [PunRPC]
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
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
    private void RPC_MonsterDefeated()
    {
        // Handle monster defeat
        isInCombat = false;
        
        // Notify game manager
        if (gameplayManager != null && opponentPlayer != null)
            gameplayManager.MonsterDefeated(photonView.Owner.ActorNumber, opponentPlayer.photonView.Owner.ActorNumber);
    }

    // Add this method to MonsterController.cs
public void ApplyDamage(int damage, string sourceId)
{
    // Call RPC to ensure damage is applied on all clients
    photonView.RPC("TakeDamage", RpcTarget.All, damage, sourceId);
    
    Debug.Log($"ApplyDamage called on {MonsterName} for {damage} damage from source {sourceId}");
}

    
    public void TakeTurn()
{
    if (!photonView.IsMine || !isInCombat || opponentPlayer == null) return;
    
    Debug.Log($"Monster {MonsterName} taking turn against player {opponentPlayer.PlayerName}");
    
    // Execute the next action
    ExecuteAction(nextAction);
    
    // Decide on the next action for the next turn
    DecideNextAction();
    
    // Reset block at end of turn
    block = 0;
    
    // Notify game manager that our turn is over
    if (gameplayManager != null)
        gameplayManager.MonsterEndedTurn();
}

    
    #endregion
    
    #region AI Methods
    
    private void InitializeActions()
    {
        // Create some basic actions for the monster
        
        // Basic attack
        MonsterAction basicAttack = new MonsterAction
        {
            ActionType = MonsterActionType.Attack,
            ActionIntent = MonsterIntent.Attack,
            BaseDamage = attackPower,
            Weight = 1.0f
        };
        possibleActions.Add(basicAttack);
        
        // Block action
        MonsterAction blockAction = new MonsterAction
        {
            ActionType = MonsterActionType.Block,
            ActionIntent = MonsterIntent.Defend,
            BlockAmount = defensePower,
            Weight = 0.7f
        };
        possibleActions.Add(blockAction);
        
        // Buff action - increase strength
        MonsterAction buffAction = new MonsterAction
        {
            ActionType = MonsterActionType.Buff,
            ActionIntent = MonsterIntent.Buff,
            StrengthModifier = 2,
            Weight = 0.3f
        };
        possibleActions.Add(buffAction);
        
        // Mixed action - attack and block
        MonsterAction mixedAction = new MonsterAction
        {
            ActionType = MonsterActionType.Mixed,
            ActionIntent = MonsterIntent.Mixed,
            BaseDamage = attackPower / 2,
            BlockAmount = defensePower / 2,
            Weight = 0.5f
        };
        possibleActions.Add(mixedAction);
    }
    
    private void DecideNextAction()
    {
        if (!photonView.IsMine) return;
        
        switch (aiType)
        {
            case MonsterAI.Basic:
                // Basic AI just picks randomly based on weights
                DecideNextActionBasic();
                break;
                
            case MonsterAI.Defensive:
                // Defensive AI prioritizes blocking, especially when low health
                DecideNextActionDefensive();
                break;
                
            case MonsterAI.Aggressive:
                // Aggressive AI prioritizes attacking
                DecideNextActionAggressive();
                break;
                
            case MonsterAI.Smart:
                // Smart AI tries to make optimal decisions based on game state
                DecideNextActionSmart();
                break;
        }
        
        // Update the intent
        currentIntent = nextAction.ActionIntent;
        UpdateIntentDisplay();
    }
    
    private void DecideNextActionBasic()
    {
        // Calculate total weight
        float totalWeight = 0;
        foreach (MonsterAction action in possibleActions)
        {
            totalWeight += action.Weight;
        }
        
        // Random roll
        float roll = Random.Range(0, totalWeight);
        
        // Pick action based on weights
        float cumulativeWeight = 0;
        foreach (MonsterAction action in possibleActions)
        {
            cumulativeWeight += action.Weight;
            if (roll <= cumulativeWeight)
            {
                nextAction = action;
                return;
            }
        }
        
        // Fallback to first action if something goes wrong
        nextAction = possibleActions[0];
    }
    
    private void DecideNextActionDefensive()
    {
        // Calculate health percentage
        float healthPercentage = (float)currentHealth / maxHealth;
        
        // Increase weight of defensive actions when health is low
        List<MonsterAction> tempActions = new List<MonsterAction>();
        foreach (MonsterAction action in possibleActions)
        {
            MonsterAction tempAction = new MonsterAction(action);
            
            if (action.ActionType == MonsterActionType.Block || 
                (action.ActionType == MonsterActionType.Mixed && action.BlockAmount > 0))
            {
                // Increase block weight when health is lower
                tempAction.Weight = action.Weight * (2.0f - healthPercentage);
            }
            else
            {
                tempAction.Weight = action.Weight;
            }
            
            tempActions.Add(tempAction);
        }
        
        // Calculate total weight
        float totalWeight = 0;
        foreach (MonsterAction action in tempActions)
        {
            totalWeight += action.Weight;
        }
        
        // Random roll
        float roll = Random.Range(0, totalWeight);
        
        // Pick action based on weights
        float cumulativeWeight = 0;
        foreach (MonsterAction action in tempActions)
        {
            cumulativeWeight += action.Weight;
            if (roll <= cumulativeWeight)
            {
                nextAction = possibleActions[tempActions.IndexOf(action)];
                return;
            }
        }
        
        // Fallback to first action if something goes wrong
        nextAction = possibleActions[0];
    }
    
    private void DecideNextActionAggressive()
    {
        // Prioritize attacks and buffs
        List<MonsterAction> tempActions = new List<MonsterAction>();
        foreach (MonsterAction action in possibleActions)
        {
            MonsterAction tempAction = new MonsterAction(action);
            
            if (action.ActionType == MonsterActionType.Attack || 
                action.ActionType == MonsterActionType.Buff)
            {
                // Increase attack/buff weight
                tempAction.Weight = action.Weight * 2.0f;
            }
            else
            {
                tempAction.Weight = action.Weight * 0.5f;
            }
            
            tempActions.Add(tempAction);
        }
        
        // Calculate total weight
        float totalWeight = 0;
        foreach (MonsterAction action in tempActions)
        {
            totalWeight += action.Weight;
        }
        
        // Random roll
        float roll = Random.Range(0, totalWeight);
        
        // Pick action based on weights
        float cumulativeWeight = 0;
        foreach (MonsterAction action in tempActions)
        {
            cumulativeWeight += action.Weight;
            if (roll <= cumulativeWeight)
            {
                nextAction = possibleActions[tempActions.IndexOf(action)];
                return;
            }
        }
        
        // Fallback to first action if something goes wrong
        nextAction = possibleActions[0];
    }
    
    private void DecideNextActionSmart()
    {
        // This would be a more complex AI that considers:
        // - Player's health
        // - Player's block
        // - Monster's health
        // - If the monster has enough attack to kill the player
        // - If the monster would die to the player's next attack
        // - Pattern recognition of player's style
        // - etc.
        
        // For now, just use the basic AI as a placeholder
        DecideNextActionBasic();
    }
    
    private void ExecuteAction(MonsterAction action)
    {
        if (!photonView.IsMine || opponentPlayer == null) return;
        
        switch (action.ActionType)
        {
            case MonsterActionType.Attack:
                // Deal damage to player
                int damageAmount = action.BaseDamage + strengthModifier;
                opponentPlayer.TakeDamage(damageAmount, photonView.Owner.ActorNumber.ToString());
                break;
                
            case MonsterActionType.Block:
                // Add block
                AddBlock(action.BlockAmount);
                break;
                
            case MonsterActionType.Buff:
                // Apply buffs
                if (action.StrengthModifier != 0)
                {
                    ModifyStrength(action.StrengthModifier);
                }
                
                if (action.DexterityModifier != 0)
                {
                    ModifyDexterity(action.DexterityModifier);
                }
                
                if (action.HealAmount > 0)
                {
                    Heal(action.HealAmount);
                }
                break;
                
            case MonsterActionType.Debuff:
                // Apply debuffs to player
                if (action.StrengthModifier != 0)
                {
                    opponentPlayer.ModifyStrength(-action.StrengthModifier);
                }
                
                if (action.DexterityModifier != 0)
                {
                    opponentPlayer.ModifyDexterity(-action.DexterityModifier);
                }
                break;
                
            case MonsterActionType.Mixed:
                // Combined action
                if (action.BaseDamage > 0)
                {
                    int mixedDamage = action.BaseDamage + strengthModifier;
                    opponentPlayer.TakeDamage(mixedDamage, photonView.Owner.ActorNumber.ToString());
                }
                
                if (action.BlockAmount > 0)
                {
                    AddBlock(action.BlockAmount);
                }
                break;
        }
    }
    
    private void InitializeMonsterDeck()
    {
        // This could be expanded later to load different decks based on monster type
        // For now, just create a basic deck
        
        // Add basic attacks
        for (int i = 0; i < 5; i++)
        {
            Card attackCard = new Card
            {
                CardId = "monster_attack_basic",
                CardName = "Monster Strike",
                CardDescription = "Deal damage based on attack power.",
                EnergyCost = 0,  // Monsters don't use energy
                CardType = CardType.Attack,
                CardRarity = CardRarity.Basic,
                DamageAmount = attackPower
            };
            monsterDeck.Add(attackCard);
        }
        
        // Add basic defense cards
        for (int i = 0; i < 3; i++)
        {
            Card defendCard = new Card
            {
                CardId = "monster_defend_basic",
                CardName = "Monster Defend",
                CardDescription = "Gain block based on defense power.",
                EnergyCost = 0,
                CardType = CardType.Skill,
                CardRarity = CardRarity.Basic,
                BlockAmount = defensePower
            };
            monsterDeck.Add(defendCard);
        }
        
        // Add a buff card
        Card buffCard = new Card
        {
            CardId = "monster_buff_strength",
            CardName = "Monster Rage",
            CardDescription = "Gain 2 Strength.",
            EnergyCost = 0,
            CardType = CardType.Power,
            CardRarity = CardRarity.Uncommon,
            StrengthModifier = 2
        };
        monsterDeck.Add(buffCard);
    }
    
    #endregion
    
    #region Public Utility Methods
    
    public void ResetForNewRound()
    {
        // Reset health to max
        currentHealth = maxHealth;
        
        // Reset modifiers
        block = 0;
        strengthModifier = 0;
        dexterityModifier = 0;
        
        // Reset combat state
        isInCombat = false;
        
        // Decide on the next action
        DecideNextAction();
        
        // Reset UI
        UpdateUI();
    }
    
    public void UpgradeMonster(MonsterUpgrade upgrade)
    {
        switch (upgrade.UpgradeType)
        {
            case MonsterUpgradeType.MaxHealth:
                maxHealth += upgrade.Value;
                currentHealth += upgrade.Value;
                break;
                
            case MonsterUpgradeType.AttackPower:
                attackPower += upgrade.Value;
                break;
                
            case MonsterUpgradeType.DefensePower:
                defensePower += upgrade.Value;
                break;
                
            case MonsterUpgradeType.AI:
                aiType = (MonsterAI)upgrade.Value;
                break;
        }
        
        // Update UI
        UpdateUI();
        
        // Recalculate intent if we're the owner
        if (photonView.IsMine)
        {
            DecideNextAction();
        }
    }
    
    #endregion
}

#region Monster Related Enums and Classes

public enum MonsterIntent
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Mixed
}

public enum MonsterActionType
{
    Attack,
    Block,
    Buff,
    Debuff,
    Mixed
}

public enum MonsterAI
{
    Basic,
    Defensive,
    Aggressive,
    Smart
}

[System.Serializable]
public class MonsterAction
{
    public MonsterActionType ActionType;
    public MonsterIntent ActionIntent;
    public int BaseDamage;
    public int BlockAmount;
    public int HealAmount;
    public int StrengthModifier;
    public int DexterityModifier;
    public float Weight;
    
    // Default constructor
    public MonsterAction() { }
    
    // Copy constructor
    public MonsterAction(MonsterAction other)
    {
        ActionType = other.ActionType;
        ActionIntent = other.ActionIntent;
        BaseDamage = other.BaseDamage;
        BlockAmount = other.BlockAmount;
        HealAmount = other.HealAmount;
        StrengthModifier = other.StrengthModifier;
        DexterityModifier = other.DexterityModifier;
        Weight = other.Weight;
    }
}

public enum MonsterUpgradeType
{
    MaxHealth,
    AttackPower,
    DefensePower,
    AI
}

[System.Serializable]
public class MonsterUpgrade
{
    public MonsterUpgradeType UpgradeType;
    public int Value;
    public string Description;
}

#endregion