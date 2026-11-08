using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AlAkirLordOfStorms
    {
        internal const int DbfId = 125442;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_153t", "CATA_153t1");
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                stack = new TaskStack { Number = ((Minion)s).AttackDamage };
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(CataclysmPools.AttackCostMinion(), PoolCostBinding.StackNumber, DbfId);
                    if (chosen == null) continue;
                    var generated = (Playable)Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    CostRules.AddModifier(generated, EffectOperator.SET, 1);
                    Generic.AddHandPhase(c, generated);
                }
            }) };
        }
        internal static void ConfigureHand(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new AdjacentAura(GameTag.TAG_SCRIPT_DATA_NUM_1) };
        }
    }
}
