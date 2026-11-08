using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SliceAndDice
    {
        [StandardCard("JAIL_500", 125983)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var cards = g.HistoryFacts.Query.Plays().Where(f => f.ControllerId == c.Id && f.Turn == g.Turn
                    && f.DefinitionKnown && f.DbfId != 125983).Select(f => f.Definition).ToArray();
                for (int i = cards.Length - 1; i > 0; i--)
                {
                    int j = g.Random.Next(i + 1);
                    (cards[i], cards[j]) = (cards[j], cards[i]);
                }
                foreach (var definition in cards)
                {
                    if (g.State == State.COMPLETE) break;
                    var replay = Entity.FromCard(c, definition.Materialize(), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, c.SetasideZone);
                    AutoPlayRules.Play(replay, s, preferEnemies: true);
                }
                AutoPlayRules.RequestTurnEnd(c);
            }) };
        }
    }
}
