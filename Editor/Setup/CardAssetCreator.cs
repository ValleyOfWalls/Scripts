using UnityEngine;
using UnityEditor;
using System.IO;

public class CardAssetCreator
{
    private const string CardAssetSavePath = "Assets/Cards"; // Define where card assets will be saved

    [MenuItem("Assets/Create/Cards/Example Strike Card")]
    public static void CreateStrikeCardAsset()
    {
        CreateCardAsset("Strike", 1, "Deal 6 damage.");
    }

    [MenuItem("Assets/Create/Cards/Example Defend Card")]
    public static void CreateDefendCardAsset()
    {
        CreateCardAsset("Defend", 1, "Gain 5 Block.");
    }

    private static void CreateCardAsset(string cardName, int cost, string description)
    {
        // Ensure the save directory exists
        if (!Directory.Exists(CardAssetSavePath))
        {
            Directory.CreateDirectory(CardAssetSavePath);
            AssetDatabase.Refresh();
            Debug.Log($"Created directory: {CardAssetSavePath}");
        }

        // Check if asset already exists
        string assetPath = Path.Combine(CardAssetSavePath, $"{cardName}.asset");
        if (AssetDatabase.LoadAssetAtPath<CardData>(assetPath) != null)
        {
            Debug.LogWarning($"Card asset already exists at {assetPath}. Skipping creation.");
            // Optionally, select the existing asset
            // Selection.activeObject = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            return;
        }

        CardData cardData = ScriptableObject.CreateInstance<CardData>();
        cardData.cardName = cardName;
        cardData.cost = cost;
        cardData.description = description;
        // Set other default properties here if needed

        AssetDatabase.CreateAsset(cardData, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created Card asset at: {assetPath}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = cardData; // Select the newly created asset
    }
} 