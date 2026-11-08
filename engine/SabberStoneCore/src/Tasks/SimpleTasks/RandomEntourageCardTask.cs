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
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class RandomEntourageTask : SimpleTask
	{
		private readonly int _count;
		private readonly bool _opponent;

		public RandomEntourageTask(int count = 1, bool opponent = false)
		{
			_count = count;
			_opponent = opponent;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			var playable = source as IPlayable;
			if (playable == null) return TaskState.STOP;
            string[] original = LegacyGenerationV1.Entourage(playable.Card);
            string[] entourage = CompanionRules.Entourage(controller, playable.Card, original);
            if (entourage.Length < 1) return TaskState.STOP;

			if (!ReferenceEquals(original, entourage) && _count > 1)
            {
                // Pick distinct slots, not identities: replacement slots may contain the same Beast.
                var owner = _opponent ? controller.Opponent : controller;
                stack.Playables = Enumerable.Range(0, entourage.Length).ToArray().ChooseNElements(_count, game.Random)
                    .Select(i => Entity.FromCard(owner, Cards.FromId(entourage[i]))).ToList();
            }
            else if (_count > 1)
			{
				if (_count > entourage.Length)
					throw new ArgumentOutOfRangeException();

				var ids = new string[_count];
				int i = 0;
				do
				{
					//string pick = Util.Choose(entourage);
					string pick = entourage.Choose(game.Random);
					if (Array.IndexOf(ids, pick) > -1)
						continue;
					ids[i] = pick;
					i++;
				} while (i != _count);

				var list = new List<IPlayable>(_count);
				for (int j = 0; j < _count; j++)
					list.Add(Entity.FromCard(_opponent ? controller.Opponent : controller, Cards.FromId(ids[j])));

				stack.Playables = list;
			}
			else
			{
				IPlayable randomCard = Entity.FromCard(_opponent ? controller.Opponent : controller,
					Cards.FromId(entourage.Choose(game.Random)));
				stack.Playables = new List<IPlayable> {randomCard};
			}

			game.OnRandomHappened(true);

			return TaskState.COMPLETE;
		}
	}
}
