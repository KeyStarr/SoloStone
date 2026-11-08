using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model
{
    public sealed class DormantDefinition
    {
        public int? Turns { get; }
        public string ConditionId { get; }
        public string WhileDormantId { get; }
        public ISimpleTask OnAwaken { get; }
        public Conditions.SelfCondition AwakenCondition { get; }
        public DormantDefinition(int? turns, string conditionId, string whileDormantId = null, ISimpleTask onAwaken = null,
            Conditions.SelfCondition awakenCondition = null)
        {
            if (turns < 0) throw new ArgumentOutOfRangeException(nameof(turns));
            if (string.IsNullOrEmpty(conditionId)) throw new ArgumentException(nameof(conditionId));
            Turns = turns; ConditionId = conditionId; WhileDormantId = whileDormantId; OnAwaken = onAwaken;
            AwakenCondition = awakenCondition;
        }
    }
}

namespace SabberStoneCore.Actions
{
    public static class DormantRules
    {
        // Private durable state, excluded from every native packet; 1 = awake copy/entry, 2 = timed, 3 = external.
        private const GameTag Mode = (GameTag)32000, Remaining = (GameTag)32001,
            Condition = (GameTag)32002, Helper = (GameTag)32003, WhileHelper = (GameTag)32004,
            EntryTurn = (GameTag)32005, LastTick = (GameTag)32006,
            PendingTurns = (GameTag)32007, PendingCondition = (GameTag)32008, Moving = (GameTag)32009;
        public static bool IsPrivateTag(GameTag tag) => (int)tag >= 32000 && (int)tag <= 32009;
        public static bool HasState(Minion minion) => minion[Mode] != 0;
        public static int SleepGeneration(Minion minion) => minion.IsDormant ? minion[Helper] : 0;
        public static int TurnsRemaining(Minion minion) => minion[Remaining];
        public static bool HasPending(IPlayable card) => card[PendingTurns] != 0;

        internal static void CheckConditions(Game game)
        {
            // Every aura update; dormant minions are rare, so the snapshot is only taken once one is on a board.
            bool any = false;
            for (int side = 0; side < 2 && !any; side++)
            {
                var board = (side == 0 ? game.Player1 : game.Player2).BoardZone;
                for (int i = 0; i < board.Count; i++)
                    if (board[i].IsDormant) { any = true; break; }
            }
            if (!any) return;
            foreach (var minion in game.Player1.BoardZone.Concat(game.Player2.BoardZone).ToArray())
                if (minion.IsDormant && !minion.IsSilenced && !minion.ToBeDestroyed &&
                    minion.Card.Dormant?.AwakenCondition?.Eval(minion) == true &&
                    minion[Condition] == Cards.FromId(minion.Card.Dormant.ConditionId).AssetId) Awaken(minion);
        }

        private static Card ConditionCard(string id)
        {
            var card = Cards.FromId(id);
            if (card == null || card.Type != CardType.ENCHANTMENT) throw new ArgumentException("Dormant condition must be an exported enchantment: " + id);
            return card;
        }

        internal static void Validate(Card owner, System.Collections.Generic.IDictionary<string, Card> cards)
        {
            var definition = owner.Dormant;
            if (owner.Type != CardType.MINION || !cards.TryGetValue(definition.ConditionId, out var condition)
                || condition.Type != CardType.ENCHANTMENT || definition.AwakenCondition == null && condition[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT] != 1
                || definition.Turns > 0 && condition[(GameTag)451] != definition.Turns)
                throw new ArgumentException("Invalid Dormant definition/condition: " + owner.Id);
            if (definition.WhileDormantId != null && (!cards.TryGetValue(definition.WhileDormantId, out var helper) || helper.Type != CardType.ENCHANTMENT))
                throw new ArgumentException("Invalid Dormant listener: " + owner.Id);
        }

        public static void MarkPending(IPlayable card, int turns, string conditionId)
        {
            if (!(card is Minion) || card.Zone == card.Controller.BoardZone) throw new ArgumentException(nameof(card));
            if (turns <= 0) throw new ArgumentOutOfRangeException(nameof(turns));
            var condition = ConditionCard(conditionId);
            card[PendingTurns] = turns; card[PendingCondition] = condition.AssetId;
        }
        internal static void ClearPending(IPlayable card)
        {
            if (!HasPending(card)) return;
            card[PendingTurns] = 0; card[PendingCondition] = 0;
        }

        public static bool Initialize(Minion minion)
        {
            if (minion.Zone != minion.Controller.BoardZone) return false;
            if (HasPending(minion))
            {
                int turns = minion[PendingTurns];
                string id = Cards.FromAssetId(minion[PendingCondition]).Id;
                ClearPending(minion);
                return Make(minion, turns, id, minion);
            }
            // A gameplay board copy already carries its awake/sleeping state, but needs fresh helpers.
            if (HasState(minion))
            {
                if (minion[Mode] == 1) return false;
                if (minion[Helper] != 0)
                {
                    foreach (int id in new[] { minion[Helper], minion[WhileHelper] })
                        if (minion.Game.IdEntityDic.TryGetValue(id, out var entity) && entity is Enchantment actor && actor.Controller != minion.Controller)
                            actor.RebindController(minion.Controller);
                    return false;
                }
                return Make(minion, minion[Mode] == 2 ? (int?)minion[Remaining] : null,
                    Cards.FromAssetId(minion[Condition]).Id, minion);
            }
            var definition = minion.Card.Dormant;
            if (definition == null) return false;
            if (definition.Turns == 0) { minion[Mode] = 1; return false; }
            return Make(minion, definition.Turns, definition.ConditionId, minion);
        }

        public static bool Make(Minion minion, int? turns, string conditionId, IEntity source)
        {
            if (turns <= 0) throw new ArgumentOutOfRangeException(nameof(turns));
            if (minion == null || minion.Zone != minion.Controller.BoardZone || minion.ToBeDestroyed
                || minion.Health <= 0 || source != null && source.Game != minion.Game || !minion.Controller.BoardZone.Slots.Contains(minion)) return false;
            var condition = ConditionCard(conditionId);
            if (minion.IsDormant && HasState(minion) && minion[Helper] != 0
                && minion[Condition] == condition.AssetId && minion[Mode] == (turns.HasValue ? 2 : 3)
                && (!turns.HasValue || minion[Remaining] == turns.Value)) return false;
            RemoveHelpers(minion);
            minion.RemoveDormantCondition();
            minion.ActivatedTrigger?.Remove();
            minion.OngoingEffect?.Remove();
            foreach (var aura in minion.Controller.BoardZone.Auras.ToArray()) aura.DeApply(minion);
            foreach (var aura in minion.Controller.BoardZone.AdjacentAuras.ToArray()) aura.DeApplyDormant(minion);
            minion.AttackableByRush = false;
            minion.Game.RushMinions.Remove(minion.Id);
            minion[Mode] = turns.HasValue ? 2 : 3;
            minion[Remaining] = turns ?? 0;
            minion[Condition] = condition.AssetId;
            minion[EntryTurn] = minion.Game.Turn;
            minion[GameTag.DORMANT] = 1;
            minion[GameTag.DORMANT_VISUAL] = 1;
            minion[GameTag.UNTOUCHABLE] = 1;
            var helper = Enchantment.GetInstance(minion.Controller, source as IPlayable ?? minion, minion, condition);
            minion[Helper] = helper.Id;
            if (!minion.IsSilenced && minion.Card.Dormant?.WhileDormantId != null)
            {
                var card = ConditionCard(minion.Card.Dormant.WhileDormantId);
                var actor = Enchantment.GetInstance(minion.Controller, minion, minion, card);
                minion[WhileHelper] = actor.Id;
                card.Power.Trigger?.Activate(actor);
            }
            PublishTimer(minion);
            foreach (var aura in minion.Controller.BoardZone.AdjacentAuras) aura.BoardChanged = true;
            return true;
        }

        private static void PublishTimer(Minion minion)
        {
            int display = minion[Mode] == 2 ? minion[Remaining] : ConditionCard(
                Cards.FromAssetId(minion[Condition]).Id)[(GameTag)451];
            if (minion.Card.Dormant != null) minion[GameTag.TAG_SCRIPT_DATA_NUM_1] = display;
            if (minion.Game.IdEntityDic.TryGetValue(minion[Helper], out var helper))
            {
                helper[GameTag.TAG_SCRIPT_DATA_NUM_1] = display;
                helper[(GameTag)451] = display;
            }
        }

        public static void TickControllerStart(Controller controller)
        {
            // Capture ids/generations before any awakening task can summon a new sleeper.
            var cohort = controller.BoardZone.Slots.OfType<Minion>().Where(m => HasState(m) && m.IsDormant)
                .Select(m => (m.Id, Generation: m[Helper])).ToArray();
            foreach (var item in cohort)
            {
                if (!(controller.Game.IdEntityDic[item.Id] is Minion m) || m.Controller != controller
                    || !m.IsDormant || m[Helper] != item.Generation || m[EntryTurn] == m.Game.Turn
                    || m[LastTick] == m.Game.Turn) continue;
                m[LastTick] = m.Game.Turn;
                foreach (int id in new[] { m[Helper], m[WhileHelper] })
                    if (m.Game.IdEntityDic.TryGetValue(id, out var e) && e is Enchantment actor && actor.Controller != controller)
                        actor.RebindController(controller);
                if (m[Mode] == 2) Accelerate(m, 1);
            }
        }

        public static bool Accelerate(Minion minion, int turns)
        {
            if (turns < 0) throw new ArgumentOutOfRangeException(nameof(turns));
            if (turns == 0 || minion == null || !minion.IsDormant || minion[Mode] != 2) return false;
            minion[Remaining] = Math.Max(0, minion[Remaining] - turns);
            PublishTimer(minion);
            if (minion[Remaining] == 0) Awaken(minion);
            return true;
        }

        public static bool Awaken(Minion minion)
        {
            if (minion == null || !HasState(minion) || !minion.IsDormant) return false;
            var game = minion.Game;
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, minion.Id, minion.Card.Id, 0, 0));
            try
            {
                minion[Mode] = 1; minion[Remaining] = 0;
                minion[GameTag.DORMANT] = 0; minion[GameTag.DORMANT_VISUAL] = 0; minion[GameTag.UNTOUCHABLE] = 0;
                minion[GameTag.DORMANT_AWAKENED_THIS_TURN] = 1;
                if (minion.Card.Dormant != null) minion[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
                RemoveHelpers(minion);
                minion.IsExhausted = !minion.HasCharge;
                BoardZone.InitialiseMinion(minion);
                if (minion.IsRush || minion.HasCharge) minion.IsExhausted = false;
                if (!minion.IsSilenced) BoardZone.ActivateAura(minion);
                foreach (var aura in minion.Controller.BoardZone.Auras.ToArray()) aura.EntityAdded(minion);
                foreach (var aura in minion.Controller.BoardZone.AdjacentAuras) aura.BoardChanged = true;
                game.Facts.Awakened(minion,minion);
                if (!minion.IsSilenced && minion.Card.Dormant?.OnAwaken != null)
                {
                    // A queued release must not recursively drain its own parent activation.
                    game.TaskQueue.StartEvent();
                    try
                    {
                        game.TaskQueue.Enqueue(minion.Card.Dormant.OnAwaken, minion.Controller, minion, minion);
                        game.ProcessTasks();
                    }
                    finally { game.TaskQueue.EndEvent(); }
                }
            }
            finally { if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd()); }
            return true;
        }

        private static void RemoveHelpers(Minion minion)
        {
            foreach (var tag in new[] { Helper, WhileHelper })
            {
                if (minion.Game.IdEntityDic.TryGetValue(minion[tag], out var e) && e is Enchantment actor
                    && (minion.AppliedEnchantments?.Contains(actor) ?? false)) actor.Remove();
                minion[tag] = 0;
            }
        }
        public static void LeavePlay(Minion minion)
        {
            if (!HasState(minion)) return;
            if (minion[GameTag.DORMANT_AWAKENED_THIS_TURN] != 0) minion[GameTag.DORMANT_AWAKENED_THIS_TURN] = 0;
            RemoveHelpers(minion);
            minion[GameTag.DORMANT] = 0; minion[GameTag.DORMANT_VISUAL] = 0; minion[GameTag.UNTOUCHABLE] = 0;
            // Publish before bounce/reset drops the override; native SHOW merges omitted tags.
            minion[GameTag.DORMANT_AWAKENED_THIS_TURN] = 0;
            if (minion.Card.Dormant != null || minion.Card.Id == "CORE_BT_156" || minion.Card.Id == "EDR_469")
                minion[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
            minion[Mode] = 0; minion[Remaining] = 0; minion[Condition] = 0; minion[LastTick] = 0;
            minion.AttackableByRush = false;
        }
        internal static bool IsControlTransfer(Minion minion) => minion[Moving] != 0;
        internal static IDisposable PreserveControl(Minion minion)
        {
            if (!HasState(minion)) return null;
            minion[Moving] = 1;
            return new ControlScope(minion);
        }
        private sealed class ControlScope : IDisposable
        {
            private readonly Minion minion;
            internal ControlScope(Minion value) { minion = value; }
            public void Dispose() { minion[Moving] = 0; }
        }
        internal static bool IsHelper(Enchantment actor) => actor.Target is Minion m
            && (m[Helper] == actor.Id || m[WhileHelper] == actor.Id);
        internal static void CopyState(Minion source, Minion copy)
        {
            if (!HasState(source)) return;
            copy[Mode] = source[Mode]; copy[Remaining] = source[Remaining]; copy[Condition] = source[Condition];
            copy[LastTick] = source[LastTick];
            copy[Helper] = 0; copy[WhileHelper] = 0;
        }
    }
}
