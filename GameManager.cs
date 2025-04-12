using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class GameManager : MonoBehaviourPunCallbacks
{
    // Singleton pattern
    public static GameManager Instance { get; private set; }

    // References to other managers
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private GameplayManager gameplayManager;

    // Game state
    public enum GameState { Connecting, Lobby, Draft, Battle }
    private GameState currentState;

    // Player information
    private List<PlayerInfo> players = new List<PlayerInfo>();
    
    // PhotonView for this GameObject
    private PhotonView gameManagerPhotonView;

    private void Awake()
    {
        // Ensure only one instance exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Get or add PhotonView component
        gameManagerPhotonView = GetComponent<PhotonView>();
        if (gameManagerPhotonView == null)
        {
            gameManagerPhotonView = gameObject.AddComponent<PhotonView>();
            gameManagerPhotonView.ViewID = 999; // Set a static view ID for simplicity
            PhotonNetwork.RegisterPhotonView(gameManagerPhotonView);
        }
        
        // Initialize managers in the correct order
        try
        {
            InitializeManagers();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error initializing managers: " + e.Message);
        }
    }

    private void InitializeManagers()
    {
        // Create all manager GameObjects as children of GameManager
        if (networkManager == null)
        {
            networkManager = CreateManager<NetworkManager>("NetworkManager");
        }
        
        if (uiManager == null)
        {
            uiManager = CreateManager<UIManager>("UIManager");
        }
        
        if (lobbyManager == null)
        {
            lobbyManager = CreateManager<LobbyManager>("LobbyManager");
        }
        
        if (gameplayManager == null)
        {
            gameplayManager = CreateManager<GameplayManager>("GameplayManager");
        }

        // Allow UI Manager to initialize before setting state
        StartCoroutine(DelayedStateChange());
    }

    private IEnumerator DelayedStateChange()
    {
        // Wait for a frame to make sure all managers are initialized
        yield return null;
        
        // Set initial game state
        SetState(GameState.Connecting);
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject managerObj = new GameObject(name);
        managerObj.transform.SetParent(transform);
        return managerObj.AddComponent<T>();
    }

    public void SetState(GameState newState)
    {
        currentState = newState;
        
        switch (currentState)
        {
            case GameState.Connecting:
                if (networkManager != null)
                {
                    networkManager.Connect();
                }
                else
                {
                    Debug.LogError("NetworkManager is null in SetState");
                }
                break;
                
            case GameState.Lobby:
                if (lobbyManager != null)
                {
                    lobbyManager.InitializeLobby();
                }
                else
                {
                    Debug.LogError("LobbyManager is null in SetState");
                }
                break;
                
            case GameState.Draft:
                if (gameplayManager != null)
                {
                    gameplayManager.InitializeDraftPhase();
                }
                else
                {
                    Debug.LogError("GameplayManager is null in SetState");
                }
                break;
                
            case GameState.Battle:
                if (gameplayManager != null)
                {
                    gameplayManager.InitializeBattlePhase();
                }
                else
                {
                    Debug.LogError("GameplayManager is null in SetState");
                }
                // Show gameplay UI
                if (uiManager != null)
                {
                    uiManager.ShowGameplayUI();
                }
                break;
        }
    }

    public GameState GetCurrentState()
    {
        return currentState;
    }

    public void ClearPlayers()
    {
        players.Clear();
        Debug.Log("Cleared player list");
    }

    public void AddPlayer(string playerId, string playerName)
    {
        // Check if player already exists
        if (players.Find(p => p.PlayerId == playerId) != null)
        {
            Debug.Log($"Player {playerName} already in list, not adding again");
            return;
        }
        
        PlayerInfo newPlayer = new PlayerInfo(playerId, playerName);
        players.Add(newPlayer);
        Debug.Log($"Added player {playerName} to list, total players: {players.Count}");
    }

    public void RemovePlayer(string playerId)
    {
        players.RemoveAll(p => p.PlayerId == playerId);
    }

    public void SetPlayerReady(string playerId, bool isReady)
    {
        PlayerInfo player = players.Find(p => p.PlayerId == playerId);
        if (player != null)
        {
            player.IsReady = isReady;
            if (lobbyManager != null)
            {
                lobbyManager.UpdatePlayerReadyStatus(playerId, isReady);
            }
        }
    }

    public bool AreAllPlayersReady()
    {
        if (players.Count == 0) return false;
        return players.TrueForAll(p => p.IsReady);
    }

    public List<PlayerInfo> GetAllPlayers()
    {
        Debug.Log($"Getting all players, count: {players.Count}");
        foreach (PlayerInfo player in players)
        {
            Debug.Log($"Player: {player.PlayerName}, ID: {player.PlayerId}, Ready: {player.IsReady}");
        }
        return new List<PlayerInfo>(players);
    }

    // Start the game when all players are ready
    public void StartGame()
    {
        Debug.Log("StartGame called. IsMasterClient: " + PhotonNetwork.IsMasterClient + ", AreAllPlayersReady: " + AreAllPlayersReady());
        
        if (PhotonNetwork.IsMasterClient && AreAllPlayersReady())
        {
            Debug.Log("Sending RPC_StartGame to all clients");
            gameManagerPhotonView.RPC("RPC_StartGame", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_StartGame()
    {
        Debug.Log("RPC_StartGame received - starting game");
        SetState(GameState.Battle);
    }
    
    // Helper methods to access managers
    public NetworkManager GetNetworkManager()
    {
        return networkManager;
    }
    
    public UIManager GetUIManager()
    {
        return uiManager;
    }
    
    public LobbyManager GetLobbyManager()
    {
        return lobbyManager;
    }
    
    public GameplayManager GetGameplayManager()
    {
        return gameplayManager;
    }
}

// Class to store player information
public class PlayerInfo
{
    public string PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public bool IsReady { get; set; }

    public PlayerInfo(string id, string name)
    {
        PlayerId = id;
        PlayerName = name;
        IsReady = false;
    }
}