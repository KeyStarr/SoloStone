using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheUnknowableOne
    {
        internal const int DbfId = 130122;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var minion in c.HandZone.GetAll().OfType<Minion>().Where(m => m.Card.Combo))
                    ctx.GiveBuff(minion.Id, "BE_725e2");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(6), Effects.SetMaxHealth(6)) };
        }
    }
}
