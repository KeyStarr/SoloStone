using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CursedCatacombs
    {
        internal const int DbfId = 117892;
        [StandardCard("TLC_451", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TLC_451/v1",
                new DiscoverThenContinueTask((g,c,s,stack) => {
                    using var scope = g.Facts.ChoiceOffers();
                    var groups = c.DeckZone.Where(p=>p.Card.Id != "TLC_451").GroupBy(p=>p.Card.Id).Select(a=>a.ToList()).ToList();
                    var selected = DiscoverRealRules.Take(groups,3,g).Select(a=>a.Count==1?a[0]:a[g.Random.Next(a.Count)]).ToArray();
                    var offers = selected.Select(real=>CreateOffer(c,s,real)).ToArray();
                    if (offers.Length > 0)
                    {
                        Generic.CreateChoice(c,s,ChoiceType.GENERAL,ChoiceAction.STACK,offers.Select(p=>p.Id).ToList());
                        g.Facts.MarkDiscover(c.Choice);
                    }
                    return Array.Empty<Card>();
                }), new CustomTask((g,c,s,t,stack)=> {
                    var offer = stack.Playables?.LastOrDefault();
                    if (offer == null || !g.IdEntityDic.TryGetValue(offer[DiscoverRealRules.BoundEntity],out var real) || real.Zone != c.DeckZone) return;
                    using var entry = c.HandZone.DeferCardStateEntry();
                    if (g.History) g.PowerHistory.Add(new PowerHistoryMetaData { Type=MetaDataType.TARGET, Info=new List<int>{s.Id} });
                    Generic.Draw(c,real);
                    if (real.Zone == c.HandZone) TemporaryRules.Make(real);
                })) };
        }
        internal static IPlayable CreateOffer(Controller controller, IEntity source, IPlayable real)
        {
            var offer = Entity.FromCard(controller, real.Card, new EntityData {
                [Generic.PrivateDecisionOfferTag] = controller.PlayerId, [DiscoverRealRules.BoundEntity] = real.Id,
                [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id
            }, controller.SetasideZone);
            offer.Cost = real.Cost;
            if (offer is Minion shown && real is Minion original)
            { shown.AttackDamage = original.AttackDamage; shown.BaseHealth = original.BaseHealth; }
            foreach (var tag in new[] { GameTag.TAUNT, GameTag.CHARGE, GameTag.RUSH, GameTag.WINDFURY,
                GameTag.DIVINE_SHIELD, GameTag.POISONOUS, GameTag.LIFESTEAL, GameTag.STEALTH, GameTag.DEATHRATTLE,
                GameTag.SPELLPOWER, GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
                GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                if (real[tag] != real.Card[tag]) offer[tag] = real[tag];
            return offer;
        }
    }
}
