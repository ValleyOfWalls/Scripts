using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for Sum()
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class HealthManager
{
    private GameManager gameManager;
    
    // Health data
    private int localPlayerHealth;
    private int localPetHealth;
    private int opponentPetHealth;
    private int localPlayerBlock = 0;
    private int localPetBlock = 0;
    private int opponentPetBlock = 0;
    
    // DoT tracking
    private int localPlayerDotTurns = 0;
    private int localPlayerDotDamage = 0;
    private int localPetDotTurns = 0;
    private int localPetDotDamage = 0;
    private int opponentPetDotTurns = 0;
    private int opponentPetDotDamage = 0;
    
    // --- MODIFIED: Crit tracking using list of buffs ---
    private struct CritBuff
    {
        public int amount;
        public int turns;
    }
    private List<CritBuff> localPlayerCritBuffs = new List<CritBuff>();
    private List<CritBuff> localPetCritBuffs = new List<CritBuff>();
    private List<CritBuff> opponentPetCritBuffs = new List<CritBuff>();
    // --- END MODIFIED ---
    
    private const int BASE_CRIT_CHANCE = 5;
    private const int CRIT_DAMAGE_MULTIPLIER = 2;
    
    public HealthManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth)
    {
        this.localPlayerHealth = startingPlayerHealth;
        this.localPetHealth = startingPetHealth;
        
        // Reset DoT
        localPlayerDotTurns = 0;
        localPlayerDotDamage = 0;
        localPetDotTurns = 0;
        localPetDotDamage = 0;
        opponentPetDotTurns = 0;
        opponentPetDotDamage = 0;
        
        // Reset block
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0;
        
        // --- MODIFIED: Reset crit buffs ---
        localPlayerCritBuffs.Clear();
        localPetCritBuffs.Clear();
        opponentPetCritBuffs.Clear();
        // --- END MODIFIED ---
    }
    
    public void InitializeCombatState(int startingPetHealth, Player opponentPlayer)
    {
        // Reset block
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0;
        
        // --- ADDED: Reset crit buffs at start of combat ---
        localPlayerCritBuffs.Clear();
        localPetCritBuffs.Clear();
        opponentPetCritBuffs.Clear();
        // --- END ADDED ---
        
        // Get opponent's base pet HP
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { opponentPetHealth = (int)oppBasePetHP; }
            catch { Debug.LogError($"Failed to cast {PlayerManager.PLAYER_BASE_PET_HP_PROP} for player {opponentPlayer.NickName}, using default {startingPetHealth}"); opponentPetHealth = startingPetHealth; }
        }
        else
        {
            opponentPetHealth = startingPetHealth; // Default if property not found
        }
        Debug.Log($"Set initial opponent pet health to {opponentPetHealth}");
    }
    
    #region Damage Methods
    
    public void DamageLocalPlayer(int amount, bool updateUIImmediate = true)
    {
        if (amount <= 0) return; // Can't deal negative damage
        
        int damageAfterBlock = amount - localPlayerBlock;
        int blockConsumed = Mathf.Min(amount, localPlayerBlock);
        localPlayerBlock -= blockConsumed;
        if (localPlayerBlock < 0) localPlayerBlock = 0;
        
        Debug.Log($"DamageLocalPlayer: Incoming={amount}, Block={blockConsumed}, RemainingDamage={damageAfterBlock}");
        
        if (damageAfterBlock > 0)
        {
            // Check for Break
            StatusEffectManager statusManager = gameManager.GetPlayerManager().GetStatusEffectManager();
            if (statusManager.IsPlayerBroken()) 
            {
                int breakBonus = Mathf.FloorToInt(damageAfterBlock * 0.5f); 
                Debug.Log($"Player has Break! Increasing damage by {breakBonus}");
                damageAfterBlock += breakBonus;
            }
            
            localPlayerHealth -= damageAfterBlock;
            if (localPlayerHealth < 0) localPlayerHealth = 0;
        }
        
        // Apply Crit Damage if applicable (Opponent Pet attacking Player)
        int opponentCritChance = GetOpponentPetEffectiveCritChance();
        if (Random.Range(0, 100) < opponentCritChance)
        {
            Debug.LogWarning($"Opponent Pet CRITICAL HIT! (Chance: {opponentCritChance}%)");
            int critDamage = damageAfterBlock * (CRIT_DAMAGE_MULTIPLIER - 1);
            Debug.Log($"Applying additional {critDamage} critical damage.");
            localPlayerHealth -= critDamage;
            if (localPlayerHealth < 0) localPlayerHealth = 0;
        }

        if (updateUIImmediate) 
        {
            gameManager.UpdateHealthUI(); // Update both health and block display
        }
        
        // --- ADDED: Update player custom property for other UIs ---
        if (PhotonNetwork.InRoom)
        {
            ExitGames.Client.Photon.Hashtable healthProp = new ExitGames.Client.Photon.Hashtable 
            {
                { CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, localPlayerHealth } 
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(healthProp);
            // Debug.Log($"Updated {CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP} to {localPlayerHealth}");
        }
        // --- END ADDED ---

        if (localPlayerHealth <= 0)
        {
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatLoss();
        }
    }
    
    /// <summary>
    /// Applies damage to the opponent's pet based on the local simulation state and checks for win condition.
    /// This method performs the actual health/block modification and UI update.
    /// It does NOT send any network notifications.
    /// </summary>
    public void ApplyDamageToOpponentPetLocally(int amount)
    {
        if (amount <= 0) return;

        int damageAfterBlock = amount - opponentPetBlock;
        int blockConsumed = Mathf.Min(amount, opponentPetBlock);
        opponentPetBlock -= blockConsumed;
        if (opponentPetBlock < 0) opponentPetBlock = 0;

        // Debug.Log($"ApplyDamageToOpponentPetLocally: Incoming={amount}, Est. Block={blockConsumed}, RemainingDamage={damageAfterBlock}"); // Optional: More granular log

        if (damageAfterBlock > 0)
        {
            // Check for Break
            StatusEffectManager statusManager = gameManager.GetPlayerManager().GetStatusEffectManager();
            if (statusManager.IsOpponentPetBroken()) 
            {
                int breakBonus = Mathf.FloorToInt(damageAfterBlock * 0.5f); 
                Debug.Log($"Opponent Pet has Break! Increasing damage by {breakBonus} (local sim)");
                damageAfterBlock += breakBonus;
            }
            
            opponentPetHealth -= damageAfterBlock;
            if (opponentPetHealth < 0) opponentPetHealth = 0;
        }
        
        // Apply Crit Damage if applicable (Player attacking Opponent Pet)
        // Crit is applied here because the *attacker's* crit chance determines it.
        int playerCritChance = GetPlayerEffectiveCritChance();
        if (Random.Range(0, 100) < playerCritChance)
        {
            Debug.LogWarning($"Player CRITICAL HIT! (Chance: {playerCritChance}%)");
            // Crit damage applies to the damage that got through block
            int critDamage = damageAfterBlock > 0 ? damageAfterBlock * (CRIT_DAMAGE_MULTIPLIER - 1) : amount * (CRIT_DAMAGE_MULTIPLIER - 1);
            if (critDamage < 0) critDamage = 0; // Ensure crit damage isn't negative if base damage was 0
            Debug.Log($"Applying additional {critDamage} critical damage to Opponent Pet.");
            opponentPetHealth -= critDamage;
            if (opponentPetHealth < 0) opponentPetHealth = 0;
        }

        gameManager.UpdateHealthUI(); // Update both health and block display
        
        // Check for win condition after applying damage
        if (opponentPetHealth <= 0)
        {
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatWin();
        }
    }

    /// <summary>
    /// Processes damage dealt by the local player to the opponent's pet.
    /// Applies the damage locally and notifies the opponent via RPC.
    /// </summary>
    public void DamageOpponentPet(int amount)
    {
        if (amount <= 0) return;
        
        Debug.Log($"DamageOpponentPet: Processing {amount} damage dealt by local player.");

        // Apply the damage locally first (handles block, break, crit, health update, UI, win check)
        ApplyDamageToOpponentPetLocally(amount);

        // Only notify the opponent if the opponent pet is still alive (prevent redundant RPCs after win)
        // And ensure we have an opponent to notify
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (opponentPetHealth > 0 && opponentPlayer != null)
        {
            // Notify the opponent that their pet took damage (send ORIGINAL amount, they calculate block/reductions locally)
            Debug.Log($"Sending RpcTakePetDamage({amount}) to {opponentPlayer.NickName}");
            gameManager.GetPhotonView().RPC("RpcTakePetDamage", opponentPlayer, amount);
        }
        else if (opponentPlayer == null)
        {
             Debug.LogWarning("DamageOpponentPet: Cannot send RPC, opponentPlayer is null.");
        }
    }
    
    public void DamageLocalPet(int amount)
    {
        if (amount <= 0) return;
        
        int damageAfterBlock = amount - localPetBlock;
        int blockConsumed = Mathf.Min(amount, localPetBlock);
        localPetBlock -= blockConsumed;
        if (localPetBlock < 0) localPetBlock = 0;
        
        Debug.Log($"DamageLocalPet: Incoming={amount}, Block={blockConsumed}, RemainingDamage={damageAfterBlock}");
        
        if (damageAfterBlock > 0)
        {
            // Check for Break
            StatusEffectManager statusManager = gameManager.GetPlayerManager().GetStatusEffectManager();
            if (statusManager.IsLocalPetBroken()) 
            {
                int breakBonus = Mathf.FloorToInt(damageAfterBlock * 0.5f); 
                Debug.Log($"Local Pet has Break! Increasing damage by {breakBonus}");
                damageAfterBlock += breakBonus;
            }
            
            localPetHealth -= damageAfterBlock;
            if (localPetHealth < 0) localPetHealth = 0;
        }
        
        gameManager.UpdateHealthUI(); // Update both health and block display
    }
    
    #endregion
    
    #region Block Methods
    
    public void AddBlockToLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        localPlayerBlock += amount;
        Debug.Log($"Added {amount} block to Local Player. New total: {localPlayerBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }

    public void AddBlockToLocalPet(int amount)
    {
        if (amount <= 0) return;
        localPetBlock += amount;
        Debug.Log($"Added {amount} block to Local Pet. New total: {localPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display

        // --- ADDED: Network Sync for Local Pet Block ---
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView().RPC("RpcUpdateLocalPetBlock", RpcTarget.Others, localPetBlock); // Send the new total block
            // Debug.Log($"Sent RpcUpdateLocalPetBlock({localPetBlock}) to others.");
        }
        // --- END ADDED ---
    }
    
    public void AddBlockToOpponentPet(int amount, bool updateUIImmediate = true)
    {
        if (amount <= 0) return;
        
        // Update local simulation immediately
        opponentPetBlock += amount;
        Debug.Log($"Added {amount} block to Opponent Pet (local sim). New total: {opponentPetBlock}");
        if (updateUIImmediate)
        {
            gameManager.UpdateHealthUI(); // Update block display
        }

        // --- ADDED: Network Sync for Opponent Pet Block ---
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            // Send RPC to the specific opponent, telling them to add block to *their* pet
            gameManager.GetPhotonView().RPC("RpcAddBlockToLocalPet", opponentPlayer, amount); 
            // Debug.Log($"Sent RpcAddBlockToLocalPet({amount}) to {opponentPlayer.NickName}.");
        }
        else if (opponentPlayer == null)
        {
            Debug.LogWarning("AddBlockToOpponentPet: Cannot send RPC, opponentPlayer is null.");
        }
        // --- END ADDED ---
    }

    // --- ADDED: Direct Setter for Opponent Pet Block ---
    public void SetOpponentPetBlock(int amount)
    {
        opponentPetBlock = Mathf.Max(0, amount); // Ensure block doesn't go below 0
        Debug.Log($"Set Opponent Pet block (from network update) to {opponentPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }
    // --- END ADDED ---

    public void ResetAllBlock()
    {
        Debug.Log($"Resetting block. Player: {localPlayerBlock} -> 0, Pet: {localPetBlock} -> 0");
        localPlayerBlock = 0;
        localPetBlock = 0;
        gameManager.UpdateHealthUI(); // Update block display
    }
    
    // --- ADDED: Reset only player block ---
    public void ResetPlayerBlockOnly()
    {
        Debug.Log($"Resetting Player Block Only: {localPlayerBlock} -> 0");
        localPlayerBlock = 0;
        gameManager.UpdateHealthUI(); // Update block display
    }
    // --- END ADDED ---
    
    // --- ADDED: Reset only opponent pet block ---
    public void ResetOpponentPetBlockOnly()
    {
        Debug.Log($"Resetting Opponent Pet Block (Local Sim): {opponentPetBlock} -> 0");
        opponentPetBlock = 0;
         // Don't call UpdateHealthUI here, it will be called by the turn logic

       // --- ADDED: Notify the actual owner to reset their pet's block --- 
       Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
       if (PhotonNetwork.InRoom && opponentPlayer != null)
       {
           Debug.Log($"Sending RpcResetMyPetBlock to opponent {opponentPlayer.NickName}.");
           gameManager.GetPhotonView()?.RPC("RpcResetMyPetBlock", opponentPlayer);
       }
       else if (opponentPlayer == null)
       {
           Debug.LogWarning("ResetOpponentPetBlockOnly: Cannot send RPC, opponentPlayer is null.");
       }
       // --- END ADDED ---
    }
    // --- END ADDED ---
    
    // --- ADDED: Reset only local pet block (called via RPC) ---
    public void ResetLocalPetBlockOnly()
    {
        Debug.Log($"Resetting Local Pet Block (RPC Triggered): {localPetBlock} -> 0");
        localPetBlock = 0;
        gameManager.UpdateHealthUI(); // Update UI immediately on the owner's client
    }
    // --- END ADDED ---
    
    #endregion
    
    #region Healing Methods
    
    public void HealLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePlayerMaxHealth();
        localPlayerHealth += amount;
        if (localPlayerHealth > effectiveMaxHP) localPlayerHealth = effectiveMaxHP;
        Debug.Log($"Healed Local Player by {amount}. New health: {localPlayerHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }

    public void HealLocalPet(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePetMaxHealth();
        localPetHealth += amount;
        if (localPetHealth > effectiveMaxHP) localPetHealth = effectiveMaxHP;
        Debug.Log($"Healed Local Pet by {amount}. New health: {localPetHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }
    
    public void HealOpponentPet(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectiveOpponentPetMaxHealth();
        opponentPetHealth += amount;
        if (opponentPetHealth > effectiveMaxHP) opponentPetHealth = effectiveMaxHP;
        Debug.Log($"Healed Opponent Pet by {amount} (local sim). New health: {opponentPetHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }
    
    #endregion
    
    #region DoT Methods
    
    public void ApplyDotLocalPlayer(int damage, int duration)
    {
        if (damage <= 0 || duration <= 0) return;
        localPlayerDotTurns += duration;
        localPlayerDotDamage = damage; 
        Debug.Log($"Applied DoT to Player: {damage} damage for {duration} turns. Total Turns: {localPlayerDotTurns}");
    }
    
    public void ApplyDotLocalPet(int damage, int duration)
    {
        if (damage <= 0 || duration <= 0) return;
        localPetDotTurns += duration;
        localPetDotDamage = damage; 
        Debug.Log($"Applied DoT to Local Pet: {damage} damage for {duration} turns. Total Turns: {localPetDotTurns}");

        // Notify others
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyDoTToMyPet", RpcTarget.Others, damage, duration);
        }
    }
    
    public void ApplyDotOpponentPet(int damage, int duration, bool originatedFromRPC = false)
    {
        if (damage <= 0 || duration <= 0) return;
        opponentPetDotTurns += duration;
        opponentPetDotDamage = damage; 
        Debug.Log($"Applied DoT to Opponent Pet: {damage} damage for {duration} turns (local sim). Total Turns: {opponentPetDotTurns}");

        // Notify the actual owner
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyDoTToMyPet", opponentPlayer, damage, duration);
        }
        else if (originatedFromRPC)
        {
            Debug.Log("ApplyDotOpponentPet called from RPC, skipping send.");
        }
    }
    
    public void ProcessPlayerDotEffect()
    {
        if (localPlayerDotTurns > 0 && localPlayerDotDamage > 0)
        {
            Debug.Log($"Player DoT ticking for {localPlayerDotDamage} damage.");
            DamageLocalPlayer(localPlayerDotDamage);
            localPlayerDotTurns--; // Decrement AFTER applying damage
            if (localPlayerDotTurns == 0) localPlayerDotDamage = 0; // Clear damage if duration ends
        }
    }
    
    public void ProcessLocalPetDotEffect()
    {
        if (localPetDotTurns > 0 && localPetDotDamage > 0)
        {
            Debug.Log($"Local Pet DoT ticking for {localPetDotDamage} damage.");
            DamageLocalPet(localPetDotDamage);
            localPetDotTurns--; 
            if (localPetDotTurns == 0) localPetDotDamage = 0; 
        }
    }
    
    public void ProcessOpponentPetDotEffect()
    {
        if (opponentPetDotTurns > 0 && opponentPetDotDamage > 0)
        {
            Debug.Log($"Opponent Pet DoT duration ticking (local sim). Turns remaining: {opponentPetDotTurns - 1}");
            // Actual damage is handled by the owner's client. We only decrement turns locally.
            opponentPetDotTurns--; 
            if (opponentPetDotTurns == 0) opponentPetDotDamage = 0; 
        }
    }
    
    public int GetEffectivePlayerMaxHealth()
    {
        // Return base health directly
        return gameManager.GetStartingPlayerHealth();
    }
    
    public int GetEffectivePetMaxHealth()
    {
        // Return base health directly
        return gameManager.GetStartingPetHealth();
    }
    
    public int GetEffectiveOpponentPetMaxHealth()
    {
        // Return base opponent pet health - Assuming symmetric starting health for now
        // TODO: Revisit if opponent base health can differ significantly and needs specific tracking
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { return (int)oppBasePetHP; } 
            catch { /* Fall through to default */ }
        }
        return gameManager.GetStartingPetHealth(); // Fallback to default starting pet health
    }
    
    #endregion
    
    #region Crit Methods
    
    // --- MODIFIED: ApplyCritChanceBuffPlayer ---
    public void ApplyCritChanceBuffPlayer(int amount, int duration)
    {
        if (amount <= 0 || duration <= 0) return;
        localPlayerCritBuffs.Add(new CritBuff { amount = amount, turns = duration });
        Debug.Log($"Applied Crit Chance Buff to Player: +{amount}% for {duration} turns.");
        // Optionally update UI immediately if needed, though turn start/end updates might be sufficient
        // gameManager.UpdateHealthUI(); 
    }
    // --- END MODIFIED ---

    // --- MODIFIED: ApplyCritChanceBuffPet ---
    public void ApplyCritChanceBuffPet(int amount, int duration)
    {
        if (amount <= 0 || duration <= 0) return;
        localPetCritBuffs.Add(new CritBuff { amount = amount, turns = duration });
        Debug.Log($"Applied Crit Chance Buff to Pet: +{amount}% for {duration} turns.");

        // Notify others
        if (PhotonNetwork.InRoom)
        {
            // Send amount and duration
            gameManager.GetPhotonView()?.RPC("RpcApplyCritBuffToMyPet", RpcTarget.Others, amount, duration);
        }
        // Optionally update UI
        // gameManager.UpdateHealthUI(); 
    }
    // --- END MODIFIED ---

    // --- MODIFIED: ApplyCritChanceBuffOpponentPet ---
    public void ApplyCritChanceBuffOpponentPet(int amount, int duration, bool originatedFromRPC = false)
    {
        if (amount <= 0 || duration <= 0) return;
        opponentPetCritBuffs.Add(new CritBuff { amount = amount, turns = duration });
        Debug.Log($"Applied Crit Chance Buff to Opponent Pet: +{amount}% for {duration} turns (local sim).");

        // Notify the actual owner IF this didn't come from an RPC itself
        if (!originatedFromRPC)
        {
            Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
            if (PhotonNetwork.InRoom && opponentPlayer != null)
            {
                // Send amount and duration
                gameManager.GetPhotonView()?.RPC("RpcApplyCritBuffToMyPet", opponentPlayer, amount, duration);
            }
        }
        // Optionally update UI
        // gameManager.UpdateHealthUI(); 
    }
    // --- END MODIFIED ---

    // --- ADDED: Decrement Player Crit Buffs ---
    public void DecrementPlayerCritBuffDurations()
    {
        bool changed = false;
        for (int i = localPlayerCritBuffs.Count - 1; i >= 0; i--)
        {
            CritBuff buff = localPlayerCritBuffs[i];
            buff.turns--;
            if (buff.turns <= 0)
            {
                localPlayerCritBuffs.RemoveAt(i);
                Debug.Log($"Player Crit Buff ({buff.amount}%) expired.");
                changed = true;
            }
            else
            {
                localPlayerCritBuffs[i] = buff; // Update turns
            }
        }
        // if (changed) gameManager.UpdateHealthUI(); // Update UI if buffs expired
    }
    // --- END ADDED ---

    // --- ADDED: Decrement Pet Crit Buffs ---
    public void DecrementLocalPetCritBuffDurations()
    {
         bool changed = false;
        for (int i = localPetCritBuffs.Count - 1; i >= 0; i--)
        {
            CritBuff buff = localPetCritBuffs[i];
            buff.turns--;
            if (buff.turns <= 0)
            {
                localPetCritBuffs.RemoveAt(i);
                Debug.Log($"Local Pet Crit Buff ({buff.amount}%) expired.");
                changed = true;
            }
            else
            {
                localPetCritBuffs[i] = buff; // Update turns
            }
        }
       // if (changed) gameManager.UpdateHealthUI(); // Update UI if buffs expired
    }
    // --- END ADDED ---
    
    // --- ADDED: Decrement Opponent Pet Crit Buffs ---
    public void DecrementOpponentPetCritBuffDurations()
    {
        bool changed = false;
        for (int i = opponentPetCritBuffs.Count - 1; i >= 0; i--)
        {
            CritBuff buff = opponentPetCritBuffs[i];
            buff.turns--;
            if (buff.turns <= 0)
            {
                opponentPetCritBuffs.RemoveAt(i);
                 Debug.Log($"Opponent Pet Crit Buff ({buff.amount}%) expired (local sim).");
                changed = true;
            }
            else
            {
                opponentPetCritBuffs[i] = buff; // Update turns
            }
        }
        // if (changed) gameManager.UpdateHealthUI(); // Update UI if buffs expired
    }
    // --- END ADDED ---

    // --- MODIFIED: GetPlayerEffectiveCritChance ---
    public int GetPlayerEffectiveCritChance()
    {
        int bonus = localPlayerCritBuffs.Sum(buff => buff.amount);
        return Mathf.Max(0, BASE_CRIT_CHANCE + bonus); // Ensure non-negative
    }
    // --- END MODIFIED ---

    // --- MODIFIED: GetPetEffectiveCritChance ---
    public int GetPetEffectiveCritChance()
    {
        int bonus = localPetCritBuffs.Sum(buff => buff.amount);
        return Mathf.Max(0, BASE_CRIT_CHANCE + bonus); // Ensure non-negative
    }
    // --- END MODIFIED ---
    
    // --- MODIFIED: GetOpponentPetEffectiveCritChance ---
    public int GetOpponentPetEffectiveCritChance()
    {
        int bonus = opponentPetCritBuffs.Sum(buff => buff.amount);
        return Mathf.Max(0, BASE_CRIT_CHANCE + bonus); // Ensure non-negative
    }
    // --- END MODIFIED ---
    
    public int GetBaseCritChance()
    {
        return BASE_CRIT_CHANCE;
    }
    
    #endregion
    
    #region Getters and Setters
    
    public int GetLocalPlayerHealth() => localPlayerHealth;
    public int GetLocalPetHealth() => localPetHealth;
    public int GetOpponentPetHealth() => opponentPetHealth;
    
    public int GetLocalPlayerBlock() => localPlayerBlock;
    public int GetLocalPetBlock() => localPetBlock;
    public int GetOpponentPetBlock() => opponentPetBlock;
    
    public int GetPlayerDotTurns() => localPlayerDotTurns;
    public int GetPlayerDotDamage() => localPlayerDotDamage;
    public int GetLocalPetDotTurns() => localPetDotTurns;
    public int GetLocalPetDotDamage() => localPetDotDamage;
    public int GetOpponentPetDotTurns() => opponentPetDotTurns;
    public int GetOpponentPetDotDamage() => opponentPetDotDamage;
    
    #endregion
}