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
    [SerializeField] private GameplayManager gameplayManager;
    [SerializeField] private DeckManager deckManager;

    // Game state
    public enum GameState { Connecting, Lobby, Draft, Battle }
    private GameState currentState;

    // Player information
    private List<PlayerInfo> players = new List<PlayerInfo>();
    
    // Flag to track if GameplayManager has been spawned
    private bool gameplayManagerSpawned = false;

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
        
        // Initialize managers in the correct order
        InitializeManagers();
    }

    private void InitializeManagers()
    {
        // Create all manager GameObjects as children of GameManager
        if (networkManager == null)
        {
            GameObject networkObj = new GameObject("NetworkManager");
            networkObj.transform.SetParent(transform);
            networkManager = networkObj.AddComponent<NetworkManager>();
        }
        
        if (uiManager == null)
        {
            GameObject uiObj = new GameObject("UIManager");
            uiObj.transform.SetParent(transform);
            uiManager = uiObj.AddComponent<UIManager>();
        }
        
        if (deckManager == null)
        {
            GameObject deckManagerObj = new GameObject("DeckManager");
            deckManagerObj.transform.SetParent(transform);
            deckManager = deckManagerObj.AddComponent<DeckManager>();
        }

        // Set initial game state
        StartCoroutine(DelayedStateChange());
    }

    private IEnumerator DelayedStateChange()
    {
        // Wait for a frame to make sure all managers are initialized
        yield return null;
        
        // Set initial game state
        SetState(GameState.Connecting);
    }

    public void SetState(GameState newState)
    {
        currentState = newState;
        Debug.Log($"Game state changed to: {currentState}");
        
        switch (currentState)
        {
            case GameState.Connecting:
                if (networkManager != null)
                {
                    networkManager.Connect();
                }
                break;
                
            case GameState.Lobby:
                if (uiManager != null)
                {
                    uiManager.ShowLobbyUI();
                }
                break;
                
            case GameState.Draft:
                if (gameplayManager != null)
                {
                    gameplayManager.InitializeDraftPhase();
                }
                break;
                
            case GameState.Battle:
                if (gameplayManager != null)
                {
                    gameplayManager.InitializeBattlePhase();
                }
                
                // Show gameplay UI
                if (uiManager != null)
                {
                    uiManager.ShowGameplayUI();
                }
                break;
        }
    }

    // Method for the master client to instantiate the networked GameplayManager
    public void InstantiateNetworkedGameplayManager()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom) return;
        if (gameplayManagerSpawned) return;
        
        Debug.Log("Master client instantiating networked GameplayManager");
        
        // Instantiate the GameplayManager prefab through PhotonNetwork
        GameObject gameplayManagerObj = PhotonNetwork.Instantiate("GameplayManager", Vector3.zero, Quaternion.identity);
        
        if (gameplayManagerObj != null)
        {
            gameplayManager = gameplayManagerObj.GetComponent<GameplayManager>();
            gameplayManagerSpawned = true;
            Debug.Log("GameplayManager instantiated successfully with PhotonView ID: " + 
                      gameplayManagerObj.GetComponent<PhotonView>().ViewID);
        }
        else
        {
            Debug.LogError("Failed to instantiate GameplayManager prefab!");
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
            if (uiManager != null)
            {
                uiManager.UpdatePlayerReadyStatus(playerId, isReady);
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
        return new List<PlayerInfo>(players);
    }

    // Start the game when all players are ready
    public void StartGame()
    {
        Debug.Log("StartGame called. IsMasterClient: " + PhotonNetwork.IsMasterClient + ", AreAllPlayersReady: " + AreAllPlayersReady());
        
        if (PhotonNetwork.IsMasterClient && AreAllPlayersReady())
        {
            // Make sure we have a GameplayManager
            if (gameplayManager == null && !gameplayManagerSpawned)
            {
                Debug.Log("Creating GameplayManager before starting game");
                InstantiateNetworkedGameplayManager();
                
                // Give a moment for network synchronization
                StartCoroutine(DelayedGameStart());
            }
            else
            {
                // GameplayManager already exists, go straight to battle
                SetState(GameState.Battle);
            }
        }
    }
    
    private IEnumerator DelayedGameStart()
    {
        // Wait for GameplayManager to be synchronized across clients
        yield return new WaitForSeconds(1.0f);
        
        // Set game state to battle
        SetState(GameState.Battle);
    }
    
    // Called when a new networked object is created
    public void RegisterNetworkedGameplayManager(GameplayManager manager)
    {
        Debug.Log("RegisterNetworkedGameplayManager called with manager: " + (manager != null ? manager.name : "null"));
        
        // Only set if we don't already have one
        if (gameplayManager == null)
        {
            gameplayManager = manager;
            Debug.Log("GameplayManager reference updated");
        }
    }
    
    // Override OnJoinedRoom to handle room joining logic
    public override void OnJoinedRoom()
    {
        Debug.Log("GameManager.OnJoinedRoom called");
        
        // If we're the master client (room creator), we'll create GameplayManager when all players are ready
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("I am master client, will create GameplayManager when game starts");
        }
        
        base.OnJoinedRoom();
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
    
    public GameplayManager GetGameplayManager()
    {
        return gameplayManager;
    }
    
    public DeckManager GetDeckManager()
    {
        return deckManager;
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