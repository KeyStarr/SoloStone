using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AncientofYore
    {
        internal const int DbfId = 117302;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Dormant = new DormantDefinition(2, "EDR_979e", "EDR_979e2");
        }
        internal static void ConfigureListener(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Enchantment helper) || !(helper.Target is Minion parent) || parent.Controller != c
                        || g.CurrentPlayer != c || parent.Zone != c.BoardZone || !parent.IsDormant || parent.IsSilenced
                        || !parent.AppliedEnchantments.Contains(helper) || helper[GameTag.ATTACHED] != parent.Id) return;
                    c.Hero.GainArmor(parent, 3);
                    Generic.Draw(c);
                })
            } };
        }
    }
}
