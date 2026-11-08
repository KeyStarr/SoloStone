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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Model.Entities
{
	public partial class Enchantment : IPlayable
	{
		private readonly EntityData _tags;
		private int _creatorId;
		private int _targetId;
		private int _controllerId;
		private IPlayable _creator;
		private IEntity _target;
		private Controller _controller;
		private Card _capturedCard;
        [NonSerialized] private bool _rebindingController;

		private Enchantment(in Controller controller, in Card card, in EntityData tags, in int id)
		{
			_history = controller.Game.History;
			_logging = controller.Game.Logging;

			Game = controller.Game;
			Controller = controller;
			Card = card;
			_tags = tags;
			Id = id;
		}

		private Enchantment(in Controller c, in Enchantment e)
		{
			_history = c.Game.History;
			_logging = c.Game.Logging;

			Game = c.Game;
			Card = e.Card;
			Id = e.Id;
			_targetId = e._targetId;
			_controllerId = e._controllerId;
			_creatorId = e._creatorId;
			_capturedCard = e._capturedCard;
			Game.IdEntityDic[Id] = this;
			if (e.IsOneTurnActive)
			{
				c.Game.OneTurnEffectEnchantments.Add(this);
				IsOneTurnActive = true;
			}

			_tags = new EntityData(entityData: e._tags);
			OrderOfPlay = e.OrderOfPlay;
            CostPaymentAppliedAt = e.CostPaymentAppliedAt;
		}

		internal void ResolveCloneReferences(Enchantment source)
		{
			_controller = Game.ControllerById(_controllerId);
			_creator = Game.IdEntityDic[_creatorId];
			_target = source.Target is Controller ? (IEntity)Game.ControllerById(_targetId) : Game.IdEntityDic[_targetId];
			Zone = Controller.BoardZone;
			// Character-local subscriptions need the completed entity registry.
			source.ActivatedTrigger?.Activate(this, cloning: true);
		}

		public int this[GameTag t]
		{
			get => Actions.PayloadRules.CapturedScript(this,t,out int captured) ? captured : _tags.TryGetValue(t, out int value) ? value : 0;
			set
			{
				if (_history && (int)t < 1000)
					if (value != this[t])
						Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, t, value));
				_tags[t] = value;
			}
		}

		/// <summary>
		/// The entity that this enchantment is attached to.
		/// </summary>
		//public IEntity Target { get; private set; }
		public IEntity Target
		{
			get => _target ?? (_target = Game.IdEntityDic[_targetId]);
			private set
			{
				_targetId = value.Id;
				_target = value;
			}
		}

		/// <summary>
		/// <see cref="SabberStoneCore.Model.Card"/> information captured in this instance.
		/// </summary>
		public Card CapturedCard
		{
			get => _capturedCard;
			set
			{
				_capturedCard = value;
				if (value != null && Game.History && (Card.Text?.Contains("{0}") ?? false))
				{
					Card c = Card.Clone();
					c.Text = String.Format(c.Text, value.Name);
					Card = c;
				}
			}
		}

		public IPlayable Creator
		{
			get => _creator ?? (_creator = Game.IdEntityDic[_creatorId]);
			private set
			{
				_creatorId = value.Id;
				_creator = value;
			}
		}

		public Controller Controller
		{
			get => _controller ?? (_controller = Game.ControllerById(_controllerId));
			set
			{
				_controllerId = value.Id;
				_controller = value;
			}
		}

        internal void RebindCardStateTarget(IEntity target)
        {
            if (target.Id != _targetId) throw new InvalidOperationException("Card-state transform must preserve entity identity.");
            Target = target;
        }

        public bool IsOneTurnActive { get; private set; }

        /// <summary>Schedule instance expiry without changing the pinned enchantment definition.</summary>
        public void ExpireAtEndOfTurn()
        {
            if (IsOneTurnActive) return;
            IsOneTurnActive = true;
            Game.OneTurnEffectEnchantments.Add(this);
        }

        internal void FreezeForPayload()
        {
            ActivatedTrigger?.Remove();
            IsOneTurnActive = false;
            Game.OneTurnEffectEnchantments.Remove(this);
        }

		public int ScriptTag1 => this[GameTag.TAG_SCRIPT_DATA_NUM_1];

		public int ScriptTag2 => this[GameTag.TAG_SCRIPT_DATA_NUM_2];

        // Bonus actors reuse Magnetic definitions but must keep their non-Magnetic lifecycle on copies.
        internal static Enchantment CopyAttachment(in Controller controller, in IPlayable creator,
            in IEntity target, Enchantment original, int num1 = 0, int num2 = 0)
        {
            var copy = GetInstance(controller, creator, target, original.Card, num1, num2, original[Actions.PayloadRules.ReferenceEntity]);
            if (original[Actions.BonusEffects.State] != 0)
                copy[Actions.BonusEffects.State] = original[Actions.BonusEffects.State];
            Actions.PlayLockRules.CopyState(original, copy);
            // Attached aura contributions are not part of copied native stats; their expiry belongs to the new actor.
            if (copy.Power.Aura is Aura { Type: AuraType.ATTACHED } &&
                !(target is IPlayable stored && Actions.PayloadRules.IsStored(stored)))
            {
                copy.Power.Aura.Activate(copy);
                copy.Power.Trigger?.Activate(copy);
            }
            return copy;
        }

		/// <summary>
		/// Creates and adds a new Enchantment entity to the given Controller's Game.
		/// </summary>
		/// <param name="controller">The controller of the enchantment.</param>
		/// <param name="creator">The entity who creates the enchantment.</param>
		/// <param name="target">The entity who is subjected to the enchantment.</param>
		/// <param name="card">The card from which the enchantment must be derived.</param>
		/// <returns>The resulting enchantment entity.</returns>
		public static Enchantment GetInstance(in Controller controller, in IPlayable creator, in IEntity target, in Card card, int num1 = 0, int num2 = 0, int reference = 0)
		{
			int id = controller.Game.NextId;

			var tags = new EntityData(4);

            bool follow = card.Id == "CAP_002e" || card.Id == "CAP_101e" || card.Id == "CAP_402e" || card.Id == "CAP_802e1";
            if (follow) Actions.CardStateBoundary.EnrollAttachments(target);

			var instance = new Enchantment(in controller, in card, in tags, in id)
			{
				Creator = creator,
				Target = target,
			};

			if (target.AppliedEnchantments == null)
				target.AppliedEnchantments = new List<Enchantment> {instance};
			else
				target.AppliedEnchantments.Add(instance);

			//controller.Game.IdEntityDic.Add(instance.Id, instance);
			controller.Game.IdEntityDic[instance.Id] = instance;
			controller.Game.Facts.EntityCreated(card);

            if (follow)
            {
                tags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                tags[GameTag.ATTACHED] = target.Id;
                tags[GameTag.CREATOR] = creator.Id;
                tags[GameTag.TAG_ONE_TURN_EFFECT] = 1;
            }

            bool gift = card[GameTag.IS_NIGHTMARE_BONUS] == 1;
            if (gift)
            {
                tags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                tags[GameTag.ATTACHED] = target.Id;
                tags[GameTag.CREATOR] = creator.Id;
                tags[GameTag.IS_NIGHTMARE_BONUS] = 1;
                tags[GameTag.TAG_SCRIPT_DATA_NUM_6] = card[GameTag.TAG_SCRIPT_DATA_NUM_6];
            }

            if (card.Id == "JAIL_907e" || card.Id == "JAIL_907e02" || card.Id == "CAP_407e")
            {
                tags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                tags[GameTag.ATTACHED] = target.Id;
                tags[GameTag.CREATOR] = creator.Id;
                tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = num1;
            }

            bool cardState = card[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT] != 0 && card.Id != "EDR_469e"
                || card.Id == "GBL_999e" || card.Id == "TLC_450e" || card[(GameTag)4390] != 0;
            if (cardState)
            {
                tags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                tags[GameTag.ATTACHED] = target.Id;
                tags[GameTag.CREATOR] = creator.Id;
                tags[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT] = card[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT];
                tags[(GameTag)976] = card[(GameTag)976];
                tags[(GameTag)451] = card[(GameTag)451];
            }

            if (card.Id == "EDR_469e")
            {
                tags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                tags[GameTag.ATTACHED] = target.Id;
                tags[GameTag.CREATOR] = creator.Id;
                tags[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT] = card[GameTag.DORMANT_AWAKEN_CONDITION_ENCHANT];
                tags[GameTag.TRIGGER_VISUAL] = card[GameTag.TRIGGER_VISUAL];
            }

			if (reference != 0) tags[Actions.PayloadRules.ReferenceEntity] = reference;

			if (controller.Game.History)
			{
				tags.Add(GameTag.ENTITY_ID, id);
				tags.Add(GameTag.ZONE, (int)Enums.Zone.SETASIDE);
				tags.Add(GameTag.CONTROLLER, controller.PlayerId);

				controller.Game.PowerHistory.Add(new PowerHistoryFullEntity
				{
					Entity = new PowerHistoryEntity
					{
						Id = instance.Id,
						Name = card.Id == "EDR_469e" || gift || cardState || follow ? card.Id : "",
						Tags = tags.ToDictionary(k => k.Key, k => k.Value)
					}
				});

				if (!(target.Zone is DeckZone))
				{
					var gameTags = new Dictionary<GameTag, int>
					{
						{GameTag.CONTROLLER, controller.PlayerId},
						{GameTag.CARDTYPE, (int) CardType.ENCHANTMENT},
						{GameTag.ATTACHED, target.Id},
						{GameTag.DAMAGE, 0},
						{GameTag.ZONE, (int) Enums.Zone.SETASIDE},
						{GameTag.ENTITY_ID, instance.Id},
						{GameTag.ZONE_POSITION, 0},
						{GameTag.CREATOR, creator.Id},
						{GameTag.TAG_LAST_KNOWN_COST_IN_HAND, 0}
						//	CREATOR_DBID
						//	479
					};
                    // Capture display payload before later instance initialization; never read live values for old SHOWs.
                    if (num1 > 0 && (card.Text?.Contains("@") == true || card.Text?.Contains("{0}") == true))
                    {
                        gameTags[GameTag.TAG_SCRIPT_DATA_NUM_1] = num1;
                        if (num2 > 0) gameTags[GameTag.TAG_SCRIPT_DATA_NUM_2] = num2;
                    }
					if (reference != 0) gameTags[Actions.PayloadRules.ReferenceEntity] = reference;
					if (card[GameTag.TAG_ONE_TURN_EFFECT] == 1)
						gameTags.Add(GameTag.TAG_ONE_TURN_EFFECT, 1);
                    if (gift)
                    {
                        gameTags[GameTag.IS_NIGHTMARE_BONUS] = 1;
                        gameTags[GameTag.TAG_SCRIPT_DATA_NUM_6] = card[GameTag.TAG_SCRIPT_DATA_NUM_6];
                    }
					controller.Game.PowerHistory.Add(new PowerHistoryShowEntity
					{
						Entity = new PowerHistoryEntity
						{
							Id = instance.Id,
							Name = instance.Card.Id,
							Tags = gameTags
						}
					});
				}
				
				instance[GameTag.ZONE] = (int)Enums.Zone.PLAY;
			}

			if (card[GameTag.TAG_ONE_TURN_EFFECT] == 1)
			{
				instance.IsOneTurnActive = true;
				controller.Game.OneTurnEffectEnchantments.Add(instance);
			}


			instance.Zone = controller.BoardZone;
			instance.OrderOfPlay = controller.Game.NextOop;
            if (card.Id == "EDR_489e1") instance.CostPaymentAppliedAt = Actions.CostRules.NextPaymentOrder(controller.Game);
            if (gift)
            {
                tags[GameTag.ENTITY_ID] = id;
                tags[GameTag.CONTROLLER] = controller.PlayerId;
                instance[GameTag.ZONE] = (int)Enums.Zone.PLAY;
                DarkGifts.SetTag((IPlayable)target, GameTag.HAS_DARK_GIFT, 1);
            }
			//	323 = 1

			if (card.Power.DeathrattleTask != null)
				((IPlayable)target).HasDeathrattle = true;

			controller.Game.Log(LogLevel.VERBOSE, BlockType.ACTION, "Enchantment",
				!controller.Game.Logging ? "" : $"Enchantment {card} created by {creator} is added to {target}.");

			if (num1 > 0)
			{
				tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = num1;
				if (num2 > 0)
					tags.Add(GameTag.TAG_SCRIPT_DATA_NUM_2, num2);
			}

            // Persist the original privacy relationship even after the target upgrades or transforms.
            if (Actions.CardStateBoundary.HasPrivateAttachments(target)) Actions.CardStateBoundary.EnrollAttachments(target);
			return instance;
		}

        public void RedactReferencesForFairView(IPlayable hidden)
        {
            if (!(hidden is Unknown)) throw new ArgumentException("Fair-view replacement must be Unknown.", nameof(hidden));
            bool redacted = false;
            if (_targetId == hidden.Id) { Target = hidden; redacted = true; }
            if (_creatorId == hidden.Id) { Creator = hidden; redacted = true; }
            if (!redacted) return;
            _capturedCard = null;
            foreach (var tag in _tags.Keys.Where(t => Actions.RepeatRules.IsInternalTag(t) || Actions.DormantRules.IsPrivateTag(t) || Actions.TemporaryRules.IsPrivateTag(t)
                || Actions.ShatterRules.IsInternalTag(t) || Actions.HandProgressRules.IsInternalTag(t) || Actions.PlayLockRules.IsPrivateTag(t)).ToArray()) _tags.Remove(tag);
        }

		public Enchantment Clone(in Controller controller)
		{
			// The destination registry is the clone identity map; never duplicate a creator.
			if (controller.Game.IdEntityDic.ContainsKey(Id))
				return (Enchantment)controller.Game.IdEntityDic[Id];
			return new Enchantment(in controller, this);
		}

        internal void RebindTarget(IEntity target) { Target = target; }

        internal void RebindController(Controller receiver, bool refreshAuras = true)
        {
            var trigger = ActivatedTrigger;
            var aura = OngoingEffect;
            trigger?.Remove();
            // Aura removal normally also removes its owning enchantment; keep this physical attachment.
            _rebindingController = true;
            try { aura?.Remove(); if (refreshAuras) Game.AuraUpdate(); }
            finally { _rebindingController = false; }
            Controller = receiver;
            this[GameTag.CONTROLLER] = receiver.PlayerId;
            Zone = receiver.BoardZone;
            trigger?.Activate(this, trigger.TriggerActivation);
            if (aura != null) Power.Aura?.Activate(this);
        }

		public void Remove()
		{
            if (_rebindingController) return;
            Actions.InfinityRules.Removing(this);
            // A committed bearer's normal text can put it in the graveyard before its after-play event.
            if (Actions.FollowRules.IsGrant(this) && ActivatedTrigger != null && Target.Zone is GraveyardZone
                && Game.CurrentEventData?.EventSource?.Id == Target.Id && !(Target is Spell countered && countered.IsCountered)) return;
            if (Actions.FollowRules.IsGrant(this)) this[GameTag.ATTACHED] = 0;
            if (Power.RetainInHeldForm) Power.Enchant?.RemoveEffect(Target);
            if (Target is Spell && Power.Enchant != null)
                foreach (var effect in Power.Enchant.Effects.Where(effect => effect.Tag == GameTag.SPELLPOWER))
                    effect.RemoveFrom(Target);
            bool removedPayload = !(Target.Zone is GraveyardZone) && Actions.PayloadRules.Entries(this).Length != 0;
            if (removedPayload) Actions.PayloadRules.Clear(this);
            bool gift = this[GameTag.IS_NIGHTMARE_BONUS] == 1;
            if (gift) this[GameTag.ATTACHED] = 0;
            SabberStoneCore.Actions.PrepareRules.Removing(this);
            Actions.PlayLockRules.Removing(this);
            SabberStoneCore.Actions.CostRules.RemoveEnchantment(Game, Id);
            if (Card.Id == "EDR_469e" || Actions.DormantRules.IsHelper(this) || Card.Id == "GBL_999e" || Card.Id == "TLC_450e") this[GameTag.ATTACHED] = 0;
			if (Game.History)
			{
				if (Zone == null)
					Zone = Controller.BoardZone;
				Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(this));
				this[GameTag.ZONE] = (int)Enums.Zone.REMOVEDFROMGAME;
			}

			// Activate enchantment deathrattle task. A minion that reaches the graveyard without dying (discarded, burned or
			// milled from hand/deck) does not trigger its granted Deathrattle; deaths keep ToBeDestroyed until the graveyard.
			if (Power.DeathrattleTask != null && Target.Zone is GraveyardZone && !(Target is Minion { ToBeDestroyed: false }))
			{
                // Granted Deathrattles use the enchantment's timestamp, not the recipient's.
                Game.TaskQueue.Enqueue(Power.DeathrattleTask, Target.Controller, Target, this,
                    deathId: Target.Id, deathOrder: OrderOfPlay, triggerKeyword: (int)GameTag.DEATHRATTLE);
			}

			OngoingEffect?.Remove();
			ActivatedTrigger?.Remove();

			Target.AppliedEnchantments.Remove(this);
            if (Card.Id == "JAIL_509e" && Target is Controller protectedPlayer
                && !protectedPlayer.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_509e"))
                protectedPlayer.Hero[(GameTag)4717] = 0;
            Actions.TemporaryRules.RemovingControl(this);
            if (Power.Enchant?.Effects.Any(effect => effect.Tag == GameTag.CHOOSE_BOTH) == true
                && !Target.AppliedEnchantments.Any(e => e.Power.Enchant?.Effects.Any(effect => effect.Tag == GameTag.CHOOSE_BOTH) == true))
                Target[GameTag.CHOOSE_BOTH] = 0;
            if (removedPayload && Target is IPlayable payloadTarget) payloadTarget.Card.UpdateHistoryDisplays(payloadTarget);
            if (gift)
            {
                this[GameTag.ZONE] = (int)Enums.Zone.REMOVEDFROMGAME;
                DarkGifts.SetTag((IPlayable)Target, GameTag.HAS_DARK_GIFT, DarkGifts.Has((IPlayable)Target) ? 1 : 0);
                if (!DarkGifts.Has((IPlayable)Target)) DarkGifts.ClearKeywordBaselines((IPlayable)Target);
            }

			if (IsOneTurnActive)
				Game.OneTurnEffectEnchantments.Remove(this);

			Game.Log(LogLevel.VERBOSE, BlockType.ACTION, "Enchantment",
				!Game.Logging ? "" : $"Enchantment {this} is removed from {Target}.");
		}

		public Power Power => Card.Power;
		public Trigger ActivatedTrigger { get; set; }

		public void Reset()
		{
			_tags.Clear();
		}

		public override string ToString()
		{
			return $"'{Card.Name}[{Id}]'";
		}
	}

	public partial class Enchantment
	{
		protected readonly bool _history;
		protected readonly bool _logging;

		public int Id { get; }
		public int OrderOfPlay { get; set; }
        internal long CostPaymentAppliedAt;
		public Game Game { get; set; }
		public Card Card { get; set; }
		public IZone Zone { get; set; }
		public IAura OngoingEffect { get; set; }
		public IEnumerable<ICharacter> ValidPlayTargets => null;
		public bool ChooseOne { get; set; }
		public bool IsPlayable => false;
		public bool IsPlayableByPlayer => false;
		public bool IsPlayableByCardReq => false;
		public bool IsIgnoreDamage { get; set; }
		public bool Combo => false;
		public int Cost { get; set; }
		public bool ToBeDestroyed { get; set; }
		public int CardTarget { get; set; }
		public int ZonePosition { get; set; }
		public bool IsExhausted { get; set; }
		public int Overload { get; set; }
		public bool HasDeathrattle { get; set; }
		public bool HasLifeSteal { get; set; }
		public bool IsEcho => false;
		public bool HasOverkill => false;
		public IPlayable[] ChooseOnePlayables { get; set; }
		public AuraEffects AuraEffects { get; set; }
		public IDictionary<GameTag, int> NativeTags => _tags;
		public List<Enchantment> AppliedEnchantments { get; set; }
		public bool HasAnyValidPlayTargets { get; }

		public string Hash(params GameTag[] ignore)
		{
			throw new NotImplementedException();
		}

		public bool IsValidPlayTarget(ICharacter target)
		{
			throw new NotImplementedException();
		}

		public void Destroy()
		{
			throw new NotImplementedException();
		}

		public void ActivateTask(in PowerActivation activation, in IPlayable target = null, in int chooseOne = 0, in IPlayable source = null, bool fromDeath = true)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<KeyValuePair<GameTag, int>> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		IPlayable IPlayable.Clone(in Controller controller) => Clone(in controller);
	}
}
