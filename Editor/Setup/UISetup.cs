using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using TMPro; // Import TextMeshPro

public class UISetup : Editor
{
    private const string UIPrefabSavePath = "Assets/Prefabs/UI";
    private const int DefaultPadding = 10;
    private const int DefaultSpacing = 5;

    // --- Canvas Creation ---    
    [MenuItem("Tools/Setup/UI/Create Start Screen Prefab")]
    public static void CreateStartScreen()
    {
        string prefabPath = CreateCanvasPrefab("StartScreenCanvas", AddStartScreenElements);
        if (!string.IsNullOrEmpty(prefabPath))
        {
            AssignPrefabToGameManager("startScreenCanvasPrefab", prefabPath);
        }
    }

    [MenuItem("Tools/Setup/UI/Create Lobby Screen Prefab")]
    public static void CreateLobbyScreen()
    {
        string prefabPath = CreateCanvasPrefab("LobbyCanvas", AddLobbyScreenElements);
        if (!string.IsNullOrEmpty(prefabPath))
        {
            AssignPrefabToGameManager("lobbyCanvasPrefab", prefabPath);
        }
    }

    [MenuItem("Tools/Setup/UI/Create Combat Gameplay Prefab")]
    public static void CreateCombatScreen()
    {
        string prefabPath = CreateCanvasPrefab("CombatCanvas", AddCombatScreenElements);
         if (!string.IsNullOrEmpty(prefabPath))
        {
            AssignPrefabToGameManager("combatCanvasPrefab", prefabPath); // Assumes field name is combatCanvasPrefab
        }
    }

    [MenuItem("Tools/Setup/UI/Create Draft Gameplay Prefab")]
    public static void CreateDraftScreen()
    {
        string prefabPath = CreateCanvasPrefab("DraftCanvas", AddDraftScreenElements);
         if (!string.IsNullOrEmpty(prefabPath))
        {
             // AssignPrefabToGameManager("draftCanvasPrefab", prefabPath); // Uncomment when field exists on GameManager
             Debug.LogWarning("DraftCanvas created, but no 'draftCanvasPrefab' field found on GameManager to assign to.");
        }
    }

    [MenuItem("Tools/Setup/UI/Create Card Template Prefab")]
    public static string CreateCardTemplatePrefab()
    {
        const string prefabName = "CardTemplate";
        string fullPrefabPath = Path.Combine(UIPrefabSavePath, prefabName + ".prefab");
        bool replacing = false;

        if (!Directory.Exists(UIPrefabSavePath))
        {
            Directory.CreateDirectory(UIPrefabSavePath);
            AssetDatabase.Refresh();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null)
        {
            if (AssetDatabase.DeleteAsset(fullPrefabPath))
            {
                Debug.Log($"Deleted existing card template prefab at {fullPrefabPath} to replace it.");
                AssetDatabase.Refresh();
                replacing = true;
            }
            else
            {
                Debug.LogError($"Failed to delete existing card template prefab at {fullPrefabPath}. Skipping creation.");
                return null;
            }
        }

        GameObject cardRoot = new GameObject(prefabName);
        string resultPath = null;
        try
        {
            // --- Build Card Hierarchy --- 
            Image cardImage = cardRoot.AddComponent<Image>();
            cardImage.color = new Color(0.2f, 0.2f, 0.25f);
            // Add CanvasGroup for drag-and-drop raycast control
            CanvasGroup cardCanvasGroup = cardRoot.AddComponent<CanvasGroup>(); 
            cardCanvasGroup.blocksRaycasts = true; // Initially blocks raycasts

            // Add the CardDragHandler script for drag functionality
            cardRoot.AddComponent<CardDragHandler>();

            LayoutElement rootLayout = cardRoot.AddComponent<LayoutElement>();
            rootLayout.minWidth = 120; rootLayout.minHeight = 180;
            rootLayout.preferredWidth = 120; rootLayout.preferredHeight = 180;
            
            VerticalLayoutGroup cardLayout = AddVerticalLayoutGroup(cardRoot, 5, 5, controlChildWidth: true, controlChildHeight: false);
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childForceExpandHeight = false;
            ContentSizeFitter rootFitter = cardRoot.GetComponent<ContentSizeFitter>();
            if(rootFitter != null) { rootFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained; rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; }

            // Header Panel 
            GameObject headerPanel = CreatePanel(cardRoot.transform, "HeaderPanel", true);
            SetLayoutElement(headerPanel, minHeight: 30);
            HorizontalLayoutGroup headerLayout = AddHorizontalLayoutGroup(headerPanel, 5, 5, controlChildHeight: true);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            ContentSizeFitter headerFitter = headerPanel.GetComponent<ContentSizeFitter>();
            if(headerFitter != null) { headerFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; headerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            TextMeshProUGUI costText = CreateText(headerPanel.transform, "CostText", "1", 18, TextAlignmentOptions.Center);
            SetLayoutElement(costText.gameObject, minWidth: 25, minHeight: 25);
            TextMeshProUGUI cardNameText = CreateText(headerPanel.transform, "CardNameText", "Card Name", 16, TextAlignmentOptions.Left);
            SetLayoutElement(cardNameText.gameObject, flexibleWidth: 1);

            // Art Panel
            GameObject artPanel = CreatePanel(cardRoot.transform, "ArtPanel");
            Image artImage = artPanel.GetComponent<Image>();
            artImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            SetLayoutElement(artPanel, minHeight: 60, flexibleHeight: 1);
            ContentSizeFitter artFitter = artPanel.GetComponent<ContentSizeFitter>();
            if(artFitter != null) { artFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; artFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained; } 

            // Description Panel
            GameObject descPanel = CreatePanel(cardRoot.transform, "DescPanel", true);
            SetLayoutElement(descPanel, minHeight: 40);
            VerticalLayoutGroup descLayout = AddVerticalLayoutGroup(descPanel, 5, 2, controlChildHeight: false);
            descLayout.childAlignment = TextAnchor.UpperCenter;
            descLayout.childForceExpandHeight = false;
            ContentSizeFitter descFitter = descPanel.GetComponent<ContentSizeFitter>();
            if(descFitter != null) { descFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; descFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            TextMeshProUGUI cardDescText = CreateText(descPanel.transform, "CardDescText", "Desc...", 12, TextAlignmentOptions.Center);
            cardDescText.enableWordWrapping = true;
            SetLayoutElement(cardDescText.gameObject, minHeight: 30); 
            // --- End Card Hierarchy --- 

            PrefabUtility.SaveAsPrefabAsset(cardRoot, fullPrefabPath);
            resultPath = fullPrefabPath;
            Debug.Log($"Successfully {(replacing ? "replaced" : "created")} Card Template prefab at: {fullPrefabPath}");
        }
        catch (System.Exception e) { Debug.LogError($"Failed to create Card Template prefab: {e.Message}\n{e.StackTrace}"); }
        finally { 
            if (cardRoot != null) DestroyImmediate(cardRoot);
            AssetDatabase.Refresh();
        }
        // Assign to GameManager if the field exists
        if (!string.IsNullOrEmpty(resultPath))
            AssignPrefabToGameManager("cardPrefab", resultPath);
            
        return resultPath;
    }

    [MenuItem("Tools/Setup/UI/Create ALL UI Prefabs", priority = 50)] 
    public static void CreateAllUIScreens()
    {
        Debug.Log("Creating all UI screen prefabs...");
        CreateStartScreen();
        CreateLobbyScreen();
        CreateCombatScreen();
        CreateDraftScreen();
        CreateCardTemplatePrefab(); // Also create the card template
        Debug.Log("Finished creating all UI screen prefabs.");
    }

    // --- Element Population --- 

    private static void AddStartScreenElements(GameObject canvasRoot)
    {
        TextMeshProUGUI title = CreateText(canvasRoot.transform, "TitleText", "My Awesome Game", 48, TextAlignmentOptions.Center, new Vector2(0, 150), new Vector2(600, 100));
        ConfigureRectTransform(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(600, 100));
        Button connectBtn = CreateButton(canvasRoot.transform, "ConnectButton", "Connect");
        ConfigureRectTransform(connectBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(200, 50));
    }

    private static void AddLobbyScreenElements(GameObject canvasRoot)
    {
        TextMeshProUGUI lobbyTitle = CreateText(canvasRoot.transform, "LobbyTitleText", "Lobby", 36, TextAlignmentOptions.Center);
        ConfigureRectTransform(lobbyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(400, 60));

        GameObject playerListPanel = CreatePanel(canvasRoot.transform, "PlayerListPanel");
        ConfigureRectTransform(playerListPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(400, 300));
        VerticalLayoutGroup playerListLayout = AddVerticalLayoutGroup(playerListPanel, DefaultPadding, DefaultSpacing);
        playerListLayout.childControlHeight = false;
        playerListLayout.childControlWidth = true;
        playerListLayout.childForceExpandHeight = false;
        playerListLayout.childForceExpandWidth = true;

        TextMeshProUGUI playerEntryTemplate = CreateText(playerListPanel.transform, "PlayerEntryTemplate", "Player Name (Ready)", 18, TextAlignmentOptions.Left);
        playerEntryTemplate.gameObject.SetActive(false);

        Button readyBtn = CreateButton(canvasRoot.transform, "ReadyButton", "Ready Up");
        ConfigureRectTransform(readyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110, 60), new Vector2(160, 40));

        Button startBtn = CreateButton(canvasRoot.transform, "StartGameButton", "Start Game");
        ConfigureRectTransform(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110, 60), new Vector2(160, 40));

        Button leaveBtn = CreateButton(canvasRoot.transform, "LeaveButton", "Leave Room");
        ConfigureRectTransform(leaveBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(160, 40));
    }

    private static void AddCombatScreenElements(GameObject canvasRoot)
    {
        GameObject topArea = CreatePanel(canvasRoot.transform, "TopArea", true);
        ConfigureRectTransform(topArea.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0, -200));

        TextMeshProUGUI scoreText = CreateText(topArea.transform, "ScoreText", "Score: P1: 0 / P2: 0", 24, TextAlignmentOptions.TopRight);
        ConfigureRectTransform(scoreText.rectTransform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-DefaultPadding, -DefaultPadding), new Vector2(300, 40));

        // <<--- MODIFIED STRUCTURE START --->>
        // 1. Create the Container for the Opponent Pet area
        GameObject opponentPetAreaContainer = CreatePanel(topArea.transform, "OpponentPetAreaContainer", true); // Container is transparent
        ConfigureRectTransform(opponentPetAreaContainer.GetComponent<RectTransform>(),
             new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
             new Vector2(0, -DefaultPadding), new Vector2(300, 150)); // Position/size the container

        // 2. Create the Drop Zone as a child of the Container, stretched to fill
        GameObject opponentDropZoneGO = new GameObject("OpponentPetDropZone");
        opponentDropZoneGO.transform.SetParent(opponentPetAreaContainer.transform, false);
        Image oppDropImage = opponentDropZoneGO.AddComponent<Image>();
        oppDropImage.color = Color.clear; 
        oppDropImage.raycastTarget = true; 
        RectTransform oppDropRect = opponentDropZoneGO.GetComponent<RectTransform>();
        ConfigureRectTransform(oppDropRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill container
        CardDropZone oppDropZone = opponentDropZoneGO.AddComponent<CardDropZone>();
        oppDropZone.targetType = CardDropZone.TargetType.EnemyPet;

        // <<--- ADDED HIGHLIGHT GRAPHIC START --->>
        GameObject oppHighlightGO = new GameObject("HighlightGraphic");
        oppHighlightGO.transform.SetParent(opponentDropZoneGO.transform, false);
        Image oppHighlightImage = oppHighlightGO.AddComponent<Image>();
        oppHighlightImage.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
        oppHighlightImage.raycastTarget = false; // Highlight shouldn't block raycasts
        RectTransform oppHighlightRect = oppHighlightGO.GetComponent<RectTransform>();
        ConfigureRectTransform(oppHighlightRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill drop zone
        oppHighlightGO.SetActive(false); // Start disabled
        // Assign the graphic to the drop zone script field
        oppDropZone.highlightGraphic = oppHighlightImage;
        // <<--- ADDED HIGHLIGHT GRAPHIC END --->>

        opponentDropZoneGO.transform.SetAsFirstSibling(); // Render behind the actual pet UI panel

        // 3. Create the Panel for UI elements (Text, Slider) as a child of the Container, stretched to fill
        GameObject opponentPetArea = CreatePanel(opponentPetAreaContainer.transform, "OpponentPetArea", true); // Panel itself can be transparent
        RectTransform opponentPetAreaRect = opponentPetArea.GetComponent<RectTransform>();
        ConfigureRectTransform(opponentPetAreaRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill container
        // Add layout group and UI elements to *this* panel
        VerticalLayoutGroup oppLayout = AddVerticalLayoutGroup(opponentPetArea, DefaultPadding, DefaultSpacing);
        oppLayout.childAlignment = TextAnchor.UpperCenter;
        // <<--- ADDED RAYCAST TARGET DISABLE START --->>
        // Ensure the panel itself doesn't block raycasts if it has an image
        Image oppAreaImage = opponentPetArea.GetComponent<Image>();
        if (oppAreaImage != null) oppAreaImage.raycastTarget = false;
        // <<--- ADDED RAYCAST TARGET DISABLE END --->>
        // <<--- MODIFIED STRUCTURE END --->>

        // Add UI elements as children of opponentPetArea (the panel with the VLG)
        TextMeshProUGUI oppNameText = CreateText(opponentPetArea.transform, "OpponentPetNameText", "Opponent Pet", 20, TextAlignmentOptions.Center);
        oppNameText.raycastTarget = false; // <<--- DISABLE RAYCAST
        Slider oppHealthSlider = CreateSlider(opponentPetArea.transform, "OpponentPetHealthSlider", new Vector2(200, 20));
        // Disable raycasts on slider sub-elements if necessary (Background, Fill, Handle Images)
        SetSliderRaycastTargets(oppHealthSlider, false); // <<--- DISABLE RAYCAST ON SLIDER PARTS
        TextMeshProUGUI oppHealthText = CreateText(opponentPetArea.transform, "OpponentPetHealthText", "50 / 50", 16, TextAlignmentOptions.Center);
        oppHealthText.raycastTarget = false; // <<--- DISABLE RAYCAST
        TextMeshProUGUI oppIntentText = CreateText(opponentPetArea.transform, "OpponentPetIntentText", "Intent: Attack 10", 16, TextAlignmentOptions.Center);
        oppIntentText.raycastTarget = false; // <<--- DISABLE RAYCAST

        // <<--- MODIFIED STRUCTURE START --->>
        // 1. Create the Container for the Own Pet area
        GameObject ownPetAreaContainer = CreatePanel(topArea.transform, "OwnPetAreaContainer", true);
        ConfigureRectTransform(ownPetAreaContainer.GetComponent<RectTransform>(),
             new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
             new Vector2(DefaultPadding, -DefaultPadding), new Vector2(200, 100)); // Position/size the container
        
        // 2. Create the Drop Zone as a child of the Container, stretched to fill
        GameObject ownPetDropZoneGO = new GameObject("OwnPetDropZone");
        ownPetDropZoneGO.transform.SetParent(ownPetAreaContainer.transform, false);
        Image ownDropImage = ownPetDropZoneGO.AddComponent<Image>();
        ownDropImage.color = Color.clear; 
        ownDropImage.raycastTarget = true; 
        RectTransform ownDropRect = ownPetDropZoneGO.GetComponent<RectTransform>();
        ConfigureRectTransform(ownDropRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill container
        CardDropZone ownDropZone = ownPetDropZoneGO.AddComponent<CardDropZone>();
        ownDropZone.targetType = CardDropZone.TargetType.OwnPet;
        
        // <<--- ADDED HIGHLIGHT GRAPHIC START --->>
        GameObject ownHighlightGO = new GameObject("HighlightGraphic");
        ownHighlightGO.transform.SetParent(ownPetDropZoneGO.transform, false);
        Image ownHighlightImage = ownHighlightGO.AddComponent<Image>();
        ownHighlightImage.color = new Color(0f, 0.8f, 1f, 0.3f); // Semi-transparent cyan
        ownHighlightImage.raycastTarget = false; // Highlight shouldn't block raycasts
        RectTransform ownHighlightRect = ownHighlightGO.GetComponent<RectTransform>();
        ConfigureRectTransform(ownHighlightRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill drop zone
        ownHighlightGO.SetActive(false); // Start disabled
        // Assign the graphic to the drop zone script field
        ownDropZone.highlightGraphic = ownHighlightImage;
        // <<--- ADDED HIGHLIGHT GRAPHIC END --->>
        
        ownPetDropZoneGO.transform.SetAsFirstSibling(); // Render behind the actual pet UI panel

        // 3. Create the Panel for UI elements (Text, Slider) as a child of the Container, stretched to fill
        GameObject ownPetArea = CreatePanel(ownPetAreaContainer.transform, "OwnPetArea", true);
        RectTransform ownPetAreaRect = ownPetArea.GetComponent<RectTransform>();
        ConfigureRectTransform(ownPetAreaRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch to fill container
        // Add layout group and UI elements to *this* panel
        VerticalLayoutGroup ownPetLayout = AddVerticalLayoutGroup(ownPetArea, DefaultPadding, DefaultSpacing);
        ownPetLayout.childAlignment = TextAnchor.UpperCenter;
        // <<--- ADDED RAYCAST TARGET DISABLE START --->>
        // Ensure the panel itself doesn't block raycasts if it has an image
        Image ownAreaImage = ownPetArea.GetComponent<Image>();
        if (ownAreaImage != null) ownAreaImage.raycastTarget = false;
        // <<--- ADDED RAYCAST TARGET DISABLE END --->>
        // <<--- MODIFIED STRUCTURE END --->>

        // Add UI elements as children of ownPetArea (the panel with the VLG)
        TextMeshProUGUI ownNameText = CreateText(ownPetArea.transform, "OwnPetNameText", "Your Pet", 18, TextAlignmentOptions.Center);
        ownNameText.raycastTarget = false; // <<--- DISABLE RAYCAST
        Slider ownPetHealthSlider = CreateSlider(ownPetArea.transform, "OwnPetHealthSlider", new Vector2(150, 15));
        SetSliderRaycastTargets(ownPetHealthSlider, false); // <<--- DISABLE RAYCAST ON SLIDER PARTS
        TextMeshProUGUI ownPetHealthText = CreateText(ownPetArea.transform, "OwnPetHealthText", "50 / 50", 14, TextAlignmentOptions.Center);
        ownPetHealthText.raycastTarget = false; // <<--- DISABLE RAYCAST

        GameObject othersStatusArea = CreatePanel(topArea.transform, "OthersStatusArea");
        ConfigureRectTransform(othersStatusArea.GetComponent<RectTransform>(),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-DefaultPadding, -DefaultPadding - 40 - DefaultSpacing), new Vector2(200, 150));
        VerticalLayoutGroup othersLayout = AddVerticalLayoutGroup(othersStatusArea, DefaultPadding, DefaultSpacing);
        othersLayout.childAlignment = TextAnchor.UpperLeft;
        CreateText(othersStatusArea.transform, "OtherStatusTitle", "Other Fights", 16, TextAlignmentOptions.Left);
        TextMeshProUGUI otherPlayerStatusTemplate = CreateText(othersStatusArea.transform, "OtherPlayerStatusTemplate", "P#: PetHP", 14, TextAlignmentOptions.Left);
        otherPlayerStatusTemplate.gameObject.SetActive(false);

        GameObject playerArea = CreatePanel(canvasRoot.transform, "PlayerArea", true);
        ConfigureRectTransform(playerArea.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0, 350)); // <<--- Increased Height to 350
        
        // Vertical layout for Player Area
        VerticalLayoutGroup playerAreaLayout = AddVerticalLayoutGroup(playerArea, DefaultPadding, DefaultSpacing, controlChildHeight: false);
        playerAreaLayout.childForceExpandHeight = false; // Don't force expand height
        playerAreaLayout.childControlWidth = true; // Control width to fill
        playerAreaLayout.childForceExpandWidth = true; // Force expand width

        // --- Create Player Stats Row --- 
        GameObject statsRow = CreatePanel(playerArea.transform, "StatsRow", true);
        // ConfigureRectTransform(statsRow.GetComponent<RectTransform>(), // REMOVE MANUAL POS
        //     new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
        //     new Vector2(0, -DefaultPadding), new Vector2(0, 40));
        SetLayoutElement(statsRow, preferredHeight: 40, flexibleHeight: 0); // <<--- ADD LAYOUT ELEMENT
        HorizontalLayoutGroup statsLayout = AddHorizontalLayoutGroup(statsRow, DefaultPadding, DefaultSpacing);
        statsLayout.childAlignment = TextAnchor.MiddleLeft;
        
        TextMeshProUGUI playerNameText = CreateText(statsRow.transform, "PlayerNameText", "Player Name", 20, TextAlignmentOptions.Left);
        SetLayoutElement(playerNameText.gameObject, flexibleWidth: 1);

        Slider playerHealthSlider = CreateSlider(statsRow.transform, "PlayerHealthSlider", new Vector2(150, 20));
        
        TextMeshProUGUI playerHealthText = CreateText(statsRow.transform, "PlayerHealthText", "100 / 100", 16, TextAlignmentOptions.Right);
        SetLayoutElement(playerHealthText.gameObject, minWidth: 80);

        // TODO: Add Energy Display
        TextMeshProUGUI energyTextElement = CreateText(statsRow.transform, "EnergyText", "Energy: 3/3", 18, TextAlignmentOptions.Left);
        energyTextElement.raycastTarget = false;
        SetLayoutElement(energyTextElement.gameObject, minWidth: 100); // <<--- ADD LAYOUT ELEMENT
        
        // --- Create Player Hand Panel --- 
        GameObject playerHandPanel = CreatePanel(playerArea.transform, "PlayerHandPanel");
        // ConfigureRectTransform(playerHandPanel.GetComponent<RectTransform>(), // REMOVE MANUAL POS/SIZE
        //     new Vector2(0f, 0f), new Vector2(1f, 1f),
        //     new Vector2(0.5f, 0.5f),
        //     new Vector2(0, 0), // Position relative to playerArea center
        //     new Vector2(-DefaultPadding*2, -100)); // Size, adjusted for padding and bottom bar
        SetLayoutElement(playerHandPanel, flexibleHeight: 1); // <<--- ADD LAYOUT ELEMENT (Flexible Height)
        HorizontalLayoutGroup handLayout = AddHorizontalLayoutGroup(playerHandPanel, DefaultPadding, DefaultSpacing, controlChildWidth: false); // Let cards control own width
        handLayout.childAlignment = TextAnchor.MiddleCenter;
        // No CardTemplate needs to be added here, it will be instantiated by GameManager

        // --- Create Bottom Bar --- 
        GameObject bottomBar = CreatePanel(playerArea.transform, "BottomBar", true);
        // ConfigureRectTransform(bottomBar.GetComponent<RectTransform>(), // REMOVE MANUAL POS
        //     new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
        //     Vector2.zero, new Vector2(0, 50));
        SetLayoutElement(bottomBar, preferredHeight: 50, flexibleHeight: 0); // Fixed height for bottom bar
        HorizontalLayoutGroup bottomLayout = AddHorizontalLayoutGroup(bottomBar, DefaultPadding, DefaultSpacing);
        bottomLayout.childAlignment = TextAnchor.MiddleRight;

        TextMeshProUGUI deckCountText = CreateText(bottomBar.transform, "DeckCountText", "Deck: 0", 18, TextAlignmentOptions.Left);
        // SetLayoutElement(deckCountText.gameObject, flexibleWidth: 1); // REMOVE flexible width
        SetLayoutElement(deckCountText.gameObject, minWidth: 100); // <<--- ADD minWidth

        TextMeshProUGUI discardCountText = CreateText(bottomBar.transform, "DiscardCountText", "Discard: 0", 18, TextAlignmentOptions.Left);
        // SetLayoutElement(discardCountText.gameObject, flexibleWidth: 1); // REMOVE flexible width
        SetLayoutElement(discardCountText.gameObject, minWidth: 100); // <<--- ADD minWidth

        // <<--- ADD SPACER START --->>
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(bottomBar.transform, false);
        SetLayoutElement(spacer, flexibleWidth: 1); // This pushes subsequent elements right
        // <<--- ADD SPACER END --->>

        Button endTurnButton = CreateButton(bottomBar.transform, "EndTurnButton", "End Turn");
        SetLayoutElement(endTurnButton.gameObject, minWidth: 160); // Ensure button has min width

        // << --- ADDED PLAYER DROP ZONE CREATION START (If needed) --->>
        // Example: If players can target themselves directly with cards
        /*
        GameObject playerDropZoneGO = new GameObject("PlayerSelfDropZone");
        playerDropZoneGO.transform.SetParent(statsRow.transform, false); // Attach to stats row or player area
        Image playerDropImage = playerDropZoneGO.AddComponent<Image>();
        playerDropImage.color = Color.clear;
        playerDropImage.raycastTarget = true;
        RectTransform playerDropRect = playerDropZoneGO.GetComponent<RectTransform>();
        ConfigureRectTransform(playerDropRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); // Stretch
        CardDropZone playerDropZone = playerDropZoneGO.AddComponent<CardDropZone>();
        playerDropZone.targetType = CardDropZone.TargetType.PlayerSelf; 
        playerDropZoneGO.transform.SetAsFirstSibling();
        */
        // << --- ADDED PLAYER DROP ZONE CREATION END --->>
    }

    private static void AddDraftScreenElements(GameObject canvasRoot)
    {
        CreateText(canvasRoot.transform, "DraftTitleText", "Choose Your Upgrade", 36, TextAlignmentOptions.Top, new Vector2(0, -30));
        
        GameObject optionsPanel = CreatePanel(canvasRoot.transform, "DraftOptionsPanel", new Vector2(0, 0), new Vector2(600, 400));
        GridLayoutGroup gridLayout = optionsPanel.AddComponent<GridLayoutGroup>();
        gridLayout.padding = new RectOffset(20, 20, 20, 20);
        gridLayout.cellSize = new Vector2(150, 100);
        gridLayout.spacing = new Vector2(15, 15);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        GameObject optionTemplate = CreateButton(optionsPanel.transform, "OptionTemplate", "", Vector2.zero).gameObject;
        RectTransform optionRect = optionTemplate.GetComponent<RectTransform>();
        optionRect.anchorMin = Vector2.zero;
        optionRect.anchorMax = Vector2.one;
        optionRect.sizeDelta = Vector2.zero;
        optionRect.anchoredPosition = Vector2.zero;
        CreateText(optionTemplate.transform, "OptionText", "Option Description Here (e.g., +5 Max HP)", 14, TextAlignmentOptions.Center);
        optionTemplate.SetActive(false);
        CreateText(canvasRoot.transform, "TimerText", "Time Left: 30s", 20, TextAlignmentOptions.Bottom, new Vector2(0, 20));
    }

    // --- Helper Functions --- 

    // Modified to return the prefab path on success
    private static string CreateCanvasPrefab(string prefabName, System.Action<GameObject> populateAction)
    {
        if (!Directory.Exists(UIPrefabSavePath))
        {
            Directory.CreateDirectory(UIPrefabSavePath);
            AssetDatabase.Refresh();
            Debug.Log($"Created directory: {UIPrefabSavePath}");
        }

        string fullPrefabPath = Path.Combine(UIPrefabSavePath, prefabName + ".prefab");
        bool replacing = false;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null)
        {
            bool deleted = AssetDatabase.DeleteAsset(fullPrefabPath);
            if (deleted)
            {
                Debug.Log($"Deleted existing UI prefab at {fullPrefabPath} to replace it.");
                AssetDatabase.Refresh(); 
                replacing = true;
            }
            else
            {
                Debug.LogError($"Failed to delete existing UI prefab at {fullPrefabPath}. Skipping creation.");
                return null; // Return null on failure
            }
        }

        GameObject canvasGO = new GameObject(prefabName);
        string resultPath = null; // Path to return
        try
        {
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; 
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); 
            scaler.matchWidthOrHeight = 0.5f;
            
            populateAction?.Invoke(canvasGO);
            
            PrefabUtility.SaveAsPrefabAsset(canvasGO, fullPrefabPath);
            resultPath = fullPrefabPath; // Set path only on successful save
            Debug.Log($"Successfully {(replacing ? "replaced" : "created")} UI prefab at: {fullPrefabPath}");
        }
        catch (System.Exception e) { Debug.LogError($"Failed to create UI prefab {prefabName}: {e.Message}"); }
        finally { 
            if (canvasGO != null) DestroyImmediate(canvasGO);
            AssetDatabase.Refresh();
        }
        return resultPath; // Return the path or null
    }

    // --- NEW HELPER --- 
    private static void AssignPrefabToGameManager(string fieldName, string prefabPath)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"AssignPrefab: Failed to load prefab asset at path: {prefabPath}");
            return;
        }

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning($"AssignPrefab: Could not find GameManager in the current scene to assign '{fieldName}'. Prefab created but not assigned.");
            return;
        }

        SerializedObject so = new SerializedObject(gameManager);
        SerializedProperty property = so.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"AssignPrefab: Could not find SerializedProperty '{fieldName}' on GameManager. Make sure the field exists and is [SerializeField].");
            return;
        }

        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
             Debug.LogError($"AssignPrefab: Field '{fieldName}' on GameManager is not an Object Reference type.");
            return;
        }

        property.objectReferenceValue = prefabAsset;
        so.ApplyModifiedProperties(); // Apply the change

        // Optional: Mark scene as dirty so the change is saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);

        Debug.Log($"Successfully assigned '{prefabAsset.name}' to GameManager field '{fieldName}'.");
    }

    // --- UI Element Creation Helpers ---

    private static void ConfigureRectTransform(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static GameObject CreatePanel(Transform parent, string name, bool transparent = false)
    {
        GameObject panelGO = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(parent, false);
        Image image = panelGO.GetComponent<Image>();
        image.color = transparent ? Color.clear : new Color(0.1f, 0.1f, 0.1f, 0.7f); // Default dark panel or clear
        ConfigureRectTransform(panelGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return panelGO;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject panelGO = CreatePanel(parent, name, false);
        ConfigureRectTransform(panelGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
        return panelGO;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textGO = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(parent, false);
        TextMeshProUGUI tmpText = textGO.GetComponent<TextMeshProUGUI>();
        tmpText.text = content;
        tmpText.fontSize = fontSize;
        tmpText.alignment = alignment;
        tmpText.color = Color.white;

        ContentSizeFitter fitter = textGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ConfigureRectTransform(textGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 50));

        return tmpText;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, TextAlignmentOptions alignment, Vector2? anchoredPosition = null, Vector2? sizeDelta = null)
    {
        TextMeshProUGUI tmpText = CreateText(parent, name, content, fontSize, alignment);
        if (anchoredPosition.HasValue || sizeDelta.HasValue)
        {
             DestroyImmediate(tmpText.GetComponent<ContentSizeFitter>());
             ConfigureRectTransform(tmpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition ?? Vector2.zero, sizeDelta ?? new Vector2(200,50));
        }
        return tmpText;
    }

    private static Button CreateButton(Transform parent, string name, string buttonText)
    {
        GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        Image image = buttonGO.GetComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.8f, 1f); // Basic blue button
        Button button = buttonGO.GetComponent<Button>();

        TextMeshProUGUI btnText = CreateText(buttonGO.transform, "Text (TMP)", buttonText, 18, TextAlignmentOptions.Center);
        ConfigureRectTransform(btnText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10, -5));
        
        ConfigureRectTransform(buttonGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 30));

        return button;
    }

    private static Button CreateButton(Transform parent, string name, string buttonText, Vector2 anchoredPosition)
    {
        Button button = CreateButton(parent, name, buttonText);
        ConfigureRectTransform(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(160,30));
        return button;
    }

    private static VerticalLayoutGroup AddVerticalLayoutGroup(GameObject target, int padding, int spacing, bool controlChildWidth = true, bool controlChildHeight = true)
    {
        VerticalLayoutGroup layoutGroup = target.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(padding, padding, padding, padding);
        layoutGroup.spacing = spacing;
        layoutGroup.childControlWidth = controlChildWidth;
        layoutGroup.childControlHeight = controlChildHeight;
        layoutGroup.childForceExpandWidth = controlChildWidth;
        layoutGroup.childForceExpandHeight = controlChildHeight;
        ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return layoutGroup;
    }

    private static HorizontalLayoutGroup AddHorizontalLayoutGroup(GameObject target, int padding, int spacing, bool controlChildWidth = true, bool controlChildHeight = true)
    {
        HorizontalLayoutGroup layoutGroup = target.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.padding = new RectOffset(padding, padding, padding, padding);
        layoutGroup.spacing = spacing;
        layoutGroup.childControlWidth = controlChildWidth;
        layoutGroup.childControlHeight = controlChildHeight;
        layoutGroup.childForceExpandWidth = controlChildWidth;
        layoutGroup.childForceExpandHeight = controlChildHeight;
        ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return layoutGroup;
    }

    private static LayoutElement SetLayoutElement(GameObject target, float minWidth = -1, float minHeight = -1, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = target.AddComponent<LayoutElement>();
        if (minWidth != -1) layoutElement.minWidth = minWidth;
        if (minHeight != -1) layoutElement.minHeight = minHeight;
        if (preferredWidth != -1) layoutElement.preferredWidth = preferredWidth;
        if (preferredHeight != -1) layoutElement.preferredHeight = preferredHeight;
        if (flexibleWidth != -1) layoutElement.flexibleWidth = flexibleWidth;
        if (flexibleHeight != -1) layoutElement.flexibleHeight = flexibleHeight;
        return layoutElement;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 sizeDelta)
    {
        GameObject sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.sizeDelta = sizeDelta;
        ConfigureRectTransform(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);

        GameObject backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundGO.transform.SetParent(sliderRect, false);
        RectTransform bgRect = backgroundGO.GetComponent<RectTransform>();
        ConfigureRectTransform(bgRect, new Vector2(0, 0.25f), new Vector2(1, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        backgroundGO.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 1f);

        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderRect, false);
        RectTransform fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
        ConfigureRectTransform(fillAreaRect, new Vector2(0, 0.25f), new Vector2(1, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0,0), new Vector2(-10, 0));
        
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        ConfigureRectTransform(fillGO.GetComponent<RectTransform>(), new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0,0));
        fillGO.GetComponent<Image>().color = Color.green;

        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderRect, false);
        RectTransform handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
        ConfigureRectTransform(handleAreaRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20, 0));

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        handleGO.GetComponent<Image>().color = Color.white;
        ConfigureRectTransform(handleGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));

        Slider slider = sliderGO.GetComponent<Slider>();
        slider.fillRect = fillGO.GetComponent<RectTransform>();
        slider.handleRect = handleGO.GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 0.5f;

        return slider;
    }
    
     private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
     {
        Slider slider = CreateSlider(parent, name, sizeDelta);
        ConfigureRectTransform(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
        return slider;
     }

    // <<--- NEW HELPER FUNCTION START --->>
    private static void SetSliderRaycastTargets(Slider slider, bool enabled)
    {
        if (slider == null) return;
        Image bgImage = slider.transform.Find("Background")?.GetComponent<Image>();
        if (bgImage != null) bgImage.raycastTarget = enabled;
        Image fillImage = slider.fillRect?.GetComponent<Image>(); // Fill Rect might not be direct child
        if (fillImage != null) fillImage.raycastTarget = enabled;
        Image handleImage = slider.handleRect?.GetComponent<Image>(); // Handle Rect might not be direct child
        if (handleImage != null) handleImage.raycastTarget = enabled;
    }
    // <<--- NEW HELPER FUNCTION END --->>
} 