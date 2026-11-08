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
using System.Diagnostics;
using System.Text;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks
{
	public enum TaskState
	{
		READY, RUNNING, COMPLETE, STOP, SUSPENDED
	}


	[DebuggerDisplay("{DebuggerDisplay, nq}")]
	public class StateTaskList : ISimpleTask
	{
		private readonly ISimpleTask[] _tasks;

        internal bool RemovesEnchantmentFirst => _tasks.Length > 0 &&
            (_tasks[0] is SimpleTasks.RemoveEnchantmentTask || _tasks[0] is StateTaskList nested && nested.RemovesEnchantmentFirst);

		private StateTaskList(ISimpleTask[] list)
		{
			// An atomic legacy chain has no cursor. Require explicit opt-in instead of losing its tail.
            foreach (var task in list)
                if (task is SuspendableTask || task is SimpleTasks.DiscoverThenContinueTask || task is SimpleTasks.SelectEntityThenContinueTask || task is SimpleTasks.DecisionBattlecryTask || task is SimpleTasks.DecisionSpellTask)
                    throw new System.ArgumentException("Use StateTaskList.Suspendable for continuation-aware steps.");
            _tasks = list;
		}

		public TaskState State { get; set; } = TaskState.READY;

		public bool IsTrigger { get; set; }

		public TaskState Process(in Game game, in Controller controller, in IEntity source, in IPlayable target,
			in TaskStack stack = null)
		{
			TaskStack currentStack = stack ?? new TaskStack();

			// Card recipes are shared across games; execution status belongs in the return value.

			for (int i = 0; i < _tasks.Length; ++i)
				if (_tasks[i].Process(in game, in controller, in source, in target, in currentStack) !=
				    TaskState.COMPLETE)
					break;

			//Game.TaskStack = temp;
			return TaskState.COMPLETE;
		}


		public static SuspendableTask Suspendable(string recipeKey, params ISimpleTask[] list)
            => new SuspendableTask(recipeKey, list);

        public static SuspendableTask SelectRepeated(string recipeKey, Model.Effects.SelectorSpec selector,
            int count, ISimpleTask afterEachPick)
        {
            if (selector == null || afterEachPick == null) throw new System.ArgumentNullException();
            if (count < 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            var steps = new ISimpleTask[checked(count * 2)];
            for (int i = 0; i < count; i++)
            {
                steps[i * 2] = new SimpleTasks.SelectEntityThenContinueTask(selector);
                steps[i * 2 + 1] = afterEachPick;
            }
            return Suspendable(recipeKey, steps);
        }

        public static StateTaskList Chain(params ISimpleTask[] list)
		{
			return new StateTaskList(list);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			for (int i = 0; i < _tasks.Length; i++)
				sb.Append($"[{_tasks[i].GetType().Name}]");

			return sb.ToString();
		}

		private string DebuggerDisplay
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var task in _tasks)
				{
					sb.Append($"[{task.GetType().Name}]");
				}

				return sb.ToString();
			}
		}
	}
}
