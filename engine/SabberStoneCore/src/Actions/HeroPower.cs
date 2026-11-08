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
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
	public static partial class Generic
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	{
		public static bool HeroPower(Controller c, ICharacter target = null, int chooseOne = 0, bool skipPrePhase = false, HeroPower power = null)
		{
			return ResolveHeroPower(c, power ?? c.Hero.HeroPower, target, chooseOne, skipPrePhase);
		}

		public static Func<Controller, ICharacter, int, bool, bool> HeroPowerBlock
            => (c, target, chooseOne, skipPrePhase) => ResolveHeroPower(c, c.Hero.HeroPower, target, chooseOne, skipPrePhase);

        private static bool ResolveHeroPower(Controller c, HeroPower heroPower, ICharacter target, int chooseOne, bool skipPrePhase)
            {
                if (!System.Linq.Enumerable.Contains(c.HeroPowers, heroPower)) return false;
                if (heroPower == c.SecondaryHeroPower && (!heroPower.IsPlayable || !heroPower.IsValidPlayTarget(target))) return false;
				Game game = c.Game;

				if (heroPower.IsPassiveHeroPower || ImbueRules.IsPassivePower(heroPower.Card)) return false;

				if (!skipPrePhase)
					if (!heroPower.IsPlayable || !heroPower.IsValidPlayTarget(target))
						return false;

				if (!PayPhase.Invoke(c, heroPower)) return false;

				if (target != null)
				{
					bool randomTargets = game.TriggerManager.HasTargetTrigger && BasicHeroPowerRules.RandomTargetsActive(c);
                    if (randomTargets) heroPower.CardTarget = target.Id;
                    game.CurrentEventData = new EventMetaData(heroPower, target);
					Trigger.ValidateTriggers(game, heroPower, SequenceType.Target);
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnTargetTrigger(heroPower);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
                    if (randomTargets && heroPower.CardTarget != 0 && heroPower.CardTarget != target.Id)
                        target = game.IdEntityDic[heroPower.CardTarget] as ICharacter;
                }

				// play block
				if (game.History)
					game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.PLAY, heroPower.Id, "", 0, target?.Id ?? 0));

				game.Log(LogLevel.INFO, BlockType.ACTION, "HeroPowerBlock", !game.Logging? "":$"Play HeroPower {heroPower}[{heroPower.Card.Id}]{(target != null ? $" targeting {target}" : "")}.");

                if (RepeatRules.DoublesHeroPower(c))
                {
                    RepeatRules.BeginHeroPower(heroPower, target, chooseOne);
                    if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                    return true;
                }

				game.TaskQueue.StartEvent();
				heroPower.ActivateTask(PowerActivation.POWER, target, chooseOne);
				game.ProcessTasks();
				game.TaskQueue.EndEvent();

				game.DeathProcessingAndAuraUpdate();

				if (game.History)
					game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());

				heroPower.IsExhausted = true;
				c.HeroPowerActivationsThisTurn++;
				c.NumTimesHeroPowerUsedThisGame++;

				game.TaskQueue.StartEvent();
				game.TriggerManager.OnInspireTrigger(heroPower);
				game.ProcessTasks();
				game.TaskQueue.EndEvent();

				game.DeathProcessingAndAuraUpdate();
				BasicHeroPowerRules.AfterUse(heroPower);
                game.CurrentEventData = null;
				return true;
			}
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
