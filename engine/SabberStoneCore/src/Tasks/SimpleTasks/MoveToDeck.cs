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
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class MoveToDeck : SimpleTask
	{
		private readonly EntityType _type;
		private readonly bool _opponent;

		public MoveToDeck(EntityType type, bool opponent = false)
		{
			_type = type;
			_opponent = opponent;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			using var historyShuffle = game.Facts.Shuffle(controller.Id);
			var c = _opponent ? controller.Opponent : controller;

			foreach (IPlayable p in IncludeTask.GetEntities(in _type, in controller, source, target, stack?.Playables))
			{
				if (p.Zone?.Type == Zone.DECK)
					continue;
				bool fromPlay = p.Zone?.Type == Zone.PLAY;
				IPlayable removedEntity = p.Zone?.Remove(p) ?? p;
				removedEntity.Reset();
				if (removedEntity.Controller != c)
				{
					removedEntity.Controller = c;
					removedEntity[GameTag.CONTROLLER] = c.PlayerId;
				}

				game.Log(LogLevel.INFO, BlockType.PLAY, "MoveToDeck",
					!game.Logging ? "" : $"{c.Name} takes control of {p} and shuffles it into their deck.");

				// Working assumption (departs from wiki Advanced rulebook Z1, which kills a board minion that cannot enter
				// a full deck where it stands, with its Deathrattle): the card goes to the deck owner's graveyard without
				// dying (no death event, no Deathrattle), as Typhoon does. It must never be left without a zone.
				bool shuffled = fromPlay ? Generic.ShuffleFromBoardIntoDeck(c, source, p) : Generic.ShuffleIntoDeck(c, source, p);
				if (!shuffled)
					c.GraveyardZone.Add(p);
			}
			return TaskState.COMPLETE;
		}
	}
}
