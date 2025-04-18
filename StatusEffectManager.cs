using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using ExitGames.Client.Photon;

public class StatusEffectManager
{
    private GameManager gameManager;
    
    // Status effect tracking
    private int localPlayerWeakTurns = 0;
    private int localPlayerBreakTurns = 0;
    private int localPetWeakTurns = 0;
    private int localPetBreakTurns = 0;
    private int opponentPetWeakTurns = 0;
    private int opponentPetBreakTurns = 0;

    // --- ADDED: Reflection Tracking ---
    private int localPlayerReflectionTurns = 0;
    private int localPlayerReflectionPercentage = 0;
    private int localPetReflectionTurns = 0;
    private int localPetReflectionPercentage = 0;
    private int opponentPetReflectionTurns = 0;
    private int opponentPetReflectionPercentage = 0;
    
    // --- ADDED: Scaling Attack Tracking (Local only) ---
    private Dictionary<string, int> scalingAttackCounters = new Dictionary<string, int>();
    
    // Combo tracking
    private int currentComboCount = 0;
    
    public StatusEffectManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize()
    {
        // Reset status effects
        localPlayerWeakTurns = 0;
        localPlayerBreakTurns = 0;
        localPetWeakTurns = 0;
        localPetBreakTurns = 0;
        opponentPetWeakTurns = 0;
        opponentPetBreakTurns = 0;

        // --- ADDED: Reset Reflection ---
        localPlayerReflectionTurns = 0;
        localPlayerReflectionPercentage = 0;
        localPetReflectionTurns = 0;
        localPetReflectionPercentage = 0;
        opponentPetReflectionTurns = 0;
        opponentPetReflectionPercentage = 0;

        // --- ADDED: Reset Scaling Counters ---
        ResetScalingAttackCounters();
        
        // Reset combo
        currentComboCount = 0;
    }

    // --- ADDED: Call this at the start of each combat ---
    public void ResetCombatSpecificState()
    {
        ResetScalingAttackCounters();
        // Reset other combat-specific states here if needed
    }
    
    #region Status Effect Methods
    
    public void ApplyStatusEffectLocalPlayer(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        switch(type)
        {
            case StatusEffectType.Weak:
                localPlayerWeakTurns += duration;
                Debug.Log($"Applied Weak to Player for {duration} turns. Total: {localPlayerWeakTurns}");
                break;
            case StatusEffectType.Break:
                localPlayerBreakTurns += duration;
                Debug.Log($"Applied Break to Player for {duration} turns. Total: {localPlayerBreakTurns}");
                break;
        }
    }
    
    public void ApplyStatusEffectLocalPet(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        switch(type)
        {
            case StatusEffectType.Weak:
                localPetWeakTurns += duration;
                Debug.Log($"Applied Weak to Local Pet for {duration} turns. Total: {localPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                localPetBreakTurns += duration;
                Debug.Log($"Applied Break to Local Pet for {duration} turns. Total: {localPetBreakTurns}");
                break;
        }
    }
    
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        // Note: Opponent pet status is often simulated locally first, then confirmed/updated via network state.
        switch(type)
        {
            case StatusEffectType.Weak:
                opponentPetWeakTurns += duration;
                Debug.Log($"Applied Weak to Opponent Pet for {duration} turns (local sim). Total: {opponentPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                opponentPetBreakTurns += duration;
                Debug.Log($"Applied Break to Opponent Pet for {duration} turns (local sim). Total: {opponentPetBreakTurns}");
                break;
        }
    }

    // --- ADDED: Reflection Methods ---
    public void ApplyReflectionPlayer(int percentage, int duration)
    {
        if (duration <= 0 || percentage <= 0) return;
        localPlayerReflectionTurns += duration;
        localPlayerReflectionPercentage = Mathf.Max(localPlayerReflectionPercentage, percentage); // Take the stronger reflection if already active
        Debug.Log($"Applied Reflection to Player: {percentage}% for {duration} turns. Total Duration: {localPlayerReflectionTurns}, Effective %: {localPlayerReflectionPercentage}");
        // --- ADDED: Sync to Custom Properties ---
        Hashtable reflectProps = new Hashtable {
            { PlayerManager.PLAYER_REFLECT_PERC_PROP, localPlayerReflectionPercentage },
            { PlayerManager.PLAYER_REFLECT_TURNS_PROP, localPlayerReflectionTurns }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
        // --- END ADDED ---
    }

    public void ApplyReflectionLocalPet(int percentage, int duration)
    {
        if (duration <= 0 || percentage <= 0) return;
        localPetReflectionTurns += duration;
        localPetReflectionPercentage = Mathf.Max(localPetReflectionPercentage, percentage); 
        Debug.Log($"Applied Reflection to Local Pet: {percentage}% for {duration} turns. Total Duration: {localPetReflectionTurns}, Effective %: {localPetReflectionPercentage}");
        // --- ADDED: Sync to Custom Properties ---
        Hashtable reflectProps = new Hashtable {
            { PlayerManager.PET_REFLECT_PERC_PROP, localPetReflectionPercentage },
            { PlayerManager.PET_REFLECT_TURNS_PROP, localPetReflectionTurns }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
        // --- END ADDED ---
    }

    public void ApplyReflectionOpponentPet(int percentage, int duration)
    {
        if (duration <= 0 || percentage <= 0) return;
        opponentPetReflectionTurns += duration;
        opponentPetReflectionPercentage = Mathf.Max(opponentPetReflectionPercentage, percentage);
        Debug.Log($"Applied Reflection to Opponent Pet: {percentage}% for {duration} turns (local sim). Total Duration: {opponentPetReflectionTurns}, Effective %: {opponentPetReflectionPercentage}");
        // TODO: Add network sync call if needed
    }
    // --- END ADDED ---
    
    public void DecrementPlayerStatusEffects()
    {
        if (localPlayerWeakTurns > 0) localPlayerWeakTurns--;
        if (localPlayerBreakTurns > 0) localPlayerBreakTurns--;
        // --- ADDED: Decrement Reflection ---
        if (localPlayerReflectionTurns > 0)
        {
            localPlayerReflectionTurns--;
            if (localPlayerReflectionTurns == 0) 
            {
                localPlayerReflectionPercentage = 0; // Reset percentage when duration ends
                // --- ADDED: Sync Zeroed State ---
                Hashtable reflectProps = new Hashtable {
                    { PlayerManager.PLAYER_REFLECT_PERC_PROP, 0 },
                    { PlayerManager.PLAYER_REFLECT_TURNS_PROP, 0 }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
                // --- END ADDED ---
            }
            else
            {
                // --- ADDED: Sync Decremented Turns ---
                Hashtable reflectProps = new Hashtable { { PlayerManager.PLAYER_REFLECT_TURNS_PROP, localPlayerReflectionTurns } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
                // --- END ADDED ---
            }
        }
        // --- END ADDED ---
        Debug.Log($"Decremented Player Status. Weak: {localPlayerWeakTurns}, Break: {localPlayerBreakTurns}, Reflect: {localPlayerReflectionPercentage}% ({localPlayerReflectionTurns}t)");
    }
    
    public void DecrementLocalPetStatusEffects()
    {
        if (localPetWeakTurns > 0) localPetWeakTurns--;
        if (localPetBreakTurns > 0) localPetBreakTurns--;
        // --- ADDED: Decrement Reflection ---
        if (localPetReflectionTurns > 0)
        {
            localPetReflectionTurns--;
            if (localPetReflectionTurns == 0) 
            {
                localPetReflectionPercentage = 0;
                // --- ADDED: Sync Zeroed State ---
                Hashtable reflectProps = new Hashtable {
                    { PlayerManager.PET_REFLECT_PERC_PROP, 0 },
                    { PlayerManager.PET_REFLECT_TURNS_PROP, 0 }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
                // --- END ADDED ---
            }
            else
            {
                // --- ADDED: Sync Decremented Turns ---
                Hashtable reflectProps = new Hashtable { { PlayerManager.PET_REFLECT_TURNS_PROP, localPetReflectionTurns } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(reflectProps);
                // --- END ADDED ---
            }
        }
        // --- END ADDED ---
        Debug.Log($"Decremented Local Pet Status. Weak: {localPetWeakTurns}, Break: {localPetBreakTurns}, Reflect: {localPetReflectionPercentage}% ({localPetReflectionTurns}t)");
    }
    
    public void DecrementOpponentPetStatusEffects()
    {
        // Note: This primarily affects the local simulation. Actual state comes from network.
        if (opponentPetWeakTurns > 0) opponentPetWeakTurns--;
        if (opponentPetBreakTurns > 0) opponentPetBreakTurns--;
        // --- ADDED: Decrement Reflection ---
        if (opponentPetReflectionTurns > 0)
        {
            opponentPetReflectionTurns--;
            if (opponentPetReflectionTurns == 0) opponentPetReflectionPercentage = 0;
        }
        // --- END ADDED ---
        Debug.Log($"Decremented Opponent Pet Status (local sim). Weak: {opponentPetWeakTurns}, Break: {opponentPetBreakTurns}, Reflect: {opponentPetReflectionPercentage}% ({opponentPetReflectionTurns}t)");
    }
    
    #endregion

    // --- ADDED: Scaling Attack Methods ---
    public void IncrementScalingAttackCounter(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return;
        if (!scalingAttackCounters.ContainsKey(identifier))
        {
            scalingAttackCounters[identifier] = 0;
        }
        scalingAttackCounters[identifier]++;
        Debug.Log($"Incremented scaling counter for '{identifier}'. New count: {scalingAttackCounters[identifier]}");
    }

    public int GetScalingAttackBonus(string identifier, int baseIncrease)
    {
        if (string.IsNullOrEmpty(identifier) || !scalingAttackCounters.ContainsKey(identifier))
        {
            return 0;
        }
        int bonus = scalingAttackCounters[identifier] * baseIncrease;
        Debug.Log($"Calculated scaling bonus for '{identifier}': {scalingAttackCounters[identifier]} uses * {baseIncrease} base increase = {bonus}");
        return bonus;
    }

    public void ResetScalingAttackCounters()
    {
        Debug.Log("Resetting all scaling attack counters for new combat.");
        scalingAttackCounters.Clear();
    }
    // --- END ADDED ---
    
    #region Combo Methods
    
    public void IncrementComboCount()
    {
        currentComboCount++;
        Debug.Log($"Combo count incremented to: {currentComboCount}");
    }

    public void ResetComboCount()
    {
        if (currentComboCount > 0)
        {
            Debug.Log($"Resetting combo count from {currentComboCount} to 0.");
            currentComboCount = 0;
        }
    }
    
    #endregion
    
    #region Status Check Methods
    
    public bool IsPlayerWeak() => localPlayerWeakTurns > 0;
    public bool IsPlayerBroken() => localPlayerBreakTurns > 0;
    public bool IsLocalPetWeak() => localPetWeakTurns > 0;
    public bool IsLocalPetBroken() => localPetBreakTurns > 0;
    public bool IsOpponentPetWeak() => opponentPetWeakTurns > 0;
    public bool IsOpponentPetBroken() => opponentPetBreakTurns > 0;

    // --- ADDED: Reflection Checks ---
    public bool IsPlayerReflecting() => localPlayerReflectionTurns > 0;
    public bool IsLocalPetReflecting() => localPetReflectionTurns > 0;
    public bool IsOpponentPetReflecting() => opponentPetReflectionTurns > 0;
    // --- END ADDED ---
    
    #endregion
    
    #region Getters
    
    public int GetCurrentComboCount() => currentComboCount;
    
    public int GetPlayerWeakTurns() => localPlayerWeakTurns;
    public int GetPlayerBreakTurns() => localPlayerBreakTurns;
    public int GetLocalPetWeakTurns() => localPetWeakTurns;
    public int GetLocalPetBreakTurns() => localPetBreakTurns;
    public int GetOpponentPetWeakTurns() => opponentPetWeakTurns;
    public int GetOpponentPetBreakTurns() => opponentPetBreakTurns;

    // --- ADDED: Reflection Getters ---
    public int GetPlayerReflectionTurns() => localPlayerReflectionTurns;
    public int GetPlayerReflectionPercentage() => localPlayerReflectionPercentage;
    public int GetLocalPetReflectionTurns() => localPetReflectionTurns;
    public int GetLocalPetReflectionPercentage() => localPetReflectionPercentage;
    public int GetOpponentPetReflectionTurns() => opponentPetReflectionTurns;
    public int GetOpponentPetReflectionPercentage() => opponentPetReflectionPercentage;
    // --- END ADDED ---
    
    #endregion

    // --- ADDED: Method to update opponent pet status from network ---
    public void UpdateOpponentPetReflectionStatus(int percentage, int turns)
    {
        // Directly set the local simulation state based on received data
        opponentPetReflectionPercentage = percentage;
        opponentPetReflectionTurns = turns;
        Debug.Log($"Updated Opponent Pet Reflection Status from Network: {percentage}% for {turns} turns.");
        // UI update should be triggered by the caller (PhotonManager/GameManager)
    }
    // --- END ADDED ---

    // TODO: Add network synchronization logic for reflection status (opponent pet primarily, maybe others for spectating/consistency)
    // This might involve implementing IPunObservable or using custom properties depending on the existing setup.
}