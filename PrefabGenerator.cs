using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR
public class PrefabGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Network Prefabs")]
    public static void GenerateNetworkPrefabs()
    {
        // Create prefabs directory if it doesn't exist
        string prefabPath = "Assets/Resources/PhotonPrefabs";
        if (!Directory.Exists(prefabPath))
        {
            Directory.CreateDirectory(prefabPath);
        }
        
        // 1. Create Player prefab
        GameObject playerObj = new GameObject("Player");
        
        // Add necessary components for a networked player
        playerObj.AddComponent<Photon.Pun.PhotonView>();
        playerObj.AddComponent<PlayerController>(); // This will be implemented later
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(playerObj, prefabPath + "/Player.prefab");
        Debug.Log("Player prefab created at: " + prefabPath + "/Player.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(playerObj);
        
        // 2. Create Monster prefab
        GameObject monsterObj = new GameObject("Monster");
        
        // Add necessary components for a networked monster
        monsterObj.AddComponent<Photon.Pun.PhotonView>();
        monsterObj.AddComponent<MonsterController>(); // This will be implemented later
        
        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(monsterObj, prefabPath + "/Monster.prefab");
        Debug.Log("Monster prefab created at: " + prefabPath + "/Monster.prefab");
        
        // Destroy the temporary GameObject
        DestroyImmediate(monsterObj);
        
        AssetDatabase.Refresh();
    }
}


#endif