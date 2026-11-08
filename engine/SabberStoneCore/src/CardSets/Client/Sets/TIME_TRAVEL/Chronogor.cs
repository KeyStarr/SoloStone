using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Chronogor
    {
        [StandardCard("TIME_032", 120491)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var recipient in new[] { c, c.Opponent })
                    for (int i = 0; i < 2 && !c.DeckZone.IsEmpty && g.State != State.COMPLETE; i++)
                    {
                        int extreme = recipient == c ? c.DeckZone.Max(p => p.Cost) : c.DeckZone.Min(p => p.Cost);
                        var ties = c.DeckZone.Where(p => p.Cost == extreme).ToArray();
                        var chosen = ties.Length == 1 ? ties[0] : ties[g.Random.Next(ties.Length)];
                        Generic.DrawFrom(recipient, c, chosen);
                    }
            }) };
        }
    }
}
