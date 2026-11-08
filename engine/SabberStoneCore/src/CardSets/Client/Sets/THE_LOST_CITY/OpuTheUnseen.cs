using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OpuTheUnseen
    {
        [StandardCard("TLC_522", 119532)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            var fan = new CustomTask((g,c,s,t,stack) => {
                var spell = Entity.FromCard(c, Cards.FromId("EX1_129"), creator: s);
                new EffectContext(g,c.Id,s.Id,stack: stack).CastSpell(spell.Id, HistoryCastKind.Effect);
            });
            card.Power = new Power { PowerTask = fan, ComboTask = fan, DeathrattleTask = fan };
        }
    }
}
