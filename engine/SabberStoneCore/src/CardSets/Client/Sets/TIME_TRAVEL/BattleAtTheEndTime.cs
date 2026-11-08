using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BattleAtTheEndTime
    {
        internal const int DbfId = 121159;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(DbfId, 2, QuestRewardKind.CardToHand,
                "END_017t", repeatable: false, handCountChanged: OnHandCountChanged));
            card.Power = new Power();
        }

        private static void OnHandCountChanged(Spell quest, HandCountFact fact)
        {
            var state = quest.QuestState;
            bool qualifies = state.Stage == 0 ? fact.Before < fact.Maximum && fact.After == fact.Maximum
                : state.Stage == 1 && fact.Before > 0 && fact.After == 0;
            if (!qualifies) return;
            // Latch stage now so nested draw/zone events cannot reverse committed full/empty order.
            state.Stage++;
            quest.Game.TaskQueue.Enqueue(new HandCountQuestTask(fact), quest.Controller, quest, null);
        }
    }
}
