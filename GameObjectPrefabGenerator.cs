using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
public class GameObjectPrefabGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Required Prefabs")]
    public static void GenerateRequiredPrefabs()
    {
        // Create prefabs directory if it doesn't exist
        string prefabPath = "Assets/Resources";
        if (!Directory.Exists(prefabPath))
        {
            Directory.CreateDirectory(prefabPath);
        }
        
        // Create Player prefab
        GameObject playerObj = new GameObject("Player");
        
        // Add necessary components for a networked player
        playerObj.AddComponent<Photon.Pun.PhotonView>();
        playerObj.AddComponent<PlayerController>();
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(playerObj, prefabPath + "/Player.prefab");
        Debug.Log("Player prefab created at: " + prefabPath + "/Player.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(playerObj);
        
        // Create Monster prefab
        GameObject monsterObj = new GameObject("Monster");
        
        // Add necessary components for a networked monster
        monsterObj.AddComponent<Photon.Pun.PhotonView>();
        monsterObj.AddComponent<MonsterController>();
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(monsterObj, prefabPath + "/Monster.prefab");
        Debug.Log("Monster prefab created at: " + prefabPath + "/Monster.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(monsterObj);
        
        // Create GameplayManager prefab
        GameObject gameplayManagerObj = new GameObject("GameplayManager");
        
        // Add necessary components
        Photon.Pun.PhotonView photonView = gameplayManagerObj.AddComponent<Photon.Pun.PhotonView>();
        photonView.Synchronization = Photon.Pun.ViewSynchronization.UnreliableOnChange;
        photonView.ObservedComponents = new List<Component>();
        
        gameplayManagerObj.AddComponent<GameplayManager>();
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(gameplayManagerObj, prefabPath + "/GameplayManager.prefab");
        Debug.Log("GameplayManager prefab created at: " + prefabPath + "/GameplayManager.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(gameplayManagerObj);
        
        AssetDatabase.Refresh();
    }
}
#endif