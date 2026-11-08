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
    internal static class CultivatingSprite
    {
        internal const int BulbDbfId = 123854;
        [StandardCard("MEND_100", 123849)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddCardTo("MEND_100t", EntityType.HAND) };
        }
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.SPELL);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("bulb-exact-spells", Filter(), new[] { "NEUTRAL" },
            PoolCostBinding.StackNumber, 1, allowThinExactCost: true, destination: PoolDestination.Other) };
        private static int Tier(IPlayable bulb) => PayloadRules.Entries(bulb, BulbDbfId).SingleOrDefault()?[PayloadRules.Value] ?? 1;
        [StandardCard("MEND_100t", BulbDbfId)]
        internal static void ConfigureBulb(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Tier);
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TURN_START) {
                    TriggerActivation = TriggerActivation.HAND,
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        var progress = PayloadRules.Entries(s, BulbDbfId).SingleOrDefault()
                            ?? PayloadRules.StoreValue(s, BulbDbfId, 1, PayloadPolicy.Copy);
                        progress[PayloadRules.Value] = checked(progress[PayloadRules.Value] + 1);
                        s[GameTag.TAG_SCRIPT_DATA_NUM_1] = progress[PayloadRules.Value];
                    })
                },
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    int tier = Tier((IPlayable)s);
                    var work = new TaskStack();
                    for (int i = 0; i < 3 && g.State != State.COMPLETE; i++)
                    {
                        work.Number = tier;
                        var chosen = new EffectContext(g, c.Id, s.Id, stack: work)
                            .PickRandomCard(Filter(), PoolCostBinding.StackNumber, BulbDbfId);
                        if (chosen == null) continue;
                        var spell = Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                        new PlayTask(PlayType.SPELL, true).Process(g, c, s, null,
                            new TaskStack { Playables = new[] { spell } });
                    }
                })
            };
        }
    }
}
