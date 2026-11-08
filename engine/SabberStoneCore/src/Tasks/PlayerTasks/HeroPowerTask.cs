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
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.PlayerTasks
{
	public class HeroPowerTask : PlayerTask
	{
		public static HeroPowerTask Any(Controller controller, ICharacter target = null, int chooseOne = 0,
			bool skipPrePhase = false, int heroPowerId = 0)
		{
			var task = new HeroPowerTask(controller, target, chooseOne, skipPrePhase);
            if (heroPowerId != 0 && controller.Game.IdEntityDic.TryGetValue(heroPowerId, out var power)) task.Source = power;
            if (heroPowerId != 0 && !(task.Source is HeroPower)) throw new System.ArgumentException("Unknown Hero Power.");
            return task;
		}

		private HeroPowerTask(Controller controller, ICharacter target, int chooseOne, bool skipPrePhase)
		{
			PlayerTaskType = PlayerTaskType.HERO_POWER;
			Game = controller.Game;
			Controller = controller;
			Target = target;
			ChooseOne = chooseOne;
			SkipPrePhase = skipPrePhase;
		}

		public override bool Process()
		{
			bool success = Generic.HeroPower(Controller, Target, ChooseOne, SkipPrePhase, Source as HeroPower);
			return success;
		}

		public override string FullPrint()
		{
			return $"HeroPowerTask => [{Controller.Name}] using {Source ?? Controller.Hero.HeroPower}" +
				   $"{(Target != null ? $" attack {Target}" : "")}";
		}
	}
}
