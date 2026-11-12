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
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class HealTask : SimpleTask
	{
		public HealTask(int amount, EntityType entityType)
		{
			Amount = amount;
			Type = entityType;
		}

		public int Amount { get; set; }

		public EntityType Type { get; set; }

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			if (Amount < 1) return TaskState.STOP;

			var playable = source as IPlayable;
			//System.Collections.Generic.List<IPlayable> entities = IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);
			//entities.ForEach(p =>
			var entities = IncludeTask.GetEntities(Type, in controller, playable, target, stack?.Playables);
            if (game.History) game.PowerHistory.HealTargetRange(entities, Type);
            SabberStoneCore.Actions.HealingRules.Heal(playable, System.Linq.Enumerable.OfType<ICharacter>(entities), Amount);

			return TaskState.COMPLETE;
		}
	}
}
