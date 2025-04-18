using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;  // Added for Button references

public class PhotonManager : MonoBehaviourPunCallbacks
{
    private GameManager gameManager;
    private new PhotonView photonView;  // Use 'new' to avoid warning

    public void Initialize(GameManager gameManager)
    {
        this.gameManager = gameManager;
        photonView = gameManager.GetPhotonView();
    }

    // Connection Methods
    public void ConnectToPhoton(string playerName, string petName)
    {
        Debug.Log("Connecting to Photon...");
        
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player_" + Random.Range(1000, 9999);
        }
        PhotonNetwork.NickName = playerName;
        
        gameManager.GetGameStateManager().SetUserInitiatedConnection(true);
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Already connected to Master, attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void TryJoinGameRoom()
    {
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("asd", roomOptions, TypedLobby.Default);
    }

    public void LeaveRoom()
    {
        Debug.Log("Leave Button Clicked");
        PhotonNetwork.LeaveRoom();
    }

    // PUN Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to Master Server. Player Nickname: {PhotonNetwork.NickName}");
        if (gameManager.GetGameStateManager().IsUserInitiatedConnection())
        {
            Debug.Log("User initiated connection - attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
            Debug.Log("Connected to Master, but not user-initiated. Waiting for user action.");
            Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
            if (connectBtn != null) connectBtn.interactable = true;
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        gameManager.GetGameStateManager().TransitionToLobby();
        gameManager.UpdatePlayerList();
        gameManager.GetPlayerManager().SetPlayerReady(false);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        gameManager.GetGameStateManager().SetCurrentState(GameState.Connecting);
        gameManager.GetGameStateManager().ShowStartScreen();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join Room Failed: ({returnCode}) {message}");
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: ({returnCode}) {message}");
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
        gameManager.GetGameStateManager().OnDisconnected();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered room.");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left room.");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdatePlayerList();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        Debug.Log($"Player {targetPlayer.NickName} properties updated. CurrentState: {gameManager.GetGameStateManager().GetCurrentState()}");
        
        if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Lobby && 
            changedProps.ContainsKey(PlayerManager.PLAYER_READY_PROPERTY))
        {
            if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
                gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
                gameManager.UpdatePlayerList();
        }
        else if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Combat && 
                 changedProps.ContainsKey(PlayerManager.COMBAT_FINISHED_PROPERTY))
        {
            Debug.Log($"Property update for {PlayerManager.COMBAT_FINISHED_PROPERTY} received from {targetPlayer.NickName}");
            if (PhotonNetwork.IsMasterClient)
            {
                gameManager.GetPlayerManager().CheckForAllPlayersFinishedCombat();
            }
        }
        else if (changedProps.ContainsKey(PlayerManager.PLAYER_SCORE_PROP))
        {
            Debug.Log($"Score property updated for {targetPlayer.NickName}. Refreshing Score UI.");
            gameManager.GetPlayerManager().UpdateScoreUI();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log($"Room properties updated. CurrentState: {gameManager.GetGameStateManager().GetCurrentState()}");

        // Master Client check: If waiting to send StartCombat RPC and pairings are now set
        if (PhotonNetwork.IsMasterClient && gameManager.GetGameStateManager().IsWaitingToStartCombatRPC() && 
            propertiesThatChanged.ContainsKey(PlayerManager.COMBAT_PAIRINGS_PROP))
        {
            Debug.Log("Master Client confirms pairings property set. Sending RpcStartCombat.");
            gameManager.GetGameStateManager().SetWaitingToStartCombatRPC(false);
            photonView.RPC("RpcStartCombat", RpcTarget.All);
        }

        if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Drafting)
        {
            gameManager.GetDraftManager().HandleRoomPropertiesUpdate(propertiesThatChanged);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"New Master Client: {newMasterClient.NickName}");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdateLobbyControls();
        }
    }
    
    // RPC Wrappers
    public void SendRPCStartCombat()
    {
        photonView.RPC("RpcStartCombat", RpcTarget.All);
    }
    
    public void SendRPCStartDraft()
    {
        photonView.RPC("RpcStartDraft", RpcTarget.All);
    }
    
    public void SendRPCTakePetDamage(Player targetPlayer, int damageAmount)
    {
        photonView.RPC("RpcTakePetDamage", targetPlayer, damageAmount);
    }
    
    public void SendRPCPlayerPickedOption(int optionId)
    {
        photonView.RPC("RpcPlayerPickedOption", RpcTarget.MasterClient, optionId);
    }
}