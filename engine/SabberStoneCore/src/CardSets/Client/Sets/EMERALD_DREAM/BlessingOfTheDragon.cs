using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlessingOfTheDragon
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DRAGON);
        [OfflinePool("EDR_445pt3", 114070)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("portal-exact-cost-dragon", Pool(), new[] { "PALADIN" }, PoolCostBinding.StackNumber, ordinaryCost: 1, destination: PoolDestination.Summon)
        };
        [StandardCard("EDR_445p", 114023)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2; i++)
                {
                    var portal = Entity.FromCard(c, Cards.FromId("EDR_445pt3"), new EntityData {
                        [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id,
                        [GameTag.TAG_SCRIPT_DATA_NUM_1] = BasicHeroPowerRules.PortalMagnitude(c)
                    });
                    if (!Generic.ShuffleIntoDeck(c, s, portal)) c.GraveyardZone.Add(portal);
                }
            }) };
        }
        [StandardCard("EDR_445pt3", 114070)]
        internal static void ConfigurePortal(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            var effect = new CustomTask((g, c, s, t, stack) => {
                stack ??= new TaskStack();
                stack.Number = BasicHeroPowerRules.PortalMagnitude(c);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(Pool(), PoolCostBinding.StackNumber, 114070);
                if (chosen != null) ctx.Summon(chosen.Id);
            });
            card.Power = new Power { PowerTask = effect, TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                new PlayTask(PlayType.SPELL).Process(g, c, s, null, new TaskStack { Playables = new[] { (IPlayable)s } });
                new DrawTask().Process(g, c, s, null);
            }) };
        }
    }
}
