using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Newtonsoft.Json;

// --- ADDED: Status Effect Enum --- 
public enum StatusEffectType
{
    None,
    Weak,   // Target deals less damage
    Break   // Target takes more damage
    // Add more status effects here (e.g., Vulnerable, Strength, Dexterity)
}
// --- END ADDED ---

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
    private int localPlayerBlock = 0;
    private int localPetBlock = 0;
    private int opponentPetBlock = 0; // Note: This is only tracked locally for display/intent
    private int localPlayerTempMaxHealthBonus = 0;
    private int localPetTempMaxHealthBonus = 0;
    private int opponentPetTempMaxHealthBonus = 0;
    private int localPlayerWeakTurns = 0;
    private int localPlayerBreakTurns = 0;
    private int localPetWeakTurns = 0;
    private int localPetBreakTurns = 0;
    private int opponentPetWeakTurns = 0; // Local simulation
    private int opponentPetBreakTurns = 0; // Local simulation
    
    // --- ADDED: Combo Tracking ---
    private int currentComboCount = 0;
    // --- END ADDED ---
    
    // --- ADDED: DoT Tracking ---
    private int localPlayerDotTurns = 0;
    private int localPlayerDotDamage = 0;
    private int localPetDotTurns = 0;
    private int localPetDotDamage = 0;
    private int opponentPetDotTurns = 0; // Local simulation
    private int opponentPetDotDamage = 0; // Local simulation
    // --- END ADDED ---
    
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
        
        // --- ADDED: Reset Combo & DoT --- 
        currentComboCount = 0;
        localPlayerDotTurns = 0;
        localPlayerDotDamage = 0;
        localPetDotTurns = 0;
        localPetDotDamage = 0;
        opponentPetDotTurns = 0;
        opponentPetDotDamage = 0;
        // --- END ADDED ---
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
        List<string> opponentPetCardNames = new List<string>(); // Store opponent pet deck card names here
        
        if (opponentPetOwnerActorNum > 0)
        {
            opponentPlayer = PhotonNetwork.CurrentRoom.GetPlayer(opponentPetOwnerActorNum);
        }
        
        if (opponentPlayer == null && opponentPetOwnerActorNum > 0)
        {
            Debug.LogError($"Could not find opponent player with ActorNum {opponentPetOwnerActorNum}!");
        }
        else if (opponentPlayer != null)
        {
             // Opponent Player Found - Get their Pet Deck and Base HP
            Debug.Log($"Found opponent: {opponentPlayer.NickName}");

            // Get opponent's base pet HP
            if (opponentPlayer.CustomProperties.TryGetValue(PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
            {
                try { opponentPetHealth = (int)oppBasePetHP; }
                catch { Debug.LogError($"Failed to cast {PLAYER_BASE_PET_HP_PROP} for player {opponentPlayer.NickName}, using default {startingPetHealth}"); opponentPetHealth = startingPetHealth; }
            }
            else
            {
                opponentPetHealth = startingPetHealth; // Default if property not found
            }
            Debug.Log($"Set initial opponent pet health to {opponentPetHealth}");

            // Get opponent's pet deck card names
            if (opponentPlayer.CustomProperties.TryGetValue(CardManager.PLAYER_PET_DECK_PROP, out object oppPetDeckObj))
            {
                try
                {
                    string petDeckJson = oppPetDeckObj as string;
                    if (!string.IsNullOrEmpty(petDeckJson))
                    {
                        opponentPetCardNames = JsonConvert.DeserializeObject<List<string>>(petDeckJson) ?? new List<string>();
                        Debug.Log($"Retrieved {opponentPetCardNames.Count} pet card names for opponent {opponentPlayer.NickName}.");
                    }
                    else
                    {
                        Debug.LogWarning($"Opponent {opponentPlayer.NickName} has empty {CardManager.PLAYER_PET_DECK_PROP} property.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to deserialize opponent pet deck JSON for player {opponentPlayer.NickName}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Opponent {opponentPlayer.NickName} has no {CardManager.PLAYER_PET_DECK_PROP} property. Using starter deck.");
                // If the property doesn't exist (e.g., first round), we'll implicitly use the starter deck later.
            }
        }
        else // No valid opponent (single player? error?)
        {
            Debug.LogWarning("InitializeCombatState called with invalid or missing opponent ActorNum. Setting default opponent pet health.");
            opponentPetHealth = startingPetHealth; // Use default health
            // Opponent pet deck will default to starter deck in CardManager
        }
        
        // Initialize Local Health
        localPlayerHealth = startingPlayerHealth;
        localPetHealth = startingPetHealth; // Reset local pet health too
        
        // Reset Block
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0; 
        
        // --- ADDED: Reset Temp Max Health Bonuses --- 
        localPlayerTempMaxHealthBonus = 0;
        localPetTempMaxHealthBonus = 0;
        opponentPetTempMaxHealthBonus = 0;
        // --- END ADDED ---
        
        // --- ADDED: Reset Status Effects --- 
        localPlayerWeakTurns = 0;
        localPlayerBreakTurns = 0;
        localPetWeakTurns = 0;
        localPetBreakTurns = 0;
        opponentPetWeakTurns = 0;
        opponentPetBreakTurns = 0;
        // --- END ADDED ---
        
        // --- ADDED: Reset Combo & DoT --- 
        currentComboCount = 0;
        localPlayerDotTurns = 0;
        localPlayerDotDamage = 0;
        localPetDotTurns = 0;
        localPetDotDamage = 0;
        opponentPetDotTurns = 0;
        opponentPetDotDamage = 0;
        // --- END ADDED ---
        
        currentEnergy = startingEnergy;
        
        // *** Pass opponent pet deck info to CardManager ***
        // This is where we tell CardManager which cards the opponent's pet actually has.
        gameManager.GetCardManager().InitializeOpponentPetDeck(opponentPetCardNames);
    }
    
    public void DamageLocalPlayer(int amount)
    {
        if (amount <= 0) return; // Can't deal negative damage
        
        int damageAfterBlock = amount - localPlayerBlock;
        int blockConsumed = Mathf.Min(amount, localPlayerBlock);
        localPlayerBlock -= blockConsumed;
        if (localPlayerBlock < 0) localPlayerBlock = 0;
        
        Debug.Log($"DamageLocalPlayer: Incoming={amount}, Block={blockConsumed}, RemainingDamage={damageAfterBlock}");
        
        if (damageAfterBlock > 0)
        {
            // --- ADDED: Check for Break --- 
            if (IsPlayerBroken()) 
            {
                 // Example: 50% increase. Potency could make this variable.
                 int breakBonus = Mathf.CeilToInt(damageAfterBlock * 0.5f); 
                 Debug.Log($"Player has Break! Increasing damage by {breakBonus}");
                 damageAfterBlock += breakBonus;
            }
            // --- END ADDED ---
            
            localPlayerHealth -= damageAfterBlock;
            if (localPlayerHealth < 0) localPlayerHealth = 0;
        }
        
        gameManager.UpdateHealthUI(); // Update both health and block display
        
        if (localPlayerHealth <= 0 && !combatEndedForLocalPlayer)
        {
            HandleCombatLoss();
        }
    }
    
    public void DamageOpponentPet(int amount)
    {
        if (amount <= 0) return;
        
        // Note: Opponent block isn't *truly* tracked here, just for local calc/display.
        // Real block calculation happens on the opponent's client.
        // However, we reduce our local representation of their block for prediction.
        int damageAfterBlock = amount - opponentPetBlock;
        int blockConsumed = Mathf.Min(amount, opponentPetBlock);
        opponentPetBlock -= blockConsumed;
        if (opponentPetBlock < 0) opponentPetBlock = 0;
        
        Debug.Log($"DamageOpponentPet: Incoming={amount}, Est. Block={blockConsumed}, RemainingDamage={damageAfterBlock}");
        
        if (damageAfterBlock > 0)
        {
            // --- ADDED: Check for Break --- 
            if (IsOpponentPetBroken()) 
            {
                 int breakBonus = Mathf.CeilToInt(damageAfterBlock * 0.5f); 
                 Debug.Log($"Opponent Pet has Break! Increasing damage by {breakBonus} (local sim)");
                 damageAfterBlock += breakBonus;
            }
            // --- END ADDED ---
            
            opponentPetHealth -= damageAfterBlock;
            if (opponentPetHealth < 0) opponentPetHealth = 0;
        }
        
        gameManager.UpdateHealthUI(); // Update both health and block display
        
        // Notify the opponent that their pet took damage (send ORIGINAL amount, they calculate block)
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
        if (amount <= 0) return;
        
        int damageAfterBlock = amount - localPetBlock;
        int blockConsumed = Mathf.Min(amount, localPetBlock);
        localPetBlock -= blockConsumed;
        if (localPetBlock < 0) localPetBlock = 0;
        
        Debug.Log($"DamageLocalPet: Incoming={amount}, Block={blockConsumed}, RemainingDamage={damageAfterBlock}");
        
        if (damageAfterBlock > 0)
        {
            // --- ADDED: Check for Break --- 
            if (IsLocalPetBroken()) 
            {
                 int breakBonus = Mathf.CeilToInt(damageAfterBlock * 0.5f); 
                 Debug.Log($"Local Pet has Break! Increasing damage by {breakBonus}");
                 damageAfterBlock += breakBonus;
            }
            // --- END ADDED ---
            
            localPetHealth -= damageAfterBlock;
            if (localPetHealth < 0) localPetHealth = 0;
        }
        
        gameManager.UpdateHealthUI(); // Update both health and block display
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
    
    // --- ADDED: Block Management ---
    public void AddBlockToLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        localPlayerBlock += amount;
        Debug.Log($"Added {amount} block to Local Player. New total: {localPlayerBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }

    public void AddBlockToLocalPet(int amount)
    {
        if (amount <= 0) return;
        localPetBlock += amount;
        Debug.Log($"Added {amount} block to Local Pet. New total: {localPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }
    
    // Note: Opponent block is generally managed on their client.
    // This might be used if an effect *predicts* opponent block gain or for AI.
    public void AddBlockToOpponentPet(int amount)
    {
        if (amount <= 0) return;
        opponentPetBlock += amount;
        Debug.Log($"Added {amount} block to Opponent Pet (local sim). New total: {opponentPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }

    public void ResetAllBlock()
    {
        Debug.Log($"Resetting block. Player: {localPlayerBlock} -> 0, Pet: {localPetBlock} -> 0, OpponentPet: {opponentPetBlock} -> 0");
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0; 
        gameManager.UpdateHealthUI(); // Update block display
    }
    // --- END ADDED ---
    
    // RPC received when our pet takes damage
    [PunRPC]
    private void RpcTakePetDamage(int damageAmount)
    {
        if (damageAmount <= 0) return;

        Debug.Log($"RPC Received: My Pet taking {damageAmount} damage. Current Block: {localPetBlock}");
        DamageLocalPet(damageAmount); // DamageLocalPet now correctly handles block
    }
    
    // --- ADDED: Block Getters ---
    public int GetLocalPlayerBlock()
    {
        return localPlayerBlock;
    }
    // --- END ADDED ---

    public int GetLocalPetBlock()
    {
        return localPetBlock;
    }
    
    // --- ADDED: Block Getters ---
    public int GetOpponentPetBlock()
    {
        return opponentPetBlock; // Returns the locally tracked value
    }
    // --- END ADDED ---

    // --- ADDED: Energy Gain ---
    public void GainEnergy(int amount)
    {
        if (amount <= 0) return;
        currentEnergy += amount;
        // Optional: Add a max energy cap if desired
        // if (currentEnergy > maxEnergy) currentEnergy = maxEnergy; 
        Debug.Log($"Gained {amount} energy. New total: {currentEnergy}");
        gameManager.UpdateEnergyUI(); // Call the UI update
    }
    // --- END ADDED ---

    // --- ADDED: Healing Methods ---
    public void HealLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePlayerMaxHealth();
        localPlayerHealth += amount;
        if (localPlayerHealth > effectiveMaxHP) localPlayerHealth = effectiveMaxHP;
        Debug.Log($"Healed Local Player by {amount}. New health: {localPlayerHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }

    public void HealLocalPet(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePetMaxHealth();
        localPetHealth += amount;
        if (localPetHealth > effectiveMaxHP) localPetHealth = effectiveMaxHP;
        Debug.Log($"Healed Local Pet by {amount}. New health: {localPetHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }
    
    // Heal opponent pet (local simulation only)
    public void HealOpponentPet(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectiveOpponentPetMaxHealth();
        opponentPetHealth += amount;
        if (opponentPetHealth > effectiveMaxHP) opponentPetHealth = effectiveMaxHP;
        Debug.Log($"Healed Opponent Pet by {amount} (local sim). New health: {opponentPetHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
    }
    // --- END ADDED ---
    
    // --- ADDED: Temporary Max Health Methods ---
    public void ApplyTempMaxHealthPlayer(int amount)
    {
        localPlayerTempMaxHealthBonus += amount;
        Debug.Log($"Applied Temp Max Health Player: {amount}. New Bonus: {localPlayerTempMaxHealthBonus}");
        // If increasing max health, also heal by the same amount
        if (amount > 0)
        {
            HealLocalPlayer(amount);
        }
        else
        {
            // If decreasing, clamp current health to new max
            int effectiveMaxHP = GetEffectivePlayerMaxHealth();
            if (localPlayerHealth > effectiveMaxHP) localPlayerHealth = effectiveMaxHP;
            gameManager.UpdateHealthUI(); // Still need to update UI if only clamping
        }
        // Note: UpdateHealthUI is called within HealLocalPlayer if amount > 0
    }

    public void ApplyTempMaxHealthPet(int amount)
    {
        localPetTempMaxHealthBonus += amount;
        Debug.Log($"Applied Temp Max Health Pet: {amount}. New Bonus: {localPetTempMaxHealthBonus}");
        if (amount > 0)
        {
            HealLocalPet(amount);
        }
        else
        {
            int effectiveMaxHP = GetEffectivePetMaxHealth();
            if (localPetHealth > effectiveMaxHP) localPetHealth = effectiveMaxHP;
            gameManager.UpdateHealthUI(); 
        }
    }
    
    public void ApplyTempMaxHealthOpponentPet(int amount)
    {
        opponentPetTempMaxHealthBonus += amount;
        Debug.Log($"Applied Temp Max Health Opponent Pet: {amount} (local sim). New Bonus: {opponentPetTempMaxHealthBonus}");
        if (amount > 0)
        {
            HealOpponentPet(amount);
        }
        else
        {
            int effectiveMaxHP = GetEffectiveOpponentPetMaxHealth();
            if (opponentPetHealth > effectiveMaxHP) opponentPetHealth = effectiveMaxHP;
             gameManager.UpdateHealthUI();
        }
    }
    // --- END ADDED ---
    
    // --- ADDED: Effective Max Health Getters ---
    public int GetEffectivePlayerMaxHealth()
    {
        return gameManager.GetStartingPlayerHealth() + localPlayerTempMaxHealthBonus;
    }
    // --- END ADDED ---

    // public int GetLocalPetHealth()
    // {
    //    return localPetHealth;
    // }

    // --- ADDED: Effective Max Health Getters ---
    public int GetEffectivePetMaxHealth()
    {
        return gameManager.GetStartingPetHealth() + localPetTempMaxHealthBonus;
    }
    // --- END ADDED ---

    // public int GetOpponentPetHealth()
    // {
    //     return opponentPetHealth;
    // }
    
    // --- ADDED: Effective Max Health Getters ---
    public int GetEffectiveOpponentPetMaxHealth()
    {
        // Opponent base health comes from their properties at start of combat, 
        // but we need GameManager's base if we don't have an opponent player object (shouldn't happen in real combat)
        int baseOpponentPetHP = (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PLAYER_BASE_PET_HP_PROP, out object hp)) ? (int)hp : gameManager.GetStartingPetHealth();
        return baseOpponentPetHP + opponentPetTempMaxHealthBonus;
    }
    // --- END ADDED ---

    // --- ADDED: Status Effect Application ---
    public void ApplyStatusEffectLocalPlayer(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        switch(type)
        {
            case StatusEffectType.Weak:
                localPlayerWeakTurns += duration;
                Debug.Log($"Applied Weak to Player for {duration} turns. Total: {localPlayerWeakTurns}");
                break;
            case StatusEffectType.Break:
                localPlayerBreakTurns += duration;
                Debug.Log($"Applied Break to Player for {duration} turns. Total: {localPlayerBreakTurns}");
                break;
        }
        // TODO: Update UI to show status
    }
    
    public void ApplyStatusEffectLocalPet(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
         switch(type)
        {
            case StatusEffectType.Weak:
                localPetWeakTurns += duration;
                Debug.Log($"Applied Weak to Local Pet for {duration} turns. Total: {localPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                localPetBreakTurns += duration;
                Debug.Log($"Applied Break to Local Pet for {duration} turns. Total: {localPetBreakTurns}");
                break;
        }
         // TODO: Update UI to show status
    }
    
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration)
    {
         if (duration <= 0) return;
         switch(type)
        {
            case StatusEffectType.Weak:
                opponentPetWeakTurns += duration;
                Debug.Log($"Applied Weak to Opponent Pet for {duration} turns (local sim). Total: {opponentPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                opponentPetBreakTurns += duration;
                 Debug.Log($"Applied Break to Opponent Pet for {duration} turns (local sim). Total: {opponentPetBreakTurns}");
               break;
        }
         // TODO: Update UI to show status
    }
    // --- END ADDED ---
    
    // --- ADDED: Status Effect Decrementing ---
    public void DecrementPlayerStatusEffects()
    {
        if (localPlayerWeakTurns > 0) localPlayerWeakTurns--;
        if (localPlayerBreakTurns > 0) localPlayerBreakTurns--;
        Debug.Log($"Decremented Player Status. Weak: {localPlayerWeakTurns}, Break: {localPlayerBreakTurns}");
        // TODO: Update UI
    }
    
    public void DecrementLocalPetStatusEffects()
    {
        if (localPetWeakTurns > 0) localPetWeakTurns--;
        if (localPetBreakTurns > 0) localPetBreakTurns--;
         Debug.Log($"Decremented Local Pet Status. Weak: {localPetWeakTurns}, Break: {localPetBreakTurns}");
       // TODO: Update UI
    }
    
    public void DecrementOpponentPetStatusEffects() // Called by CardManager before pet acts
    {
        if (opponentPetWeakTurns > 0) opponentPetWeakTurns--;
        if (opponentPetBreakTurns > 0) opponentPetBreakTurns--;
        Debug.Log($"Decremented Opponent Pet Status (local sim). Weak: {opponentPetWeakTurns}, Break: {opponentPetBreakTurns}");
        // TODO: Update UI
    }
    // --- END ADDED ---
    
    // --- ADDED: Status Effect Getters ---
    public bool IsPlayerWeak() => localPlayerWeakTurns > 0;
    public bool IsPlayerBroken() => localPlayerBreakTurns > 0;
    public bool IsLocalPetWeak() => localPetWeakTurns > 0;
    public bool IsLocalPetBroken() => localPetBreakTurns > 0;
    public bool IsOpponentPetWeak() => opponentPetWeakTurns > 0;
    public bool IsOpponentPetBroken() => opponentPetBreakTurns > 0;
    // Add getters for potency if implemented
    // --- END ADDED ---

    // --- ADDED: Combo Methods ---
    public void IncrementComboCount()
    {
        currentComboCount++;
        Debug.Log($"Combo count incremented to: {currentComboCount}");
        // TODO: Update UI potentially?
    }

    public void ResetComboCount()
    {
        if (currentComboCount > 0)
        {
            Debug.Log($"Resetting combo count from {currentComboCount} to 0.");
            currentComboCount = 0;
            // TODO: Update UI potentially?
        }
    }
    
    public int GetCurrentComboCount() => currentComboCount;
    // --- END ADDED ---
    
    // --- ADDED: DoT Getters ---
    public int GetPlayerDotTurns() => localPlayerDotTurns;
    public int GetPlayerDotDamage() => localPlayerDotDamage;
    public int GetLocalPetDotTurns() => localPetDotTurns;
    public int GetLocalPetDotDamage() => localPetDotDamage;
    public int GetOpponentPetDotTurns() => opponentPetDotTurns;
    public int GetOpponentPetDotDamage() => opponentPetDotDamage;
    // --- END ADDED ---

    // --- ADDED: DoT Application ---
    public void ApplyDotLocalPlayer(int damage, int duration)
    {
        if (damage <= 0 || duration <= 0) return;
        // Simple approach: Stack duration, use latest damage value.
        // More complex logic (e.g., highest damage, separate stacks) could be added.
        localPlayerDotTurns += duration;
        localPlayerDotDamage = damage; 
        Debug.Log($"Applied DoT to Player: {damage} damage for {duration} turns. Total Turns: {localPlayerDotTurns}");
        // TODO: Update UI
    }
    
     public void ApplyDotLocalPet(int damage, int duration)
    {
        if (damage <= 0 || duration <= 0) return;
        localPetDotTurns += duration;
        localPetDotDamage = damage; 
        Debug.Log($"Applied DoT to Local Pet: {damage} damage for {duration} turns. Total Turns: {localPetDotTurns}");
        // TODO: Update UI
    }
    
    public void ApplyDotOpponentPet(int damage, int duration)
    {
         if (damage <= 0 || duration <= 0) return;
        opponentPetDotTurns += duration;
        opponentPetDotDamage = damage; 
        Debug.Log($"Applied DoT to Opponent Pet: {damage} damage for {duration} turns (local sim). Total Turns: {opponentPetDotTurns}");
        // TODO: Update UI
    }
    // --- END ADDED ---
    
    // --- REVISED: Turn Start Processing --- 
    public void ProcessPlayerTurnStartEffects()
    {
        // Apply DoT Damage FIRST
        if (localPlayerDotTurns > 0 && localPlayerDotDamage > 0)
        {
            Debug.Log($"Player DoT ticking for {localPlayerDotDamage} damage.");
            DamageLocalPlayer(localPlayerDotDamage);
            localPlayerDotTurns--; // Decrement AFTER applying damage
            if (localPlayerDotTurns == 0) localPlayerDotDamage = 0; // Clear damage if duration ends
        }
        
        // Then Decrement Status Effects
        if (localPlayerWeakTurns > 0) localPlayerWeakTurns--;
        if (localPlayerBreakTurns > 0) localPlayerBreakTurns--;
        
        Debug.Log($"Processed Player Turn Start. Weak: {localPlayerWeakTurns}, Break: {localPlayerBreakTurns}, DoT Turns: {localPlayerDotTurns}");
        // TODO: Update UI
    }
    
    public void ProcessLocalPetTurnStartEffects()
    {
         // Apply DoT Damage FIRST
        if (localPetDotTurns > 0 && localPetDotDamage > 0)
        {
            Debug.Log($"Local Pet DoT ticking for {localPetDotDamage} damage.");
            DamageLocalPet(localPetDotDamage);
            localPetDotTurns--; 
            if (localPetDotTurns == 0) localPetDotDamage = 0; 
        }
        
        // Then Decrement Status Effects
        if (localPetWeakTurns > 0) localPetWeakTurns--;
        if (localPetBreakTurns > 0) localPetBreakTurns--;
        
        Debug.Log($"Processed Local Pet Turn Start. Weak: {localPetWeakTurns}, Break: {localPetBreakTurns}, DoT Turns: {localPetDotTurns}");
        // TODO: Update UI
    }
    
    public void ProcessOpponentPetTurnStartEffects() // Called by CardManager before pet acts
    {
         // Apply DoT Damage FIRST
        if (opponentPetDotTurns > 0 && opponentPetDotDamage > 0)
        {
            Debug.Log($"Opponent Pet DoT ticking for {opponentPetDotDamage} damage (local sim).");
            DamageOpponentPet(opponentPetDotDamage);
            opponentPetDotTurns--; 
            if (opponentPetDotTurns == 0) opponentPetDotDamage = 0; 
        }
        
        // Then Decrement Status Effects
        if (opponentPetWeakTurns > 0) opponentPetWeakTurns--;
        if (opponentPetBreakTurns > 0) opponentPetBreakTurns--;
        
        Debug.Log($"Processed Opponent Pet Turn Start (local sim). Weak: {opponentPetWeakTurns}, Break: {opponentPetBreakTurns}, DoT Turns: {opponentPetDotTurns}");
        // TODO: Update UI
    }
    // --- END REVISED ---
}