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

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FacelessEnigma
    {
        internal const int DbfId = 119740;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TIME_860/v1",
                new DiscoverThenContinueTask((g, c, s, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id);
                    var offers = ctx.SampleDistinct(OfflineCardPools.Candidates(g, c.HeroClass, TimeTravelPools.Secrets(), DbfId, PoolDestination.Other), 2).ToArray();
                    stack.Number1 = offers.Length > 0 ? offers[0].AssetId : 0;
                    stack.Number2 = offers.Length > 1 ? offers[1].AssetId : 0;
                    return offers;
                }),
                new CustomTask((g, c, s, t, stack) => {
                    var selected = stack.Playables?.LastOrDefault() as Spell;
                    if (selected == null) return;
                    int other = selected.Card.AssetId == stack.Number1 ? stack.Number2 : stack.Number1;
                    new PlayTask(PlayType.SPELL, true).Process(g, c, s, null, new TaskStack { Playables = new[] { (IPlayable)selected } });
                    if (other == 0) return;
                    var opponentSpell = Entity.FromCard(c.Opponent, Cards.FromAssetId(other),
                        new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                    new PlayTask(PlayType.SPELL, true).Process(g, c.Opponent, s, null,
                        new TaskStack { Playables = new[] { opponentSpell } });
                })) };
        }
    }
}
