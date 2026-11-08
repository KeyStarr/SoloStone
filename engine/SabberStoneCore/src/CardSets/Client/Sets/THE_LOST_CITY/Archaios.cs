using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Archaios
    {
        internal const int DbfId = 117232;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s is Minion source && t is Minion attacker)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_811e"), source, attacker, source.Health, 0, 0);
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetMaxHealth(0)) { UseScriptTag = true } };
        }
    }
}
