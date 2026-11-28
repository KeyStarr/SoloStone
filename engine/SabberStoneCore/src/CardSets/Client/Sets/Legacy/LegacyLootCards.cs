using System;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets.Legacy
{
    // Gelbin's inventions are needed by Dungeon Run's Battlecrier bucket.
    internal static class LegacyLootCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Gelbin Mekkatorque (Shaman Battlecrier): Battlecry: Summon an AWESOME invention (one of Mekka1-4, its entourage).
            register(858, "EX1_112", card => {
                card.Power = new Power { PowerTask = ComplexTask.Create(new RandomEntourageTask(), new SummonTask(SummonSide.RIGHT)) };
                card.Implemented = true;
            });
            // Homing Chicken: At the start of your turn, destroy this minion and draw 3 cards.
            register(227, "Mekka1", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(new DestroyTask(EntityType.SOURCE), new DrawTask(3))
                    }
                };
                card.Implemented = true;
            });
            // Repair Bot: At the end of your turn, restore 6 Health to a damaged character.
            register(329, "Mekka2", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_END) {
                        SingleTask = ComplexTask.Create(
                            new IncludeTask(EntityType.ALL),
                            new FilterStackTask(SelfCondition.IsDamaged),
                            new RandomTask(1, EntityType.STACK),
                            new HealTask(6, EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            // Emboldener 3000: At the end of your turn, give a random minion +1/+1.
            register(52, "Mekka3", card => {
                card.Power = new Power {
                    InfoCardId = "Mekka3e",
                    Trigger = new Trigger(TriggerType.TURN_END) {
                        SingleTask = ComplexTask.Create(
                            new RandomTask(1, EntityType.ALLMINIONS),
                            new AddEnchantmentTask("Mekka3e", EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            register(346, "Mekka3e", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
                card.Implemented = true;
            });
            // Poultryizer: At the start of your turn, transform a random minion into a 1/1 Chicken.
            register(146, "Mekka4", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(
                            new RandomTask(1, EntityType.ALLMINIONS),
                            new TransformTask("Mekka4t", EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            register(262, "Mekka4t", card => card.Implemented = true);
        }
    }
}
