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
    internal static class VioletTreasuregill
    {
        internal const int DbfId = 117562;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var spells = c.DeckZone.OfType<Spell>().Where(p => p.Cost <= 2).ToArray();
                if (spells.Length == 0) return;
                var selected = spells[g.Random.Next(spells.Length)];
                new PlayTask(PlayType.SPELL, true, preferSource: true).Process(g, c, s, null,
                    new TaskStack { Playables = new[] { (IPlayable)selected } });
            }) };
        }
    }
}
