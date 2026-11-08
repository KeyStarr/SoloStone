using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WardenMaiev
    {
        internal const int DbfId = 126600;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion source) || source.Zone != c.BoardZone || source.IsSilenced || source.IsDormant
                        || !(t is Minion minion) || minion.Id == source.Id || minion.Controller != c
                        || minion.Zone != c.BoardZone || minion.ToBeDestroyed || minion.Health <= 0) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_850e2"), source, minion);
                    DormantRules.Make(minion, 1, "JAIL_850e", source);
                })
            } };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3), Effects.Health_N(3)) };
        }
    }
}
