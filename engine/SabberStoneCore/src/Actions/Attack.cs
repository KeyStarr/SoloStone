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
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
	public static partial class Generic
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	{
		public static Func<Controller, ICharacter, ICharacter, bool, bool, bool> AttackBlock
			=> delegate (Controller c, ICharacter source, ICharacter target, bool skipPrePhase, bool skipDeathPhase)
			{
				using var resolution = c.Game.Resolution.Sequence(ResolutionKind.Attack, c.Game, source);
				Game g = c.Game;

				if (skipPrePhase)
				{
					if (g.History)
					{
						g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.ATTACK, source.Id, "", -1, target.Id));

						// Force proposals even when the previous attack left the same engine values.
						if (g.ProposedAttacker == source.Id)
							g.PowerHistory.Add(PowerHistoryBuilder.TagChange(g.Id, GameTag.PROPOSED_ATTACKER, source.Id));
						if (g.ProposedDefender == target.Id)
							g.PowerHistory.Add(PowerHistoryBuilder.TagChange(g.Id, GameTag.PROPOSED_DEFENDER, target.Id));
						g.ProposedAttacker = source.Id;
						g.ProposedDefender = target.Id;
					}
					g.CurrentEventData = new EventMetaData(source, target);
				}
				else if (!PreAttackPhase.Invoke(c, source, target))
					return false;

				g.Facts.AcceptPlayerAction(source);
				using var historyAttack = g.Facts.Attack(source,target);
                using var combatContext = DeathContextRules.EnterAttack(source);

				int previousDepth = g[CombatRules.PreparationDepth], previousPasses = g[CombatRules.DeathPasses];
                bool continues; int deathPasses;
                g[CombatRules.PreparationDepth] = previousDepth + 1;
                g[CombatRules.DeathPasses] = 0;
                try { continues = OnAttackTrigger.Invoke(g, source, target); deathPasses = g[CombatRules.DeathPasses]; }
                finally
                {
                    if (previousDepth == 0) { g.NativeTags.Remove(CombatRules.PreparationDepth); g.NativeTags.Remove(CombatRules.DeathPasses); }
                    else { g[CombatRules.PreparationDepth] = previousDepth; g[CombatRules.DeathPasses] += previousPasses; }
                }
                if (!continues)
				{
					historyAttack.SetTarget((ICharacter)g.CurrentEventData.EventTarget);
                    historyAttack.Finish(false);
                    if (deathPasses >= 2 && !historyAttack.IsForced)
                    {
                        source.NumAttacksThisTurn++;
                        if (!skipDeathPhase && source.NumAttacksThisTurn >= CombatRules.AttackLimit(source)) source.IsExhausted = true;
                    }
					// end block
					if (g.History)
						EndAttackHistory(g);
					if (!skipDeathPhase)
						g.DeathProcessingAndAuraUpdate();
					g.CurrentEventData = null;
					// A trigger canceled combat after a valid attack was accepted.
					return true;
				}
                if (PastColossalCombat.Replace(source, historyAttack.IsForced))
                {
                    historyAttack.SetTarget((ICharacter)g.CurrentEventData.EventTarget);
                    historyAttack.Finish(false);
                    if (g.History) EndAttackHistory(g);
                    if (!skipDeathPhase) g.DeathProcessingAndAuraUpdate();
                    g.CurrentEventData = null;
                    g.NextStep = Step.MAIN_ACTION;
                    return true;
                }
				Trigger.ValidateTriggers(g, source, SequenceType.Target);
				g.Resolution.CaptureValidation(SequenceType.Target);
				if (!AttackPhase.Invoke(c, source, skipDeathPhase, skipPrePhase && historyAttack.IsForced))
				{
					historyAttack.SetTarget((ICharacter)g.CurrentEventData.EventTarget);
                    historyAttack.Finish(false);
					// end block
					if (g.History)
						EndAttackHistory(g);
					if (!skipDeathPhase)
						g.DeathProcessingAndAuraUpdate();
					g.CurrentEventData = null;
					return true;
				}
				// end block
				if (g.History)
					EndAttackHistory(g);

				historyAttack.Finish(true);
				using (g.Resolution.Enter(ResolutionKind.AttackAfter, source.Id))
				{
					g.TaskQueue.StartEvent();
					source.OnAfterAttackTrigger();
					g.TriggerManager.OnAfterAttackTrigger(source);
					g.ProcessTasks();
					g.TaskQueue.EndEvent();
				}

				if (!skipDeathPhase)
					g.DeathProcessingAndAuraUpdate();
				g.CurrentEventData = null;
				g.NextStep = Step.MAIN_ACTION;
				CthunRules.AfterAttack(g, c);
                AfterAttackRules.Flush(g);

				return true;
			};

		private static void EndAttackHistory(Game game)
		{
			game.PowerHistory.Add(PowerHistoryBuilder.TagChange(game.Id, GameTag.PROPOSED_ATTACKER, 0));
			game.PowerHistory.Add(PowerHistoryBuilder.TagChange(game.Id, GameTag.PROPOSED_DEFENDER, 0));
			game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
		}

		private static Func<Controller, ICharacter, ICharacter, bool> PreAttackPhase
			=> delegate (Controller c, ICharacter source, ICharacter target)
			{
				if (c != source.Controller || c == target.Controller)
				{
					if (source[GameTag.AUTOATTACK] == 1)
						return true;
					c.Game.Log(LogLevel.ERROR, BlockType.ATTACK, "PreAttackPhase", !c.Game.Logging? "":"wrong controller in phase.");
					return false;
				}
				if (!source.CanAttack || !source.IsValidAttackTarget(target))
				{
					if (source[GameTag.AUTOATTACK] == 1)
						return true;
					c.Game.Log(LogLevel.ERROR, BlockType.ATTACK, "PreAttackPhase", !c.Game.Logging? "":"can't attack with this card.");
					return false;
				}
				var hero = source as Hero;

				c.Game.Log(LogLevel.INFO, BlockType.ATTACK, "PreAttackPhase", !c.Game.Logging? "":$"[PreAttackPhase]{source}[ATK:{source.AttackDamage}/HP:{source.Health}{(hero != null ? $"/ARM:{hero.Armor}" : "")}] " +
						$"{(hero?.Weapon != null ? $"[{hero.Weapon}[A:{hero.Weapon.AttackDamage}/D:{hero.Weapon.Durability}]] " : "")}is attacking " +
						$"{target}[ATK:{target.AttackDamage}/HP:{target.Health}].");

				// attack block
				if (c.Game.History)
				{
					c.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.ATTACK, source.Id, "", -1, target.Id));

					// Force proposals even when the previous attack left the same engine values.
					if (c.Game.ProposedAttacker == source.Id)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(c.Game.Id, GameTag.PROPOSED_ATTACKER, source.Id));
					if (c.Game.ProposedDefender == target.Id)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(c.Game.Id, GameTag.PROPOSED_DEFENDER, target.Id));
					c.Game.ProposedAttacker = source.Id;
					c.Game.ProposedDefender = target.Id;
				}

				c.Game.CurrentEventData = new EventMetaData(source, target);

				// TODO: need to be manipulated for 50% chance to attack  someone else 

				return true;
			};

		private static Func<Game, ICharacter, ICharacter, bool> OnAttackTrigger
			=> delegate (Game g, ICharacter source, ICharacter target)
			{
				using var resolution = g.Resolution.Enter(ResolutionKind.AttackPreparation, source.Id);
				//if (g.History)
				//{

				//}
				if (g.History)
				{
					if (source.IsAttacking) g.PowerHistory.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.ATTACKING, 1));
					if (target.IsDefending) g.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.DEFENDING, 1));
				}
				source.IsAttacking = true;
				target.IsDefending = true;

				// Invoke onAttackTrigger
				//Trigger.ValidateTriggers(g, source, SequenceType.Attack);

				g.TaskQueue.StartEvent();
				source.Game.TriggerManager.OnAttackTrigger(source);
				g.ProcessTasks();
				g.TaskQueue.EndEvent();
				if (DeathContextRules.ConsumeAttackCancellation(source) || source.ToBeDestroyed || target.ToBeDestroyed || (source.Zone != null && source.Zone.Type != Zone.PLAY) ||
				    (target.Zone != null && target.Zone.Type != Zone.PLAY))
				{
					source.IsAttacking = false;
					target.IsDefending = false;

					g.Log(LogLevel.INFO, BlockType.ATTACK, "OnAttackTrigger",
						!g.Logging ? "" : "Oh shizzle, something died to the shizzeling of triggering ...");
					return false;
				}

				return true;
			};

		private static Func<Controller, ICharacter, bool, bool, bool> AttackPhase
			=> delegate (Controller c, ICharacter source, bool noExhaustion, bool forcedAttack)
			{
				using var resolution = c.Game.Resolution.Enter(ResolutionKind.AttackDamage, source.Id);
				Game game = c.Game;

				game.TaskQueue.StartEvent();
				game.TriggerManager.OnTargetTrigger(source);
				game.ProcessTasks();
				game.TaskQueue.EndEvent();

				var hero = source as Hero;
				var minion = source as Minion;
				//if (!game.IdEntityDic.TryGetValue(game.ProposedDefender, out IPlayable proposedDefender))
				//{
				//	game.Log(LogLevel.INFO, BlockType.ATTACK, "AttackPhase", !game.Logging? "":"target wasn't found by proposed defender call.");
				//	source.IsAttacking = false;
				//	source.IsDefending = false;
				//	return false;
				//}

				var target = (ICharacter) game.CurrentEventData.EventTarget;
                game.Facts.CombatTarget(target);
				// Redirection changes event data, not the original defender's engine flags.
				if (game.History && game.PowerHistory.CurrentBlock?.Target != target.Id)
				{
					int original = game.PowerHistory.CurrentBlock?.Target ?? 0;
					if (original != 0) game.PowerHistory.Add(PowerHistoryBuilder.TagChange(original, GameTag.DEFENDING, 0));
					game.PowerHistory.Add(PowerHistoryBuilder.TagChange(game.Id, GameTag.PROPOSED_DEFENDER, target.Id));
					game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.DEFENDING, 1));
				}

				// Force the game into MAIN_COMBAT step!
				game.Step = Step.MAIN_COMBAT;

				// Save defender's attack as it might change after being damaged (e.g. enrage)
				int targetAttack = /*targetHero != null ? 0 : */target.AttackDamage;
				int sourceAttack = /*hero?.TotalAttackDamage ?? */source.AttackDamage;

				var cleaveTargets = CombatRules.CleaveTargets(source, target);
                int excess = DeathContextRules.AllocateExcess(source, target, sourceAttack);

				int targetRealDamage = target.TakeDamage(source, sourceAttack, combat: true);
				bool targetDamaged = targetRealDamage > 0;

				// freeze target if attacker is freezer
				if (targetDamaged && minion != null && minion.Freeze)
				{
					game.Log(LogLevel.VERBOSE, BlockType.ATTACK, "AttackPhase", !game.Logging? "":$"freezer attacker has frozen target.");
					game.Facts.Freeze(target,(IEntity)source);
				}

				// ignore damage from defenders with 0 attack
				if (targetAttack > 0)
				{
					int sourceRealDamage = source.TakeDamage(target, targetAttack, combat: true);
					bool sourceDamaged = sourceRealDamage > 0;

					// freeze source if defender is freezer
					var targetMinion = target as Minion;
					if (sourceDamaged && targetMinion != null && targetMinion.Freeze)
					{
						game.Facts.Freeze(source,(IEntity)target);
					}


				}

                DeathContextRules.ApplyExcess(source, excess);

                foreach (var neighbor in cleaveTargets)
                    neighbor.TakeDamage(source, sourceAttack);

				if (minion != null && minion.HasStealth)
				{
					minion.HasStealth = false;
				}

				// remove durability from weapon if hero attack
				if (hero?.Weapon != null && !hero.Weapon.IsImmune)
				{
					//hero.Weapon.Durability -= hero.Weapon.Durability > 0 ? 1 : 0;
					hero.Weapon.Damage += 1;
				}

				//if (game.History)
				//{

				//}
				if (game.History)
				{
					if (!source.IsAttacking) game.PowerHistory.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.ATTACKING, 0));
					if (!target.IsDefending) game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.DEFENDING, 0));
				}
				source.IsAttacking = false;
				target.IsDefending = false;

				int numAtk = source.NumAttacksThisTurn + 1;

				c.NumOptionsPlayedThisTurn++;
				if (minion != null)
					c.NumFriendlyMinionsThatAttackedThisTurn++;

				// set exhausted ...
				if (!noExhaustion && !forcedAttack && numAtk >= CombatRules.AttackLimit(source))
				{
					game.Log(LogLevel.INFO, BlockType.ATTACK, "AttackPhase", !game.Logging? "":$"{source} is now exhausted.");
					source.IsExhausted = true;
				}

				// Forced combat keeps normal attack allowance; events and damage still count.
				if (!forcedAttack) source.NumAttacksThisTurn = numAtk;

				game.Log(LogLevel.INFO, BlockType.ATTACK, "AttackPhase", !game.Logging? "":$"[AttackPhase]{source}[ATK:{source.AttackDamage}/HP:{source.Health}{(hero != null ? $"/ARM:{hero.Armor}" : "")}] " +
						$"{(hero?.Weapon != null ? $"[{hero.Weapon}[A:{hero.Weapon.AttackDamage}/D:{hero.Weapon.Durability}]] " : "")}attacked " +
						$"{target}[ATK:{target.AttackDamage}/HP:{target.Health}].");
				return true;
			};
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
