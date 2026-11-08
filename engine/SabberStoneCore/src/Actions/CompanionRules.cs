using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>Persistent per-player slots, independent of the entity that upgraded them.</summary>
    public static class CompanionRules
    {
        public const GameTag Cost = (GameTag)31070;
        private const int FirstSlot = 31071;
        public const GameTag ExtraSummons = (GameTag)31074;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31070 && (int)tag <= 31074;
        public static bool IsActive(Controller controller) => controller[Cost] > 0;
        public static string[] Roster(Controller controller) => IsActive(controller)
            ? Enumerable.Range(FirstSlot, 3).Select(tag => Cards.FromAssetId(controller[(GameTag)tag]).Id).ToArray()
            : new[] { "NEW1_032", "NEW1_033", "NEW1_034" };
        public static OfflinePoolFilter BeastPool() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.BEAST);

        public static bool Upgrade(Controller controller, IEntity source, int increase, int? recipeDbfId = null)
        {
            if (increase <= 0) throw new ArgumentOutOfRangeException(nameof(increase));
            int cost = (int)Math.Min(10L, (IsActive(controller) ? controller[Cost] : 3L) + increase);
            var stack = new TaskStack { Number = cost };
            var ctx = new EffectContext(controller.Game, controller.Id, source.Id, stack: stack);
            var slots = new Card[3];
            // shortcut: slots sample independently; revisit when retail duplicate-selection policy is traced.
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = ctx.PickRandomCard(BeastPool(), PoolCostBinding.StackNumber, recipeDbfId);
                if (slots[i] == null) return false;
            }
            for (int i = 0; i < slots.Length; i++) controller[(GameTag)(FirstSlot + i)] = slots[i].AssetId;
            controller[Cost] = cost;
            return true;
        }

        public static void SummonExtras(Controller controller, IEntity source)
        {
            int count = controller[ExtraSummons];
            for (int i = 0; i < count && !controller.BoardZone.IsFull && controller.Game.State != State.COMPLETE; i++)
            {
                string id = Roster(controller)[controller.Game.Random.Next(3)];
                controller.Game.OnRandomHappened(true);
                new SummonTask(id, SummonSide.SPELL).Process(controller.Game, controller, source, null);
            }
        }

        internal static bool IsSummoningSpell(Card card) => card.Id == "NEW1_031" || card.Id == "CORE_NEW1_031"
            || card.Id == "LOOT_217" || card.Id == "CORE_LOOT_217" || card.Id == "OG_211" || card.Id == "CORE_OG_211";

        internal static string[] Entourage(Controller controller, Card card, string[] original)
        {
            if (!IsActive(controller) || !(card.Id == "NEW1_031" || card.Id == "CORE_NEW1_031"
                || card.Id == "LOOT_217" || card.Id == "CORE_LOOT_217")) return original;
            return Roster(controller);
        }
    }
}
