using System;
using System.Collections.Generic;
using System.Text;
using SabberStoneCore.Auras;
using SabberStoneCore.Actions;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;

namespace SabberStoneCore.Model.Entities
{
	public partial class Playable
	{
		internal class CostManager
		{
			private readonly List<(EffectOperator Operator, int Value, int Source)> _costEffects =
				new List<(EffectOperator Operator, int Value, int Source)>();
			private readonly List<(EffectOperator Operator, int Value)> _costEnchantments =
				new List<(EffectOperator Operator, int Value)>();

            internal readonly List<int> AdjacencySources = new List<int>();
            internal int? TransformBaseCost;

			private int _cachedValue;
			private bool _toBeUpdated;
			private AdaptiveCostEffect _adaptiveCostEffect;

			public CostManager()
			{
				_toBeUpdated = true;
			}

			public CostManager(AdaptiveCostEffect adaptiveEffect)
			{
				_adaptiveCostEffect = adaptiveEffect;
			}

			private CostManager(CostManager original)
			{
				_cachedValue = original._cachedValue;
				_toBeUpdated = original._toBeUpdated;
				_costEffects.AddRange(original._costEffects);
                _costEnchantments.AddRange(original._costEnchantments);
                AdjacencySources.AddRange(original.AdjacencySources);
                TransformBaseCost = original.TransformBaseCost;
			}

			public int CachedValue
			{
				get => _cachedValue;
				set => _cachedValue = value;
			}

			/// <summary>
			/// Apply a new Cost effect of an <see cref="Aura"/>
			/// </summary>
			public void AddCostAura(EffectOperator @operator, int value, int sourceId = 0)
			{
				_costEffects.Add((@operator, value, sourceId));

                // A capped cache has lost information; replay the retained aura operations.
                if (_cachedValue == int.MaxValue || _cachedValue == int.MinValue) _toBeUpdated = true;

				switch (@operator)
				{
					case EffectOperator.ADD:
						_cachedValue = AttributeArithmetic.Apply(_cachedValue, EffectOperator.ADD, value);
						break;
					case EffectOperator.SUB:
						_cachedValue = AttributeArithmetic.Apply(_cachedValue, EffectOperator.SUB, value);
						break;
					case EffectOperator.SET:
						_cachedValue = value;
						break;
				}
			}

			/// <summary>
			/// Remove an applied Cost effect of an <see cref="Aura"/>.
			/// </summary>
			/// <exception cref="KeyNotFoundException"></exception>
			public void RemoveCostAura(EffectOperator @operator, int value, int sourceId = 0)
			{
                int index = -1;
                for (int i = 0; i < _costEffects.Count; i++)
                    if (_costEffects[i] == (@operator, value, sourceId)) { index = i; break; }
                if (index < 0) return;
                _costEffects.RemoveAt(index);
                // A later SET may have overwritten the removed operation; replay survivors in order.
                _toBeUpdated = true;
			}

            internal bool HasAuraEffects => _costEffects.Count != 0;

            internal void HashAuras(StringBuilder hash)
            {
                if (!HasAuraEffects) return;
                hash.Append("[CA:");
                foreach (var effect in _costEffects)
                    hash.Append((int)effect.Operator).Append(',').Append(effect.Value).Append(',').Append(effect.Source).Append(';');
                hash.Append(']');
            }

			/// <summary>
			/// Activate <see cref="AdaptiveCostEffect"/> to calculate and reflect its result.
			/// </summary>
			public void ActivateAdaptiveEffect(AdaptiveCostEffect adaptiveCostEffect)
			{
				_adaptiveCostEffect = adaptiveCostEffect;
			}

			/// <summary>
			/// Tie in for <see cref="AdaptiveCostEffectObsolete"/> to calculate and reflect its result.
			/// </summary>
			public void UpdateAdaptiveEffect(int setValue = -1)
			{
				if (setValue > 0)
					_cachedValue = setValue;
				else
					_toBeUpdated = true;
			}

			public void DeactivateAdaptiveEffect()
			{
				_adaptiveCostEffect = null;
			}

			/// <summary>
			/// Add a new permanent cost enchantment effect
			/// </summary>
			public void AddCostEnchantment(EffectOperator @operator, int value)
			{
                if (_cachedValue == int.MaxValue || _cachedValue == int.MinValue) _toBeUpdated = true;
                if (TransformBaseCost.HasValue)
                    TransformBaseCost = @operator switch
                    {
                        EffectOperator.ADD => AttributeArithmetic.Apply(TransformBaseCost.Value, EffectOperator.ADD, value),
                        EffectOperator.SUB => AttributeArithmetic.Apply(TransformBaseCost.Value, EffectOperator.SUB, value),
                        EffectOperator.SET => value,
                        EffectOperator.MUL => AttributeArithmetic.Apply(TransformBaseCost.Value, EffectOperator.MUL, value),
                        _ => TransformBaseCost
                    };
				ref int c = ref _cachedValue;

				switch (@operator)
				{
					case EffectOperator.SUB:
						c = AttributeArithmetic.Apply(c, EffectOperator.SUB, value);
						break;
					case EffectOperator.ADD:
						c = AttributeArithmetic.Apply(c, EffectOperator.ADD, value);
						break;
					case EffectOperator.SET:
						_toBeUpdated = true;
						break;
				}

				_costEnchantments.Add((@operator, value));
			}

			public int GetCost(int c)
			{
				// A retained transform's scalar already includes its auras; replay from the signed pre-aura input.
				int cost = _toBeUpdated ? GetCostInternal(TransformBaseCost ?? c) : _cachedValue;

				return cost > 0 ? cost : 0;
			}

			internal void QueueUpdate()
			{
				_toBeUpdated = true;
			}

            internal void ScalarChanged()
            {
                TransformBaseCost = null;
                // Preserve legacy aura/adaptive cache arithmetic; permanent-only managers track the scalar.
                if (_costEffects.Count == 0 && _adaptiveCostEffect == null) QueueUpdate();
            }

            internal void ResetEnchantments()
            {
                _costEnchantments.Clear();
                TransformBaseCost = null;
                QueueUpdate();
            }

			/// <summary>
			/// Applies older entity's cost enchantments to the new one.
			/// </summary>
			/// <param name="newCardCost"></param>
			/// <returns></returns>
			internal int EntityChanged(int newCardCost)
			{
				for (int i = 0; i < _costEnchantments.Count; i++)
				{
					(EffectOperator @operator, int value) = _costEnchantments[i];
					switch (@operator)
					{
						case EffectOperator.SUB:
							newCardCost = AttributeArithmetic.Apply(newCardCost, EffectOperator.SUB, value);
							break;
						case EffectOperator.ADD:
							newCardCost = AttributeArithmetic.Apply(newCardCost, EffectOperator.ADD, value);
							break;
						case EffectOperator.SET:
							newCardCost = value;
							break;
					}
				}

                // Retained transforms store an aura-inclusive legacy scalar; overlays need the signed input.
                TransformBaseCost = newCardCost;
				newCardCost = GetCostInternal(newCardCost);

				return newCardCost > 0 ? newCardCost : 0;
			}

            internal (EffectOperator Operator, int Value)[] PersistentOperations() => _costEnchantments.ToArray();

            internal int WithoutExternalAuras(int c) => Math.Max(0, GetCostInternal(c, false, false));

            internal int GetOverlayCost(int c) => Math.Max(0, GetCostInternal(c, false));

			private int GetCostInternal(int c, bool cache = true, bool includeAuras = true)
			{
				// 1. Get cost with enchantments first (c)
				// e.g. Emperor Thaurissan

				// 2. Apply cost aura effects next. (e.g. Naga Sea Witch, Sorcerer's Apprentice)
				long wide = c;
				var effects = _costEffects;
				for (int i = 0; includeAuras && i < effects.Count; i++)
				{
					var e = effects[i];
					switch (e.Operator)
					{
						case EffectOperator.ADD:
							wide += e.Value;
							break;
						case EffectOperator.SUB:
							wide -= e.Value;
							break;
						case EffectOperator.SET:
							wide = e.Value;
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				// 3. Lastly apply Adaptive Cost Effect (e.g. Giants)
				c = AttributeArithmetic.Clamp(wide);
				c = GeneratedDefinitionRules.ApplyAdaptive(_adaptiveCostEffect, c);

                // Overlay evaluation must not overwrite HEAD's permanent cache (notably SET auras).
                if (cache)
                {
                    _cachedValue = c;
                    _toBeUpdated = false;
                }

				return c;
			}

			public CostManager Clone()
			{
				return new CostManager(this);
			}
		}

		internal CostManager _costManager;
		internal int? _modifiedCost;
        internal EntityCostState _costOverlay;

        internal int NumericCost
        {
            get
            {
                if (InfinityRules.HasCost(this) || CostRules.HasManaNumbers(this) || _costOverlay?.Modifiers.Count > 0)
                {
                    int permanent = CostRules.PersistentInput(this);
                    int overlaid = _costOverlay?.Apply(permanent) ?? permanent;
                    if (InfinityRules.HasCost(this)) overlaid = AttributeArithmetic.Apply(overlaid, EffectOperator.SUB, CostRules.ScalarReduction(this));
                    int cost = _costManager?.GetOverlayCost(overlaid) ?? Math.Max(0, overlaid);
                    return CostRules.AfterExternalCostAuras(this, Card[LeylineRules.Keyword] != 0 ? LeylineRules.AdjustNumericCost(this, cost) : cost);
                }
                int numeric = _costManager?.GetCost(_modifiedCost ?? (_modifiedCost = Card.Cost).Value) ??
                    (_modifiedCost.HasValue ? _modifiedCost < 0 ? 0 : _modifiedCost.Value : (_modifiedCost = Card.Cost).Value);
                return CostRules.AfterExternalCostAuras(this, Card[LeylineRules.Keyword] != 0 ? LeylineRules.AdjustNumericCost(this, numeric) : numeric);
            }
        }

        public int Cost
        {
            get => CostRules.AdjustCost(this, NumericCost);
            set => SetCostValue(value, true);
        }
        internal void SetCostValue(int value, bool persistent)
        {
            if (persistent) { InfinityRules.RecordCost(this, EffectOperator.SET, value); CostRules.RememberScalarSet(this, value); }
            _modifiedCost = value;
            _costManager?.ScalarChanged();
            if (persistent && CostRules.HasManaNumbers(this))
                CostRules.Manager(this).AddCostEnchantment(EffectOperator.SET, value);
            if (Card.Shatter != null) _costManager?.QueueUpdate();
            if (_history) Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.COST, Cost));
        }

		internal void ResetCost()
		{
            InfinityRules.ResetCost(this);
            SabberStoneCore.Actions.PrepareRules.Reset(this);
            CostRules.ClearHeldQualification(this);
            CostRules.ResetOverlay(this);
            CostRules.ClearScalarAuraMemory(this);
			_costManager = null;
			_modifiedCost = null;
			if (OngoingEffect is AdaptiveCostEffect ace)
				ace.Remove();
            else if (Card.Id == "CATA_470t1" && Zone?.Type == Enums.Zone.HAND && OngoingEffect is MultiAura combined)
                combined.Remove();

			if (_history)
				Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.COST, Card.Cost));
		}

		public override int this[GameTag t]
		{
			get => t == GameTag.CARD_ALTERNATE_COST ? (int)CostRules.PaymentKind(this) : t == GameTag.COST ? Cost
                : t == GameTag.CANT_PLAY && (this[GameTag.PREPARING] != 0 || Actions.PlayLockRules.BlocksPlay(this)) ? 1
                : t == GameTag.CANT_ATTACK && (Actions.PlayLockRules.IsSleeping(this) || Actions.TemporaryRules.BlocksAttack(this)) ? 1 : base[t];
			set
			{
				if (t == GameTag.COST)
					Cost = value;
				else
					base[t] = value;
			}
		}
	}
}
