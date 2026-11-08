using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PastSilvermoon
    {
        internal const int DbfId = 119166;
        [StandardCard("TIME_810", DbfId)]
        internal static void Configure(Card card) => ConfigureStage(card, 0, "TIME_810t1");
        [StandardCard("TIME_810t1", 119168)]
        internal static void ConfigurePresent(Card card) => ConfigureStage(card, 1, "TIME_810t2");
        [StandardCard("TIME_810t2", 119169)]
        internal static void ConfigureFuture(Card card) => ConfigureStage(card, 2, null);
        private static void ConfigureStage(Card card, int stage, string next)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Task = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    var candidates = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.IsDormant && !m.Untouchable).ToArray();
                    if (stage == 2 && candidates.Length > 0)
                    {
                        int lowest = candidates.Min(m => m.Health);
                        candidates = candidates.Where(m => m.Health == lowest).ToArray();
                    }
                    if (candidates.Length > 0)
                    {
                        var target = candidates[g.Random.Next(candidates.Length)];
                        int spill = stage == 0 ? 0 : System.Math.Max(0, 5 - target.Health);
                        ctx.Damage(target.Id, 5);
                        if (spill > 0) ctx.Damage(c.Opponent.Hero.Id, spill);
                    }
                    if (next != null) ctx.AdvanceLocationStage(next);
                    ctx.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                })
            } };
        }
    }
}
