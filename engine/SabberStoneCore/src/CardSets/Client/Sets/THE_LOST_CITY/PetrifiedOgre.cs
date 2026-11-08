using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PetrifiedOgre
    {
        internal const int DbfId = 118254;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Dormant = new DormantDefinition(null, "TLC_253e", "TLC_253e1");
            card.Power = new Power();
        }
        internal static void ConfigureListener(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Enchantment helper) || !(helper.Target is Minion parent) || parent.IsSilenced
                        || !parent.IsDormant || parent.Controller != c || g.CurrentPlayer != c
                        || parent.Zone != c.BoardZone || !c.BoardZone.Slots.Contains(parent)
                        || !parent.AppliedEnchantments.Contains(helper)) return;
                    if (g.Random.Next(2) == 0) DormantRules.Awaken(parent);
                    else Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_253e2"), parent, parent);
                })
            } };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
