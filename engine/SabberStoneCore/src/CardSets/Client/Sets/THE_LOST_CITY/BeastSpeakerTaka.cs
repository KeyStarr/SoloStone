using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BeastSpeakerTaka
    {
        internal const int DbfId = 118653;
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.BEAST, rarity: Rarity.LEGENDARY);
        [OfflinePool("DINO_430", DbfId)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("legendary-beast", Filter(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon) };
        [StandardCard("DINO_430", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new DecisionBattlecryTask("client/DINO_430/v1",
                    new DiscoverThenContinueTask((g,c,s,stack) => OfflineCardPools.Candidates(g,c.HeroClass,Filter(),DbfId, PoolDestination.Summon)
                        .Where(x => x.ColossalAppendageIds == null && x[(GameTag)2247] == 0).ToArray().ChooseNElements(3,g.Random), DiscoverRealRules.PrepareDiscover),
                    new CustomTask((g,c,s,t,stack) => {
                        var chosen = stack.Playables.SingleOrDefault(); if (chosen == null) return;
                        Generic.AddEnchantmentBlock(g,Cards.FromId("DINO_430e"),(IPlayable)s,s,chosen.Card.ATK,chosen.Card.Health);
                        PayloadRules.StoreCard(s,1,chosen.Card,PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.Resummon);
                    })),
                DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                    int position = EffectContext.DeathrattlePosition(c,s);
                    foreach (var entry in PayloadRules.Entries(s,1))
                    {
                        if (entry.Card.Type != CardType.MINION || c.BoardZone.IsFull) continue;
                        var body = (Minion)Entity.FromCard(c,entry.Card,new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                        Generic.SummonBlock(g,body,Math.Min(position++,c.BoardZone.SlotCount),s);
                    }
                })
            };
        }
        [StandardCard("DINO_430e",123511)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0),Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
