using UnityEngine;
using UnityEditor;

public class CardGeneratorMenu : Editor
{
    [MenuItem("Game/Generate Card Assets")]
    public static void GenerateCards()
    {
        CardGenerator.GenerateCardAssets();
    }
} 