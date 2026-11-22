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
using System.Collections.Generic;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class SplitTask : SimpleTask
	{
		public SplitTask(int amount, EntityType type)
		{
			Amount = amount;
			Type = type;
		}

		public int Amount { get; set; }

		public EntityType Type { get; set; }

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			//List<IPlayable> entities = IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);

			//if (entities.Count == 0)
			//	return TaskState.STOP;

			IList<IPlayable> entities =
				IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);

			//if (game.Splitting && game.Splits.Count == 0)
			//{
			//	if (Amount == 1)
			//	{
			//		//entities.ForEach(p =>
			//		foreach (IPlayable p in IncludeTask.GetEntities(Type, in controller, source, target,
			//			stack?.Playables))
			//		{
			//			//game.Dump("SplitTask", $"{sets.IndexOf(p)}: {string.Join(";", p)}");
			//			stack.Playables = new List<IPlayable> {p};
			//			State = TaskState.COMPLETE;
			//			Game clone = game.Clone();
			//			game.Splits.Add(clone);
			//		}
			//	}
			//	else
			//	{
			//		List<IEnumerable<IPlayable>> sets = Util.GetPowerSet(entities.ToList())
			//			.Where(plist => plist.Count() == Amount).ToList();
			//		foreach (IEnumerable<IPlayable> plist in sets)
			//		{
			//			game.Dump("SplitTask", $"{sets.IndexOf(plist)}: {String.Join(";", plist)}");
			//			stack.Playables = plist.ToList();
			//			State = TaskState.COMPLETE;
			//			Game clone = game.Clone();
			//			game.Splits.Add(clone);
			//		}
			//	}

			//	//game.TaskQueue.TaskList.Clear();
			//	game.TaskQueue.ClearCurrentEvent();
			//	return TaskState.STOP;
			//}


			//stack.Playables = entities is List<IPlayable> list ? list : entities.ToList();
			stack.Playables = entities;

			return TaskState.COMPLETE;
		}
	}

	public class RandomTask : SimpleTask
	{
		private readonly int _amount;
		private readonly EntityType _type;

		/// <summary>
		/// Set while the chain is built when the picked entities go straight into a damage or destroy task: such an
		/// effect ignores a character that is already mortally wounded or pending destruction (it cannot be picked
		/// again while the death phase is still to come). Healing, buffs and area effects still see those characters.
		/// </summary>
		internal bool SkipMortallyWounded { get; private set; }

		/// <summary>
		/// A random pick whose result a card then damages or destroys: use it where a card builds and runs the pick itself
		/// (a chain built with <c>ComplexTask.Create</c> or a StateTaskList is marked automatically).
		/// </summary>
		internal static RandomTask Harmful(int amount, EntityType type) => new RandomTask(amount, type) { SkipMortallyWounded = true };

		/// <summary>Marks every random pick in a chain that feeds a damage or destroy task on the picked stack.</summary>
		internal static void MarkHarmfulPicks(ISimpleTask[] chain)
		{
			for (int i = 0; i + 1 < chain.Length; i++)
				if (chain[i] is RandomTask pick &&
				    (chain[i + 1] is DamageTask { Type: EntityType.STACK } ||
				     chain[i + 1] is DamageNumberTask { Type: EntityType.STACK } ||
				     chain[i + 1] is AttackTask { DefenderType: EntityType.STACK } ||
				     chain[i + 1] is DestroyTask { TargetType: EntityType.STACK }))
					pick.SkipMortallyWounded = true;
		}

		public RandomTask(int amount, EntityType type)
		{
			_amount = amount;
			_type = type;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			//IEnumerable<IPlayable> temp =
			//	IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);
			//List<IPlayable> entities = temp is List<IPlayable> list ? list : temp.ToList();

			IList<IPlayable> entities = IncludeTask.GetEntities(in _type, in controller, source, target, stack?.Playables);

			if (SkipMortallyWounded && entities.Count != 0)
			{
				// Scan first: most picks have no dead candidate and then need no new list (same order and count, same RNG use).
				bool anyDead = false;
				foreach (IPlayable candidate in entities)
					if (candidate is ICharacter dead && dead.IsDead) { anyDead = true; break; }
				if (anyDead)
				{
					var living = new List<IPlayable>(entities.Count);
					foreach (IPlayable candidate in entities)
						if (!(candidate is ICharacter character && character.IsDead))
							living.Add(candidate);
					entities = living;
				}
			}

			if (entities.Count == 0)
				return TaskState.STOP;

			if (entities.Count <= _amount)
			{
				stack.Playables = entities;
                if (game.History) game.PowerHistory.SelectTargets(stack.Playables);
				return TaskState.COMPLETE;
			}

			//stack.Playables = new List<IPlayable>();
			//for (int i = 0; i < _amount && entities.Count > 0; i++)
			//{
			//	IPlayable randPlayable = entities[Util.Random.Next(entities.Count)];
			//	entities.Remove(randPlayable);
			//	stack?.Playables.Add(randPlayable);
			//}

			stack.Playables = _amount == 1 ?
				new[] {entities[game.Random.Next(entities.Count)]} :
				((IReadOnlyList<IPlayable>)entities).ChooseNElements(_amount, game.Random);

            if (game.History) game.PowerHistory.SelectTargets(stack.Playables);

			game.OnRandomHappened(true);

			return TaskState.COMPLETE;
		}
	}
}
