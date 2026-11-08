using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KeeperOfFlame
    {
        internal const int DbfId = 115653;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var held in c.HandZone.OfType<Minion>().ToArray())
                {
                    Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_928e"), (IPlayable)s, held);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_928e3"), (IPlayable)s, held, 3);
                    held.AppliedEnchantments.Last(e => e.Card.Id == "FIR_928e3")[PayloadRules.Generation] = g.Turn;
                }
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true; card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3), Effects.Health_N(3)) };
        }
        internal static void ConfigureTimer(Card card)
        {
            card.Implemented = true; card.CopyHandTrigger = true; card.ExpiresOnHandExit = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var actor = (Enchantment)s; var held = (IPlayable)actor.Target;
                    if (held.Zone != held.Controller.HandZone || actor[PayloadRules.Generation] == g.Turn
                        || --actor[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0) return;
                    if (!DrawReplacementRules.DestroyOffboard(held)) held.Controller.GraveyardZone.Add(held.Controller.HandZone.Remove(held));
                })
            } };
        }
    }
}
