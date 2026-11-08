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
using System.Text;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

// ReSharper disable InconsistentNaming

namespace SabberStoneCore.Enchants
{
	/// <summary>
	/// A simple container for saving tag value perturbations from external Auras. Call indexer to get value for a particular Tag.
	/// </summary>
	public class AuraEffects : IEquatable<AuraEffects>
	{
		private const int PlayableLength = 2;
		private const int WeaponLength = PlayableLength + 1;
		private const int CharacterLength = PlayableLength + 2;
		private const int HeroLength = CharacterLength + 3;
		private const int MinionLength = CharacterLength + 7;

		// Slots overlap across entity types; getters must only read their own type.
		// Indices:
		// Playables
		// 0 : CardCostHealth
		// 1 : Echo
		// Weapon
		// 2 : Immune
		// Characters
		// 2 : CantBeTargetedBySpells
		// 3 : ATK
		// Hero
		// 4 : CannotAttackHeroes
		// 5 : Immune
		// 6 : HEROPOWER_DAMAGE
		// Minion
		// 4 : Health
		// 5 : Charge
		// 6 : Taunt
		// 7 : Lifesteal
		// 8 : Rush
		// 9 : CantAttack
		internal int[] _data;
        internal Game Game;
        internal long HealthCostAppliedAt;
        private List<(GameTag Tag, int Source, int Value)> _setEffects;

		internal AuraEffects(CardType type)
		{
			switch (type)
			{
				case CardType.HERO:
					_data = new int[HeroLength + 2];
					break;
				case CardType.MINION:
					_data = new int[MinionLength + 3];
					break;
				case CardType.WEAPON:
					_data = new int[WeaponLength];
					break;
                case CardType.LOCATION:
				case CardType.SPELL:
					_data = new int[PlayableLength];
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}

			Type = type;
		}

		private AuraEffects(AuraEffects original) : this (original.Type)
		{
			if (_data.Length < original._data.Length) Array.Resize(ref _data, original._data.Length);
			Buffer.BlockCopy(original._data, 0, _data, 0, original._data.Length * sizeof(int));
            HealthCostAppliedAt = original.HealthCostAppliedAt;
            if (original._setEffects != null) _setEffects = new List<(GameTag, int, int)>(original._setEffects);
		}

        // Keep SET sources separate from additive auras; numeric SETs use the latest surviving source.
        internal static bool NumericSet(GameTag tag) => tag == GameTag.ATK || tag == GameTag.HEALTH
            || tag == GameTag.HEALTH_MINIMUM || tag == GameTag.HEROPOWER_DAMAGE || tag == GameTag.TIMEOUT;

        internal static int SetValue(List<(GameTag Tag, int Source, int Value)> effects, GameTag tag)
        {
            int value = 0;
            if (effects != null)
                foreach (var effect in effects)
                    if (effect.Tag == tag) value = NumericSet(tag) ? effect.Value : value + effect.Value;
            return value;
        }

        internal static int UpdateSet(ref List<(GameTag Tag, int Source, int Value)> effects,
            GameTag tag, int source, int value, bool remove)
        {
            int before = SetValue(effects, tag);
            if (remove)
            {
                int index = -1;
                if (effects != null)
                    for (int i = 0; i < effects.Count; i++)
                        if (effects[i] == (tag, source, value)) { index = i; break; }
                if (index < 0) return 0;
                effects.RemoveAt(index);
            }
            else
            {
                if (effects == null) effects = new List<(GameTag, int, int)>();
                effects.Add((tag, source, value));
            }
            return SetValue(effects, tag) - before;
        }

        internal int SetValue(GameTag tag) => SetValue(_setEffects, tag);

        internal void Set(GameTag tag, int value, int source, bool remove = false)
        {
            if (Type == CardType.LOCATION && tag != GameTag.CARD_COSTS_HEALTH && tag != GameTag.ECHO) return;
            // Both targeting tags share one legacy slot.
            if (tag == GameTag.CANT_BE_TARGETED_BY_HERO_POWERS) tag = GameTag.CANT_BE_TARGETED_BY_SPELLS;
            int delta = UpdateSet(ref _setEffects, tag, source, value, remove);
            switch (tag)
            {
                case GameTag.ATK: ATK += delta; break;
                case GameTag.HEALTH: Health += delta; break;
                case GameTag.CHARGE: Charge += delta; break;
                case GameTag.RUSH: RushCount += delta; break;
                case GameTag.TAUNT: TauntCount += delta; break;
                case GameTag.LIFESTEAL: LifestealCount += delta; break;
                case GameTag.CARD_COSTS_HEALTH:
                    if (!remove && value > 0 && Game != null) HealthCostAppliedAt = Actions.CostRules.NextPaymentOrder(Game);
                    _data[0] += delta; break;
                case GameTag.ECHO: _data[1] += delta; break;
                case GameTag.CANT_BE_TARGETED_BY_SPELLS: _data[2] += delta; break;
                case GameTag.CANNOT_ATTACK_HEROES when Type == CardType.HERO: _data[4] += delta; break;
                case GameTag.HEALTH_MINIMUM: break;
                default: this[tag] += delta; break;
            }
        }

        internal static void HashSets(StringBuilder hash, List<(GameTag Tag, int Source, int Value)> effects)
        {
            if (effects == null || effects.Count == 0) return;
            hash.Append(";sets:");
            foreach (var effect in effects) hash.Append((int)effect.Tag).Append(',').Append(effect.Source).Append(',').Append(effect.Value).Append(';');
        }

		public readonly CardType Type;

		public bool CardCostHealth
		{
			get => _data[0] > 0;
			set
            {
                if (value && Game != null) HealthCostAppliedAt = Actions.CostRules.NextPaymentOrder(Game);
                _data[0] = value ? 1 : 0;
            }
		}

		public bool Echo
		{
			get => _data[1] > 0;
			set => _data[1] = value ? 1 : 0;
		}

		public bool CantBeTargetedBySpells
		{
			get => (Type == CardType.HERO || Type == CardType.MINION) && _data[2] > 0;
			set => _data[2] = value ? 1 : 0;
		}

		public int ATK
		{
			get => _data.Length > 3 ? _data[3] : 0;
            set { if (_data.Length <= 3) Array.Resize(ref _data, 4); _data[3] = value; }
		}

		public int Health
		{
			get => Type == CardType.MINION ? _data[4] : 0;
			set => _data[4] = value;
		}

		public int Charge
		{
			get => Type == CardType.MINION ? _data[5] : 0;
			set => _data[5] = value;
		}

		public bool Taunt
		{
			get => Type == CardType.MINION && _data[6] > 0;
			set => _data[6] = value ? 1 : 0;
		}

        internal int TauntCount { get => _data[6]; set => _data[6] = value; }
        internal int LifestealCount { get => _data[7]; set => _data[7] = value; }

		public bool Lifesteal
		{
			get => _data[7] > 0;
			set => _data[7] = value ? 1 : 0;
		}

		public bool Rush
		{
			get => _data[8] > 0;
			set => _data[8] = value ? 1 : 0;
		}

        internal int RushCount
        {
            get => _data[8];
            set => _data[8] = value;
        }

		public bool CantAttack
		{
			get => _data[9] > 0;
			set => _data[9] = value ? 1 : 0;
		}

		// Only for Hero entities
		public bool CannotAttackHeroes
		{
			get
			{
				if (Type != CardType.HERO)
					return false;
				return _data[4] > 0;
			}
			set
			{
				if (Type != CardType.HERO)
					throw new NotImplementedException();
				_data[4] = value ? 1 : 0;
			}
		}

		public int Immune
		{
			get
			{
				if (Type == CardType.HERO)
					return _data[5];
				if (Type == CardType.WEAPON)
					return _data[2];
                if (Type == CardType.MINION) return _data[10];

				return 0;
			}
			set
			{
				if (Type == CardType.HERO)
					_data[5] = value;
				else if (Type == CardType.WEAPON)
					_data[2] = value;
                else if (Type == CardType.MINION) _data[10] = value;
				else
					throw new NotImplementedException();
			}
		}

		public int HeroPowerDamage
		{
			get
			{
				if (Type == CardType.HERO)
					return _data[6];
				return 0;
			}
			set
			{
				if (Type == CardType.HERO)
					_data[6] = value;
				else
					throw new NotImplementedException();
			}
		}

		public int this[in GameTag t]
		{
			get
			{
                if (Type == CardType.LOCATION && t != GameTag.CARD_COSTS_HEALTH && t != GameTag.ECHO) return 0;
				if (Type == CardType.HERO || Type == CardType.MINION)
                {
                    if (t == GameTag.WINDFURY) return _data[Type == CardType.HERO ? HeroLength : MinionLength];
                    if (t == GameTag.IMMUNE_WHILE_ATTACKING) return _data[(Type == CardType.HERO ? HeroLength : MinionLength) + 1];
                }
                switch (t)
				{
					case GameTag.ATK when Type != CardType.MINION && Type != CardType.WEAPON:
						return 0;
					case GameTag.ATK:
						return ATK;
					case GameTag.HEALTH:
						return Health;
                    case GameTag.HEALTH_MINIMUM:
                        return SetValue(_setEffects, t);
					//case GameTag.COST:
					//	//return GetCost() - ((Entity)Owner)._data[GameTag.COST];
					//	return GetCost() - Owner.GetNativeGameTag(GameTag.COST);
					case GameTag.CHARGE:
						return Charge > 0 ? 1 : 0;
                    case GameTag.TAUNT:
                        return Type == CardType.MINION ? _data[6] : 0;
					case GameTag.POISONOUS:
                        return Type == CardType.MINION && _data.Length > MinionLength + 2 ? _data[MinionLength + 2] : 0;
                    case GameTag.IMMUNE:
						return Immune;
                    case GameTag.LIFESTEAL when Type == CardType.MINION:
                        return Lifesteal ? 1 : 0;
					//case GameTag.CANT_ATTACK:
					//	return CANT_ATTACK;
					case GameTag.CANT_BE_TARGETED_BY_SPELLS:
					case GameTag.CANT_BE_TARGETED_BY_HERO_POWERS:
						return CantBeTargetedBySpells ? 1 : 0;
					case GameTag.CARD_COSTS_HEALTH:
						return CardCostHealth ? 1 : 0;
					case GameTag.RUSH:
						return Rush ? 1 : 0;
					case GameTag.ECHO:
						return Echo ? 1 : 0;
					case GameTag.CANNOT_ATTACK_HEROES:
						return CannotAttackHeroes ? 1 : 0;
					case GameTag.HEROPOWER_DAMAGE:
						return HeroPowerDamage;
					default:
						return 0;
				}
			}
			set
			{
                if (Type == CardType.LOCATION && t != GameTag.CARD_COSTS_HEALTH && t != GameTag.ECHO) return;
				if (Type == CardType.HERO || Type == CardType.MINION)
                {
                    if (t == GameTag.WINDFURY) { _data[Type == CardType.HERO ? HeroLength : MinionLength] = value; return; }
                    if (t == GameTag.IMMUNE_WHILE_ATTACKING) { _data[(Type == CardType.HERO ? HeroLength : MinionLength) + 1] = value; return; }
                }
                switch (t)
				{
					case GameTag.ATK:
						ATK = value;
						return;
					case GameTag.HEALTH:
						Health = value;
						return;
					case GameTag.CHARGE:
						Charge += value;
						return;
                    case GameTag.TAUNT:
                        if (Type == CardType.MINION) _data[6] = value;
                        return;
					case GameTag.HEALTH_MINIMUM:
						//Owner[GameTag.HEALTH_MINIMUM] = value;
						return;
					case GameTag.POISONOUS:
                        if (Type != CardType.MINION) return;
                        // Old snapshot arrays have no Poisonous slot; grow only when used.
                        if (_data.Length <= MinionLength + 2) Array.Resize(ref _data, MinionLength + 3);
                        _data[MinionLength + 2] = value;
                        return;
                    case GameTag.IMMUNE:
						Immune = value;
						return;
                    case GameTag.LIFESTEAL:
                        _data[7] = value;
                        return;
					//case GameTag.CANT_ATTACK:
					//	CANT_ATTACK = value;
					//	return;
					case GameTag.CANT_BE_TARGETED_BY_SPELLS:
					case GameTag.CANT_BE_TARGETED_BY_HERO_POWERS:
						CantBeTargetedBySpells = value > 0;
						return;
					case GameTag.CARD_COSTS_HEALTH:
						CardCostHealth = value > 0;
						return;
					case GameTag.RUSH:
                        RushCount += value;
						return;
					case GameTag.ECHO:
						Echo = value > 0;
						return;
					case GameTag.CANNOT_ATTACK_HEROES:
						CannotAttackHeroes = value > 0;
						return;
					case GameTag.HEROPOWER_DAMAGE:
						HeroPowerDamage = value;
						return;
					default:
						return;
				}
			}
		}

		public AuraEffects Clone()
		{
			return new AuraEffects(this);
		}

		public string Hash()
		{
			var hash = new StringBuilder();
			hash.Append("[AE:");
			int[] data = _data;
			int legacyLength = Type == CardType.HERO ? HeroLength : Type == CardType.MINION ? MinionLength : data.Length;
            for (int i = 0; i < legacyLength; i++) hash.Append(data[i]);
            if (legacyLength < data.Length && (data[legacyLength] != 0 || data[legacyLength + 1] != 0))
                hash.Append(";combat:").Append(data[legacyLength]).Append(',').Append(data[legacyLength + 1]);
            if (Type == CardType.MINION && data.Length > MinionLength + 2 && data[MinionLength + 2] != 0)
                hash.Append(";poison:").Append(data[MinionLength + 2]);
            HashSets(hash, _setEffects);
			hash.Append("]");
			return hash.ToString();
		}

		#region Equality members

		public bool Equals(AuraEffects other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Type == other.Type &&
			       Enumerable.SequenceEqual(_data, other._data)
                   && (_setEffects ?? Enumerable.Empty<(GameTag, int, int)>()).SequenceEqual(other._setEffects ?? Enumerable.Empty<(GameTag, int, int)>());
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((AuraEffects) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (int) Type;
				int[] data = _data;
				for (int i = 0; i < data.Length; i++)
					hashCode = (hashCode * 397) ^ data[i];
				return hashCode;
			}
		}

		public static bool operator ==(AuraEffects left, AuraEffects right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(AuraEffects left, AuraEffects right)
		{
			return !Equals(left, right);
		}

		#endregion
	}

	/// <summary>
	/// A collecton of controller Tag increments from Auras.
	/// These tags tends to be checked when a player plays any cards.
	/// </summary>
	public class ControllerAuraEffects : IEquatable<ControllerAuraEffects>
	{
		private Action<IPowerHistoryEntry> _sendHistory;
		private int _controllerEntityId;
        private Game _game;
        internal long HealthCostAppliedAt;
        private List<(GameTag Tag, int Source, int Value)> _setEffects;

		private int _timeOut;
		private int _spellPowerDouble;
		private int _heroPowerDouble;
		private int _restoreToDamage;
		private int _extraBattecry;
		private int _chooseBoth;
		private int _spellsCostHealth;
		private int _extraEndTurnEffect;
		private int _heroPowerDisabled;
		private int _allHealingDouble;
		private int _extraBattlecryAndCombo;
		private int _spellPower;

		public ControllerAuraEffects() { }

		public ControllerAuraEffects(in Game g, in Controller c)
		{
            _game = g;
			if (g.History)
			{
				_sendHistory = g.PowerHistory.Add;
				_controllerEntityId = c.Id;
			}
		}

		public int this[GameTag t]
		{
			get
			{
				switch (t)
				{
					case GameTag.TIMEOUT:
						return _timeOut;
					case GameTag.SPELLPOWER_DOUBLE:
					case GameTag.SPELL_HEALING_DOUBLE:
						return _spellPowerDouble;
					case GameTag.HERO_POWER_DOUBLE:
						return _heroPowerDouble;
					case GameTag.HEALING_DOES_DAMAGE:
						return _restoreToDamage >= 1 ? 1 : 0;
					case GameTag.CHOOSE_BOTH:
						return _chooseBoth >= 1 ? 1 : 0;
					case GameTag.SPELLS_COST_HEALTH:
						return _spellsCostHealth >= 1 ? 1 : 0;
					case GameTag.EXTRA_BATTLECRIES_BASE:
						return _extraBattecry;
					case GameTag.EXTRA_END_TURN_EFFECT:
						return _extraEndTurnEffect;
					case GameTag.HERO_POWER_DISABLED:
						return _heroPowerDisabled >= 1 ? 1 : 0;
					case GameTag.ALL_HEALING_DOUBLE:
						return _allHealingDouble;
					case GameTag.EXTRA_MINION_BATTLECRIES_BASE:
						return _extraBattlecryAndCombo > 0 ? 1 : 0;
					case GameTag.SPELLPOWER:
						return _spellPower;
					default:
						return 0;
				}
			}
			set
			{
				_sendHistory?.Invoke(PowerHistoryBuilder.TagChange(_controllerEntityId, t, value));
				switch (t)
				{
					case GameTag.TIMEOUT:
						_timeOut = value;
						return;
					case GameTag.SPELLPOWER_DOUBLE:
					case GameTag.SPELL_HEALING_DOUBLE:
						_spellPowerDouble = value;
						return;
					case GameTag.HERO_POWER_DOUBLE:
						_heroPowerDouble = value;
						return;
					case GameTag.HEALING_DOES_DAMAGE:
						_restoreToDamage = value;
						return;
					case GameTag.CHOOSE_BOTH:
						_chooseBoth = value;
						return;
					case GameTag.SPELLS_COST_HEALTH:
						// A count decrease removes an aura; it is not a new application.
                        if (value > _spellsCostHealth && _game != null)
                            HealthCostAppliedAt = Actions.CostRules.NextPaymentOrder(_game);
                        _spellsCostHealth = value;
						return;
					case GameTag.EXTRA_BATTLECRIES_BASE:
						_extraBattecry = value;
						return;
					case GameTag.EXTRA_END_TURN_EFFECT:
						_extraEndTurnEffect = value;
						return;
					case GameTag.HERO_POWER_DISABLED:
						_heroPowerDisabled = value;
						return;
					case GameTag.ALL_HEALING_DOUBLE:
						_allHealingDouble = value;
						return;
					case GameTag.EXTRA_MINION_BATTLECRIES_BASE:
						_extraBattlecryAndCombo = value;
						return;
					case GameTag.SPELLPOWER:
						_spellPower = value;
						return;
					default:
						return;
				}
			}
		}

        internal void Set(GameTag tag, int value, int source, bool remove = false)
            => this[tag] = Count(tag) + AuraEffects.UpdateSet(ref _setEffects, tag, source, value, remove);

        internal int Count(GameTag tag) => tag == GameTag.CHOOSE_BOTH ? _chooseBoth
            : tag == GameTag.HEALING_DOES_DAMAGE ? _restoreToDamage
            : tag == GameTag.SPELLS_COST_HEALTH ? _spellsCostHealth
            : tag == GameTag.HERO_POWER_DISABLED ? _heroPowerDisabled
            : tag == GameTag.EXTRA_MINION_BATTLECRIES_BASE ? _extraBattlecryAndCombo : this[tag];

		public ControllerAuraEffects Clone(Controller c)
		{

			var cae = (ControllerAuraEffects)MemberwiseClone();
            if (_setEffects != null) cae._setEffects = new List<(GameTag, int, int)>(_setEffects);
            cae._game = c.Game;
			cae._sendHistory = c.Game.History
				? (Action<IPowerHistoryEntry>) c.Game.PowerHistory.Add
				: null;
			return cae;
		}

		public string Hash()
		{
			var sb = new StringBuilder("[CAE:");
			sb.Append(_timeOut);
			sb.Append(_spellPowerDouble);
			sb.Append(_heroPowerDouble);
			sb.Append(_restoreToDamage);
			sb.Append(_extraBattecry);
			sb.Append(_chooseBoth);
			sb.Append(_spellsCostHealth);
			sb.Append(_extraEndTurnEffect);
			sb.Append(_heroPowerDisabled);
			sb.Append(_allHealingDouble);
			sb.Append(_extraBattlecryAndCombo);
			sb.Append(_spellPower);
            AuraEffects.HashSets(sb, _setEffects);
			sb.Append("]");
			return sb.ToString();
		}

		#region Equality members

		public bool Equals(ControllerAuraEffects other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return _timeOut == other._timeOut && _spellPowerDouble == other._spellPowerDouble &&
			       _heroPowerDouble == other._heroPowerDouble && _restoreToDamage == other._restoreToDamage &&
			       _extraBattecry == other._extraBattecry && _chooseBoth == other._chooseBoth &&
			       _spellsCostHealth == other._spellsCostHealth && _extraEndTurnEffect == other._extraEndTurnEffect &&
			       _heroPowerDisabled == other._heroPowerDisabled && _allHealingDouble == other._allHealingDouble &&
			       _extraBattlecryAndCombo == other._extraBattlecryAndCombo && _spellPower == other._spellPower
                   && (_setEffects ?? Enumerable.Empty<(GameTag, int, int)>()).SequenceEqual(other._setEffects ?? Enumerable.Empty<(GameTag, int, int)>());
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ControllerAuraEffects) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = _timeOut;
				hashCode = (hashCode * 397) ^ _spellPowerDouble;
				hashCode = (hashCode * 397) ^ _heroPowerDouble;
				hashCode = (hashCode * 397) ^ _restoreToDamage;
				hashCode = (hashCode * 397) ^ _extraBattecry;
				hashCode = (hashCode * 397) ^ _chooseBoth;
				hashCode = (hashCode * 397) ^ _spellsCostHealth;
				hashCode = (hashCode * 397) ^ _extraEndTurnEffect;
				hashCode = (hashCode * 397) ^ _heroPowerDisabled;
				hashCode = (hashCode * 397) ^ _allHealingDouble;
				hashCode = (hashCode * 397) ^ _extraBattlecryAndCombo;
				hashCode = (hashCode * 397) ^ _spellPower;
				return hashCode;
			}
		}

		public static bool operator ==(ControllerAuraEffects left, ControllerAuraEffects right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ControllerAuraEffects left, ControllerAuraEffects right)
		{
			return !Equals(left, right);
		}

		#endregion
	}
}
