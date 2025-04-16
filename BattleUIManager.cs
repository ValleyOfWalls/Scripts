// NOTE: This class is now integrated into the UIManager.cs file in the Combat UI section.
// All battle overview UI functionality has been moved to the centralized UIManager class.

// For reference, the key methods that were moved include:
// - ShowBattleOverview(Dictionary<int, int> battlePairings)
// - HideBattleOverview()
// - CreateBattleCard(Transform parent, int playerActorNumber, int monsterOwnerActorNumber, PlayerController player, MonsterController monster)
// - ClearBattleCards()
// - UpdateBattleCard(int actorNumber, bool isPlayer, int currentHealth, int maxHealth)