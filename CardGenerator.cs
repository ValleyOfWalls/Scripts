using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class CardGenerator : MonoBehaviour
{
    private const string CardSavePath = "Assets/Cards";

    [MenuItem("Tools/Generate Card Assets")]
    public static void GenerateCardAssets()
    {
        // Ensure the directory exists
        if (!Directory.Exists(CardSavePath))
        {
            Directory.CreateDirectory(CardSavePath);
        }

        // Generate all the card variants
        GenerateBasicCards();
        GenerateStatusEffectCards();
        GenerateComboCards();
        GenerateDiscardEffectCards();
        GenerateCostModificationCards();
        GenerateTemporaryUpgradeCards();
        GenerateHealthCards();
        GenerateSpecialCards();
        
        // New card categories
        GeneratePetCards();
        GenerateAdvancedComboCards();
        GenerateDefensiveSpecialtyCards();
        GenerateHighRiskCards();
        GenerateScalingCards();
        GenerateDeckManipulationCards();
        GenerateUtilityCards();
        GenerateLegendaryCards();

        // Refresh the asset database to show the new cards
        AssetDatabase.Refresh();
        Debug.Log("Card generation complete!");
    }

    private static void GenerateBasicCards()
    {
        // Basic Attack Cards
        CreateCard("Botanical Lacerator", "Deal 6 damage.", 1, card => {
            card.damage = 6;
        }, "Botanical Lacerator+", "Deal 9 damage.", 1, card => {
            card.damage = 9;
        });

        CreateCard("Mycological Rupture", "Deal 10 damage.", 2, card => {
            card.damage = 10;
        }, "Mycological Rupture+", "Deal 14 damage.", 2, card => {
            card.damage = 14;
        });

        // Basic Block Cards
        CreateCard("Sporal Carapace", "Gain 5 block.", 1, card => {
            card.block = 5;
        }, "Sporal Carapace+", "Gain 8 block.", 1, card => {
            card.block = 8;
        });

        CreateCard("Luminescent Shell", "Gain 12 block.", 2, card => {
            card.block = 12;
        }, "Luminescent Shell+", "Gain 16 block.", 2, card => {
            card.block = 16;
        });

        // Energy and Draw Cards
        CreateCard("Phosphorescent Essence", "Gain 2 energy.", 0, card => {
            card.energyGain = 2;
        }, "Phosphorescent Essence+", "Gain 3 energy.", 0, card => {
            card.energyGain = 3;
        });

        CreateCard("Chromomantic Insight", "Draw 2 cards.", 1, card => {
            card.drawAmount = 2;
        }, "Chromomantic Insight+", "Draw 3 cards.", 1, card => {
            card.drawAmount = 3;
        });

        // Discard Cards
        CreateCard("Parasitic Intrusion", "Opponent discards 1 random card.", 1, card => {
            card.discardRandomAmount = 1;
        }, "Parasitic Intrusion+", "Opponent discards 2 random cards.", 1, card => {
            card.discardRandomAmount = 2;
        });
    }

    private static void GenerateStatusEffectCards()
    {
        // Weak Effect
        CreateCard("Miasmatic Vapors", "Apply 2 Weak to enemy pet.", 1, card => {
            card.statusToApply = StatusEffectType.Weak;
            card.statusDuration = 2;
            card.statusPotency = 50; // 50% less damage
        }, "Miasmatic Vapors+", "Apply 3 Weak to enemy pet.", 1, card => {
            card.statusToApply = StatusEffectType.Weak;
            card.statusDuration = 3;
            card.statusPotency = 50;
        });

        // Break Effect
        CreateCard("Calcified Dissolution", "Apply 2 Break to enemy pet.", 1, card => {
            card.statusToApply = StatusEffectType.Break;
            card.statusDuration = 2;
            card.statusPotency = 50; // 50% more damage taken
        }, "Calcified Dissolution+", "Apply 3 Break to enemy pet.", 1, card => {
            card.statusToApply = StatusEffectType.Break;
            card.statusDuration = 3;
            card.statusPotency = 50;
        });

        // DoT Effect
        CreateCard("Irradiant Spores", "Apply 3 damage per turn for 3 turns.", 2, card => {
            card.dotDamageAmount = 3;
            card.dotDuration = 3;
        }, "Irradiant Spores+", "Apply 4 damage per turn for 4 turns.", 2, card => {
            card.dotDamageAmount = 4;
            card.dotDuration = 4;
        });
    }

    private static void GenerateComboCards()
    {
        // Combo Starter that deals damage
        CreateCard("Hypertrophic Sequence", "Deal 4 damage. Combo: After 3 combo cards, deal 10 damage.", 1, card => {
            card.damage = 4;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 10;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
        }, "Hypertrophic Sequence+", "Deal 5 damage. Combo: After 3 combo cards, deal 15 damage.", 1, card => {
            card.damage = 5;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 15;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
        });

        // Combo Starter that grants block
        CreateCard("Chitin Synthesis", "Gain 4 block. Combo: After 3 combo cards, gain 10 block.", 1, card => {
            card.block = 4;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.GainBlock;
            card.comboEffectValue = 10;
            card.comboEffectTarget = CardDropZone.TargetType.PlayerSelf;
        }, "Chitin Synthesis+", "Gain 5 block. Combo: After 3 combo cards, gain 15 block.", 1, card => {
            card.block = 5;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.GainBlock;
            card.comboEffectValue = 15;
            card.comboEffectTarget = CardDropZone.TargetType.PlayerSelf;
        });

        // Combo card that draws
        CreateCard("Prismatic Illumination", "Draw 1 card. Combo: After 3 combo cards, draw 3 cards.", 1, card => {
            card.drawAmount = 1;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DrawCard;
            card.comboEffectValue = 3;
        }, "Prismatic Illumination+", "Draw 2 cards. Combo: After 3 combo cards, draw 4 cards.", 1, card => {
            card.drawAmount = 2;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DrawCard;
            card.comboEffectValue = 4;
        });
    }

    private static void GenerateDiscardEffectCards()
    {
        // Discard effect - Deal damage when discarded
        CreateCard("Volatile Spores", "Deal 3 damage. When discarded, deal 5 damage.", 1, card => {
            card.damage = 3;
            card.discardEffectType = DiscardEffectType.DealDamageToOpponentPet;
            card.discardEffectValue = 5;
        }, "Volatile Spores+", "Deal 4 damage. When discarded, deal 7 damage.", 1, card => {
            card.damage = 4;
            card.discardEffectType = DiscardEffectType.DealDamageToOpponentPet;
            card.discardEffectValue = 7;
        });

        // Discard effect - Gain block when discarded
        CreateCard("Emergent Membrane", "Gain 4 block. When discarded, gain 3 block.", 1, card => {
            card.block = 4;
            card.discardEffectType = DiscardEffectType.GainBlockPlayer;
            card.discardEffectValue = 3;
        }, "Emergent Membrane+", "Gain 5 block. When discarded, gain 5 block.", 1, card => {
            card.block = 5;
            card.discardEffectType = DiscardEffectType.GainBlockPlayer;
            card.discardEffectValue = 5;
        });

        // Discard effect - Draw when discarded
        CreateCard("Cryptic Reflexes", "Draw 1 card. When discarded, draw 1 card.", 1, card => {
            card.drawAmount = 1;
            card.discardEffectType = DiscardEffectType.DrawCard;
            card.discardEffectValue = 1;
        }, "Cryptic Reflexes+", "Draw 2 cards. When discarded, draw 2 cards.", 1, card => {
            card.drawAmount = 2;
            card.discardEffectType = DiscardEffectType.DrawCard;
            card.discardEffectValue = 2;
        });
    }

    private static void GenerateCostModificationCards()
    {
        // Reduce cost of other cards
        CreateCard("Chronofluid Infusion", "Your cards cost 1 less this turn.", 2, card => {
            card.costChangeTarget = CostChangeTargetType.PlayerHand;
            card.costChangeAmount = -1;
            card.costChangeDuration = 1;
            card.costChangeCardCount = 0; // All cards
        }, "Chronofluid Infusion+", "Your cards cost 1 less for 2 turns.", 2, card => {
            card.costChangeTarget = CostChangeTargetType.PlayerHand;
            card.costChangeAmount = -1;
            card.costChangeDuration = 2;
            card.costChangeCardCount = 0;
        });

        // Increase cost of opponent cards
        CreateCard("Entropic Disruption", "Enemy cards cost 1 more next turn.", 2, card => {
            card.costChangeTarget = CostChangeTargetType.OpponentHand;
            card.costChangeAmount = 1;
            card.costChangeDuration = 1;
            card.costChangeCardCount = 0; // All cards
        }, "Entropic Disruption+", "Enemy cards cost 1 more for 2 turns.", 2, card => {
            card.costChangeTarget = CostChangeTargetType.OpponentHand;
            card.costChangeAmount = 1;
            card.costChangeDuration = 2;
            card.costChangeCardCount = 0;
        });
    }

    private static void GenerateTemporaryUpgradeCards()
    {
        // Temporary upgrade random cards
        CreateCard("Metamorphic Catalyst", "Upgrade 2 random cards in hand for this turn.", 1, card => {
            card.upgradeHandCardCount = 2;
            card.upgradeHandCardDuration = 1;
            card.upgradeHandCardTargetRule = UpgradeTargetRule.Random;
        }, "Metamorphic Catalyst+", "Upgrade 3 random cards in hand for this turn.", 1, card => {
            card.upgradeHandCardCount = 3;
            card.upgradeHandCardDuration = 1;
            card.upgradeHandCardTargetRule = UpgradeTargetRule.Random;
        });

        // Temporary upgrade most expensive cards
        CreateCard("Biological Transmutation", "Upgrade your most expensive card in hand for this combat.", 1, card => {
            card.upgradeHandCardCount = 1;
            card.upgradeHandCardDuration = 0; // Rest of combat
            card.upgradeHandCardTargetRule = UpgradeTargetRule.MostExpensive;
        }, "Biological Transmutation+", "Upgrade your 2 most expensive cards in hand for this combat.", 1, card => {
            card.upgradeHandCardCount = 2;
            card.upgradeHandCardDuration = 0;
            card.upgradeHandCardTargetRule = UpgradeTargetRule.MostExpensive;
        });
        
        // Critical hit chance
        CreateCard("Predatory Instinct", "Gain +15% critical hit chance for this combat.", 1, card => {
            card.critChanceBuffTarget = CardDropZone.TargetType.PlayerSelf;
            card.critChanceBuffAmount = 15;
            card.critChanceBuffDuration = 0; // 0 = end of combat
        }, "Predatory Instinct+", "Gain +25% critical hit chance for this combat.", 1, card => {
            card.critChanceBuffTarget = CardDropZone.TargetType.PlayerSelf;
            card.critChanceBuffAmount = 25;
            card.critChanceBuffDuration = 0;
        });
    }

    private static void GenerateHealthCards()
    {
        // Healing cards
        CreateCard("Bioluminescent Regeneration", "Restore 4 health.", 1, card => {
            card.healingAmount = 4;
        }, "Bioluminescent Regeneration+", "Restore 6 health.", 1, card => {
            card.healingAmount = 6;
        });

        // Temporary max health increase
        CreateCard("Hypertrophic Growth", "Increase max health by 5 for this combat.", 2, card => {
            card.tempMaxHealthChange = 5;
        }, "Hypertrophic Growth+", "Increase max health by 8 for this combat.", 2, card => {
            card.tempMaxHealthChange = 8;
        });
    }

    private static void GenerateSpecialCards()
    {
        // Multi-effect card: Damage + Block
        CreateCard("Chitinous Extrusion", "Deal 5 damage and gain 3 block.", 1, card => {
            card.damage = 5;
            card.block = 3;
        }, "Chitinous Extrusion+", "Deal 7 damage and gain 5 block.", 1, card => {
            card.damage = 7;
            card.block = 5;
        });

        // Multi-effect card: Damage + Status
        CreateCard("Necrotic Tendrils", "Deal 4 damage and apply 2 Weak.", 1, card => {
            card.damage = 4;
            card.statusToApply = StatusEffectType.Weak;
            card.statusDuration = 2;
            card.statusPotency = 50;
        }, "Necrotic Tendrils+", "Deal 6 damage and apply 2 Weak.", 1, card => {
            card.damage = 6;
            card.statusToApply = StatusEffectType.Weak;
            card.statusDuration = 2;
            card.statusPotency = 50;
        });

        // Multi-effect card: Damage + DoT
        CreateCard("Fungal Infestation", "Deal 3 damage and apply 2 damage per turn for 2 turns.", 1, card => {
            card.damage = 3;
            card.dotDamageAmount = 2;
            card.dotDuration = 2;
        }, "Fungal Infestation+", "Deal 5 damage and apply 3 damage per turn for 2 turns.", 1, card => {
            card.damage = 5;
            card.dotDamageAmount = 3;
            card.dotDuration = 2;
        });

        // Multi-effect card: Energy + Draw
        CreateCard("Ethereal Insights", "Gain 1 energy and draw 2 cards.", 0, card => {
            card.energyGain = 1;
            card.drawAmount = 2;
        }, "Ethereal Insights+", "Gain 2 energy and draw 2 cards.", 0, card => {
            card.energyGain = 2;
            card.drawAmount = 2;
        });

        // Pet-targeted card
        CreateCard("Symbiotic Membrane", "Give your pet 8 block.", 1, card => {
            card.block = 8;
            // This would need a specific way to target your pet
        }, "Symbiotic Membrane+", "Give your pet 12 block.", 1, card => {
            card.block = 12;
        });
        
        // Multi-effect card: Attack + Energy
        CreateCard("Bioluminescent Assault", "Deal 5 damage and gain 1 energy.", 1, card => {
            card.damage = 5;
            card.energyGain = 1;
        }, "Bioluminescent Assault+", "Deal 7 damage and gain 1 energy.", 1, card => {
            card.damage = 7;
            card.energyGain = 1;
        });
        
        // Multi-effect card: Block + Draw
        CreateCard("Crystalline Lattice", "Gain 4 block and draw 1 card.", 1, card => {
            card.block = 4;
            card.drawAmount = 1;
        }, "Crystalline Lattice+", "Gain 6 block and draw 1 card.", 1, card => {
            card.block = 6;
            card.drawAmount = 1;
        });
        
        // Triple-effect card
        CreateCard("Adaptive Evolution", "Deal 4 damage, gain 3 block, and draw 1 card.", 2, card => {
            card.damage = 4;
            card.block = 3;
            card.drawAmount = 1;
        }, "Adaptive Evolution+", "Deal 6 damage, gain 4 block, and draw 1 card.", 2, card => {
            card.damage = 6;
            card.block = 4;
            card.drawAmount = 1;
        });
    }

    private static void GeneratePetCards()
    {
        // Pet healing
        CreateCard("Mutualistic Grafting", "Heal your pet for 5 HP.", 1, card => {
            card.healingAmount = 5;
            // Would need a way to target your pet
        }, "Mutualistic Grafting+", "Heal your pet for 8 HP.", 1, card => {
            card.healingAmount = 8;
        });
        
        // Pet buff damage
        CreateCard("Adrenaline Secretion", "Your pet deals 50% more damage for 2 turns.", 2, card => {
            card.statusToApply = StatusEffectType.None; // Would need a custom status
            card.statusDuration = 2;
            card.statusPotency = 50;
        }, "Adrenaline Secretion+", "Your pet deals 50% more damage for 3 turns.", 2, card => {
            card.statusToApply = StatusEffectType.None;
            card.statusDuration = 3;
            card.statusPotency = 50;
        });
        
        // Pet max health
        CreateCard("Cellular Augmentation", "Increase your pet's max health by 6 for this combat.", 2, card => {
            card.tempMaxHealthChange = 6;
            // Would need a way to target your pet
        }, "Cellular Augmentation+", "Increase your pet's max health by 10 for this combat.", 2, card => {
            card.tempMaxHealthChange = 10;
        });
    }
    
    private static void GenerateAdvancedComboCards()
    {
        // Dual combo effects
        CreateCard("Shimmer Cascade", "Deal 3 damage. Combo: After 3 combo cards, deal 8 damage and gain 8 block.", 1, card => {
            card.damage = 3;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 8;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
            // Would need a way to support secondary combo effects
        }, "Shimmer Cascade+", "Deal 4 damage. Combo: After 3 combo cards, deal 10 damage and gain 10 block.", 1, card => {
            card.damage = 4;
            card.isComboStarter = true;
            card.comboTriggerValue = 3;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 10;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
        });
        
        // Lower combo requirement
        CreateCard("Catalytic Reaction", "Deal 3 damage. Combo: After 2 combo cards, deal 6 damage.", 1, card => {
            card.damage = 3;
            card.isComboStarter = true;
            card.comboTriggerValue = 2;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 6;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
        }, "Catalytic Reaction+", "Deal 4 damage. Combo: After 2 combo cards, deal 8 damage.", 1, card => {
            card.damage = 4;
            card.isComboStarter = true;
            card.comboTriggerValue = 2;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 8;
            card.comboEffectTarget = CardDropZone.TargetType.EnemyPet;
        });
    }
    
    private static void GenerateDefensiveSpecialtyCards()
    {
        // Thorns-like effect
        CreateCard("Retaliatory Thorns", "Gain 4 block. When attacked, deal 3 damage back.", 1, card => {
            card.block = 4;
            // Would need a way to handle counter effects
        }, "Retaliatory Thorns+", "Gain 6 block. When attacked, deal 4 damage back.", 1, card => {
            card.block = 6;
        });
        
        // Block retention
        CreateCard("Ossified Barrier", "Gain 5 block. Block doesn't expire this turn.", 1, card => {
            card.block = 5;
            // Would need a way to handle block retention
        }, "Ossified Barrier+", "Gain 8 block. Block doesn't expire this turn.", 1, card => {
            card.block = 8;
        });
        
        // Block multiplier
        CreateCard("Exoskeletal Amplification", "Double your current block.", 2, card => {
            // Would need a way to check and modify current block
        }, "Exoskeletal Amplification+", "Triple your current block.", 2, card => {
            // More advanced version
        });
    }
    
    private static void GenerateHighRiskCards()
    {
        // Self-damage for benefit
        CreateCard("Hemophagic Symbiosis", "Take 3 damage and draw 2 cards.", 0, card => {
            card.drawAmount = 2;
            // Would need a way to damage self
        }, "Hemophagic Symbiosis+", "Take 3 damage and draw 3 cards.", 0, card => {
            card.drawAmount = 3;
        });
        
        // Exhaust for power
        CreateCard("Cellular Cannibalism", "Discard your hand. Gain 1 energy for each card discarded.", 1, card => {
            // Would need a way to count and handle discard
        }, "Cellular Cannibalism+", "Discard your hand. Gain 1 energy and draw 1 card for each card discarded.", 1, card => {
            // More powerful effect
        });
        
        // Delayed power
        CreateCard("Cryptic Incubation", "At the end of your 3rd turn, deal 20 damage.", 2, card => {
            // Would need a way to track turns
        }, "Cryptic Incubation+", "At the end of your 3rd turn, deal 30 damage.", 2, card => {
            // More powerful version
        });
    }
    
    private static void GenerateScalingCards()
    {
        // Scaling with damage taken
        CreateCard("Traumatic Adaptation", "Deal damage equal to the damage you've taken this combat.", 1, card => {
            // Would need a way to track damage taken
        }, "Traumatic Adaptation+", "Deal damage equal to 1.5x the damage you've taken this combat.", 1, card => {
            // More powerful version
        });
        
        // Scaling with cards played
        CreateCard("Kaleidoscopic Culmination", "Deal 2 damage for each card played this turn.", 1, card => {
            // Would need a way to track cards played
        }, "Kaleidoscopic Culmination+", "Deal 3 damage for each card played this turn.", 1, card => {
            // More powerful version
        });
        
        // Scaling with energy spent
        CreateCard("Bioluminescent Discharge", "Deal damage equal to your spent energy this turn.", 0, card => {
            // Would need a way to track spent energy
        }, "Bioluminescent Discharge+", "Deal damage equal to 2x your spent energy this turn.", 0, card => {
            // More powerful version
        });
    }
    
    private static void GenerateDeckManipulationCards()
    {
        // Deck search
        CreateCard("Mnemonic Extraction", "Search your draw pile for a card and put it into your hand.", 1, card => {
            // Would need a way to search deck
        }, "Mnemonic Extraction+", "Search your draw pile for a card and put it into your hand. It costs 1 less this turn.", 1, card => {
            // More powerful version
        });
        
        // Recycle from discard
        CreateCard("Genetic Reclamation", "Put a card from your discard pile into your hand.", 1, card => {
            // Would need a way to interact with discard pile
        }, "Genetic Reclamation+", "Put a card from your discard pile into your hand. It costs 1 less this turn.", 1, card => {
            // More powerful version
        });
        
        // Top deck manipulation
        CreateCard("Prescient Mutation", "Look at the top 3 cards of your draw pile. You may put them back in any order.", 1, card => {
            // Would need a way to view and rearrange draw pile
        }, "Prescient Mutation+", "Look at the top 5 cards of your draw pile. You may put them back in any order.", 1, card => {
            // More powerful version
        });
    }
    
    private static void GenerateUtilityCards()
    {
        // Exhaust prevention
        CreateCard("Entropic Reversal", "The next card you play this turn isn't discarded after use.", 1, card => {
            // Would need a way to handle exhaust prevention
        }, "Entropic Reversal+", "The next 2 cards you play this turn aren't discarded after use.", 1, card => {
            // More powerful version
        });
        
        // Card copy
        CreateCard("Recursive Replication", "Choose a card in your hand. Add a temporary copy to your hand.", 1, card => {
            // Would need a way to copy cards
        }, "Recursive Replication+", "Choose a card in your hand. Add a temporary copy to your hand that costs 1 less.", 1, card => {
            // More powerful version
        });
        
        // Enemy targeting
        CreateCard("Optical Aberration", "Your next attack deals double damage.", 2, card => {
            // Would need a way to enhance next attack
        }, "Optical Aberration+", "Your next 2 attacks deal double damage.", 2, card => {
            // More powerful version
        });
    }
    
    private static void GenerateLegendaryCards()
    {
        // Extremely powerful but expensive card
        CreateCard("The Annihilation", "Deal 30 damage. Apply 3 Break.", 4, card => {
            card.damage = 30;
            card.statusToApply = StatusEffectType.Break;
            card.statusDuration = 3;
            card.statusPotency = 50;
        }, "The Annihilation+", "Deal 40 damage. Apply 3 Break.", 4, card => {
            card.damage = 40;
            card.statusToApply = StatusEffectType.Break;
            card.statusDuration = 3;
            card.statusPotency = 50;
        });
        
        // Multi-effect powerful defensive card
        CreateCard("Atavistic Revival", "Gain 20 block. Heal 5 health. Draw 2 cards.", 3, card => {
            card.block = 20;
            card.healingAmount = 5;
            card.drawAmount = 2;
        }, "Atavistic Revival+", "Gain 25 block. Heal 8 health. Draw 3 cards.", 3, card => {
            card.block = 25;
            card.healingAmount = 8;
            card.drawAmount = 3;
        });
        
        // Powerful pet synergy card
        CreateCard("Biothaumaturgic Link", "You and your pet each gain 12 block and heal 6 health.", 3, card => {
            card.block = 12;
            card.healingAmount = 6;
            // Would need a way to affect both player and pet
        }, "Biothaumaturgic Link+", "You and your pet each gain 15 block and heal 8 health.", 3, card => {
            card.block = 15;
            card.healingAmount = 8;
        });
    }

    private static void CreateCard(
        string cardName, string description, int cost, System.Action<CardData> setupBase,
        string upgradedName, string upgradedDescription, int upgradedCost, System.Action<CardData> setupUpgraded)
    {
        // Create the upgraded version first (so we can link to it)
        CardData upgradedCard = ScriptableObject.CreateInstance<CardData>();
        upgradedCard.cardName = cardName; // Keep the base name, adding '+' in the filename
        upgradedCard.cost = upgradedCost;
        upgradedCard.description = upgradedDescription;
        
        // Apply the setup for the upgraded card
        setupUpgraded(upgradedCard);
        
        // Save the upgraded card to the asset database
        string upgradedPath = Path.Combine(CardSavePath, $"{upgradedName}.asset");
        AssetDatabase.CreateAsset(upgradedCard, upgradedPath);
        
        // Create the base card
        CardData baseCard = ScriptableObject.CreateInstance<CardData>();
        baseCard.cardName = cardName;
        baseCard.cost = cost;
        baseCard.description = description;
        baseCard.upgradedVersion = upgradedCard; // Link to the upgraded version
        
        // Apply the setup for the base card
        setupBase(baseCard);
        
        // Save the base card to the asset database
        string basePath = Path.Combine(CardSavePath, $"{cardName}.asset");
        AssetDatabase.CreateAsset(baseCard, basePath);
        
        Debug.Log($"Created card: {cardName} with upgraded version: {upgradedName}");
    }
} 