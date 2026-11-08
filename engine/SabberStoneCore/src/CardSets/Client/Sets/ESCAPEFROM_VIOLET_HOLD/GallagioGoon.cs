using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GallagioGoon
    {
        internal const int DbfId = 126409;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Same AFTER_PLAY_MINION condition as Rumbling Elemental (LOE_016).
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = SelfCondition.IsBattlecryMinion,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (t is Minion m && m.Zone == c.BoardZone && !m.ToBeDestroyed)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_802e"), (IPlayable)s, m);
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1)) };
        }
    }
}
