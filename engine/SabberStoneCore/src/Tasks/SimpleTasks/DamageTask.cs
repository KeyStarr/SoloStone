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
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class DamageTask : SimpleTask
	{
		public DamageTask(int amount, int randAmount, EntityType entityType, bool spellDmg = false)
		{
			Amount = amount;
			RandAmount = randAmount;
			Type = entityType;
			SpellDmg = spellDmg;
		}

		public DamageTask(int amount, EntityType entityType, bool spellDmg = false)
		{
			Amount = amount;
			RandAmount = 0;
			Type = entityType;
			SpellDmg = spellDmg;
		}

		public int Amount { get; set; }

		public int RandAmount { get; set; }

		public EntityType Type { get; set; }

		public bool SpellDmg { get; set; }

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			if (Amount < 1 && RandAmount < 1)
				return TaskState.STOP;

			IList<IPlayable> entities = IncludeTask.GetEntities(Type, in controller, source, target, stack?.Playables);
            if (game.History) game.PowerHistory.TargetRange(entities, Type);
            using var visual = game.History ? (entities.Count > 1
                ? game.PowerHistory.Visual(game, Presentation.VisualPhase.DamageGroup, source as IPlayable)
                    ?? game.PowerHistory.Visual(game, Presentation.VisualPhase.Damage, source as IPlayable)
                : game.PowerHistory.Visual(game, Presentation.VisualPhase.Damage, source as IPlayable)) : null;

            bool collectLifesteal = HealingRules.BeginDamage(source as IPlayable);
            try
            {
            using var wave = game.History ? game.PowerHistory.DamageWave(entities.Count) : null;

			for (int i = 0; i < entities.Count; i++)
			{
				int randAmount = 0;
				if (RandAmount > 0)
				{
					randAmount = game.Random.Next(0, RandAmount + 1);
					game.OnRandomHappened(true);
				}

				int amount = Amount + randAmount;

				game.Log(LogLevel.WARNING, BlockType.ACTION, "DamageTask",
					!game.Logging ? "" : $"Amount is {amount} damage of {source}.");

				Generic.DamageCharFunc.Invoke(source as IPlayable, entities[i] as ICharacter, amount, SpellDmg);
			}
            }
            finally { if (collectLifesteal) HealingRules.EndDamage(source as IPlayable); }

			return TaskState.COMPLETE;
		}
	}
}
