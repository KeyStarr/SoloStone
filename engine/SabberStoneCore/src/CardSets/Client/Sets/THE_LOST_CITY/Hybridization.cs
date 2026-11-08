using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hybridization
    {
        internal const int DbfId = 117656;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                var captured = new List<Playable>();
                for (int cost = 1; cost <= 4; cost++)
                {
                    var candidates = c.DeckZone.Where(p => p.Card.Type == CardType.MINION && p.Cost == cost).ToArray();
                    if (candidates.Length == 0) continue;
                    var drawn = candidates[g.Random.Next(candidates.Length)];
                    if (candidates.Length > 1) g.OnRandomHappened(true);
                    Generic.Draw(c, drawn);
                    if (drawn.Zone == c.HandZone) captured.Add((Playable)drawn);
                }
                // Only this effect's surviving recipients, never a later hand scan.
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats; i++)
                    foreach (var drawn in captured.Where(p => p.Zone == c.HandZone))
                        CostRules.AddModifier(drawn, EffectOperator.SUB, 1);
            }) };
        }
    }
}
