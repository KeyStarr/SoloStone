using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class ForestLordCenarius
    {
        internal const int DbfId = 113588;
        internal static void Configure(Card card)
        {
            // Pinned 37.0.0.254477 EDR_209: each choice resolves before the next, and may repeat.
            // https://hearthstone.blizzard.com/en-us/cards/113588-forest-lord-cenarius
            var choice = StateTaskList.Suspendable("client/113588/choice/v1",
                new DiscoverThenContinueTask("EDR_209a", "EDR_209b"), EffectContext.Task(Resolve));
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/113588/choose-thrice/v1",
                choice, choice, choice) };
            card.Implemented = true;
            // Preserve LegacyV1 seeded generation pools; this module is explicitly admitted by spike decks.
            card.ExcludeFromLegacyRandomPools = true;
        }

        internal static void ConfigureBuff(Card card) => card.Power = new Power {
            Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(3))
        };

        private static TaskState Resolve(EffectContext ctx)
        {
            if (ctx.LastChosenCardId == "EDR_209a")
            {
                foreach (int id in ctx.FriendlyMinionIds)
                    if (id != ctx.SourceId) ctx.GiveBuff(id, "EDR_209e2");
            }
            else if (ctx.LastChosenCardId == "EDR_209b") ctx.Summon("EDR_209t5");
            else throw new System.InvalidOperationException("Cenarius received an unregistered branch.");
            return TaskState.COMPLETE;
        }
    }
}
