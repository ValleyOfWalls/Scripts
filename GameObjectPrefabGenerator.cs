using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Photon.Pun; // Added missing namespace

#if UNITY_EDITOR
public class GameObjectPrefabGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Required Prefabs")]
    public static void GenerateRequiredPrefabs()
    {
        Debug.Log("Starting to generate required prefabs...");
        
        // Create Resources directory if it doesn't exist
        string prefabPath = "Assets/Resources";
        if (!Directory.Exists(prefabPath))
        {
            Directory.CreateDirectory(prefabPath);
            Debug.Log("Created Resources directory");
        }
        
        // Create Player prefab
        GameObject playerObj = new GameObject("Player");
        
        // Add necessary components for a networked player
        PhotonView playerView = playerObj.AddComponent<PhotonView>();
        playerView.Synchronization = ViewSynchronization.UnreliableOnChange;
        playerView.ObservedComponents = new List<Component>();
        PlayerController playerController = playerObj.AddComponent<PlayerController>();
        playerView.ObservedComponents.Add(playerController);
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(playerObj, prefabPath + "/Player.prefab");
        Debug.Log("Player prefab created at: " + prefabPath + "/Player.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(playerObj);
        
        // Create Monster prefab
        GameObject monsterObj = new GameObject("Monster");
        
        // Add necessary components for a networked monster
        PhotonView monsterView = monsterObj.AddComponent<PhotonView>();
        monsterView.Synchronization = ViewSynchronization.UnreliableOnChange;
        monsterView.ObservedComponents = new List<Component>();
        MonsterController monsterController = monsterObj.AddComponent<MonsterController>();
        monsterView.ObservedComponents.Add(monsterController);
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(monsterObj, prefabPath + "/Monster.prefab");
        Debug.Log("Monster prefab created at: " + prefabPath + "/Monster.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(monsterObj);
        
        // Create GameplayManager prefab
        GameObject gameplayManagerObj = new GameObject("GameplayManager");
        
        // Add necessary components
        PhotonView gameplayManagerView = gameplayManagerObj.AddComponent<PhotonView>();
        gameplayManagerView.Synchronization = ViewSynchronization.UnreliableOnChange;
        gameplayManagerView.ObservedComponents = new List<Component>();
        GameplayManager gameplayManager = gameplayManagerObj.AddComponent<GameplayManager>();
        gameplayManagerView.ObservedComponents.Add(gameplayManager);
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(gameplayManagerObj, prefabPath + "/GameplayManager.prefab");
        Debug.Log("GameplayManager prefab created at: " + prefabPath + "/GameplayManager.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(gameplayManagerObj);
        
        AssetDatabase.Refresh();
        
        Debug.Log("All prefabs generated successfully!");
    }
}
#endif