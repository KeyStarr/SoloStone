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
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class DamageNumberTask : SimpleTask
	{
		public DamageNumberTask(EntityType type, bool spellDmg = false)
		{
			Type = type;
			SpellDmg = spellDmg;
		}

		public EntityType Type { get; set; }

		public bool SpellDmg { get; set; }

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			if (stack.Number < 1) return TaskState.STOP;


			var entities = IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);
            if (game.History) game.PowerHistory.TargetRange(entities, Type);

            bool collectLifesteal = HealingRules.BeginDamage(source as IPlayable);
            try
            {
            using var wave = game.History ? game.PowerHistory.DamageWave(entities.Count) : null;
            foreach (IPlayable p in entities)
				Generic.DamageCharFunc.Invoke(source as IPlayable, p as ICharacter, stack.Number, SpellDmg);

            }
            finally { if (collectLifesteal) HealingRules.EndDamage(source as IPlayable); }

			return TaskState.COMPLETE;
		}
	}
}
