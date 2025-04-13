using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PrefabSpawner : MonoBehaviourPunCallbacks
{
    private GameplayManager gameplayManager;
    private bool hasSpawnedPrefabs = false;
    
    private void Awake()
    {
        TryGetGameplayManager();
    }
    
    private void TryGetGameplayManager()
    {
        gameplayManager = FindObjectOfType<GameplayManager>();
    }
    
    public void SpawnPlayerAndMonster()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogError("Cannot spawn: Not connected to Photon or not in a room");
            return;
        }
        
        if (hasSpawnedPrefabs)
        {
            Debug.Log("Prefabs already spawned, not spawning again");
            return;
        }
        
        // Spawn player character at a position based on actor number
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        Vector3 spawnPosition = new Vector3(-5 + (actorNumber * 2.5f), 0, 0);
        
        // Instantiate player
        GameObject playerObj = PhotonNetwork.Instantiate("Player", spawnPosition, Quaternion.identity);
        Debug.Log("Player instantiated: " + playerObj.name);
        
        // Instantiate monster slightly to the right of the player
        Vector3 monsterPosition = spawnPosition + new Vector3(1.5f, 0, 0);
        GameObject monsterObj = PhotonNetwork.Instantiate("Monster", monsterPosition, Quaternion.identity);
        Debug.Log("Monster instantiated: " + monsterObj.name);
        
        // Set flag
        hasSpawnedPrefabs = true;
        
        // Notify GameplayManager that prefabs have been spawned
        StartCoroutine(NotifyGameplayManager());
    }
    
    private IEnumerator NotifyGameplayManager()
    {
        // Wait to ensure components are initialized
        yield return new WaitForSeconds(1.0f);
        
        // Try to find GameplayManager for several attempts
        int attempts = 0;
        while (gameplayManager == null && attempts < 5)
        {
            gameplayManager = FindObjectOfType<GameplayManager>();
            
            if (gameplayManager == null)
            {
                Debug.LogWarning($"GameplayManager not found, attempt {attempts+1}/5 - waiting...");
                yield return new WaitForSeconds(0.5f);
                attempts++;
            }
        }
        
        if (gameplayManager != null)
        {
            gameplayManager.OnPlayerAndMonsterSpawned();
            Debug.Log("Successfully notified GameplayManager that prefabs have been spawned");
        }
        else
        {
            Debug.LogWarning("GameplayManager not found after multiple attempts");
            
            // As a fallback, try to get it via GameManager
            if (GameManager.Instance != null)
            {
                gameplayManager = GameManager.Instance.GetGameplayManager();
                
                if (gameplayManager != null)
                {
                    gameplayManager.OnPlayerAndMonsterSpawned();
                    Debug.Log("Successfully notified GameplayManager via GameManager instance");
                }
                else
                {
                    Debug.LogError("Failed to find GameplayManager - this may cause issues in gameplay");
                }
            }
        }
    }
}