using UnityEngine;
using UnityEditor;
using System.IO;
using Photon.Pun; // Make sure PUN2 is imported

public class PrefabSetup : Editor
{
    private const string PrefabSavePath = "Assets/Prefabs";

    [MenuItem("Tools/Setup/Create Basic Player Prefab")]
    public static void CreatePlayerPrefab()
    {
        CreatePrefab("PlayerPrefab", true);
    }

    [MenuItem("Tools/Setup/Create Basic Pet Prefab")]
    public static void CreatePetPrefab()
    {
        CreatePrefab("PetPrefab", true);
    }

    [MenuItem("Tools/Setup/Create Basic Card Prefab")] // Added Card Prefab
    public static void CreateCardPrefab()
    {
        CreatePrefab("CardPrefab", false); // Card might not need PhotonView directly, depends on how you sync card states
    }

    [MenuItem("Tools/Setup/Create ALL Non-UI Prefabs", priority = 51)] // Added priority for spacing
    public static void CreateAllNonUIPrefabs()
    {
        Debug.Log("Creating all non-UI prefabs...");
        CreatePlayerPrefab();
        CreatePetPrefab();
        CreateCardPrefab();
        Debug.Log("Finished creating all non-UI prefabs.");
    }

    private static void CreatePrefab(string prefabName, bool addPhotonView)
    {
        // Ensure the Prefabs directory exists
        if (!Directory.Exists(PrefabSavePath))
        {
            Directory.CreateDirectory(PrefabSavePath);
            AssetDatabase.Refresh();
            Debug.Log($"Created directory: {PrefabSavePath}");
        }

        string fullPrefabPath = Path.Combine(PrefabSavePath, prefabName + ".prefab");

        // Check if prefab already exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null)
        {
            // Delete existing asset
            bool deleted = AssetDatabase.DeleteAsset(fullPrefabPath);
            if (deleted)
            {
                Debug.Log($"Deleted existing prefab at {fullPrefabPath} to replace it.");
                AssetDatabase.Refresh(); // Ensure deletion is registered before creating new one
            }
            else
            {
                Debug.LogError($"Failed to delete existing prefab at {fullPrefabPath}. Skipping creation.");
                return; // Stop if deletion failed
            }
        }

        // Create a new GameObject
        GameObject prefabRoot = new GameObject(prefabName);

        // --- Add Basic Components (Customize as needed) ---
        // Add a simple visual representation (e.g., a Cube)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(prefabRoot.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one; // Adjust size as needed
        // Remove collider if not needed for the base prefab
        // DestroyImmediate(visual.GetComponent<Collider>());

        // Add PhotonView if requested
        if (addPhotonView)
        {
            PhotonView photonView = prefabRoot.AddComponent<PhotonView>();
            // You might need to configure observed components later
            // photonView.ObservedComponents = new System.Collections.Generic.List<Component>();
            Debug.Log($"Added PhotonView to {prefabName}");
        }

        // --- End Basic Components ---

        // Save the GameObject as a prefab
        try
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, fullPrefabPath);
            Debug.Log($"Successfully created prefab at: {fullPrefabPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create prefab {prefabName}: {e.Message}");
        }
        finally
        {
            // Clean up the temporary GameObject from the scene
            if (prefabRoot != null) DestroyImmediate(prefabRoot);
            AssetDatabase.Refresh();
        }
    }
} 