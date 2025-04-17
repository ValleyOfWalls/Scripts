using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Newtonsoft.Json;

public class PlayerManager
{
    private GameManager gameManager;
    
    // Player data
    private string localPetName = "MyPet";
    private int localPlayerHealth;
    private int localPetHealth;
    private int opponentPetHealth;
    private int currentEnergy;
    private int startingEnergy;
    private Player opponentPlayer;
    private bool combatEndedForLocalPlayer = false;
    
    // Player list management
    private List<GameObject> playerListEntries = new List<GameObject>();
    
    // Network constants (property keys)
    public const string PLAYER_READY_PROPERTY = "IsReady";
    public const string COMBAT_FINISHED_PROPERTY = "CombatFinished";
    public const string PLAYER_SCORE_PROP = "PlayerScore";
    public const string PLAYER_BASE_PET_HP_PROP = "BasePetHP";
    public const string COMBAT_PAIRINGS_PROP = "CombatPairings";
    
    public PlayerManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        this.localPlayerHealth = startingPlayerHealth;
        this.localPetHealth = startingPetHealth;
        this.startingEnergy = startingEnergy;
        this.currentEnergy = startingEnergy;
    }
    
    public void UpdatePlayerList(GameObject playerListPanel, GameObject playerEntryTemplate)
    {
        if (playerListPanel == null || playerEntryTemplate == null) return;
        
        foreach (GameObject entry in playerListEntries) 
            Object.Destroy(entry);
        
        playerListEntries.Clear();
        
        foreach (Player player in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
        {
            GameObject newEntry = Object.Instantiate(playerEntryTemplate, playerListPanel.transform);
            TMPro.TextMeshProUGUI textComponent = newEntry.GetComponent<TMPro.TextMeshProUGUI>();
            string readyStatus = "";
            if (player.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReadyStatus))
                readyStatus = (bool)isReadyStatus ? " <color=green>(Ready)</color>" : "";
            
            textComponent.text = $"{player.NickName}{(player.IsMasterClient ? " (Host)" : "")}{readyStatus}";
            newEntry.SetActive(true);
            playerListEntries.Add(newEntry);
        }
    }
    
    public void SetPlayerReady(bool isReady)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable {{ PLAYER_READY_PROPERTY, isReady }};
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
    
    public bool CheckAllPlayersReady()
    {
        return PhotonNetwork.PlayerList.Length > 1 && // Need at least 2 players
               PhotonNetwork.PlayerList.All(p => 
                   p.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReady) && (bool)isReady);
    }
    
    public void SetLocalPetName(string name)
    {
        localPetName = name;
    }
    
    public string GetLocalPetName()
    {
        return localPetName;
    }
    
    public void InitializeCombatState(int opponentPetOwnerActorNum, int startingPlayerHealth, int startingPetHealth)
    {
        Debug.Log($"Initializing Combat State for round. Fighting pet of ActorNum {opponentPetOwnerActorNum}");
        
        combatEndedForLocalPlayer = false; // Reset combat end flag for the new round
        
        // Determine opponent player based on provided ActorNumber
        opponentPlayer = null; // Reset from previous round
        if (opponentPetOwnerActorNum > 0)
        {
            opponentPlayer = PhotonNetwork.CurrentRoom.GetPlayer(opponentPetOwnerActorNum);
        }
        
        if (opponentPlayer == null && opponentPetOwnerActorNum > 0)
        {
            Debug.LogError($"Could not find opponent player with ActorNum {opponentPetOwnerActorNum}!");
        }
        else if (opponentPetOwnerActorNum <= 0)
        {
            Debug.LogWarning("InitializeCombatState called with invalid opponent ActorNum. Likely single player or pairing issue.");
        }
        
        // Initialize Health - Use potentially upgraded BASE values
        localPlayerHealth = startingPlayerHealth;
        localPetHealth = startingPetHealth;
        
        // Opponent pet health resets based on THEIR base value (from Player Property)
        int opponentBasePetHealth = startingPetHealth; // Default if property not found
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { opponentBasePetHealth = (int)oppBasePetHP; }
            catch { Debug.LogError($"Failed to cast {PLAYER_BASE_PET_HP_PROP} for player {opponentPlayer.NickName}"); }
        }
        opponentPetHealth = opponentBasePetHealth; // Refresh opponent pet health
        Debug.Log($"Set opponent pet health to {opponentPetHealth} (Base: {opponentBasePetHealth})");
        
        currentEnergy = startingEnergy;
    }
    
    public void DamageLocalPlayer(int amount)
    {
        localPlayerHealth -= amount;
        if (localPlayerHealth < 0) localPlayerHealth = 0;
        
        gameManager.UpdateHealthUI();
        
        if (localPlayerHealth <= 0 && !combatEndedForLocalPlayer)
        {
            HandleCombatLoss();
        }
    }
    
    public void DamageOpponentPet(int amount)
    {
        opponentPetHealth -= amount;
        if (opponentPetHealth < 0) opponentPetHealth = 0;
        
        gameManager.UpdateHealthUI();
        
        // Notify the opponent that their pet took damage
        if (opponentPlayer != null)
        {
            Debug.Log($"Sending RpcTakePetDamage({amount}) to {opponentPlayer.NickName}");
            gameManager.GetPhotonView().RPC("RpcTakePetDamage", opponentPlayer, amount);
        }
        
        if (opponentPetHealth <= 0 && !combatEndedForLocalPlayer)
        {
            HandleCombatWin();
        }
    }
    
    public void DamageLocalPet(int amount)
    {
        localPetHealth -= amount;
        if (localPetHealth < 0) localPetHealth = 0;
        
        gameManager.UpdateHealthUI();
    }
    
    private void HandleCombatWin()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;
        
        Debug.Log("COMBAT WIN! Player defeated the opponent's pet.");
        
        // Award point using Player Property
        int currentScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
        {
            currentScore = (int)scoreVal;
        }
        int newScore = currentScore + 1;
        Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newScore } };
        // Use Check-And-Swap for safety against race conditions
        Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentScore } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(scoreUpdate, expectedScore);
        Debug.Log($"Attempted to set local player score to {newScore} (expected {currentScore}).");
        
        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);
        
        // Disable further actions
        gameManager.DisableEndTurnButton();
    }
    
    private void HandleCombatLoss()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;
        
        Debug.Log("COMBAT LOSS! Player was defeated.");
        
        // Award point to opponent using Player Property
        if (opponentPlayer != null)
        {
            int currentOpponentScore = 0;
            if (opponentPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
            {
                currentOpponentScore = (int)scoreVal;
            }
            int newOpponentScore = currentOpponentScore + 1;
            Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newOpponentScore } };
            Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentOpponentScore } };
            opponentPlayer.SetCustomProperties(scoreUpdate, expectedScore);
            Debug.Log($"Attempted to set opponent ({opponentPlayer.NickName}) score to {newOpponentScore} (expected {currentOpponentScore}).");
        }
        else
        {
            Debug.LogError("HandleCombatLoss: Cannot award point, opponentPlayer is null!");
        }
        
        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);
        
        // Disable further actions
        gameManager.DisableEndTurnButton();
    }
    
    public void SetPlayerCombatFinished(bool finished)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { COMBAT_FINISHED_PROPERTY, finished }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"Set {COMBAT_FINISHED_PROPERTY} to {finished} for local player.");
        
        // Master client should check if all players are finished
        if (PhotonNetwork.IsMasterClient)
        {
            CheckForAllPlayersFinishedCombat();
        }
    }
    
    public void CheckForAllPlayersFinishedCombat()
    {
        if (!PhotonNetwork.IsMasterClient || gameManager.GetGameStateManager().GetCurrentState() != GameState.Combat)
        {
            return; // Only Master Client checks, and only during combat
        }
        
        Debug.Log("Master Client checking if all players finished combat...");
        
        bool allFinished = true;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            object finishedStatus;
            if (p.CustomProperties.TryGetValue(COMBAT_FINISHED_PROPERTY, out finishedStatus))
            {
                if (!(bool)finishedStatus)
                {
                    allFinished = false;
                    Debug.Log($"Player {p.NickName} has not finished combat yet.");
                    break; // No need to check further
                }
            }
            else
            {
                // Player hasn't set the property yet
                allFinished = false;
                Debug.Log($"Player {p.NickName} property {COMBAT_FINISHED_PROPERTY} not found.");
                break;
            }
        }
        
        if (allFinished)
        {
            Debug.Log("All players have finished combat! Starting Draft Phase.");
            // Reset flags for next round before starting draft
            ResetCombatFinishedFlags();
            // Tell all clients to start the draft phase
            gameManager.GetPhotonView().RPC("RpcStartDraft", RpcTarget.All);
        }
        else
        {
            Debug.Log("Not all players finished combat yet.");
        }
    }
    
    public void ResetCombatFinishedFlags()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { COMBAT_FINISHED_PROPERTY, false }
        };
        
        // Reset for all players
        foreach(Player p in PhotonNetwork.PlayerList)
        {
            p.SetCustomProperties(props);
        }
        Debug.Log("Reset CombatFinished flags for all players.");
    }
    
    public void PrepareNextCombatRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("Master Client preparing next combat round...");
        
        List<Player> players = PhotonNetwork.PlayerList.ToList();
        // Shuffle players for randomized pairings
        System.Random rng = new System.Random();
        players = players.OrderBy(p => rng.Next()).ToList();
        
        Dictionary<int, int> pairings = new Dictionary<int, int>();
        if (players.Count < 2)
        {
            Debug.LogWarning("Cannot create pairings with less than 2 players.");
        }
        else
        {
            for (int i = 0; i < players.Count; i++)
            {
                Player currentPlayer = players[i];
                Player opponentPetOwner = players[(i + 1) % players.Count]; // Cyclical pairing
                pairings[currentPlayer.ActorNumber] = opponentPetOwner.ActorNumber;
                Debug.Log($"Pairing: {currentPlayer.NickName} vs {opponentPetOwner.NickName}'s Pet");
            }
        }
        
        // Store pairings in room properties
        string pairingsJson = JsonConvert.SerializeObject(pairings);
        Hashtable roomProps = new Hashtable
        {
            { COMBAT_PAIRINGS_PROP, pairingsJson }
        };
        
        // Reset combat finished flags BEFORE setting properties that trigger the next step
        ResetCombatFinishedFlags();
        
        // Set properties
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        
        // Set flag to wait for OnRoomPropertiesUpdate confirmation
        gameManager.GetGameStateManager().SetWaitingToStartCombatRPC(true);
        Debug.Log($"Master Client set pairings property and isWaitingToStartCombatRPC = true. Waiting for confirmation.");
    }
    
    public void UpdateScoreUI()
    {
        TMPro.TextMeshProUGUI scoreText = gameManager.GetScoreText();
        if (scoreText == null) return;
        
        // Fetch scores from Player Properties
        int localScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object localScoreVal))
        {
            try { localScore = (int)localScoreVal; } catch { /* Ignore cast error, keep 0 */ }
        }
        
        int opponentScore = 0;
        // Find the opponent player (simple 2-player logic assumption)
        Player currentOpponent = null; 
        foreach(Player p in PhotonNetwork.PlayerList)
        {
            if (p != PhotonNetwork.LocalPlayer) 
            {
                currentOpponent = p;
                break;
            }
        }
        
        if (currentOpponent != null && currentOpponent.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object oppScoreVal))
        {
            try { opponentScore = (int)oppScoreVal; } catch { /* Ignore cast error, keep 0 */ }
        }
        
        // Basic 2-player score display
        scoreText.text = $"Score: You: {localScore} / Opp: {opponentScore}";
    }
    
    public bool IsCombatEndedForLocalPlayer()
    {
        return combatEndedForLocalPlayer;
    }
    
    public void SetCombatEndedForLocalPlayer(bool value)
    {
        combatEndedForLocalPlayer = value;
    }
    
    public int GetLocalPlayerHealth()
    {
        return localPlayerHealth;
    }
    
    public int GetLocalPetHealth()
    {
        return localPetHealth;
    }
    
    public int GetOpponentPetHealth()
    {
        return opponentPetHealth;
    }
    
    public int GetCurrentEnergy()
    {
        return currentEnergy;
    }
    
    public void SetCurrentEnergy(int energy)
    {
        currentEnergy = energy;
    }
    
    public int GetStartingEnergy()
    {
        return startingEnergy;
    }
    
    public void SetOpponentPlayer(Player player)
    {
        opponentPlayer = player;
    }
    
    public Player GetOpponentPlayer()
    {
        return opponentPlayer;
    }
    
    public void ConsumeEnergy(int amount)
    {
        currentEnergy -= amount;
        if (currentEnergy < 0) currentEnergy = 0;
        gameManager.UpdateEnergyUI();
    }
}