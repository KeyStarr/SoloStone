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
    internal static class RoyalInformant
    {
        [StandardCard("TIME_036", 120534)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("b14-h2/royal-informant/v1",
                new DiscoverThenContinueTask((g,c,s,stack) => {
                    PayloadRules.Clear(s); var original = c.Opponent.HandZone.LastOrDefault();
                    if (original == null) return Array.Empty<Card>();
                    var saved = PayloadRules.Store(s,1,original);
                    saved[PayloadRules.Link] = original.Id;
                    return new[] { original.Card, Cards.FromId("TIME_036t") };
                }, (source,offer) => DiscoverRealRules.CopyDisplay(offer,
                    PayloadRules.Entries(source,1).FirstOrDefault(p => p.Card.Id == offer.Card.Id))),
                new CustomTask((g,c,s,t,stack) => {
                    var saved = PayloadRules.Entries(s,1).SingleOrDefault();
                    var choice = stack.Playables?.LastOrDefault();
                    if (saved != null && choice != null)
                    {
                        if (choice.Card.Id == "TIME_036t")
                        {
                            if (g.IdEntityDic.TryGetValue(saved[PayloadRules.Link],out var original) && original.Zone == c.Opponent.HandZone)
                                Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_036e"),(IPlayable)s,original);
                        }
                        else Generic.Copy(c,s,saved,Zone.HAND,preserveCapturedState:true);
                    }
                    PayloadRules.Clear(s);
                })) };
        }
        [StandardCard("TIME_036t", 122962)]
        internal static void TaxOption(Card card) { card.Implemented = true; card.Power = new Power(); }
        [StandardCard("TIME_036e", 122964)]
        internal static void Tax(Card card) { card.Implemented = true; card.Power = new Power { Enchant = new Enchant(Effects.AddCost(2)) }; }
    }
}
