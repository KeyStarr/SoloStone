using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AstralMediator
    {
        internal const int DbfId = 129798;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var cast = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(BEPools.ThreeCostSpell(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var spell = Entity.FromCard(c, chosen, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                new PlayTask(PlayType.SPELL, true).Process(g, c, s, null, new TaskStack { Playables = new[] { spell } });
            });
            card.Power = new Power { PowerTask = cast, DeathrattleTask = cast };
        }
    }
}
