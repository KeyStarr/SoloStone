using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DejaVu
    {
        [StandardCard("TIME_039", 120647)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("b14-h2/deja-vu/v1",
                new DiscoverThenContinueTask((g,c,s,stack) => {
                    PayloadRules.Clear(s);
                    var groups = c.Opponent.HandZone.GroupBy(p => p.Card.Id,StringComparer.Ordinal).Select(group => group.ToArray()).ToList();
                    var offers = new List<Card>();
                    while (offers.Count < 3 && groups.Count > 0)
                    {
                        int i = g.Random.Next(groups.Count); var group = groups[i]; groups.RemoveAt(i);
                        var representative = group[g.Random.Next(group.Length)];
                        PayloadRules.Store(s,1,representative); offers.Add(representative.Card);
                    }
                    if (offers.Count > 0) g.OnRandomHappened(true);
                    return offers.ToArray();
                }, (source,offer) => {
                    DiscoverRealRules.CopyDisplay(offer,PayloadRules.Entries(source,1).FirstOrDefault(p => p.Card.Id == offer.Card.Id));
                    if(source.Controller.Choice.HistoryDiscoverId==0) source.Game.Facts.MarkDiscover(source.Controller.Choice);
                }),
                new CustomTask((g,c,s,t,stack) => {
                    var selected = stack.Playables?.LastOrDefault();
                    var saved = PayloadRules.Entries(s,1).FirstOrDefault(p => p.Card.Id == selected?.Card.Id);
                    if (saved != null) Generic.Copy(c,s,saved,Zone.HAND,preserveCapturedState:true);
                    PayloadRules.Clear(s);
                })) };
        }
    }
}
