using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Newtonsoft.Json;

public class PlayerManager
{
    private GameManager gameManager;
    
    // Sub-managers
    private HealthManager healthManager;
    private StatusEffectManager statusEffectManager;
    private EnergyManager energyManager;
    private CombatStateManager combatStateManager;
    private LobbyManager lobbyManager;
    
    // Constants (maintaining original API constants)
    public const string PLAYER_READY_PROPERTY = "IsReady";
    public const string COMBAT_FINISHED_PROPERTY = "CombatFinished";
    public const string PLAYER_SCORE_PROP = "PlayerScore";
    public const string PLAYER_BASE_PET_HP_PROP = "BasePetHP";
    public const string COMBAT_PAIRINGS_PROP = "CombatPairings";
    
    public PlayerManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
        
        // Initialize sub-managers
        this.healthManager = new HealthManager(gameManager);
        this.statusEffectManager = new StatusEffectManager(gameManager);
        this.energyManager = new EnergyManager(gameManager);
        this.combatStateManager = new CombatStateManager(gameManager);
        this.lobbyManager = new LobbyManager(gameManager);
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        healthManager.Initialize(startingPlayerHealth, startingPetHealth);
        statusEffectManager.Initialize();
        energyManager.Initialize(startingEnergy);
        combatStateManager.Initialize();
        lobbyManager.Initialize();
    }
    
    #region Process Turn Start Methods
    
    public void ProcessPlayerTurnStartEffects()
    {
        // Always use entity system
        gameManager.GetLocalPlayerEntity().ProcessTurnStartEffects();
        Debug.Log("Processed Player Turn Start effects");
    }
    
    public void ProcessLocalPetTurnStartEffects()
    {
        // Always use entity system
        gameManager.GetLocalPetEntity().ProcessTurnStartEffects();
        Debug.Log("Processed Local Pet Turn Start effects");
    }
    
    public void ProcessOpponentPetTurnStartEffects()
    {
        // Always use entity system
        gameManager.GetOpponentPetEntity().ProcessTurnStartEffects();
        Debug.Log("Processed Opponent Pet Turn Start effects");
    }
    
    #endregion
    
    #region Health & Damage Proxies
    
    public void DamageLocalPlayer(int amount, bool updateUIImmediate = true, HealthManager.DamageSource source = HealthManager.DamageSource.OpponentPetAttack)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().TakeDamage(amount);
    }
    
    public void DamageLocalPet(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().TakeDamage(amount);
    }
    
    public void DamageOpponentPet(int amount)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().TakeDamage(amount);
    }
    
    public CombatCalculator.DamageResult ApplyDamageToLocalPetLocally(int amount)
    {
        // Use entity system, but keep original return type for backward compatibility
        gameManager.GetLocalPetEntity().TakeDamage(amount);
        // Create and return a compatible result object
        return new CombatCalculator.DamageResult
        {
            DamageAfterBlock = amount,
            IsCritical = false, // No way to know from entity system
            BlockConsumed = 0,
            DamageBeforeBlock = amount
        };
    }
    
    public void AddBlockToLocalPlayer(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().AddBlock(amount);
    }
    
    public void AddBlockToLocalPet(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().AddBlock(amount);
    }
    
    public void AddBlockToOpponentPet(int amount, bool updateUIImmediate = true)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().AddBlock(amount);
    }
    
    public void ResetAllBlock()
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ResetBlock();
        gameManager.GetLocalPetEntity().ResetBlock();
        gameManager.GetOpponentPetEntity().ResetBlock();
    }
    
    public void HealLocalPlayer(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().Heal(amount);
    }
    
    public void HealLocalPet(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().Heal(amount);
    }
    
    public void HealOpponentPet(int amount)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().Heal(amount);
    }
    
    #endregion
    
    #region Status Effect Proxies
    
    public void ApplyStatusEffectLocalPlayer(StatusEffectType type, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ApplyStatusEffect(type, duration);
    }
    
    public void ApplyStatusEffectLocalPet(StatusEffectType type, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().ApplyStatusEffect(type, duration);
    }
    
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().ApplyStatusEffect(type, duration);
    }
    
    public void ApplyDotLocalPlayer(int damage, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ApplyDoT(damage, duration);
    }
    
    public void ApplyDotLocalPet(int damage, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().ApplyDoT(damage, duration);
    }
    
    public void ApplyDotOpponentPet(int damage, int duration)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().ApplyDoT(damage, duration);
    }
    
    public void ApplyHotLocalPlayer(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ApplyHoT(amount, duration);
    }
    
    public void ApplyHotLocalPet(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().ApplyHoT(amount, duration);
    }
    
    public void ApplyHotOpponentPet(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().ApplyHoT(amount, duration);
    }
    
    public void IncrementComboCount() => statusEffectManager.IncrementComboCount();
    public void ResetComboCount() => statusEffectManager.ResetComboCount();
    
    public void ApplyCritChanceBuffPlayer(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ApplyCritBuff(amount, duration);
    }
    
    public void ApplyCritChanceBuffPet(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetLocalPetEntity().ApplyCritBuff(amount, duration);
    }
    
    public void ApplyCritChanceBuffOpponentPet(int amount, int duration)
    {
        // Use entity system directly
        gameManager.GetOpponentPetEntity().ApplyCritBuff(amount, duration);
    }
    
    public void DecrementPlayerCritBuffDurations() => healthManager.DecrementPlayerCritBuffDurations();
    public void DecrementLocalPetCritBuffDurations() => healthManager.DecrementLocalPetCritBuffDurations();
    public void DecrementOpponentPetCritBuffDurations() => healthManager.DecrementOpponentPetCritBuffDurations();
    
    #endregion
    
    #region Energy Management Proxies
    
    public void ConsumeEnergy(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().ConsumeEnergy(amount);
    }
    
    public void GainEnergy(int amount)
    {
        // Use entity system directly
        gameManager.GetLocalPlayerEntity().AddEnergy(amount);
    }
    
    public void ApplyCostModifierToOpponentHand(int amount, int duration, int cardCount) => 
        energyManager.ApplyCostModifierToOpponentHand(amount, duration, cardCount);
    
    public void ApplyCostModifierToLocalHand(int amount, int duration, int cardCount) => 
        energyManager.ApplyCostModifierToLocalHand(amount, duration, cardCount);
    
    public int GetLocalHandCostModifier(CardData card) => energyManager.GetLocalHandCostModifier(card);
    
    public void RemoveCostModifierForCard(CardData card) => energyManager.RemoveCostModifierForCard(card);
    
    #endregion
    
    #region Combat State Proxies
    
    public void InitializeCombatState(int opponentPetOwnerActorNum, int startingPlayerHealth, int startingPetHealth) =>
        combatStateManager.InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
    
    public void SetPlayerCombatFinished(bool finished) => combatStateManager.SetPlayerCombatFinished(finished);
    
    public void CheckForAllPlayersFinishedCombat() => combatStateManager.CheckForAllPlayersFinishedCombat();
    
    public void ResetCombatFinishedFlags() => combatStateManager.ResetCombatFinishedFlags();
    
    public void PrepareNextCombatRound() => combatStateManager.PrepareNextCombatRound();
    
    public void UpdateScoreUI() => combatStateManager.UpdateScoreUI();
    
    public bool IsCombatEndedForLocalPlayer() => combatStateManager.IsCombatEndedForLocalPlayer();
    
    public void SetCombatEndedForLocalPlayer(bool value) => combatStateManager.SetCombatEndedForLocalPlayer(value);
    
    public void HandleCombatWin() => combatStateManager.HandleCombatWin();
    
    public void HandleCombatLoss() => combatStateManager.HandleCombatLoss();
    
    #endregion
    
    #region Lobby Management Proxies
    
    public void UpdatePlayerList(GameObject playerListPanel, GameObject playerEntryTemplate) => 
        lobbyManager.UpdatePlayerList(playerListPanel, playerEntryTemplate);
    
    public void SetPlayerReady(bool isReady) => lobbyManager.SetPlayerReady(isReady);
    
    public bool CheckAllPlayersReady() => lobbyManager.CheckAllPlayersReady();
    
    public void SetLocalPetName(string name) => lobbyManager.SetLocalPetName(name);
    
    public string GetLocalPetName() => lobbyManager.GetLocalPetName();
    
    #endregion
    
    #region Status Check Proxies
    
    // These methods moved to Getters using Entity system region
    
    #endregion
    
    #region Getters using Entity system
    
    // Health getters
    public int GetLocalPlayerHealth() => gameManager.GetLocalPlayerEntity().GetHealth();
    public int GetLocalPetHealth() => gameManager.GetLocalPetEntity().GetHealth();
    public int GetOpponentPetHealth() => gameManager.GetOpponentPetEntity().GetHealth();
    
    // Block getters
    public int GetLocalPlayerBlock() => gameManager.GetLocalPlayerEntity().GetBlock();
    public int GetLocalPetBlock() => gameManager.GetLocalPetEntity().GetBlock();
    public int GetOpponentPetBlock() => gameManager.GetOpponentPetEntity().GetBlock();
    
    // Energy getters
    public int GetCurrentEnergy() => energyManager.GetCurrentEnergy();
    public void SetCurrentEnergy(int energy) => energyManager.SetCurrentEnergy(energy);
    public int GetStartingEnergy() => energyManager.GetStartingEnergy();
    public int GetStartingPetEnergy() => gameManager.GetStartingPetEnergy();
    
    // Combo getters
    public int GetCurrentComboCount() => statusEffectManager.GetCurrentComboCount();
    
    // Status effect getters
    public bool IsPlayerWeak() => gameManager.GetLocalPlayerEntity().IsWeakened();
    public bool IsPlayerBroken() => gameManager.GetLocalPlayerEntity().IsBroken();
    public bool IsLocalPetWeak() => gameManager.GetLocalPetEntity().IsWeakened();
    public bool IsLocalPetBroken() => gameManager.GetLocalPetEntity().IsBroken();
    public bool IsOpponentPetWeak() => gameManager.GetOpponentPetEntity().IsWeakened();
    public bool IsOpponentPetBroken() => gameManager.GetOpponentPetEntity().IsBroken();
    
    public int GetPlayerWeakTurns() => gameManager.GetLocalPlayerEntity().GetWeakTurns();
    public int GetPlayerBreakTurns() => gameManager.GetLocalPlayerEntity().GetBreakTurns();
    public int GetLocalPetWeakTurns() => gameManager.GetLocalPetEntity().GetWeakTurns();
    public int GetLocalPetBreakTurns() => gameManager.GetLocalPetEntity().GetBreakTurns();
    public int GetOpponentPetWeakTurns() => gameManager.GetOpponentPetEntity().GetWeakTurns();
    public int GetOpponentPetBreakTurns() => gameManager.GetOpponentPetEntity().GetBreakTurns();
    
    public int GetPlayerThorns() => gameManager.GetLocalPlayerEntity().GetThorns();
    public int GetLocalPetThorns() => gameManager.GetLocalPetEntity().GetThorns();
    public int GetOpponentPetThorns() => gameManager.GetOpponentPetEntity().GetThorns();
    
    public int GetPlayerStrength() => gameManager.GetLocalPlayerEntity().GetStrength();
    public int GetLocalPetStrength() => gameManager.GetLocalPetEntity().GetStrength();
    public int GetOpponentPetStrength() => gameManager.GetOpponentPetEntity().GetStrength();
    
    // DoT & HoT getters
    public int GetPlayerDotTurns() => gameManager.GetLocalPlayerEntity().GetDotTurns();
    public int GetPlayerDotDamage() => gameManager.GetLocalPlayerEntity().GetDotDamage();
    public int GetLocalPetDotTurns() => gameManager.GetLocalPetEntity().GetDotTurns();
    public int GetLocalPetDotDamage() => gameManager.GetLocalPetEntity().GetDotDamage();
    public int GetOpponentPetDotTurns() => gameManager.GetOpponentPetEntity().GetDotTurns();
    public int GetOpponentPetDotDamage() => gameManager.GetOpponentPetEntity().GetDotDamage();
    
    public int GetPlayerHotTurns() => gameManager.GetLocalPlayerEntity().GetHotTurns();
    public int GetPlayerHotAmount() => gameManager.GetLocalPlayerEntity().GetHotAmount();
    public int GetLocalPetHotTurns() => gameManager.GetLocalPetEntity().GetHotTurns();
    public int GetLocalPetHotAmount() => gameManager.GetLocalPetEntity().GetHotAmount();
    public int GetOpponentPetHotTurns() => gameManager.GetOpponentPetEntity().GetHotTurns();
    public int GetOpponentPetHotAmount() => gameManager.GetOpponentPetEntity().GetHotAmount();
    
    // Crit getters
    public int GetPlayerEffectiveCritChance() => gameManager.GetLocalPlayerEntity().GetEffectiveCritChance();
    public int GetPetEffectiveCritChance() => gameManager.GetLocalPetEntity().GetEffectiveCritChance();
    public int GetOpponentPetEffectiveCritChance() => gameManager.GetOpponentPetEntity().GetEffectiveCritChance();
    public int GetBaseCritChance() => CombatCalculator.BASE_CRIT_CHANCE;
    
    // Max health getters
    public int GetEffectivePlayerMaxHealth() => gameManager.GetLocalPlayerEntity().GetMaxHealth();
    public int GetEffectivePetMaxHealth() => gameManager.GetLocalPetEntity().GetMaxHealth();
    public int GetEffectiveOpponentPetMaxHealth() => gameManager.GetOpponentPetEntity().GetMaxHealth();
    
    // Other management getters
    public Player GetOpponentPlayer() => combatStateManager.GetOpponentPlayer();
    public void SetOpponentPlayer(Player player) => combatStateManager.SetOpponentPlayer(player);
    
    public HealthManager GetHealthManager() => healthManager;
    public StatusEffectManager GetStatusEffectManager() => statusEffectManager;
    public EnergyManager GetEnergyManager() => energyManager;
    public CombatStateManager GetCombatStateManager() => combatStateManager;
    public LobbyManager GetLobbyManager() => lobbyManager;
    
    #endregion
}