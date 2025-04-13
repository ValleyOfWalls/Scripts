using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PrefabSpawner : MonoBehaviourPunCallbacks
{
    private GameplayManager gameplayManager;
    
    private void Awake()
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
        
        // Notify GameplayManager that prefabs have been spawned
        StartCoroutine(NotifyGameplayManager());
    }
    
    private IEnumerator NotifyGameplayManager()
    {
        // Wait a frame to ensure components are initialized
        yield return null;
        
        // Find GameplayManager if not already set
        gameplayManager = FindObjectOfType<GameplayManager>();
        
        if (gameplayManager != null)
        {
            gameplayManager.OnPlayerAndMonsterSpawned();
        }
        else
        {
            Debug.LogWarning("GameplayManager not found after spawning - will try again when it's created");
        }
    }
}