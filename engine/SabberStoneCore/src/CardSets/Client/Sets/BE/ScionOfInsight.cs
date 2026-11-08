using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScionOfInsight
    {
        internal const int DbfId = 130555;
        private static readonly GameTag[] Colours = { GameTag.COST_BLOOD, GameTag.COST_FROST, GameTag.COST_UNHOLY };
        private static OfflinePoolFilter Filter(GameTag colour) => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DEATHKNIGHT, keyword: colour);
        internal static PoolRequirement[] DeclarePools() => Colours.Select(colour =>
            new PoolRequirement("rune-" + (int)colour, Filter(colour), new[] { "DEATHKNIGHT" })).ToArray();
        [StandardCard("BE_298", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_298/v1",
                new DiscoverThenContinueTask(Offers, MarkDiscover),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    if (s is Minion minion && minion.Zone == minion.Controller.BoardZone)
                    {
                        if (chosen.Card[GameTag.COST_BLOOD] > 0) minion.HasLifeSteal = true;
                        if (chosen.Card[GameTag.COST_FROST] > 0) minion[GameTag.REBORN] = 1;
                        if (chosen.Card[GameTag.COST_UNHOLY] > 0) minion.IsRush = true;
                    }
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                })) };
        }
        private static void MarkDiscover(IEntity source, IPlayable offer)
        {
            if (source.Controller.Choice.HistoryDiscoverId == 0) source.Game.Facts.MarkDiscover(source.Controller);
        }
        private static Card[] Offers(Game g, Controller c, IEntity s, TaskStack stack)
        {
            var offers = new System.Collections.Generic.List<Card>();
            foreach (var colour in Colours)
            {
                var candidates = OfflineCardPools.Candidates(g, c.HeroClass, Filter(colour), s.Card.AssetId)
                    .Where(card => Colours.Sum(tag => card[tag]) <= 2 && !offers.Any(prior => prior.Id == card.Id)).ToArray();
                if (candidates.Length != 0) offers.Add(candidates[g.Random.Next(candidates.Length)]);
            }
            return offers.ToArray();
        }
    }
}
