using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LotusTroublemaker
    {
        internal const int DbfId = 129983;
        private static int Shots(HistoryQuery history, int entityId, int controllerId) =>
            1 + history.TwoManaPlaysWhileInHandOrDeck(entityId, controllerId);
        private static int DisplayShots(IPlayable card) => Shots(card.Game.HistoryFacts.Query, card.Id, card.Controller.Id);

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: R6 counts own actual two-Mana card plays during this entity's hand/deck residences, excluding its own play; 36.0.3 extends scope to deck.
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, DisplayShots);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(SplitHits.Inline(
                Shots(ctx.History, ctx.SourceId, ctx.ControllerId),
                ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: true))) };
        }
    }
}
