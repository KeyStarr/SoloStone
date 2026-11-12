#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System;
using SabberStoneCore.Model.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Exceptions;
using SabberStoneCore.Triggers;

//using TaskInstance = System.ValueTuple<SabberStoneCore.Tasks.ISimpleTask, SabberStoneCore.Model.Entities.Controller, SabberStoneCore.Model.Entities.IEntity, SabberStoneCore.Model.Entities.IEntity>;

namespace SabberStoneCore.Model
{
	public class TaskQueue
	{
		private class TaskInstance
		{
			public readonly ISimpleTask Task;
			public readonly Controller Controller;
			public readonly IEntity Source;
			public readonly IPlayable Target;
            public readonly ShadowResponse Observation;
            public readonly int DeathId;
            public readonly int DeathOrder;
            public readonly bool OrderDeathByPlay;
            public readonly Trigger DeathTrigger;
            public readonly QueuedSpellActivation HistoryCast;
            public readonly bool HistoryTrigger;
            public readonly int? TriggerKeyword;
            public readonly int SubOption;

			public TaskInstance(in ISimpleTask task, in Controller controller, in IEntity source, in IPlayable target, ShadowResponse observation,
                int deathId = 0, bool orderDeathByPlay = false, int? deathOrder = null, Trigger deathTrigger = null, QueuedSpellActivation historyCast = null,
                bool historyTrigger = false, int? triggerKeyword = null, int subOption = -1)
			{
				Task = task;
				Controller = controller;
				Source = source;
				Target = target;
                Observation = observation;
                DeathId = deathId;
                DeathOrder = deathOrder ?? (source is IPlayable playable ? playable.OrderOfPlay : 0);
                OrderDeathByPlay = orderDeathByPlay;
                DeathTrigger = deathTrigger;
                HistoryCast=historyCast;
                HistoryTrigger = historyTrigger || triggerKeyword.HasValue || task.IsTrigger;
                TriggerKeyword = triggerKeyword;
                SubOption = subOption;
                if(historyCast!=null) historyCast.Bodies++;
			}

			public static implicit operator (ISimpleTask, Controller, IEntity, IPlayable) (TaskInstance t)
			{
				return (t.Task, t.Controller, t.Source, t.Target);
			}

			public void Deconstruct(out ISimpleTask simpleTask, out Controller controller, out IEntity entity, out IPlayable target)
			{
				simpleTask = Task;
				controller = Controller;
				entity = Source;
				target = Target;
			}
		}

		//private readonly struct Event
		//{
		//	public readonly Queue<TaskInstance> Queue;

		//	public Event(Queue<TaskInstance> newQueue)
		//	{
		//		Queue = newQueue;
		//	}
		//}

		private readonly Game _game;
		private readonly Stack<Queue<TaskInstance>> _eventStack;
		//private readonly Stack<Event> _eventStack;
		private readonly Queue<TaskInstance> _baseQueue;
		private readonly Queue<TaskInstance> _pendingTasks;
		private int _executionDepth;

		public TaskQueue(Game game)
		{
			_game = game;
			_eventStack = new Stack<Queue<TaskInstance>>();
			//_eventStack = new Stack<Event>();
			_baseQueue = new Queue<TaskInstance>();
			CurrentQueue = _baseQueue;
			_pendingTasks = new Queue<TaskInstance>();
		}

		//private Queue<TaskInstance> CurrentQueue => _eventStack.Count == 0 ? _baseQueue : _eventStack.Peek();
		private Queue<TaskInstance> CurrentQueue { get; set; }

		// nothing left in current event
		//public bool IsEmpty => _eventFlag || CurrentQueue.Count == 0;
		public bool IsEmpty => CurrentQueue == null || CurrentQueue.Count == 0;

		internal bool IsQuiescent => IsEmpty && _baseQueue.Count == 0 && _pendingTasks.Count == 0 &&
			_eventStack.Count == 0 && CurrentTask == null && !_hasPendingTask && _executionDepth == 0;

		public ISimpleTask CurrentTask { get; private set; }
        internal IPlayable CurrentTarget => CurrentTask != null && CurrentQueue?.Count > 0 ? CurrentQueue.Peek().Target : null;

		public void StartEvent()
		{
			//_eventFlag = true;
			//_numEventsStarted++;
			//_eventStack.Push(null);

			_eventStack.Push(CurrentQueue);
			CurrentQueue = null;
		}

		public void EndEvent()
		{



			//if (!_eventFlag)
			//{
			//	if (_eventStack.Count > 0)
			//		_eventStack.Pop();
			//}
			//else
			//{
			//	_eventFlag = false;
			//	_numEventsStarted--;
			//}

			//if (_numEventsStarted > 0)
			//	_eventFlag = true;

			CurrentQueue = _eventStack.Pop();
		}

		public void Enqueue(in ISimpleTask task, in Controller controller, in IEntity source, in IPlayable target,
            int deathId = 0, bool orderDeathByPlay = false, int? deathOrder = null, Trigger deathTrigger = null, QueuedSpellActivation historyCast = null,
            bool historyTrigger = false, int? triggerKeyword = null, int subOption = -1)
		{
			//if (_eventFlag)	// flag = true means Event starts and no tasks queue yet
			//{
			//	if (CurrentQueue.Count != 0) // Check if an ongoing event exists
			//		_eventStack.Push(new Queue<TaskInstance>());

			//	_eventFlag = false;
			//	_numEventsStarted--;
			//}

			//Stack<Queue<TaskInstance>> stack = _eventStack;
			Queue<TaskInstance> queue = CurrentQueue;
			if (queue == null)
			{
				queue = new Queue<TaskInstance>();
				CurrentQueue = queue;
			}

			var capturedTarget = target ?? Actions.PayloadRules.ExecutionCapture(source);
            int repeats = triggerKeyword == (int)GameTag.DEATHRATTLE ? Actions.DeathrattleRules.Repeats(controller,source) : 1;
            for (int i = 0; i < repeats; i++)
                queue.Enqueue(new TaskInstance(task, controller, source, capturedTarget, _game.Resolution.Queued(task), deathId, orderDeathByPlay, deathOrder, deathTrigger, historyCast,
                    historyTrigger, triggerKeyword, subOption));
		}

		public void EnqueueBase(in ISimpleTask task, in Controller controller, in IEntity source, in IPlayable target)
		{
			//_baseQueue.Enqueue((task, controller, source, target));
			//if (!_eventFlag && _eventStack.Count == 0)
			//{
			//	_eventStack.Push(new Queue<TaskInstance>(_baseQueue));
			//	_baseQueue.Clear();
			//}

			_baseQueue.Enqueue(new TaskInstance(in task, in controller, in source, in target, _game.Resolution.Queued(task)));

			_game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "TaskQueue",
				!_game.Logging ? "" : $"{task.GetType().Name} is Enqueued in 0th stack");
		}

		/// <summary>
		/// Queue a task that will be processed after a task is queued and processed.
		/// </summary>
		/// <param name="task"></param>
		public void EnqueuePendingTask(in ISimpleTask task, in Controller controller, in IEntity source, in IPlayable target)
		{
			_pendingTasks.Enqueue(new TaskInstance(in task, in controller, in source, in target, _game.Resolution.Queued(task)));
			_hasPendingTask = true;
		}

		private bool _hasPendingTask;

		public void ResumePendingTasks()
		{
			if (!_hasPendingTask) return;
			_hasPendingTask = false;

			do CurrentQueue.Enqueue(_pendingTasks.Dequeue());
			while (_pendingTasks.Count != 0);
		}

		internal void ObserveOrder()
        {
            if (CurrentQueue == null) return;
            OrderDeathResponses();
            ShadowResponse previous = default;
            foreach (var task in CurrentQueue)
            {
                var current = task.Observation;
                if (current.Id == 0 || current.V2Eligible != true) continue;
                _game.Resolution.CompareOrder(previous, current);
                previous = current;
            }
        }

        private void OrderDeathResponses()
        {
            HashSet<int> deaths = null;
            foreach (var task in CurrentQueue)
                if (task.OrderDeathByPlay && task.DeathId != 0)
                    (deaths ??= new HashSet<int>()).Add(task.DeathId);
            if (deaths == null) return;

            // A death's Deathrattles and listeners share play order; separate deaths keep their existing slots.
            // Opt-in preserves historical FIFO for death events with no ordered listener.
            var tasks = CurrentQueue.ToArray();
            foreach (int death in deaths)
            {
                var slots = Enumerable.Range(0, tasks.Length).Where(i => tasks[i].DeathId == death).ToArray();
                var ordered = slots.Select(i => tasks[i]).OrderBy(t => t.DeathOrder).ToArray();
                for (int i = 0; i < slots.Length; i++) tasks[slots[i]] = ordered[i];
            }
            CurrentQueue.Clear();
            foreach (var task in tasks) CurrentQueue.Enqueue(task);
        }

        public TaskState Process()
        {
            var observation = CurrentQueue.Peek().Observation;
            using var resolution = _game.Resolution.Enter(observation.Id != 0 ? ResolutionKind.TriggerResolution : ResolutionKind.TaskResolution,
                CurrentQueue.Peek().Source?.Id ?? 0);
            using var callback = _game.Resolution.Suspend();
            // An opted-in listener removed by an earlier death response must not resolve its queued effect.
            if (CurrentQueue.Peek().DeathTrigger?.IsActive == false)
            {
                CurrentQueue.Dequeue();
                _game.Resolution.Resolved(observation);
                return TaskState.COMPLETE;
            }
			(ISimpleTask task, Controller controller, IEntity source, IPlayable target) = CurrentQueue.Peek();
			using var historyExecution = _game.Facts.Executing(source);
            using var historyCast = _game.Facts.QueuedCast(CurrentQueue.Peek().HistoryCast);
			if(CurrentQueue.Peek().TriggerKeyword==(int)GameTag.DEATHRATTLE)
			{
				// Negative ids are explicit activations; positive ids link actual deaths.
				int deathId = CurrentQueue.Peek().DeathId;
				_game.Facts.DeathrattleResolved(deathId > 0 ? deathId : deathId < 0 ? -deathId : source?.Id ?? 0, source, controller, deathId <= 0);
			}
            ISimpleTask temp = CurrentTask;
			CurrentTask = task;

			//if (currentTask is StateTaskList tasks)
			//	tasks.Stack = new TaskStack(_game);

			_game.Log(LogLevel.VERBOSE, BlockType.TRIGGER, "TaskQueue", !_game.Logging ? "" : $"LazyTask[{source}]: '{CurrentTask.GetType().Name}' is processed!" +
			                                                                                $"'{source.Card.Text?.Replace("\n", " ")}'");
            bool spellBody = _game.History && !CurrentQueue.Peek().HistoryTrigger
                && Actions.Generic.IsTrackedSpellBody(_game, source) && !_game.PowerHistory.HasPowerAncestor(source.Id);
            bool historyTrigger = CurrentQueue.Peek().HistoryTrigger;
            PowerHistoryBlockStart historyStart = null;
            bool parentHadEffects = false;
			if (_game.History)
            {
                parentHadEffects = _game.PowerHistory.CurrentBlockHasEffects;
				historyStart = PowerHistoryBuilder.BlockStart(CurrentQueue.Peek().HistoryTrigger ? BlockType.TRIGGER : BlockType.POWER, source.Id, "", CurrentQueue.Peek().HistoryTrigger ? -1 : CurrentQueue.Peek().SubOption, target?.Id ?? 0,
                    triggerKeyword: CurrentQueue.Peek().TriggerKeyword);
				_game.PowerHistory.Add(historyStart);
                if (!CurrentQueue.Peek().HistoryTrigger) _game.PowerHistory.TargetHeader(source, target);
            }

			TaskState success;
#if DEBUG
			try
			{
#endif
                // A granted Deathrattle runs as the recipient's own block with the enchantment as target; presentation recipes need it.
                if (_game.History) _game.PowerHistory.GrantedDeathrattle = CurrentQueue.Peek().TriggerKeyword == (int)GameTag.DEATHRATTLE ? target as Enchantment : null;
                try
                {
				using (_game.History ? _game.PowerHistory.Visual(_game, Presentation.VisualPhase.BoardBuff) : null)
                    success = task.Process(in _game, in controller, in source, in target);
                }
                finally { if (_game.History) _game.PowerHistory.GrantedDeathrattle = null; }
#if DEBUG
			}
			catch (Exception e)
			{
				throw new TaskException(
					$"Exception occurs during processing a task.\nTask:{task}, Source:{source}, Target:{target}", e);
			}
#endif

			historyCast?.Complete();
            // A human choice suspends this activation's history, not the invocation-local task queue.
            if (_game.History && !(spellBody && Actions.Generic.SpellBodyPending(_game))
                && !DiscardUnfiredSecret(historyTrigger, source, historyStart, parentHadEffects))
                _game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());

			// reset between task execution
			//_game.TaskStack.Reset();

			CurrentQueue.Dequeue();
			CurrentTask = temp;

			_game.Resolution.Resolved(observation);
            // The recipe owns its remaining work; the surrounding legacy queue still finishes atomically.
            return success == TaskState.SUSPENDED && _game.Resolution.HasSuspendedEffect ? TaskState.COMPLETE : success;
		}

		public void Execute(in ISimpleTask task, in Controller controller, in IPlayable source, in IPlayable target, int number = 0,
            bool historyTrigger = false, int subOption = -1, int? triggerKeyword = null)
		{
            var observation = _game.Resolution.Queued(task);
            using var resolution = _game.Resolution.Enter(observation.Id != 0 ? ResolutionKind.TriggerResolution : ResolutionKind.TaskResolution, source?.Id ?? 0);
            using var callback = _game.Resolution.Suspend();

			_game.Log(LogLevel.VERBOSE, BlockType.TRIGGER, "TaskQueue", !_game.Logging ? "" : $"PriorityTask[{source}]: '{task.GetType().Name}' is processed!" +
			                                                                                $"'{source.Card.Text?.Replace("\n", " ")}'");

			// power block
            PowerHistoryBlockStart historyStart = null;
            bool parentHadEffects = false;
			if (controller.Game.History)
            {
                parentHadEffects = _game.PowerHistory.CurrentBlockHasEffects;
				historyStart = PowerHistoryBuilder.BlockStart(historyTrigger || task.IsTrigger ? BlockType.TRIGGER : BlockType.POWER, source.Id, "", historyTrigger || task.IsTrigger ? -1 : subOption, target?.Id ?? 0,
                    triggerKeyword: triggerKeyword);
				controller.Game.PowerHistory.Add(historyStart);
                if (!historyTrigger && !task.IsTrigger) _game.PowerHistory.TargetHeader(source, target);
            }

			_executionDepth++;
			try
            {
                using var visual = _game.History ? _game.PowerHistory.Visual(_game, Presentation.VisualPhase.BoardBuff) : null;
                task.Process(in _game, in controller, source, target);
            }
			finally { _executionDepth--; }
            _game.Resolution.Resolved(observation);

			if (controller.Game.History && !DiscardUnfiredSecret(historyTrigger || task.IsTrigger, source, historyStart, parentHadEffects))
				controller.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());

			//_game.TaskStack.Reset();
		}

        // Retail: a Secret whose event happens but whose effect cannot take place (Mirror Entity or Noble Sacrifice on a
        // full board, Mirror Entity on a mortally wounded minion) simply stays in place, unrevealed. The engine already
        // keeps such a Secret; its queued activation must not publish a TRIGGER block either, because the native client
        // plays the Secret reveal for any TRIGGER sourced by a Secret. History-only: no state, RNG or HistoryFacts change.
        private bool DiscardUnfiredSecret(bool historyTrigger, IEntity source, PowerHistoryBlockStart start, bool parentHadEffects) =>
            historyTrigger && source is Spell secret && secret.IsSecret && secret.Zone?.Type == Zone.SECRET
            && _game.PowerHistory.DiscardEmptyBlock(start, parentHadEffects);

		public void ClearCurrentEvent()
		{
			CurrentQueue.Clear();
		}

		public void Clone()
		{
			//return new TaskQueue();
		}
	}


	internal class EventMetaData
	{
		public IPlayable EventSource { get; set; }
		public IPlayable EventTarget { get; set; }
		public int EventNumber { get; set; }

		public EventMetaData(IPlayable source, IPlayable target, int number = 0)
		{
			EventSource = source;
			EventTarget = target;
			EventNumber = number;
		}
	}
}
