using System.Collections.Generic;
using SabberStoneCore.Enums;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Timestop
    {
        internal const int DbfId = 120389;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                if (t != null) ctx.Damage(t.Id, 3, spellDamage: true);
                var selected = new TaskStack { Playables = c.Opponent.BoardZone.GetAll(m => !m.IsDead).Cast<IPlayable>().ToArray() };
                if (selected.Playables.Count == 0) return;
                new RandomTask(2, EntityType.STACK).Process(g, c, s, t, selected);
                ComplexTask.Freeze(EntityType.STACK).Process(g, c, s, t, selected);
            }) };
        }
    }
}
