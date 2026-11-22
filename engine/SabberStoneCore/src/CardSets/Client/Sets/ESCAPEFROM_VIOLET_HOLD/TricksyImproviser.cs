using System;
using System.Collections.Generic;
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
    internal static class TricksyImproviser
    {
        internal const int DbfId = 126253;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(HasCastThisTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!HasCastThisTurn((IPlayable)s)) return;
                // Unpinned (owner ruling 2026-11-18, #179): never a Secret already active for this player, nor one this cast already picked.
                var picked = new List<string>();
                for (int i = 0; i < 2; i++)
                {
                    var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(EscapeFromVioletHoldPools.MageSecret(),
                        card => picked.Contains(card.Id) || c.SecretZone.Any(active => active.Card.Id == card.Id), DbfId);
                    if (chosen == null) continue;
                    picked.Add(chosen.Id);
                    var spell = Entity.FromCard(c, chosen, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                    new PlayTask(PlayType.SPELL, true).Process(g, c, s, null, new TaskStack { Playables = new[] { spell } });
                }
            }) };
        }

        private static bool HasCastThisTurn(IPlayable source)
        {
            var query = source.Game.HistoryFacts.Query;
            return query.Casts(query.ThisTurn).Any(f => f.ControllerId == source.Controller.Id && f.Outcome == HistoryCastOutcome.Completed);
        }
    }
}
