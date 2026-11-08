using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class Tracking
    {
        internal const int DbfId = 69545;
        internal static void Configure(Card card)
        {
            // Rules: https://hearthstone.wiki.gg/wiki/Discover — discovering from deck draws the chosen card.
            // Use real entities and the indexed draw primitive to retain buffs and fire on-draw effects.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var remaining = c.DeckZone.ToList();
                var picks = new List<int>();
                while (remaining.Count > 0 && picks.Count < 3)
                {
                    var pick = remaining.Choose(g.Random);
                    picks.Add(pick.Id);
                    remaining.RemoveAll(p => p.Card.Id == pick.Card.Id);
                }
                if (picks.Count > 0)
                {
                    Generic.CreateChoice(c, s, ChoiceType.GENERAL, ChoiceAction.STACK, picks);
                    c.Choice.AfterChooseTask = new CustomTask((game, player, source, chosen, _) =>
                        Generic.Draw(player, player.DeckZone.ToList().IndexOf(chosen)));
                }
            }) };
        }
    }
}
