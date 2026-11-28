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
    // Legacy (Wild) cards whose legacy definition was left empty, completed where run content needs them (strict admission:
    // every card a run mode offers must be implemented).
    internal static class LegacyWildCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Power of the Wild (Dungeon Run's Druid start deck): Choose One - its two halves (EX1_160a/b, shared with the
            // Core reprint CORE_EX1_160) carry the effects, as for the reprint.
            register(503, "EX1_160", card => card.Implemented = true);

            // Wrath (Blackseed's and Elder Jari's decks): Choose One - its halves (EX1_154a/b) are shared with the Core reprint.
            register(836, "EX1_154", card => card.Implemented = true);

            // Mogor's Champion (Whompwhisker's deck): 50% chance to attack the wrong enemy (Forgetful).
            register(2491, "AT_088", card => {
                card.Power = new Power { Trigger = Actions.ForgetfulRules.Trigger() };
                card.Implemented = true;
            });

            // King of Beasts (Bristlesnarl's deck).
            register(2014, "GVG_046", card => {
                card.Power = new Power {
                    InfoCardId = "GVG_046e",
                    PowerTask = ComplexTask.Create(
                        new IncludeTask(EntityType.MINIONS_NOSOURCE),
                        new FilterStackTask(SelfCondition.IsRace(Race.BEAST)),
                        new CountTask(EntityType.STACK),
                        new AddEnchantmentTask("GVG_046e", EntityType.SOURCE, true))
                };
                card.Implemented = true;
            });
            register(2202, "GVG_046e", card => {
                card.Power = new Power { Enchant = Enchants.Enchants.AddAttackScriptTag };
                card.Implemented = true;
            });
            // Animated Armor (Chronomancer Inara's deck): Your hero can only take 1 damage at a time.
            register(36111, "LOE_119", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.PREDAMAGE) {
                        TriggerSource = TriggerSource.HERO,
                        SingleTask = new FuncNumberTask(p => {
                            var data = p.Game.CurrentEventData;
                            if (data.EventNumber > 1) data.EventNumber = 1;
                            return 0;
                        })
                    }
                };
                card.Implemented = true;
            });
        }
    }
}
