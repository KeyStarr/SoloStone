using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NellieTheGreatThresher
    {
        [StandardCard("TSC_660", 74894)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 1 }, "TSC_660t");
            var steps = new List<ISimpleTask> { new CustomTask((g,c,s,t,k) => {
                k.Number = ColossalRules.AppendagesOf((Minion)s).FirstOrDefault(m => m.Card.Id == "TSC_660t")?.Id ?? 0;
            }) };
            for (int i = 0; i < 3; i++) {
                steps.Add(new DiscoverThenContinueTask(Offers, DiscoverRealRules.PrepareDiscover));
                steps.Add(new CustomTask((g,c,s,t,k) => {
                    var chosen = k.Playables.LastOrDefault();
                    if (chosen != null && g.IdEntityDic.TryGetValue(k.Number, out var ship) && ship.Zone == c.BoardZone)
                        PayloadRules.StoreCard(ship, Crew, chosen.Card);
                }));
            }
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TSC_660/crew-v1", steps.ToArray()) };
        }

        [StandardCard("TSC_660t", 74895)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,k) => {
                foreach (var crew in PayloadRules.Entries(s, Crew)) {
                    var pirate = (Playable)Entity.FromCard(c, crew.Card, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                    CostRules.AddModifier(pirate, EffectOperator.SET, 1);
                    Generic.AddHandPhase(c, pirate);
                }
            }) };
        }

        [StandardCard("TSC_660e", 76901)]
        internal static void Fee(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.COST, EffectOperator.SET, 1)) };
        }

        [StandardCard("TSC_660e2", 79606)]
        internal static void Annotation(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        internal const int Crew = 74894;
        internal static OfflinePoolFilter Pirates() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.PIRATE);
        [OfflinePool("TSC_660", 74894)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("nellie-crew", Pirates(), new[] { "WARRIOR" }) };
        private static Card[] Offers(Game g, Controller c, IEntity s, TaskStack k)
        {
            if (!g.IdEntityDic.TryGetValue(k.Number, out var ship) || ship.Zone != c.BoardZone) return Array.Empty<Card>();
            var repeated = PayloadRules.Entries(ship, Crew).GroupBy(p => p.Card.Id).Where(group => group.Count() >= 2).Select(group => group.Key).ToHashSet();
            var candidates = OfflineCardPools.Candidates(g,c.HeroClass,Pirates(),74894,sourceClassMask:OfflineCardPools.PrintedClassMask(s.Card)).Where(card => !repeated.Contains(card.Id)).ToList();
            var offers = new List<Card>();
            while (candidates.Count > 0 && offers.Count < 3) {
                int index = g.Random.Next(candidates.Count); offers.Add(candidates[index]); candidates.RemoveAt(index);
            }
            return offers.ToArray();
        }
    }
}
