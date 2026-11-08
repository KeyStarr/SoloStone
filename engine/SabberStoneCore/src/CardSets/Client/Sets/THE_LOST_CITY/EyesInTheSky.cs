using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EyesInTheSky
    {
        internal const int DbfId = 117703;
        [StandardCard("TLC_521", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TLC_521/v1",
                new DiscoverThenContinueTask((g,c,s,stack)=> {
                    using var scope = g.Facts.ChoiceOffers();
                    var selected = DiscoverRealRules.Take(c.Opponent.DeckZone.ToList(),3,g);
                    var offers = selected.Select(real=>CursedCatacombs.CreateOffer(c,s,real)).ToArray();
                    if (offers.Length > 0) Generic.CreateChoice(c,s,ChoiceType.GENERAL,ChoiceAction.STACK,offers.Select(p=>p.Id).ToList());
                    return Array.Empty<Card>();
                }), new CustomTask((g,c,s,t,stack)=> {
                    var offer = stack.Playables?.LastOrDefault();
                    if (offer != null && g.IdEntityDic.TryGetValue(offer[DiscoverRealRules.BoundEntity],out var real) && real.Zone == c.Opponent.DeckZone)
                        c.Opponent.DeckZone.PutOnTop(real);
                })) };
        }
    }
}
