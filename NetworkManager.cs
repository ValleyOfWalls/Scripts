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
    private LobbyManager lobbyManager;

    private void Start()
    {
        // Find manager references
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null && GameManager.Instance != null)
            {
                uiManager = GameManager.Instance.GetUIManager();
            }
        }
        
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager == null && GameManager.Instance != null)
            {
                lobbyManager = GameManager.Instance.GetLobbyManager();
            }
        }
        
        Debug.Log("NetworkManager initialized");
    }

    public void Connect()
    {
        // Make sure uiManager is initialized
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null && GameManager.Instance != null)
            {
                uiManager = GameManager.Instance.GetUIManager();
            }
        }

        // Connect to Photon servers if not already connected
        if (!PhotonNetwork.IsConnected)
        {
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
                Debug.LogError("Error connecting to Photon: " + e.Message);
                if (uiManager != null)
                {
                    uiManager.ShowErrorUI("Connection Error: " + e.Message);
                }
            }
        }
        else
        {
            // Already connected, go to lobby
            Debug.Log("Already connected to Photon, going to lobby");
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
        }
    }

    public void CreateRoom(string roomName, string playerName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "asd"; // Default room name
        }

        // Set player nickname
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

        // Create the room
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
            roomName = "asd"; // Default room name
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
        
        // Make sure GameManager exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
        }
        else
        {
            Debug.LogError("GameManager.Instance is null in OnConnectedToMaster");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from server: {cause}");
        
        if (uiManager != null)
        {
            uiManager.ShowErrorUI($"Disconnected: {cause}");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        
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
        }
        
        // IMPORTANT: Set the game state to Lobby which will trigger initialization
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
        }
        else
        {
            // Fallback if GameManager is not available
            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }
            
            if (lobbyManager == null)
            {
                lobbyManager = FindObjectOfType<LobbyManager>();
            }
            
            if (uiManager != null)
            {
                uiManager.ShowLobbyUI();
            }
            
            if (lobbyManager != null)
            {
                lobbyManager.UpdateLobbyUI();
            }
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearPlayers();
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
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
        if (lobbyManager != null)
        {
            lobbyManager.UpdateLobbyUI();
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
        if (lobbyManager != null)
        {
            lobbyManager.UpdateLobbyUI();
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
        Debug.LogError($"Room creation failed: {message}");
        
        if (uiManager != null)
        {
            uiManager.ShowErrorUI($"Room creation failed: {message}");
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
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
            GameManager.Instance.SetState(GameManager.GameState.Lobby);
        }
    }
}