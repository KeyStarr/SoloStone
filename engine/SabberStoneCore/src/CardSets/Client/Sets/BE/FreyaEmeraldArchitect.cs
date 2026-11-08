using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FreyaEmeraldArchitect
    {
        internal const int DbfId = 129937;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var minion in c.HandZone.GetAll().Concat(c.DeckZone.GetAll()).OfType<Minion>().Where(m => m.IsRace(Race.BEAST)))
                    ctx.GiveBuff(minion.Id, "BE_856e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.MUL, 2), Health.Effect(EffectOperator.MUL, 2)) };
        }
    }
}
