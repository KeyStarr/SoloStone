using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Charity
    {
        internal const int DbfId = 123625;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: R7 copies fresh death-form definitions in death order, retaining duplicates but no entity modifiers.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var deaths = ctx.History.Deaths(ctx.History.ThisTurn).Where(f => f.ControllerId == c.Id).ToArray();
                foreach (var death in deaths)
                {
                    var copy = Entity.FromCard(c, death.Definition.Materialize());
                    copy[GameTag.DISPLAYED_CREATOR] = s.Id;
                    if (Generic.AddHandPhase(c, copy)) ctx.GiveBuff(copy.Id, "MEND_805e");
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(3)) };
        }
    }
}
