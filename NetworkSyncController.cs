using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// Centralizes network property synchronization for consistent handling of entity state.
/// Handles both property updates and RPC calls in a standardized way.
/// </summary>
public class NetworkSyncController : MonoBehaviour
{
    private GameManager gameManager;
    private PhotonView photonView;
    
    // Dictionary to store entity references by player ActorNumber
    private Dictionary<int, CombatEntity> entityByPlayerActor = new Dictionary<int, CombatEntity>();
    
    // Cache of local player's properties to avoid redundant updates
    private Dictionary<string, object> localPropertyCache = new Dictionary<string, object>();
    
    public void Initialize(GameManager gameManager)
    {
        this.gameManager = gameManager;
        this.photonView = gameManager.GetPhotonView();
        
        if (photonView == null)
        {
            Debug.LogError("NetworkSyncController requires a PhotonView component!");
            return;
        }
        
        ClearCaches();
    }
    
    public void ClearCaches()
    {
        entityByPlayerActor.Clear();
        localPropertyCache.Clear();
    }
    
    public void RegisterEntity(CombatEntity entity, Player ownerPlayer)
    {
        if (ownerPlayer == null) return;
        
        entityByPlayerActor[ownerPlayer.ActorNumber] = entity;
        Debug.Log($"Registered entity {entity.GetName()} with owner ActorNumber {ownerPlayer.ActorNumber}");
    }
    
    /// <summary>
    /// Updates a custom property on the local player with proper caching to avoid redundant updates.
    /// </summary>
    public void SetLocalPlayerProperty(string propName, object value, bool forceUpdate = false)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        
        // Check if the value has changed or if update is forced
        bool hasChanged = forceUpdate;
        if (!hasChanged && localPropertyCache.TryGetValue(propName, out object cachedValue))
        {
            hasChanged = !Equals(cachedValue, value);
        }
        else
        {
            // No cached value yet, so it has changed
            hasChanged = true;
        }
        
        if (hasChanged)
        {
            // Update cache and send to network
            localPropertyCache[propName] = value;
            Hashtable props = new Hashtable { { propName, value } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            //Debug.Log($"Set local player property {propName}={value}");
        }
    }
    
    /// <summary>
    /// Processes player property updates and updates the relevant entities.
    /// Called from GameManager.OnPlayerPropertiesUpdate.
    /// </summary>
    public void ProcessPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer == null) return;
        
        bool isLocalPlayer = targetPlayer == PhotonNetwork.LocalPlayer;
        
        // Get the corresponding entity if registered
        CombatEntity entity = null;
        if (entityByPlayerActor.TryGetValue(targetPlayer.ActorNumber, out entity))
        {
            // Process entity-specific properties
            UpdateEntityFromProperties(entity, changedProps);
        }
        else
        {
            // Handle properties for unregistered entities (like opponent pet)
            HandleUnregisteredEntityProperties(targetPlayer, changedProps);
        }
        
        // Process other game state properties
        HandleGameStateProperties(targetPlayer, changedProps);
        
        // Update UI to reflect changes
        gameManager.UpdateHealthUI();
    }
    
    private void UpdateEntityFromProperties(CombatEntity entity, Hashtable changedProps)
    {
        // Update entity based on property changes
        if (entity.GetEntityType() == CombatEntity.EntityType.LocalPlayer)
        {
            // Update local player from properties (mainly for syncing after reconnect)
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP))
                entity.SetHealth((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP))
                entity.SetBlock((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_ENERGY_PROP))
                entity.SetEnergy((int)changedProps[CombatStateManager.PLAYER_COMBAT_ENERGY_PROP]);
                
            // Status effects
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP))
                entity.SetWeakTurns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP))
                entity.SetBreakTurns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP))
                entity.SetThorns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP))
                entity.SetStrength((int)changedProps[CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP]);
        }
        else if (entity.GetEntityType() == CombatEntity.EntityType.LocalPet)
        {
            // Update local pet from properties
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP))
                entity.SetHealth((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_HP_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP))
                entity.SetEnergy((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP]);
                
            // Status effects
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP))
                entity.SetWeakTurns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP))
                entity.SetBreakTurns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP))
                entity.SetThorns((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP]);
                
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP))
                entity.SetStrength((int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP]);
        }
    }
    
    private void HandleUnregisteredEntityProperties(Player targetPlayer, Hashtable changedProps)
    {
        // This is for handling opponent pet properties when we don't have a CombatEntity for it yet
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        bool isOpponent = opponentPlayer != null && targetPlayer == opponentPlayer;
        
        if (isOpponent)
        {
            // Update opponent pet health in our local simulation
            if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP))
            {
                int newOpponentPetHealth = (int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_HP_PROP];
                gameManager.GetPlayerManager()?.GetHealthManager()?.SetOpponentPetHealth(newOpponentPetHealth);
            }
            
            // Other opponent pet properties would be handled similarly
        }
    }
    
    private void HandleGameStateProperties(Player targetPlayer, Hashtable changedProps)
    {
        // Handle game state properties like combat finished, scores, etc.
        if (changedProps.ContainsKey(CombatStateManager.COMBAT_FINISHED_PROPERTY))
        {
            bool combatFinished = (bool)changedProps[CombatStateManager.COMBAT_FINISHED_PROPERTY];
            Debug.Log($"Player {targetPlayer.NickName} combat finished: {combatFinished}");
            
            // If master client, check if all players are finished
            if (PhotonNetwork.IsMasterClient)
            {
                gameManager.GetPlayerManager()?.GetCombatStateManager()?.CheckForAllPlayersFinishedCombat();
            }
        }
        
        // Handle score changes
        if (changedProps.ContainsKey(CombatStateManager.PLAYER_SCORE_PROP))
        {
            gameManager.GetPlayerManager()?.GetCombatStateManager()?.UpdateScoreUI();
        }
    }
    
    /// <summary>
    /// Sends an RPC to a specific player.
    /// </summary>
    public void SendRPC(string methodName, Player targetPlayer, params object[] parameters)
    {
        if (!PhotonNetwork.InRoom || photonView == null) return;
        
        if (targetPlayer != null)
        {
            photonView.RPC(methodName, targetPlayer, parameters);
            Debug.Log($"Sent RPC {methodName} to {targetPlayer.NickName}");
        }
        else
        {
            Debug.LogWarning($"Cannot send RPC {methodName}: target player is null");
        }
    }
    
    /// <summary>
    /// Sends an RPC to all players.
    /// </summary>
    public void SendRPCToAll(string methodName, params object[] parameters)
    {
        if (!PhotonNetwork.InRoom || photonView == null) return;
        
        photonView.RPC(methodName, RpcTarget.All, parameters);
        Debug.Log($"Sent RPC {methodName} to all players");
    }
    
    /// <summary>
    /// Sends an RPC to all other players (excluding self).
    /// </summary>
    public void SendRPCToOthers(string methodName, params object[] parameters)
    {
        if (!PhotonNetwork.InRoom || photonView == null) return;
        
        photonView.RPC(methodName, RpcTarget.Others, parameters);
        Debug.Log($"Sent RPC {methodName} to other players");
    }
}