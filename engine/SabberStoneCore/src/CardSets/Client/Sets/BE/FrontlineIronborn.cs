using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FrontlineIronborn
    {
        internal const int DbfId = 131432;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.OP_MINIONS,
                OrderDeathByPlay = true,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (g.CurrentPlayer != c || !(s is Minion m) || m.Zone != c.BoardZone || m.IsDead) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_996e"), m, m);
                    BonusEffects.GiveRandom(m,m,1);
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
