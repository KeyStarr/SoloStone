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
    internal static class Ascendance
    {
        internal const int DbfId = 122963;
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("evolve-minion", Filter(), new[] { "SHAMAN" },
            PoolCostBinding.StackNumber, 2, allowThinExactCost: true, destination: PoolDestination.Transform) };
        [StandardCard("CATA_567", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var work = new TaskStack();
                foreach (var minion in c.BoardZone.GetAll().Where(m => !m.IsDormant).ToArray())
                {
                    if (minion.Zone != c.BoardZone || minion.IsDead || minion.IsDormant) continue;
                    var original = minion.Card;
                    int cost = minion.Cost;
                    work.Number = cost >= 10 ? cost : cost + 1;
                    // D2: offline-empty exact tiers do nothing, never substitute a nearby tier.
                    var chosen = new EffectContext(g, c.Id, s.Id, stack: work)
                        .PickRandomCard(Filter(), PoolCostBinding.StackNumber, DbfId);
                    if (chosen == null) continue;
                    var result = Generic.ChangeEntityBlock(c, minion, chosen, true);
                    // "Summon {0}" (builder 19) names an entity: a stored copy of the original, made first so the attachment's first
                    // snapshots carry its id. The original stood face up on the board, so both players may read its name while the
                    // Ascended minion is on the board (SharedWith, ShowAll).
                    var stored = PayloadRules.PrepareCard(c, original);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_567e"), (IPlayable)s, result, original.AssetId, entityId: stored.Id);
                    var ascended = result.AppliedEnchantments.Last(e => e.Card.Id == "CATA_567e");
                    ascended.CapturedCard = original;
                    PayloadRules.Bind(ascended, 1, stored, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowAll);
                }
            }) };
        }
        [StandardCard("CATA_567e", 122965)]
        internal static void ConfigureReturn(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(GetCapturedCardTask.Task,
                new SummonTask(SummonSide.DEATHRATTLE)) };
        }
    }
}
