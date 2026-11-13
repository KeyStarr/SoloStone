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
using SabberStoneCore.Tasks;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Model.Entities
{
	/// <summary>
	/// Represents an entity in game which behaves as an actor. 
	/// The actions it can perform, as well as actions it undergoes,
	/// are defined by this interface.
	/// 
	/// The properties defined by this type are non complex, they have a
	/// very superficial meaning.
	/// </summary>
	public partial interface ICharacter : IPlayable
	{
		/// <summary>
		/// Indicates if this character can continue performing actions.
		/// </summary>
		bool IsDead { get; }

		/// <summary>
		/// Indicates if this character has the possibility to attack.
		/// </summary>
		bool CanAttack { get; }

		/// <summary>
		/// Contains a sequence of valid targets, also Characters, which
		/// can be attacked.
		/// </summary>
		IEnumerable<ICharacter> ValidAttackTargets { get; }

		/// <summary>
		/// Indicates if the provided target is attackable.
		/// 
		/// A quick solution would be to test if there are no taunt minions
		/// present and the target is not immune.
		/// </summary>
		/// <param name="target"></param>
		/// <returns></returns>
		bool IsValidAttackTarget(ICharacter target);

		/// <summary>
		/// This character takes damage from a certain other entity.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="damage"></param>
		/// <returns></returns>
		int TakeDamage(IPlayable source, int damage, bool combat = false, bool outgoingApplied = false);

		/// <summary>
		/// This character gets healed by a certain other entity.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="heal"></param>
		void TakeHeal(IPlayable source, int heal);

		/// <summary>
		/// This character receives a heal for it's maximum health.
		/// </summary>
		/// <param name="source"></param>
		void TakeFullHeal(IPlayable source);

		/// <summary>
		/// This character receives a specified amount of armor.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="armor"></param>
		void GainArmor(IPlayable source, int armor);

		event TriggerManager.TriggerHandler AfterAttackTrigger;
		void OnAfterAttackTrigger();

		bool HasAnyValidAttackTargets { get; }
	}

	/// <summary>
	/// Base implementation of ICharacter.
	/// <seealso cref="ICharacter"/>
	/// <seealso cref="Playable"/>
	/// </summary>
	public abstract partial class Character : Playable, ICharacter
	{
		public GameEventBus Events { get; } = new GameEventBus();

		public event TriggerManager.TriggerHandler PreDamageTrigger
		{
			add => Events.Subscribe<PreDamageEvent>(value);
			remove => Events.Unsubscribe<PreDamageEvent>(value);
		}
		public event TriggerManager.TriggerHandler TakeDamageTrigger
		{
			add => Events.Subscribe<TakeDamageEvent>(value);
			remove => Events.Unsubscribe<TakeDamageEvent>(value);
		}
		public event TriggerManager.TriggerHandler AfterAttackTrigger
		{
			add => Events.Subscribe<AfterAttackEvent>(value);
			remove => Events.Unsubscribe<AfterAttackEvent>(value);
		}

		/// <summary>
		/// Build a new character from the provided data.
		/// </summary>
		/// <param name="controller">Owner of the character; not specifically limited to players.</param>
		/// <param name="card">The card which this character embodies.</param>
		/// <param name="tags">Properties of this entity.</param>
		/// <param name="id">Integral id of this entity. </param>
		protected Character(in Controller controller, in Card card, in IDictionary<GameTag, int> tags, in int id)
			: base(in controller, in card, in tags, in id)
		{

		}

		/// <summary>
		/// A copy constructor. This constructor is only used to the inherited copy constructors.
		/// </summary>
		/// <param name="controller">The target <see cref="T:SabberStoneCore.Model.Entities.Controller" /> instance.</param>
		/// <param name="character">The source <see cref="T:SabberStoneCore.Model.Entities.Character`1" />.</param>
		protected Character(in Controller controller, in Character character) : base(in controller, character)
		{
			character.CopyInternalAttributes(this);
		}

		/// <summary>
		/// Character is dead or destroyed.
		/// </summary>
		internal void RestoreCloneSubscriptions(Character source, Dictionary<object, object> copies)
		{
			Events.RestoreCloneSubscriptions(source.Events, copies, Game);
		}

		public bool IsDead => Health <= 0 || ToBeDestroyed;

		public override int this[GameTag t]
		{
			get
			{
				switch (t)
				{
					case GameTag.ATK:
						return AttackDamage;
					case GameTag.HEALTH:
						return BaseHealth;
					case GameTag.DAMAGE:
						return Damage;
					case GameTag.STEALTH:
						return HasStealth ? 1 : 0;
					case GameTag.IMMUNE:
						return IsImmune ? 1 : 0;
					case GameTag.TAUNT:
						return HasTaunt ? 1 : 0;
					case GameTag.CANT_BE_TARGETED_BY_SPELLS:
					case GameTag.CANT_BE_TARGETED_BY_HERO_POWERS:
						return CantBeTargetedBySpells ? 1 : 0;
					case GameTag.NUM_ATTACKS_THIS_TURN:
						return _numAttackThisTurn;
					default:
						return base[t];
				}
			}
			set
			{
				switch (t)
				{
					case GameTag.ATK:
						AttackDamage = value;
						return;
					case GameTag.HEALTH:
						BaseHealth = value;
						return;
					case GameTag.DAMAGE:
						Damage = value;
						return;
                    case GameTag.ARMOR:
                        int armorBefore=Armor;
                        base[t]=value;
                        if(!(this is Hero) || ReferenceEquals(Controller.Hero,this)) Game.Facts.ArmorGained(this,Armor-armorBefore);
                        return;
					case GameTag.STEALTH:
						HasStealth = value > 0;
						return;
					case GameTag.IMMUNE:
						IsImmune = value > 0;
						return;
					case GameTag.TAUNT:
						HasTaunt = value > 0;
						return;
					case GameTag.CANT_BE_TARGETED_BY_SPELLS:
					case GameTag.CANT_BE_TARGETED_BY_HERO_POWERS:
						CantBeTargetedBySpells = value > 0;
						return;
					case GameTag.NUM_ATTACKS_THIS_TURN:
						_numAttackThisTurn = value;
						return;
					default:
						base[t] = value;
						return;
				}
			}
		}

		/// <summary>
		/// Character can attack.
		/// </summary>
		public virtual bool CanAttack => !IsExhausted && !IsFrozen && HasAnyValidAttackTargets && !CantAttack;

		/// <summary>
		/// Indicates if the provided character can be attacked by this character.
		/// </summary>
		/// <param name="target"></param>
		/// <returns></returns>
		public virtual bool IsValidAttackTarget(ICharacter target)
		{
			// got target but isn't contained in valid targets
			if (!ValidAttackTargets.Contains(target))
			{
				Game.Log(LogLevel.WARNING, BlockType.ACTION, "Character", !Game.Logging? "":$"{this} has an invalid target {target}.");
				return false;
			}

			if (target is Hero)
			{
				if (CantAttackHeroes || (this is Minion m && m.AttackableByRush))
				{
					Game.Log(LogLevel.WARNING, BlockType.ACTION, "Character", !Game.Logging ? "" : $"Can't attack Heroes!");
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Returns a sequence of characters which are attackable.
		/// </summary>
		public IEnumerable<ICharacter> ValidAttackTargets
		{
			get
			{
				bool tauntFlag = false;
                bool ignoreTaunt = Actions.CombatRules.IgnoresTaunt(this);
				var allTargets = new List<ICharacter>(4);
				var allTargetsTaunt = new List<ICharacter>(2);
				foreach (Minion minion in Controller.Opponent.BoardZone.GetAll())
				{
					if (!minion.HasStealth && !minion.IsImmune && !minion.Untouchable)
					{
						if (minion.HasTaunt && !ignoreTaunt)
						{
							allTargetsTaunt.Add(minion);
							tauntFlag = true;
							continue;
						}
						if (!tauntFlag)
							allTargets.Add(minion);
					}
				}
				if (tauntFlag)
					return allTargetsTaunt;

				Hero opHero = Controller.Opponent.Hero;

				if (!(this is Minion m && m.AttackableByRush) && !CantAttackHeroes && !opHero.IsImmune && !opHero.HasStealth)
					allTargets.Add(opHero);

				return allTargets;
			}
		}

		public bool HasAnyValidAttackTargets
		{
			get
			{
				var span = Controller.Opponent.BoardZone.GetSpan();
				for (int i = 0; i < span.Length; i++)
				{
					if (!(span[i].HasStealth || span[i].IsImmune || span[i].Untouchable))
						return true;
				}

				bool isOpHeroValidPlayTarget =
					!Controller.Opponent.Hero.HasStealth && !Controller.Opponent.Hero.IsImmune;

				if (isOpHeroValidPlayTarget && (!CantAttackHeroes || this is Minion m && m.AttackableByRush))
					return true; // Op Hero is a valid attack target

				return false;
			}
		}

		public bool HasDivineShield
		{
			get
			{
				if (!_data.TryGetValue(GameTag.DIVINE_SHIELD, out int value))
					return Card.DivineShield;
				return value > 0;
			}
			set
			{
				Actions.DeathContextRules.ResetShield(this);
                if (!value)
				{
					if (this[GameTag.DIVINE_SHIELD] == 1)
						Game.TriggerManager.OnLoseDivineShield(this);
					this[GameTag.DIVINE_SHIELD] = 0;
					return;
				}

				this[GameTag.DIVINE_SHIELD] = 1;
			}
		}

		/// <summary>
		/// Inflict damage onto this character.
		/// The actual amount still needs to be determined by the current
		/// state of the game. eg: The presence of immunity effects can cause
		/// the damage to be ignored.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="damage"></param>
		/// <param name="combat">Only the two direct hits resolved by Attack.</param>
		/// <param name="outgoingApplied">Redirected damage already passed the source modifiers.</param>
		/// <returns></returns>
		public int TakeDamage(IPlayable source, int damage, bool combat = false, bool outgoingApplied = false)
		{
			Game game = Game;
			using var damageHit = _history ? game.PowerHistory.DamageHit() : null;
			var hero = this as Hero;
            bool lightcaller = combat && source is Hero attackingHero && attackingHero.Weapon?.Card.Id == "BE_000"
                || source is Weapon weaponSource && weaponSource.Card.Id == "BE_000";
			var minion = this as Minion;
            if (!outgoingApplied) damage = Actions.CombatRules.Outgoing(source, damage);
            if (Actions.CombatRules.PreventsFire(source.Card.Type, source.Card[GameTag.SPELL_SCHOOL], this)) return 0;

			if (minion != null && (minion.Zone.Type != Enums.Zone.PLAY || minion.Untouchable))
				return 0;

			using var damageHistory = _history ? game.PowerHistory.EffectSource(source, this) : null;
            using var visual = _history && !combat ? game.PowerHistory.Visual(game,
                Presentation.VisualPhase.Damage, source, this) : null;

			if (HasDivineShield)
			{
				// Prevention must not consume a shield when no damage can reach it.
				if (damage <= 0 || IsImmune) return 0;
				game.Log(LogLevel.INFO, BlockType.ACTION, "Character", !game.Logging? "":$"{this} divine shield absorbed incoming damage.");
                if (_history)
                {
                    game.PowerHistory.HitTarget(Id);
                    game.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.DAMAGE,
                        Data = 0, Info = new List<int> { Id } });
                }
				if (!Actions.DeathContextRules.KeepShield(this)) HasDivineShield = false;
				return 0;
			}

			if (!IsImmune && Actions.CombatRules.ReplaceDamage(hero, damage)) return 0;

			int armor = hero?.Armor ?? 0;

			int amount = hero == null ? damage : armor < damage ? damage - armor : 0;

			// Damage event is created
			// Collect all the tasks and sort them by order of play
			// Death phase and aura update are not emerge here

			// place event related data
			game.TaskQueue.StartEvent();
			EventMetaData temp = game.CurrentEventData;
			game.CurrentEventData = new EventMetaData(source, this, amount);

			// added pre damage
			if (_history)
				PreDamage = amount;

			// Predamage triggers (e.g. Ice Block)
			if (Events.HasSubscribers<PreDamageEvent>())
			{
				Events.Raise(new PreDamageEvent(this));
				game.ProcessTasks();
				amount = game.CurrentEventData.EventNumber;
				if (amount == 0 && armor == 0)
				{
					if (_history)
						PreDamage = 0;

					game.TaskQueue.EndEvent();
					game.CurrentEventData = temp;
					return 0;
				}
			}
			if (IsImmune)
			{
				game.TaskQueue.EndEvent();
				game.CurrentEventData = temp;

				game.Log(LogLevel.INFO, BlockType.ACTION, "Character", !game.Logging ? "" : $"{this} is immune.");
				if (_history)
					PreDamage = 0;
				return 0;
			}

            if (hero != null && CardSets.Client.Sets.TheLivingPlague.Replace(source, hero,
                Actions.DamageArithmetic.Add(Math.Max(0, amount), Math.Min(armor, Math.Max(0, damage)))))
            {
                if (_history) PreDamage = 0;
                game.TaskQueue.EndEvent();
                game.CurrentEventData = temp;
                return 0;
            }

			// reset predamage
			if (_history)
				PreDamage = 0;

            if (_history)
            {
                // Per-hit damage includes absorbed armor, after prevention and before native stat updates.
                int resolvedDamage = Actions.DamageArithmetic.Add(Math.Max(0, amount), Math.Min(armor, Math.Max(0, damage)));
                if (resolvedDamage > 0)
                {
                    game.PowerHistory.HitTarget(Id);
                    game.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.DAMAGE,
                        Data = resolvedDamage, Info = new List<int> { Id } });
                }
            }

			// remove armor first from hero ....
			if (armor > 0)
				hero.Armor = armor < damage ? 0 : armor - damage;

			// final damage is beeing accumulated
			int damageBefore = Damage;
            bool healthReplaced = Actions.HealingRules.ReplaceHealthLoss(hero, amount);
            int healthBefore = Health;
            bool fatalCandidate = !ToBeDestroyed && Health > 0;
            var poisonSource = minion != null && amount > 0 && !ToBeDestroyed
                ? PoisonousDamageSource(source, combat) : null;
            if (!healthReplaced)
                using (game.Facts.SuppressHealth()) SetDamage(Actions.DamageArithmetic.Add(Damage, amount), poisonSource == null);
            // Hero health facts describe the capped tag delta; observers below still receive full resolved damage.
            long historyDamageSequence=game.Facts.DamageResolved(source,this,healthBefore,armor-(hero?.Armor??0),hero == null ? amount : Damage-damageBefore,combat);
            visual?.Dispose();

			// Damage dealt = armor lost + health lost, fixed before damage triggers can change either.
			int dealt = Actions.DamageArithmetic.Add(Math.Max(0, amount), Math.Max(0, armor - (hero?.Armor ?? 0)));

			if (Actions.QuestRules.HasDamageListener(source.Controller))
				Actions.QuestRules.DamageResolved(source.Controller, new DamageResolvedFact(source, this, dealt));

            // Resolve the keyword before damage reactions can silence the source or test survival.
            if (minion != null && Damage - damageBefore > 0 && !ToBeDestroyed
                && minion.Zone.Type == Enums.Zone.PLAY && poisonSource != null)
            {
                using (_history ? game.PowerHistory.PoisonousSource(poisonSource, this) : null)
                    ToBeDestroyed = true;
            }

			game.Log(LogLevel.INFO, BlockType.ACTION, "Character", !game.Logging? "":$"{this} took damage for {amount}({damage}).");

			if (fatalCandidate && ToBeDestroyed)
                game.Facts.CaptureFatal(this,poisonSource??source,poisonSource!=null?HistoryDeathCause.Poisonous:
                    combat?HistoryDeathCause.Combat:HistoryDeathCause.SpellOrEffect,historyDamageSequence);
			if (fatalCandidate) Actions.DeathContextRules.RecordFatal(minion, source, combat);

            Actions.SwapRules.DamageResolved(this, dealt);
            Actions.NativeTailRules.DamageResolved(this, dealt);
            Actions.StatDamageRules.Resolved(source, dealt);

			// on-damage triggers
			Events.Raise(new TakeDamageEvent(this));
			game.TriggerManager.OnDamageTrigger(this);
			game.TriggerManager.OnDealDamageTrigger(source);

			// Check if the source is Overkill
			if (source.HasOverkill && source.Controller == game.CurrentPlayer && Health < 0)
			{
				game.Log(LogLevel.VERBOSE, BlockType.TRIGGER, "TakeDamage", !_logging ? "" : $"{source}' Overkill is triggered.");

				ISimpleTask task = source is Hero h ? h.Weapon.Card.Power.OverkillTask : source.Card.Power.OverkillTask;
				game.TaskQueue.Enqueue(task, source.Controller, source, null);
			}

			game.ProcessTasks();
			game.TaskQueue.EndEvent();
			game.CurrentEventData = temp;

            if (source.HasLifeSteal || lightcaller)
                Actions.HealingRules.Lifesteal(source, dealt, lightcaller);

			if (hero != null)
				hero.DamageTakenThisTurn = Actions.DamageArithmetic.Add(hero.DamageTakenThisTurn, amount);

			if (source.Card.Type == CardType.HERO_POWER)
				source.Controller.NumHeroPowerDamageThisGame = Actions.DamageArithmetic.Add(source.Controller.NumHeroPowerDamageThisGame, amount);

			return amount;
		}

        private static IPlayable PoisonousDamageSource(IPlayable source, bool combat)
        {
            if (combat && source is Hero hero && hero.Weapon?.Poisonous == true) return hero.Weapon;
            return (source is Minion minion ? minion.Poisonous : source[GameTag.POISONOUS] == 1) ? source : null;
        }

		/// <summary>
		/// Heal up all taken damage.
		/// </summary>
		/// <param name="source"></param>
		public void TakeFullHeal(IPlayable source)
		{
			TakeHeal(source, Damage);
		}

		/// <summary>
		/// Heal a specified amount of health.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="heal"></param>
		public void TakeHeal(IPlayable source, int heal)
		{
            Actions.HealingRules.Heal(source, new[] { this }, heal);
        }

        internal void ApplyHeal(IPlayable source, int heal)
        {
			// we don't heal undamaged entities
			if (Damage == 0)
			{
				return;
			}

			int amount = Damage > heal ? heal : Damage;
            using var healHistory = _history && amount > 0 ? Game.PowerHistory.EffectSource(source, this) : null;
            using var visual = _history && amount > 0 ? Game.PowerHistory.Visual(Game,
                Presentation.VisualPhase.Heal, source, this) : null;
            if (_history && amount > 0)
            {
                Game.PowerHistory.HitTarget(Id);
                Game.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.HEALING,
                    Data = amount, Info = new List<int> { Id } });
            }
			if (Game.Logging)
				Game.Log(LogLevel.INFO, BlockType.ACTION, "Character", $"{this} took healing for {amount}.");
			int healthBefore = Health;
            using (Game.Facts.SuppressHealth()) Damage -= amount;
            Game.Facts.HealthChanged(this,healthBefore,HistoryHealthChangeReason.Heal);
            visual?.Dispose();

			// Heal event created
			// Process gathered tasks
			Game.TaskQueue.StartEvent();
			EventMetaData temp = Game.CurrentEventData;
			Game.CurrentEventData = new EventMetaData(source, this, amount);
			Game.TriggerManager.OnHealTrigger(this);
			Game.ProcessTasks();
			Game.TaskQueue.EndEvent();
			Game.CurrentEventData = temp;

			if (this is Hero)
				Controller.AmountHeroHealedThisTurn = Actions.DamageArithmetic.Add(Controller.AmountHeroHealedThisTurn, amount);
		}

		/// <summary>
		/// Gain the specified amount of armor.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="armor"></param>
		public void GainArmor(IPlayable source, int armor)
		{
			Game.Log(LogLevel.INFO, BlockType.ACTION, "Character", !Game.Logging? "":$"{this} gaining armor for {armor}.");
			Armor += armor;
			EventMetaData temp = Game.CurrentEventData;
			Game.CurrentEventData = new EventMetaData(source, this, armor);
			Game.TriggerManager.OnArmorTrigger(this);
			//Game.ProcessTasks();
			Game.CurrentEventData = temp;
		}

		public void OnAfterAttackTrigger()
		{
			Events.Raise(new AfterAttackEvent(this));
		}

		public override string Hash(params GameTag[] ignore)
		{
			var sb = new StringBuilder(base.Hash(ignore));
			sb.Append($"[A:{_modifiedATK}, ");
			sb.Append($"H:{_modifiedHealth}, ");
			sb.Append($"D:{_damage}]");
			return sb.ToString();
		}
	}

	public partial interface ICharacter
	{
		/// <summary>
		/// The amount of damage this character can output.
		/// </summary>
		int AttackDamage { get; set; }

		/// <summary>
		/// The amount of damage taken during the evaluation of one phase.
		/// </summary>
		int Damage { get; set; }

		/// <summary>
		/// The amount of damage this character is about to take.
		/// </summary>
		int PreDamage { get; set; }

		/// <summary>
		/// The amount of health this character has remaing. This value is returned
		/// AFTER SUBTRACTING the pending damage for this entity.
		/// </summary>
		int Health { get; set; }

		/// <summary>
		/// The starting amount of health of this character.
		/// </summary>
		int BaseHealth { get; }

		/// <summary>
		/// This character is currently attacking another character.
		/// </summary>
		bool IsAttacking { get; set; }

		/// <summary>
		/// This character is currently defending against another character.
		/// </summary>
		bool IsDefending { get; set; }

		/// <summary>
		/// Amount of attacks this character has executed during this turn.
		/// </summary>
		int NumAttacksThisTurn { get; set; }

		/// <summary>
		/// Character is member of Race.
		/// Characters of Race.ALL.  IE Amalgam.IsRace(Race.MULROC/Race.DRAGON/...) => true
		/// </summary>
		bool IsRace(Race race);

		///// <summary>
		///// Character should exit combat.
		///// </summary>
		//bool ShouldExitCombat { get; set; }

		/// <summary>
		/// Character is frozen.
		/// </summary>
		bool IsFrozen { get; set; }

		/// <summary>
		/// Character is silenced.
		/// </summary>
		bool IsSilenced { get; set; }

		/// <summary>
		/// Character is immune.
		/// </summary>
		bool IsImmune { get; set; }

		/// <summary>
		/// Character has taunt.
		/// </summary>
		bool HasTaunt { get; set; }

		/// <summary>
		/// Character has windfury.
		/// </summary>
		bool HasWindfury { get; }

		/// <summary>
		/// Character has stealth.
		/// </summary>
		bool HasStealth { get; }

		/// <summary>
		/// Character can't be targeted by spells.
		/// </summary>
		bool CantBeTargetedBySpells { get; set; }

		/// <summary>
		/// Character can't be targeted by opponents.
		/// </summary>
		bool CantBeTargetedByOpponents { get; set; }

		/// <summary>
		/// Character can't be targeted by heropowers.
		/// </summary>
		bool CantBeTargetedByHeroPowers { get; }

	}

	public abstract partial class Character
	{
		internal int? _modifiedATK;
		internal int? _modifiedHealth;
		internal bool? _modifiedStealth;
		internal bool? _modifiedImmune;
		internal bool? _modifiedTaunt;
		internal bool? _modifiedCantBeTargetedBySpells;

		internal int _damage;
		internal int _numAttackThisTurn;

		internal void CopyInternalAttributes(in Character copy)
		{
			if (copy.IsSilenced != IsSilenced) copy.IsSilenced = IsSilenced;
			copy._modifiedATK = _modifiedATK;
			copy._modifiedHealth = _modifiedHealth;
			copy._damage = _damage;
			copy._numAttackThisTurn = _numAttackThisTurn;
			copy._modifiedStealth = _modifiedStealth;
			copy._modifiedImmune = _modifiedImmune;
			copy._modifiedTaunt = _modifiedTaunt;
			copy._modifiedCantBeTargetedBySpells = _modifiedCantBeTargetedBySpells;
		}

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element

		/// <summary>Diagnostics (match event log): current attack, max health and damage by the getters' own formulas, WITHOUT initializing
		/// the lazily cached printed values (AttackDamage/BaseHealth write _modifiedATK/_modifiedHealth on first read, which changes
		/// state): a log must never alter the game it describes. Heroes: attack here excludes the weapon.</summary>
		public void PeekStats(out int attack, out int maxHealth, out int damage)
		{
			long a = (long)(_modifiedATK ?? Card.ATK) + (AuraEffects?.ATK ?? 0);
			attack = Actions.DamageArithmetic.Cap(a);
			long h = (long)(_modifiedHealth ?? Card.Health) + (AuraEffects?.Health ?? 0);
			maxHealth = Actions.AttributeArithmetic.Clamp(h);
			damage = _damage;
		}

		public virtual int AttackDamage
		{
			get
			{
				long value = _modifiedATK ?? (_modifiedATK = Card.ATK).Value;

				value += AuraEffects?.ATK ?? 0;

				return Actions.DamageArithmetic.Cap(value);
			}
			set
			{
				if (_logging)
					Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging ? "" : $"{this} set data {GameTag.ATK} to {value}");
				if (_history && (long)value + (AuraEffects?.ATK ?? 0) != AttackDamage)
				{
					Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.ATK, value));
					_data[GameTag.ATK] = value;
				}

				_modifiedATK = value;
			}
		}

		public int BaseHealth
		{
			get
			{
				int value = _modifiedHealth ?? (_modifiedHealth = Card.Health).Value;

				return Actions.AttributeArithmetic.Clamp((long)value + (AuraEffects?.Health ?? 0));
			}
			set
			{
                int historyHealthBefore = this is Hero && ReferenceEquals(Controller.Hero,this) ? Health : 0;
				if (_logging)
					Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging ? "" : $"{this} set data {GameTag.HEALTH} to {value}");
				if (_history && (long)value + (AuraEffects?.Health ?? 0) != BaseHealth)
				{
					Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.HEALTH, value));
					_data[GameTag.HEALTH] = value;
				}

				_modifiedHealth = value;
                Game.Facts.HealthSet(this,historyHealthBefore);
			}
		}

		public int Damage
		{
			get => _damage;
			set => SetDamage(value);
		}

        // Poisonous owns the lethal flag for its hit so the destroy tag stays inside the keyword block.
		private void SetDamage(int value, bool destroyOnLethal = true)
		{
                int historyHealthBefore = this is Hero && ReferenceEquals(Controller.Hero,this) ? Health : 0;
			// don't allow negative values
			if (value < 0)
				value = 0;
			else if (destroyOnLethal && BaseHealth <= value)
				ToBeDestroyed = true;

			if (_logging)
				Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging ? "" : $"{this} set data {GameTag.DAMAGE} to {value}");
			if (_history && value != _damage)
			{
				Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.DAMAGE, value));
				_data[GameTag.DAMAGE] = value;
			}

			_damage = value;
                Game.Facts.HealthSet(this,historyHealthBefore);
		}

		public int Health
		{
			get => BaseHealth - _damage;
			set
			{
                int historyHealthBefore = this is Hero && ReferenceEquals(Controller.Hero,this) ? Health : 0;
				if (value == 0)
				{
					ToBeDestroyed = true;
				}

				_modifiedHealth = value;
				_damage = 0;

				if (_logging)
				{
					Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging ? "" : $"{this} set data {GameTag.HEALTH} to {value}");
					Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging ? "" : $"{this} set data {GameTag.DAMAGE} to {value}");
				}

				if (_history)
				{
					Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.HEALTH, value));
					_data[GameTag.HEALTH] = value;
					Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, GameTag.DAMAGE, 0));
					_data[GameTag.DAMAGE] = 0;
				}
                Game.Facts.HealthSet(this,historyHealthBefore);
			}
		}

		public bool CantAttack
		{
			get
			{
                if (Actions.PlayLockRules.IsSleeping(this) || Actions.TemporaryRules.BlocksAttack(this)) return true;
				if (!_data.TryGetValue(GameTag.CANT_ATTACK, out int value))
					return Card.CantAttack;
				
				return value > 0;
			}
			set => this[GameTag.CANT_ATTACK] = value ? 1 : 0;
		}

		public virtual bool CantAttackHeroes
		{
			get
			{
				_data.TryGetValue(GameTag.CANNOT_ATTACK_HEROES, out int value);
				return value > 0;
			}
			set => this[GameTag.CANNOT_ATTACK_HEROES] = value ? 1 : 0;
		}

		internal bool CantBeTargetedBySpellsNoCache => (AuraEffects?.CantBeTargetedBySpells ?? false) ||
			(_modifiedCantBeTargetedBySpells ?? Card.CantBeTargetedBySpells);

		public bool CantBeTargetedBySpells
		{
			get => (AuraEffects?.CantBeTargetedBySpells ?? false) ||
			       (_modifiedCantBeTargetedBySpells ??
			        (_modifiedCantBeTargetedBySpells = Card.CantBeTargetedBySpells).Value);
			set
			{
				_modifiedCantBeTargetedBySpells = value;
				if (_history)
				{
					base[GameTag.CANT_BE_TARGETED_BY_SPELLS] = value ? 1 : 0;
					base[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = value ? 1 : 0;
				}
			}
		}

		public bool CantBeTargetedByHeroPowers
		{
			get => CantBeTargetedBySpells;
		}

		public int Armor
		{
			get
			{
				_data.TryGetValue(GameTag.ARMOR, out int value);
				return value;
			}
			set { this[GameTag.ARMOR] = value; }
		}

		public int LastAffectedBy
		{
			get
			{
				_data.TryGetValue(GameTag.LAST_AFFECTED_BY, out int value);
				return value;
			}
			set { this[GameTag.LAST_AFFECTED_BY] = value; }
		}

		public bool CantBeTargetedByOpponents
		{
			get { return this[GameTag.CANT_BE_TARGETED_BY_OPPONENTS] == 1; }
			set { this[GameTag.CANT_BE_TARGETED_BY_OPPONENTS] = value ? 1 : 0; }
		}

		public bool IsAttacking
		{
			get { return this[GameTag.ATTACKING] == 1; }
			set { this[GameTag.ATTACKING] = value ? 1 : 0; }
		}

		public bool IsDefending
		{
			get { return this[GameTag.DEFENDING] == 1; }
			set { this[GameTag.DEFENDING] = value ? 1 : 0; }
		}

		public virtual bool IsImmune
		{
			get => (AuraEffects?.Immune > 0) || _modifiedImmune == true || IsAttacking && this[GameTag.IMMUNE_WHILE_ATTACKING] > 0;
			set
			{
				_modifiedImmune = value;
				base[GameTag.IMMUNE] = value ? 1 : 0;
			}
		}

		public bool IsFrozen
		{
			get
			{
				_data.TryGetValue(GameTag.FROZEN, out int value);
				return value == 1;
			}
			set
			{
				if (value)
				{
					Game.TriggerManager.OnFreezeTrigger(this);
					base[GameTag.FROZEN] = 1;
				}
				else
				{
					base[GameTag.FROZEN] = 0;
				}
			}
		}

		public bool IsSilenced
		{
			get { return _data.TryGetValue(GameTag.SILENCED, out int value) && value != 0; }
			set { this[GameTag.SILENCED] = value ? 1 : 0; }
		}

		public bool HasTaunt
		{
			// Printed Taunt follows in-place card changes; only explicit changes are overrides.
			get => (AuraEffects?.Taunt ?? false) || (_modifiedTaunt ?? Card.Taunt);
			set
			{
				// External Taunt keeps the effective value unchanged.
				if (AuraEffects?.Taunt ?? false)
					_data[GameTag.TAUNT] = value ? 1 : 0;
				else
					base[GameTag.TAUNT] = value ? 1 : 0;
				_modifiedTaunt = value;
			}
		}

		public abstract bool HasWindfury { get; set; }

		public bool HasStealth
		{
			get => _modifiedStealth ?? (_modifiedStealth = Card.Stealth).Value;
			set
			{
				base[GameTag.STEALTH] = value ? 1 : 0;
				_modifiedStealth = value;
			}
		}

		public int NumAttacksThisTurn
		{
			get => _numAttackThisTurn;
			set
			{
				_numAttackThisTurn = value;
				if (_history)
					base[GameTag.NUM_ATTACKS_THIS_TURN] = value;
			}
		}

		public int PreDamage
		{
			get => _data[GameTag.PREDAMAGE];
			set => this[GameTag.PREDAMAGE] = value;
		}

		public bool IsRace(Race race) => Card.IsRace(race);

		public bool ShouldExitCombat
		{
			get { return this[GameTag.SHOULDEXITCOMBAT] == 1; }
			set { this[GameTag.SHOULDEXITCOMBAT] = value ? 1 : 0; }
		}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element

	}
}
