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
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Enchants
{
	public enum EffectOperator
	{
		ADD, SUB, MUL, SET
	}

	/// <summary>
	/// Defines methods for tags value variation.
	/// </summary>
	public interface IEffect
	{
		GameTag Tag { get; }
		EffectOperator Operator { get; }
		int Value { get; }


		void ApplyTo(IEntity entity, bool isOneTurnEffect = false);
		void ApplyAuraTo(IPlayable playable, int sourceId = 0);
		//void ApplyTo(AuraEffects auraEffects);
		//void ApplyTo(ControllerAuraEffects controllerAuraEffects);

		void RemoveFrom(IEntity entity);
		void RemoveAuraFrom(IPlayable playable, int sourceId = 0);
		//void RemoveFrom(AuraEffects auraEffects);
		//void RemoveFrom(ControllerAuraEffects controllerAuraEffects);

		IEffect ChangeValue(int newValue);
	}

	/// <summary>
	///	A structure for tag value variation.
	/// </summary>
	public readonly struct Effect : IEffect, IEquatable<Effect>
	{
		public readonly GameTag Tag;
		public readonly EffectOperator Operator;
		public readonly int Value;

		/// <summary>
		/// Create a new Effect. An Effect consists of <see cref="GameTag"/>, <see cref="EffectOperator"/>, and <see cref="int"/> value.
		/// </summary>
		/// <param name="tag">The <see cref="GameTag"/> to be affected.</param>
		/// <param name="operator">The operation this effect performs.</param>
		/// <param name="value">The right operand of the operator.</param>
		public Effect(GameTag tag, EffectOperator @operator, int value)
		{
			Tag = tag;
			Operator = @operator;
			Value = value;
		}

		/// <summary>
		/// Apply this effect to the target entity.
		/// </summary>
		public void ApplyTo(IEntity entity, bool oneTurnEffect = false)
		{
            if (!oneTurnEffect && Tag == GameTag.COST && entity is Playable shatter && (shatter.Card.Shatter != null || Actions.InfinityRules.HasCost(shatter)))
            {
                Cost.Effect(Operator, Value).ApplyTo(shatter);
                return;
            }
            if (oneTurnEffect && Tag == GameTag.COST && entity is Playable costTarget)
            {
                Actions.CostRules.AddModifier(costTarget, Operator, Value, entity.Game.Turn);
                return;
            }

            if (Tag == GameTag.DIVINE_SHIELD && Value > 0 && (Operator == EffectOperator.SET || Operator == EffectOperator.ADD))
                Actions.DeathContextRules.ResetShield(entity);

            bool hadAttackKeyword = (Tag == GameTag.CHARGE || Tag == GameTag.RUSH || Tag == GameTag.WINDFURY)
                && entity[Tag] > 0;
			if (!entity.NativeTags.ContainsKey(Tag))
				entity.NativeTags.Add(Tag, entity.Card[Tag]);

			if (oneTurnEffect)
				entity.Game.OneTurnEffects.Add((entity.Id, this));

            if (Tag == GameTag.ATK) Actions.InfinityRules.WeaponModified(entity, Operator, Value);

			switch (Operator)
			{
				case EffectOperator.ADD:
					entity.NativeTags[Tag] = Tag == GameTag.ATK
                        ? Actions.AttributeArithmetic.Apply(entity.NativeTags[Tag], EffectOperator.ADD, Value)
                        : entity.NativeTags[Tag] + Value;
					break;
				case EffectOperator.SUB:
					entity.NativeTags[Tag] = Tag == GameTag.ATK
                        ? Actions.AttributeArithmetic.Apply(entity.NativeTags[Tag], EffectOperator.SUB, Value)
                        : entity.NativeTags[Tag] - Value;
					break;
				case EffectOperator.MUL:
					entity.NativeTags[Tag] = Tag == GameTag.ATK
                        ? Actions.AttributeArithmetic.Apply(entity.NativeTags[Tag], EffectOperator.MUL, Value)
                        : entity.NativeTags[Tag] * Value;
					break;
				case EffectOperator.SET:
					// experimental implmentation for simulating tricky situations
					switch (Tag)
					{
						case GameTag.CHARGE when Value > 0:
							{
								var m = (Minion)entity;
								if (m.IsExhausted && m.NumAttacksThisTurn == 0)
									m.IsExhausted = false;
								if (m.AttackableByRush)
									m.AttackableByRush = false;
								break;
							}
						case GameTag.WINDFURY when Value > 0:
							{
								var m = (Minion)entity;
								if (m.NumAttacksThisTurn > 0 && m.IsExhausted)
									m.IsExhausted = false;
								break;
							}
						case GameTag.TAUNT:
							((Character) entity).HasTaunt = Value > 0;
							return;
						case GameTag.IMMUNE:
							if (entity is Character c)
								c.IsImmune = Value > 0;
							else
								break;
							return;
						case GameTag.RUSH when Value > 0:
						{
							var m = (Minion)entity;
							if (m.IsExhausted)
							{
								if (m.HasWindfury)
								{
									if (m.NumAttacksThisTurn < 2)
									{
										m.IsExhausted = false;
										m.AttackableByRush = true;
                                        m.Game.RushMinions.Add(m.Id);
									}
								}
								else
								{
									if (m.NumAttacksThisTurn == 0)
									{
										m.IsExhausted = false;
										m.AttackableByRush = true;
                                        m.Game.RushMinions.Add(m.Id);
									}
								}
							}
							break;
						}
					}

					if (oneTurnEffect && entity.NativeTags[Tag] == Value)
						entity.Game.OneTurnEffects.Remove((entity.Id, this));

					entity.NativeTags[Tag] = Value;

					break;
	
				default:
					throw new ArgumentException();
			}
            if (Tag == GameTag.MEGA_WINDFURY && entity is Minion mega && mega.NumAttacksThisTurn > 0)
                mega.IsExhausted = mega.NumAttacksThisTurn >= Actions.CombatRules.AttackLimit(mega);
            if (hadAttackKeyword && entity[Tag] == 0 && entity is Minion minion)
                minion.UpdateAttackReadinessAfterKeywordLoss();
		}

		/// <summary>
		/// Apply this effect to the target as an aura effect.
		/// </summary>
		public void ApplyAuraTo(IPlayable playable, int sourceId = 0)
		{
            var windfuryHero = Tag == GameTag.WINDFURY ? playable as Hero : null;
            int previousAttackLimit = windfuryHero?.AttackLimit ?? 1;

            if (Tag == GameTag.COST && playable is Playable costTarget)
            { Actions.CostRules.Manager(costTarget).AddCostAura(Operator, Value, sourceId); return; }

            bool hadAttackKeyword = (Tag == GameTag.CHARGE || Tag == GameTag.RUSH || Tag == GameTag.WINDFURY)
                && playable[Tag] > 0;
			AuraEffects auraEffects = playable.AuraEffects;
			if (auraEffects == null)
			{
				auraEffects = new AuraEffects(playable.Card.Type);
				playable.AuraEffects = auraEffects;
			}

			switch (Operator)
			{
				case EffectOperator.ADD:
                    if (Tag == GameTag.CHARGE && playable is Minion) auraEffects.Charge += Value;
                    else if (Tag == GameTag.RUSH && playable is Minion) auraEffects.RushCount += Value;
                    else if (Tag == GameTag.TAUNT && playable is Minion) auraEffects.TauntCount += Value;
                    else if (Tag == GameTag.LIFESTEAL && playable is Minion) auraEffects.LifestealCount += Value;
                    else auraEffects[Tag] += Value;
					break;
				case EffectOperator.SUB:
                    if (Tag == GameTag.CHARGE && playable is Minion) auraEffects.Charge -= Value;
                    else if (Tag == GameTag.RUSH && playable is Minion) auraEffects.RushCount -= Value;
                    else if (Tag == GameTag.TAUNT && playable is Minion) auraEffects.TauntCount -= Value;
                    else if (Tag == GameTag.LIFESTEAL && playable is Minion) auraEffects.LifestealCount -= Value;
                    else auraEffects[Tag] -= Value;
					break;
				case EffectOperator.SET:
					auraEffects.Set(Tag, Value, sourceId);

					if (playable is Minion m)
					{
						switch (Tag)
						{
							case GameTag.CHARGE when Value > 0:
								if (m.IsExhausted && m._numAttackThisTurn < 1)
									m.IsExhausted = false;
								if (m.AttackableByRush)
									m.AttackableByRush = false;
								break;
                            case GameTag.WINDFURY when Value > 0:
                                if (m.NumAttacksThisTurn > 0 && m.NumAttacksThisTurn < Actions.CombatRules.AttackLimit(m))
                                    m.IsExhausted = false;
                                break;
							case GameTag.RUSH when Value > 0:
								if (m.IsExhausted && m._numAttackThisTurn == 0)
								{
									m.IsExhausted = false;
									m.AttackableByRush = true;
									playable.Game.RushMinions.Add(playable.Id);
								}
								break;
						}
					}
					break;
				default:
					throw new NotImplementedException();
			}
            if (hadAttackKeyword && playable[Tag] == 0 && playable is Minion minion)
                minion.UpdateAttackReadinessAfterKeywordLoss();
            windfuryHero?.UpdateAttackReadinessAfterAttackLimitChange(previousAttackLimit);
		}

		/// <summary>
		/// Apply this effect to the target controller as an aura effect.
		/// </summary>
		public void ApplyTo(ControllerAuraEffects auraEffects, int sourceId = 0)
		{
			switch (Operator)
			{
				case EffectOperator.SET:
                    auraEffects.Set(Tag, Value, sourceId);
                    return;
				case EffectOperator.ADD:
					auraEffects[Tag] = auraEffects.Count(Tag) + Value;
					return;
				case EffectOperator.SUB:
					auraEffects[Tag] = auraEffects.Count(Tag) + Value;
					return;
				default:
					throw new NotImplementedException();
			}
		}

		/// <summary>
		/// Remove this effect from the target entity.
		/// </summary>
		public void RemoveFrom(IEntity entity)
		{
            if (Tag == GameTag.ATK && (Operator == EffectOperator.ADD || Operator == EffectOperator.SUB))
                Actions.InfinityRules.WeaponModified(entity, Operator == EffectOperator.ADD ? EffectOperator.SUB : EffectOperator.ADD, Value);

            bool hadAttackKeyword = (Tag == GameTag.CHARGE || Tag == GameTag.RUSH || Tag == GameTag.WINDFURY)
                && entity[Tag] > 0;
			switch (Operator)
			{
				case EffectOperator.ADD:
					entity[Tag] -= Value;
					break;
				case EffectOperator.SUB:
					entity[Tag] = entity.NativeTags[Tag] + Value;
					break;
				case EffectOperator.SET:
					entity[Tag] = 0;
					entity.NativeTags.Remove(Tag);		// unstable
					if (entity.Game.History)
						entity.Game.PowerHistory.Add(
							Kettle.PowerHistoryBuilder
								.TagChange(entity.Id, Tag, entity.Card[Tag]));
					break;
			}
            if (hadAttackKeyword && entity[Tag] == 0 && entity is Minion minion)
                minion.UpdateAttackReadinessAfterKeywordLoss();
		}

		/// <summary>
		/// Remove ths aura effect from the target entity.
		/// </summary>
		public void RemoveAuraFrom(IPlayable playable, int sourceId = 0)
		{
            var windfuryHero = Tag == GameTag.WINDFURY ? playable as Hero : null;
            int previousAttackLimit = windfuryHero?.AttackLimit ?? 1;

            if (Tag == GameTag.COST && playable is Playable costTarget)
            { costTarget._costManager?.RemoveCostAura(Operator, Value, sourceId); return; }

            bool hadAttackKeyword = (Tag == GameTag.CHARGE || Tag == GameTag.RUSH || Tag == GameTag.WINDFURY)
                && playable[Tag] > 0;
			switch (Operator)
			{
				case EffectOperator.SET:
                    playable.AuraEffects.Set(Tag, Value, sourceId, true);
                    break;
                case EffectOperator.ADD:
                    // Keyword indexers expose booleans; remove from their source counts directly.
                    if (Tag == GameTag.CHARGE && playable is Minion) playable.AuraEffects.Charge -= Value;
                    else if (Tag == GameTag.RUSH && playable is Minion) playable.AuraEffects.RushCount -= Value;
                    else if (Tag == GameTag.TAUNT && playable is Minion) playable.AuraEffects.TauntCount -= Value;
                    else if (Tag == GameTag.LIFESTEAL && playable is Minion) playable.AuraEffects.LifestealCount -= Value;
                    else playable.AuraEffects[Tag] -= Value;
					break;
                case EffectOperator.SUB:
                    if (Tag == GameTag.CHARGE && playable is Minion) playable.AuraEffects.Charge += Value;
                    else if (Tag == GameTag.RUSH && playable is Minion) playable.AuraEffects.RushCount += Value;
                    else if (Tag == GameTag.TAUNT && playable is Minion) playable.AuraEffects.TauntCount += Value;
                    else if (Tag == GameTag.LIFESTEAL && playable is Minion) playable.AuraEffects.LifestealCount += Value;
                    else playable.AuraEffects[Tag] += Value;
                    break;
			}
            if (hadAttackKeyword && playable[Tag] == 0 && playable is Minion minion)
                minion.UpdateAttackReadinessAfterKeywordLoss();
            windfuryHero?.UpdateAttackReadinessAfterAttackLimitChange(previousAttackLimit);
		}

		public void RemoveFrom(ControllerAuraEffects auraEffects, int sourceId = 0)
		{
			switch (Operator)
			{
				case EffectOperator.SET:
                    auraEffects.Set(Tag, Value, sourceId, true);
                    return;
				case EffectOperator.ADD:
					auraEffects[Tag] = auraEffects.Count(Tag) - Value;
					return;
				case EffectOperator.SUB:
					auraEffects[Tag] = auraEffects.Count(Tag) + Value;
					return;
				default:
					throw new NotImplementedException();
			}
		}

		/// <summary>
		/// Creates a new Effect having changed amount of <see cref="Value"/>.
		/// </summary>
		public IEffect ChangeValue(int newValue)
		{
			return new Effect(Tag, Operator, newValue);
		}

		GameTag IEffect.Tag => Tag;
		EffectOperator IEffect.Operator => Operator;
		int IEffect.Value => Value;

		public bool Equals(Effect other)
		{
			return Tag == other.Tag && Operator == other.Operator && Value == other.Value;
		}

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			return obj is Effect effect && Equals(effect);
		}

		public static bool operator ==(Effect e1, Effect e2) => e1.Equals(e2);

		public static bool operator !=(Effect e1, Effect e2) => !(e1.Equals(e2));

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (int) Tag;
				hashCode = (hashCode * 397) ^ (int) Operator;
				hashCode = (hashCode * 397) ^ Value;
				return hashCode;
			}
		}
		public override string ToString()
		{
			return $"[{Operator} {Tag} {Value}]";
		}
	}
}
