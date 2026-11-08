using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Actions
{
    public static class QuestRules
    {
        private static readonly Dictionary<int, Func<IPlayable, DamageResolvedFact, ISimpleTask>> DamageListeners =
            new Dictionary<int, Func<IPlayable, DamageResolvedFact, ISimpleTask>>();

        public static void RegisterDamageListener(int dbfId, Func<IPlayable, DamageResolvedFact, ISimpleTask> recipe) =>
            DamageListeners.Add(dbfId, recipe);

        public static bool HasDamageListener(Controller controller)
        {
            if (!controller.MayHaveModernDamageListeners) return false;
            foreach (var quest in controller.SecretZone.ModernQuests)
                if (DamageListeners.ContainsKey(quest.Card.AssetId)) return true;
            if (controller.AppliedEnchantments != null)
                foreach (var effect in controller.AppliedEnchantments)
                    if (DamageListeners.ContainsKey(effect.Card.AssetId)) return true;
            return false;
        }

        public static void DamageResolved(Controller controller, DamageResolvedFact fact)
        {
            // Qualification happens now; queued tasks never re-read mutable event attribution.
            foreach (var quest in controller.SecretZone.ModernQuests) QueueDamageReaction(quest, fact);
            if (controller.AppliedEnchantments != null)
                foreach (var effect in controller.AppliedEnchantments) QueueDamageReaction(effect, fact);
        }

        private static void QueueDamageReaction(IPlayable listener, DamageResolvedFact fact)
        {
            if (!DamageListeners.TryGetValue(listener.Card.AssetId, out var recipe)) return;
            var task = recipe(listener, fact);
            if (task != null)
                listener.Game.TaskQueue.Enqueue(task, listener.Controller, listener, listener.Game.IdEntityDic[fact.TargetId]);
        }

        public static void AddPlayerEffect(Controller controller, IPlayable source, string enchantmentId)
        {
            var card = Cards.FromId(enchantmentId);
            if (DamageListeners.ContainsKey(card.AssetId)) controller.MayHaveModernDamageListeners = true;
            var effect = controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == card.Id);
            if (effect != null) effect[GameTag.TAG_SCRIPT_DATA_NUM_1]++;
            else
            {
                effect = Enchantment.GetInstance(controller, source, controller, card, 1);
                card.Power.Trigger?.Activate(effect);
            }
        }

        private static readonly Dictionary<int, string[]> Groups = new Dictionary<int, string[]>();
        private static readonly Dictionary<int, int> ComponentGroups = new Dictionary<int, int>();
        private static readonly HashSet<int> CorpseSpendQuests = new HashSet<int>();
        private static readonly HashSet<int> TemporaryPlayQuests = new HashSet<int>();
        private static readonly Dictionary<int, Action<Enchantment, Minion>> PlayedMinionEffects =
            new Dictionary<int, Action<Enchantment, Minion>>();
        private static readonly List<(string First, string Second, string Result)> HandPairs =
            new List<(string, string, string)>();

        public static void RegisterGroup(int wrapperDbfId, params string[] components)
        {
            if (components == null || components.Length < 2 || components.Distinct().Count() != components.Length)
                throw new ArgumentException(nameof(components));
            Groups.Add(wrapperDbfId, (string[])components.Clone());
        }

        public static void RegisterComponent(int dbfId, int wrapperDbfId) => ComponentGroups.Add(dbfId, wrapperDbfId);
        public static void RegisterCorpseSpend(int dbfId) => CorpseSpendQuests.Add(dbfId);
        public static void RegisterTemporaryPlay(int dbfId) => TemporaryPlayQuests.Add(dbfId);
        public static void RegisterPlayedMinionEffect(int dbfId, Action<Enchantment, Minion> effect) =>
            PlayedMinionEffects.Add(dbfId, effect);
        public static void RegisterHandPair(string first, string second, string result) => HandPairs.Add((first, second, result));
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 33060 && (int)tag <= 33067;

        public static void TemporaryPlayed(Controller controller)
        {
            foreach (var quest in controller.SecretZone.ModernQuests.ToArray())
                if (TemporaryPlayQuests.Contains(quest.Card.AssetId)) Advance(quest);
        }

        private const GameTag RiftUsedTurn = (GameTag)33067;
        public static bool IsRift(IPlayable source) => source is Minion minion && source.Card.Id == "TLC_446t1" && !minion.IsSilenced;

        public static bool CanActivateRift(Controller controller, IPlayable source, IPlayable handCard)
        {
            if (controller == null || !IsRift(source) || handCard == null) return false;
            var game = controller.Game;
            return game.CurrentPlayer == controller && game.State == State.RUNNING && game.Step == Step.MAIN_ACTION
                && game.Player1.Choice == null && game.Player2.Choice == null && !game.Resolution.HasSuspendedEffect
                && source.Game == game && source.Controller == controller && source.Zone == controller.BoardZone
                && game.IdEntityDic.TryGetValue(source.Id, out var registered) && registered == source
                && !source.ToBeDestroyed && !((Minion)source).IsDormant && source[RiftUsedTurn] != game.Turn
                && !controller.BoardZone.IsFull && handCard.Controller == controller && handCard.Zone == controller.HandZone
                && game.IdEntityDic.TryGetValue(handCard.Id, out var actual) && actual == handCard;
        }

        public static bool ActivateRift(Controller controller, IPlayable source, IPlayable handCard)
        {
            if (!CanActivateRift(controller, source, handCard)) return false;
            var game = controller.Game;
            game.Facts.AcceptPlayerAction(source);
            using var resolution = game.Resolution.Sequence(Model.Events.ResolutionKind.EffectResolution, game, source);
            if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, handCard.Id));
            source[RiftUsedTurn] = game.Turn;
            ((Minion)source).IsExhausted = true;
            if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", 0, handCard.Id));
            game.TaskQueue.StartEvent();
            using (controller.HandZone.BeginCardStateMutation()) RetireHiddenHandCard(handCard);
            var context = new EffectSystem.EffectContext(game, controller.Id, source.Id);
            string[] beasts = { "TLC_446t2", "TLC_446t3", "TLC_446t4" };
            for (int i = 0; i < 2 && !controller.BoardZone.IsFull; i++)
                context.Summon(beasts[game.Random.Next(beasts.Length)]);
            game.ProcessTasks();
            game.TaskQueue.EndEvent();
            if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            do { game.DeathProcessingAndAuraUpdate(); }
            while (game.SummonedMinions.Count != 0 || game.DeadMinions.Count != 0 || game.DeadBoardObjects.Count != 0);
            controller.NumOptionsPlayedThisTurn++;
            if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            return true;
        }

        public static void CorpsesSpent(Controller controller, int amount)
        {
            foreach (var quest in controller.SecretZone.ModernQuests.ToArray())
                if (CorpseSpendQuests.Contains(quest.Card.AssetId)) Advance(quest, amount);
        }

        public static void BeforeMinionBattlecry(Controller player, Minion minion)
        {
            CardSets.Client.Sets.TheFoodChain.Capture(minion);
            // Play rewards belong to the caster even when placement transfers control.
            if (player.AppliedEnchantments == null) return;
            foreach (var effect in player.AppliedEnchantments.ToArray())
                if (PlayedMinionEffects.TryGetValue(effect.Card.AssetId, out var apply)) apply(effect, minion);
        }

        internal static void CombineHeldRewards(Controller controller)
        {
            var hand = controller.HandZone;
            if (hand.CardStateDeferred) return;
            foreach (var pair in HandPairs)
            {
                IPlayable first, second;
                while ((first = hand.FirstOrDefault(c => c.Card.Id == pair.First)) != null
                    && (second = hand.FirstOrDefault(c => c.Card.Id == pair.Second)) != null)
                using (hand.BeginCardStateMutation())
                {
                    bool history = controller.Game.History;
                    if (history) controller.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(
                        BlockType.TRIGGER, first.Id, "", -1, 0));
                    try
                    {
                        int position = Math.Min(first.ZonePosition, second.ZonePosition);
                        RetireHiddenHandCard(first);
                        RetireHiddenHandCard(second);
                        var result = Entity.FromCard(controller, Cards.FromId(pair.Result), new EntityData {
                            [GameTag.ZONE] = (int)Zone.HAND, [GameTag.DISPLAYED_CREATOR] = first.Id
                        });
                        hand.Add(result, position);
                    }
                    finally { if (history) controller.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd()); }
                }
            }
        }

        private static void RetireHiddenHandCard(IPlayable half)
        {
            var controller = half.Controller;
            half[ShatterRules.PrivateHandOwner] = controller.PlayerId;
            CardStateBoundary.EnrollAttachments(half);
            foreach (var effect in half.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()) effect.Remove();
            controller.SetasideZone.Add(controller.HandZone.Remove(half));
            half[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
            if (controller.Game.History) controller.Game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryHideEntity {
                EntityID = half.Id, Zone = Zone.REMOVEDFROMGAME
            });
        }

        public static bool CanInstall(Controller controller, Card card)
        {
            var zone = controller.SecretZone;
            if (card[(GameTag)1192] != 0) return zone.OccupiedSecretSlots + (zone.Quest != null && !zone.ModernQuests.Any(q => q.Card[(GameTag)1192] != 0) ? 1 : 0) < 5
                && !zone.ModernQuests.Any(q => q.Card.Id == card.Id) && zone.Quest?.Card.Id != card.Id;
            if (zone.Quest != null) return false;
            if (zone.ModernQuests.Any(q => q.Card[(GameTag)1192] != 0) && zone.IsFull) return false;
            if (zone.ModernQuests.Any(q => q.Card[(GameTag)1192] == 0))
                return ComponentGroups.TryGetValue(card.AssetId, out int group) && !zone.IsFull
                    && zone.ModernQuests.Where(q => q.Card[(GameTag)1192] == 0).All(q => q.Card.AssetId != card.AssetId
                        && ComponentGroups.TryGetValue(q.Card.AssetId, out int other) && other == group);
            if (Groups.TryGetValue(card.AssetId, out var components)) return zone.FreeSpace >= components.Length;
            // Legacy-only games retain their independent quest slot.
            return !QuestDefinition.TryGet(card.AssetId, out _) || !zone.IsFull;
        }

        public static bool Install(Spell spell)
        {
            if (Groups.TryGetValue(spell.Card.AssetId, out var components))
            {
                if (!CanInstall(spell.Controller, spell.Card)) return false;
                spell.Controller.GraveyardZone.Add(spell);
                foreach (string id in components)
                {
                    var component = (Spell)Entity.FromCard(spell.Controller, Cards.FromId(id), new EntityData {
                        [GameTag.ZONE] = (int)Zone.SECRET, [GameTag.DISPLAYED_CREATOR] = spell.Id
                    });
                    if (!Install(component)) throw new InvalidOperationException("Invalid quest group recipe.");
                }
                return true;
            }
            if (!QuestDefinition.TryGet(spell.Card.AssetId, out var definition)
                || !CanInstall(spell.Controller, spell.Card)) return false;
            spell.QuestState = new QuestState();
            if (DamageListeners.ContainsKey(spell.Card.AssetId)) spell.Controller.MayHaveModernDamageListeners = true;
            spell.QuestProgress = 0;
            spell[GameTag.QUEST_PROGRESS_TOTAL] = definition.RequiredProgress;
            spell.Controller.SecretZone.Add(spell);
            spell.Power.Trigger?.Activate(spell);
            spell.IsExhausted = true;
            return true;
        }

        public static bool Advance(Spell spell, int amount = 1)
        {
            var state = spell?.QuestState;
            if (state == null || amount <= 0 || state.Completing
                || state.RewardedGeneration == state.CompletionGeneration
                || spell.Zone != spell.Controller.SecretZone
                || !spell.Controller.SecretZone.ModernQuests.Contains(spell)
                || !QuestDefinition.TryGet(spell.Card.AssetId, out var definition)) return false;
            int before = spell.QuestProgress;
            int total = spell.QuestTotalProgress;
            if (before >= total) return false;
            spell.QuestProgress = (int)Math.Min(total, (long)before + amount);
            if (spell.QuestProgress < total) return true;

            state.Completing = true;
            state.RewardedGeneration = state.CompletionGeneration;
            if (!definition.Repeatable) spell.ActivatedTrigger?.Remove();
            GrantReward(spell, definition);
            if (definition.Repeatable)
            {
                spell.QuestProgress = 0;
                state.CompletionGeneration++;
                state.Completing = false;
            }
            else
            {
                spell[GameTag.REVEALED] = 1;
                spell.Controller.SecretZone.Remove(spell);
                spell.Controller.GraveyardZone.Add(spell);
            }
            return true;
        }

        internal static void SettleHandCountChanges(Controller controller)
        {
            // Nested task queues drain through their caller; never re-enter the current task.
            if (controller.SecretZone.ModernQuests.Count != 0 && HasHandCountListener(controller)
                && controller.Game.TaskQueue.CurrentTask == null)
                controller.Game.ProcessTasks();
        }

        public static bool HasHandCountListener(Controller controller) => controller.SecretZone.ModernQuests.Any(
            quest => QuestDefinition.TryGet(quest.Card.AssetId, out var definition) && definition.HandCountChanged != null);

        public static void HandCountChanged(Controller controller, int before, int after)
        {
            if (before == after) return;
            var fact = new HandCountFact(before, after, controller.HandZone.Capacity);
            foreach (var quest in controller.SecretZone.ModernQuests)
                if (quest.QuestState is { Completing: false }
                    && QuestDefinition.TryGet(quest.Card.AssetId, out var definition))
                    definition.HandCountChanged?.Invoke(quest, fact);
        }

        public static bool HasPlayedQuest(Controller controller) => controller.PlayHistory.Any(p => p.SourceCard.IsQuest);

        // Called at committed board membership changes, before later effects can undo fullness.
        public static void FriendlyBoardChanged(Controller controller, bool settle = true)
        {
            if (controller.Game.CurrentPlayer != controller || !controller.BoardZone.IsFull) return;
            var quests = controller.SecretZone.ModernQuests;
            for (int i = quests.Count - 1; i >= 0; i--)
            {
                var quest = quests[i];
                var state = quest.QuestState;
                if (state == null || state.LastCountedTurn == controller.Game.Turn
                    || !QuestDefinition.TryGet(quest.Card.AssetId, out var definition)
                    || !definition.CountsFullBoardTurns) continue;
                // Latch before advancing: reward effects can cause more board changes.
                state.LastCountedTurn = controller.Game.Turn;
                var advance = EffectContext.Task(ctx => { ctx.AdvanceQuest(); return TaskState.COMPLETE; });
                advance.IsTrigger = true;
                controller.Game.TaskQueue.Enqueue(advance, controller, quest, null);
            }
            if (settle) SettleBoardChanges(controller);
        }

        internal static void SettleBoardChanges(Controller controller)
        {
            if (controller.SecretZone.ModernQuests.Any(quest =>
                    QuestDefinition.TryGet(quest.Card.AssetId, out var definition) && definition.CountsFullBoardTurns)
                && controller.Game.TaskQueue.CurrentTask == null)
                controller.Game.ProcessTasks();
        }

        public static int PlayerEffectStacks(Controller controller, string enchantmentId) =>
            controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == enchantmentId)?.ScriptTag1 ?? 0;

        internal static void GrantCardReward(Controller controller, string cardId, int creatorId)
        {
            controller.PendingQuestRewards.Enqueue((cardId, creatorId));
            DeliverPendingRewards(controller);
        }

        internal static void DeliverPendingRewards(Controller controller)
        {
            if (controller.HandZone.CardStateMutationActive) return;
            // q12: only create the entity when a hand slot exists; the wait's wire encoding is unpinned.
            while (controller.PendingQuestRewards.Count != 0 && !controller.HandZone.IsFull)
            {
                var pending = controller.PendingQuestRewards.Dequeue();
                // Publish destination before FULL_ENTITY/SHOW so opponent rewards stay hidden.
                var reward = Entity.FromCard(controller, Cards.FromId(pending.CardId), new EntityData {
                    [GameTag.ZONE] = (int)Zone.HAND,
                    [GameTag.DISPLAYED_CREATOR] = pending.CreatorId
                });
                Generic.AddHandPhase(controller, reward);
            }
        }

        private static void GrantReward(Spell spell, QuestDefinition definition)
        {
            var controller = spell.Controller;
            if (spell.Card.Id == "TLC_EVENT_400") { CardSets.Client.BuildABeast.BeginStorm(spell); return; }
            var card = Cards.FromId(definition.RewardCardId);
            switch (definition.RewardKind)
            {
                case QuestRewardKind.CardToHand:
                    GrantCardReward(controller, card.Id, spell.Id);
                    break;
                case QuestRewardKind.PlayerEffect:
                    AddPlayerEffect(controller, spell, card.Id);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
