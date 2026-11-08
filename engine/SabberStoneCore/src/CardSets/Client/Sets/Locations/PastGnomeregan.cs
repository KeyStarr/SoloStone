using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PastGnomeregan
    {
        internal const int DbfId = 120821;
        internal static void Configure(Card card) => ConfigureStage(card, 0, "TIME_044t1");
        internal static void ConfigurePresent(Card card) => ConfigureStage(card, 1, "TIME_044t2");
        internal static void ConfigureFuture(Card card) => ConfigureStage(card, 2, null);
        private static void ConfigureStage(Card card, int stage, string next)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.Minion,
                Task = EffectContext.Task(context => {
                    context.GiveBuff(context.TargetId.Value, "TIME_044e");
                    if (stage > 0) context.GiveBuff(context.TargetId.Value, "TIME_044t1e");
                    if (stage == 2) context.RunLegacy(new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.TARGET));
                    if (next != null) context.AdvanceLocationStage(next);
                    return context.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                })
            } };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(1)) };
        }
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new DamageTask(2, EntityType.OP_HERO) };
        }
    }
}
