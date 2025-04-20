using UnityEngine;
using System.Collections.Generic;
using Photon.Realtime;
using Photon.Pun;

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
        
        // Reset combo
        currentComboCount = 0;
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
    
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration, bool originatedFromRPC = false)
    {
        if (duration <= 0) return;
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

        Debug.Log($"Applied Status {type} to Opponent Pet for {duration} turns (local sim)");

        // Notify the actual owner
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyStatusToMyPet", opponentPlayer, type, duration);
        }
        else if (originatedFromRPC)
        {
            Debug.Log("ApplyStatusEffectOpponentPet called from RPC, skipping send.");
        }
    }
    
    public void DecrementPlayerStatusEffects()
    {
        if (localPlayerWeakTurns > 0) localPlayerWeakTurns--;
        if (localPlayerBreakTurns > 0) localPlayerBreakTurns--;
        Debug.Log($"Decremented Player Status. Weak: {localPlayerWeakTurns}, Break: {localPlayerBreakTurns}");
    }
    
    public void DecrementLocalPetStatusEffects()
    {
        if (localPetWeakTurns > 0) localPetWeakTurns--;
        if (localPetBreakTurns > 0) localPetBreakTurns--;
        Debug.Log($"Decremented Local Pet Status. Weak: {localPetWeakTurns}, Break: {localPetBreakTurns}");
    }
    
    public void DecrementOpponentPetStatusEffects()
    {
        if (opponentPetWeakTurns > 0) opponentPetWeakTurns--;
        if (opponentPetBreakTurns > 0) opponentPetBreakTurns--;
        Debug.Log($"Decremented Opponent Pet Status (local sim). Weak: {opponentPetWeakTurns}, Break: {opponentPetBreakTurns}");
    }
    
    #endregion
    
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
    
    #endregion
    
    #region Getters
    
    public int GetCurrentComboCount() => currentComboCount;
    
    public int GetPlayerWeakTurns() => localPlayerWeakTurns;
    public int GetPlayerBreakTurns() => localPlayerBreakTurns;
    public int GetLocalPetWeakTurns() => localPetWeakTurns;
    public int GetLocalPetBreakTurns() => localPetBreakTurns;
    public int GetOpponentPetWeakTurns() => opponentPetWeakTurns;
    public int GetOpponentPetBreakTurns() => opponentPetBreakTurns;
    
    #endregion
}