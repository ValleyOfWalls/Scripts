using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private const string GameVersion = "1.0";
    private const int MaxPlayersPerRoom = 4;

    private UIManager uiManager;
    private PrefabSpawner prefabSpawner;
    
    // Flag to check if connection is in progress
    private bool isConnecting = false;

    // Fields for deferred room creation
    private bool _shouldCreateRoomAfterConnect = false;
    private string _pendingRoomName;
    private string _pendingPlayerName;
    private RoomOptions _pendingRoomOptions;

    private void Start()
    {
        // Make sure we have references
        EnsureReferences();
        Debug.Log("NetworkManager initialized");
    }
    
    private void EnsureReferences()
    {
        // Find manager references
        if (uiManager == null && GameManager.Instance != null)
        {
            uiManager = GameManager.Instance.GetUIManager();
        }
        
        // Initialize prefab spawner
        if (prefabSpawner == null)
        {
            prefabSpawner = gameObject.AddComponent<PrefabSpawner>();
        }
    }

    public void Connect()
    {
        // Only proceed if not already connecting or connected
        if (isConnecting || PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsConnected)
            {
                // Already connected, go to lobby
                Debug.Log("Already connected to Photon, going to lobby");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetState(GameManager.GameState.Lobby);
                }
            }
            return;
        }
        
        // Make sure references are initialized
        EnsureReferences();

        // Connect to Photon servers
        isConnecting = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
        
        Debug.Log("Attempting to connect to Photon servers...");
        
        try
        {
            PhotonNetwork.ConnectUsingSettings();
            if (uiManager != null)
            {
                uiManager.ShowConnectingUI("Connecting to server...");
            }
            else
            {
                Debug.LogWarning("UIManager is null when connecting");
            }
        }
        catch (System.Exception e)
        {
            isConnecting = false;
            Debug.LogError("Error connecting to Photon: " + e.Message);
            if (uiManager != null)
            {
                uiManager.ShowErrorUI("Connection Error: " + e.Message);
            }
        }
    }

    public void CreateRoom(string roomName, string playerName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room" + Random.Range(1000, 9999); // Random room name
        }

        // Set player nickname - Do this regardless of connection state
        PhotonNetwork.NickName = playerName;

        // Create room options
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = MaxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true,
            // Enable player list synchronization
            PublishUserId = true
        };

        // Check if connected and ready
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Not connected to Master Server yet. Will create room after connecting.");
            _shouldCreateRoomAfterConnect = true;
            _pendingRoomName = roomName;
            _pendingPlayerName = playerName; // NickName is already set, but store for clarity/logging if needed
            _pendingRoomOptions = roomOptions;

            // If not even connected initially, start the connection process
            if (!PhotonNetwork.IsConnected && !isConnecting)
            {
                Connect(); // Assuming Connect handles the UI updates
            }
            // If connecting or connected but not ready, show appropriate UI
            else if (uiManager != null)
            {
                 uiManager.ShowConnectingUI("Connecting to Master...");
            }
            return; // Wait for OnConnectedToMaster callback
        }


        // Create the room directly if already connected and ready
        Debug.Log($"Creating room: {roomName}");
        PhotonNetwork.CreateRoom(roomName, roomOptions);
        
        if (uiManager != null)
        {
            uiManager.ShowConnectingUI("Creating room...");
        }
    }

    public void JoinRoom(string roomName, string playerName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room" + Random.Range(1000, 9999); // Random room name
        }

        // Set player nickname
        PhotonNetwork.NickName = playerName;

        // Join the room
        Debug.Log($"Joining room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
        
        if (uiManager != null)
        {
            uiManager.ShowConnectingUI("Joining room...");
        }
    }

    public void LeaveRoom()
    {
        Debug.Log("Leaving room");
        PhotonNetwork.LeaveRoom();
        
        if (uiManager != null)
        {
            uiManager.ShowConnectingUI("Leaving room...");
        }
    }

    public void SetPlayerReady(bool isReady)
    {
        Debug.Log($"Setting player ready status: {isReady}");
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { "IsReady", isReady }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }

    // Photon Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        isConnecting = false;
        
        // Check if we need to create a room
        if (_shouldCreateRoomAfterConnect)
        {
            Debug.Log($"Connected to Master. Proceeding to create room: {_pendingRoomName}");
            PhotonNetwork.CreateRoom(_pendingRoomName, _pendingRoomOptions);
            _shouldCreateRoomAfterConnect = false; // Reset flag

            if (uiManager != null)
            {
                 uiManager.ShowConnectingUI("Creating room..."); // Update UI
            }
        }
        else
        {
            // Original behavior: Go to main menu if not creating a room
            if (GameManager.Instance != null)
            {
                 // Check current state? Or always go to main menu? Let's assume main menu for now.
                 // If the user was trying to JOIN, we'd need different logic here.
                 // For now, creating a room implies starting from the main menu flow.
                GameManager.Instance.SetState(GameManager.GameState.MainMenu);
                if (uiManager != null)
                {
                    uiManager.ShowMainMenuUI(); // Ensure correct UI is shown
                }
            }
            else
            {
                Debug.LogError("GameManager.Instance is null in OnConnectedToMaster");
            }
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from server: {cause}");
        isConnecting = false;
        
        if (uiManager != null)
        {
            uiManager.ShowErrorUI($"Disconnected: {cause}");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        
        // Call base implementation to ensure GameManager creates GameplayManager
        base.OnJoinedRoom();
        
        // Add all players including self to GameManager's player list
        if (GameManager.Instance != null)
        {
            // First clear existing players to avoid duplicates
            GameManager.Instance.ClearPlayers();
            
            // Add self
            GameManager.Instance.AddPlayer(PhotonNetwork.LocalPlayer.UserId, PhotonNetwork.NickName);
            
            // Add all other players in the room
            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (player.UserId != PhotonNetwork.LocalPlayer.UserId)
                {
                    GameManager.Instance.AddPlayer(player.UserId, player.NickName);
                    
                    // Get and set ready status if it exists
                    if (player.CustomProperties.ContainsKey("IsReady"))
                    {
                        bool isReady = (bool)player.CustomProperties["IsReady"];
                        GameManager.Instance.SetPlayerReady(player.UserId, isReady);
                    }
                }
            }
            
            // Change state to Lobby since we're now in a room
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
        }
        
        // Spawn player and monster prefabs if prefab spawner exists
        if (prefabSpawner == null)
        {
            prefabSpawner = gameObject.AddComponent<PrefabSpawner>();
        }
        
        StartCoroutine(DelayedSpawn());
    }
    
    private IEnumerator DelayedSpawn()
    {
        // Wait a short time to ensure everything is properly initialized
        yield return new WaitForSeconds(0.5f);
        
        // If we're the master client, ensure GameplayManager is instantiated before spawning prefabs
        if (PhotonNetwork.IsMasterClient && GameManager.Instance != null && GameManager.Instance.GetGameplayManager() == null)
        {
            GameManager.Instance.InstantiateNetworkedGameplayManager();
            
            // Wait for GameplayManager to be instantiated
            yield return new WaitForSeconds(0.5f);
        }
        // If not master client, wait a bit longer to make sure we receive the GameplayManager
        else if (!PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(1.0f);
        }
        
        // Now spawn player and monster prefabs
        prefabSpawner.SpawnPlayerAndMonster();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearPlayers();
            GameManager.Instance.SetState(GameManager.GameState.MainMenu);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player entered room: {newPlayer.NickName}");
        
        // Add new player to GameManager's player list
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddPlayer(newPlayer.UserId, newPlayer.NickName);
        }
        
        // Update lobby UI
        if (uiManager != null)
        {
            uiManager.UpdateLobbyUI();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left room: {otherPlayer.NickName}");
        
        // Remove player from GameManager's player list
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RemovePlayer(otherPlayer.UserId);
        }
        
        // Update lobby UI
        if (uiManager != null)
        {
            uiManager.UpdateLobbyUI();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            bool isReady = (bool)changedProps["IsReady"];
            Debug.Log($"Player {targetPlayer.NickName} ready status updated: {isReady}");
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerReady(targetPlayer.UserId, isReady);
                
                // Check if all players are ready and we're the master client
                if (PhotonNetwork.IsMasterClient && GameManager.Instance.AreAllPlayersReady())
                {
                    GameManager.Instance.StartGame();
                }
            }
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: {message} (Code: {returnCode})");
        isConnecting = false; // Should reset connecting flags here too potentially
        _shouldCreateRoomAfterConnect = false; // Reset flag on failure

        if (uiManager != null)
        {
            uiManager.ShowErrorUI($"Failed to create room: {message}");
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Room join failed: {message}");
        
        if (uiManager != null)
        {
            uiManager.ShowErrorUI($"Room join failed: {message}");
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.MainMenu);
        }
    }
    
    // Method to check if already connected
    public bool IsConnected()
    {
        return PhotonNetwork.IsConnected;
    }
}