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
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Model
{
    public class TriggerManager
    {
	    public delegate void TriggerHandler(IEntity sender);

        public GameEventBus Events { get; } = new GameEventBus();

        // Source-compatible views: Trigger and aura callbacks share the same ordinal registry.
        public event TriggerHandler StatGainedTrigger
        {
            add => Events.Subscribe<StatGainedEvent>(value);
            remove => Events.Unsubscribe<StatGainedEvent>(value);
        }
        internal void OnStatGained(StatGainedEvent value)
        {
            Events.Raise(value);
            Actions.StatGainRules.AfterGain(value);
        }

        public event TriggerHandler DealDamageTrigger
        {
            add => Events.Subscribe<DealDamageEvent>(value);
            remove => Events.Unsubscribe<DealDamageEvent>(value);
        }
        public event TriggerHandler DamageTrigger
        {
            add => Events.Subscribe<DamageEvent>(value);
            remove => Events.Unsubscribe<DamageEvent>(value);
        }
        public event TriggerHandler HealIntentTrigger
        {
            add => Events.Subscribe<HealIntentEvent>(value);
            remove => Events.Unsubscribe<HealIntentEvent>(value);
        }
        public event TriggerHandler HealTrigger
        {
            add => Events.Subscribe<HealEvent>(value);
            remove => Events.Unsubscribe<HealEvent>(value);
        }
        public event TriggerHandler LoseDivineShield
        {
            add => Events.Subscribe<LoseDivineShieldEvent>(value);
            remove => Events.Unsubscribe<LoseDivineShieldEvent>(value);
        }
        public event TriggerHandler EndTurnTrigger
        {
            add => Events.Subscribe<EndTurnEvent>(value);
            remove => Events.Unsubscribe<EndTurnEvent>(value);
        }
        public event TriggerHandler TurnStartTrigger
        {
            add => Events.Subscribe<TurnStartEvent>(value);
            remove => Events.Unsubscribe<TurnStartEvent>(value);
        }
        public event TriggerHandler SummonTrigger
        {
            add => Events.Subscribe<SummonEvent>(value);
            remove => Events.Unsubscribe<SummonEvent>(value);
        }
        public event TriggerHandler AfterSummonTrigger
        {
            add => Events.Subscribe<AfterSummonEvent>(value);
            remove => Events.Unsubscribe<AfterSummonEvent>(value);
        }
        public event TriggerHandler AttackTrigger
        {
            add => Events.Subscribe<AttackEvent>(value);
            remove => Events.Unsubscribe<AttackEvent>(value);
        }
        public event TriggerHandler AfterAttackTrigger
        {
            add => Events.Subscribe<AfterAttackEvent>(value);
            remove => Events.Unsubscribe<AfterAttackEvent>(value);
        }
        public event TriggerHandler DeathTrigger
        {
            add => Events.Subscribe<DeathEvent>(value);
            remove => Events.Unsubscribe<DeathEvent>(value);
        }
        public event TriggerHandler PlayCardTrigger
        {
            add => Events.Subscribe<PlayCardEvent>(value);
            remove => Events.Unsubscribe<PlayCardEvent>(value);
        }
        public event TriggerHandler AfterPlayCardTrigger
        {
            add => Events.Subscribe<AfterPlayCardEvent>(value);
            remove => Events.Unsubscribe<AfterPlayCardEvent>(value);
        }
        public event TriggerHandler PlayMinionTrigger
        {
            add => Events.Subscribe<PlayMinionEvent>(value);
            remove => Events.Unsubscribe<PlayMinionEvent>(value);
        }
        public event TriggerHandler AfterPlayMinionTrigger
        {
            add => Events.Subscribe<AfterPlayMinionEvent>(value);
            remove => Events.Unsubscribe<AfterPlayMinionEvent>(value);
        }
        public event TriggerHandler CastSpellTrigger
        {
            add => Events.Subscribe<CastSpellEvent>(value);
            remove => Events.Unsubscribe<CastSpellEvent>(value);
        }
        public event TriggerHandler AfterCastTrigger
        {
            add => Events.Subscribe<AfterCastEvent>(value);
            remove => Events.Unsubscribe<AfterCastEvent>(value);
        }
        public event TriggerHandler SecretRevealedTrigger
        {
            add => Events.Subscribe<SecretRevealedEvent>(value);
            remove => Events.Unsubscribe<SecretRevealedEvent>(value);
        }
        public event TriggerHandler ZoneTrigger
        {
            add => Events.Subscribe<ZoneEvent>(value);
            remove => Events.Unsubscribe<ZoneEvent>(value);
        }
        public event TriggerHandler DiscardTrigger
        {
            add => Events.Subscribe<DiscardEvent>(value);
            remove => Events.Unsubscribe<DiscardEvent>(value);
        }
        public event TriggerHandler GameStartTrigger
        {
            add => Events.Subscribe<GameStartEvent>(value);
            remove => Events.Unsubscribe<GameStartEvent>(value);
        }
        public event TriggerHandler DrawTrigger
        {
            add => Events.Subscribe<DrawEvent>(value);
            remove => Events.Unsubscribe<DrawEvent>(value);
        }
        public event TriggerHandler TargetTrigger
        {
            add => Events.Subscribe<TargetEvent>(value);
            remove => Events.Unsubscribe<TargetEvent>(value);
        }
        public event TriggerHandler InspireTrigger
        {
            add => Events.Subscribe<InspireEvent>(value);
            remove => Events.Unsubscribe<InspireEvent>(value);
        }
        public event TriggerHandler FreezeTrigger
        {
            add => Events.Subscribe<FreezeEvent>(value);
            remove => Events.Unsubscribe<FreezeEvent>(value);
        }
        public event TriggerHandler ArmorTrigger
        {
            add => Events.Subscribe<ArmorEvent>(value);
            remove => Events.Unsubscribe<ArmorEvent>(value);
        }
        public event TriggerHandler EquipWeaponTrigger
        {
            add => Events.Subscribe<EquipWeaponEvent>(value);
            remove => Events.Unsubscribe<EquipWeaponEvent>(value);
        }
        public event TriggerHandler ShuffleIntoDeckTrigger
        {
            add => Events.Subscribe<ShuffleIntoDeckEvent>(value);
            remove => Events.Unsubscribe<ShuffleIntoDeckEvent>(value);
        }
        public event TriggerHandler OverloadTrigger
        {
            add => Events.Subscribe<OverloadEvent>(value);
            remove => Events.Unsubscribe<OverloadEvent>(value);
        }

        internal event TriggerHandler ShatterTrigger
        {
            add => Events.Subscribe<ShatterEvent>(value);
            remove => Events.Unsubscribe<ShatterEvent>(value);
        }

        public event TriggerHandler CardPreparedTrigger
        {
            add => Events.Subscribe<CardPreparedEvent>(value);
            remove => Events.Unsubscribe<CardPreparedEvent>(value);
        }

        internal void OnCardPreparedTrigger(IPlayable source, int spent, int discount, int reduction)
        {
            var previous = source.Game.CurrentEventData;
            source.Game.CurrentEventData = new EventMetaData(source, null, discount);
            try
            {
                Events.Raise(new CardPreparedEvent(source, spent, discount, reduction));
                source.Game.ProcessTasks();
            }
            finally { source.Game.CurrentEventData = previous; }
        }

        // Rebind callback owners after entity/aura reconstruction, preserving registration identities.
        internal static TriggerHandler CloneHandlers(TriggerHandler source, Dictionary<object, object> copies, Game game)
        {
            TriggerHandler result = null;
            if (source == null) return null;
            // Ownership follows code as well as target type, including compiler-generated closures.
            // External observers are intentionally not inherited; missing gameplay mappings are errors.
            int owned = 0, rebound = 0;
            foreach (TriggerHandler handler in source.GetInvocationList())
            {
                if (handler.Method.Module.Assembly != typeof(TriggerManager).Assembly &&
                    handler.Target?.GetType().Assembly != typeof(TriggerManager).Assembly)
                    continue;
                owned++;
                if (handler.Target == null && handler.Method.IsStatic)
                    result += handler;
                else
                {
                    if (handler.Target == null)
                        throw new InvalidOperationException($"Cannot clone unbound engine handler {handler.Method}.");
                    if (!copies.TryGetValue(handler.Target, out object target))
                    {
                        // Some callbacks outlive their owner's active trigger/aura registration.
                        if (handler.Target is Trigger trigger)
                            target = trigger.CloneDetached(game);
                        else if (handler.Target is AdaptiveCostEffect effect)
                            target = effect.CloneDetached(game);
                        else
                            throw new InvalidOperationException($"Cannot clone engine handler {handler.Method.DeclaringType}.{handler.Method.Name}: missing target mapping.");
                        copies.Add(handler.Target, target);
                    }
                    result += (TriggerHandler)Delegate.CreateDelegate(typeof(TriggerHandler), target, handler.Method);
                }
                rebound++;
            }
            System.Diagnostics.Debug.Assert(owned == rebound, "Every engine-owned handler in this channel must be rebound.");
            return result;
        }

        internal void RestoreCloneSubscriptions(TriggerManager source, Dictionary<object, object> copies, Game game)
        {
            Events.RestoreCloneSubscriptions(source.Events, copies, game);
        }

		public bool HasTargetTrigger => Events.HasSubscribers<TargetEvent>();
		public bool HasOnSummonTrigger => Events.HasSubscribers<SummonEvent>();
		public bool HasShuffleIntoDeckTrigger => Events.HasSubscribers<ShuffleIntoDeckEvent>();

	    internal void OnDealDamageTrigger(IEntity sender)
	    {
		    Events.Raise(new DealDamageEvent(sender));
	    }

		internal void OnDamageTrigger(IEntity sender)
	    {
		    Events.Raise(new DamageEvent(sender));
	    }

	    internal void OnHealTrigger(IEntity sender)
	    {
			Events.Raise(new HealEvent(sender));
	    }

	    internal void OnLoseDivineShield(IEntity sender)
	    {
		    Events.Raise(new LoseDivineShieldEvent(sender));
	    }

	    internal void OnEndTurnTrigger(IEntity sender)
	    {
			Events.Raise(new EndTurnEvent(sender));
	    }

	    internal void OnTurnStartTrigger(IEntity sender)
	    {
			Events.Raise(new TurnStartEvent(sender));
	    }

	    internal void OnSummonTrigger(IEntity sender)
	    {
		    Events.Raise(new SummonEvent(sender));
	    }

	    internal void OnAfterSummonTrigger(IEntity sender)
	    {
			Events.Raise(new AfterSummonEvent(sender));
	    }

	    internal void OnAttackTrigger(IEntity sender)
	    {
		    Events.Raise(new AttackEvent(sender));
	    }

        internal void OnAfterAttackTrigger(IEntity sender) => Events.Raise(new AfterAttackEvent(sender));

	    internal void OnDeathTrigger(IEntity sender)
	    {
			Events.Raise(new DeathEvent(sender));
	    }

	    internal void OnPlayCardTrigger(IEntity sender)
	    {
			Events.Raise(new PlayCardEvent(sender));
	    }

	    internal void OnAfterPlayCardTrigger(IEntity sender)
	    {
		    Events.Raise(new AfterPlayCardEvent(sender));
	    }

	    internal void OnPlayMinionTrigger(IEntity sender)
	    {
		    Events.Raise(new PlayMinionEvent(sender));
	    }

	    internal void OnAfterPlayMinionTrigger(IEntity sender)
	    {
		    Events.Raise(new AfterPlayMinionEvent(sender));
	    }

	    internal void OnCastSpellTrigger(IEntity sender)
	    {
		    Events.Raise(new CastSpellEvent(sender));
	    }

	    internal void OnAfterCastTrigger(IEntity sender)
	    {
		    Events.Raise(new AfterCastEvent(sender));
	    }

	    internal void OnSecretRevealedTrigger(IEntity sender)
	    {
			Events.Raise(new SecretRevealedEvent(sender));
	    }

	    internal void OnZoneTrigger(IEntity sender)
	    {
		    if (!Events.HasSubscribers<ZoneEvent>()) return;
		    sender.Game.TaskQueue.StartEvent();
		    Events.Raise(new ZoneEvent(sender));
		    sender.Game.ProcessTasks();
		    sender.Game.TaskQueue.EndEvent();
		}

	    internal void OnDiscardTrigger(IEntity sender)
	    {
		    Events.Raise(new DiscardEvent(sender));
	    }

	    internal void OnGameStartTrigger()
	    {
		    Events.Raise(new GameStartEvent(null));
	    }

	    internal void OnDrawTrigger(IEntity sender)
	    {
		    Events.Raise(new DrawEvent(sender));
	    }

	    internal void OnTargetTrigger(IEntity sender)
	    {
		    Events.Raise(new TargetEvent(sender));
	    }

	    internal void OnInspireTrigger(IEntity sender)
	    {
		    Events.Raise(new InspireEvent(sender));
	    }

	    internal void OnFreezeTrigger(IEntity sender)
	    {
		    Events.Raise(new FreezeEvent(sender));
	    }

	    internal void OnArmorTrigger(IEntity sender)
	    {
		    Events.Raise(new ArmorEvent(sender));
	    }

	    internal void OnEquipWeaponTrigger(IEntity sender)
	    {
		    Events.Raise(new EquipWeaponEvent(sender));
	    }

	    internal void OnShuffleIntoDeckTrigger(IEntity sender)
	    {
		    Events.Raise(new ShuffleIntoDeckEvent(sender));
	    }

	    internal void OnOverloadTrigger(IPlayable sender, int amount)
	    {
			if (!Events.HasSubscribers<OverloadEvent>())
				return;

			EventMetaData temp = sender.Game.CurrentEventData;
			sender.Game.CurrentEventData = new EventMetaData(sender, null, amount);
			Events.Raise(new OverloadEvent(sender, amount));
			sender.Game.CurrentEventData = temp;
	    }

	    public void AddTrigger(TriggerType type, TriggerHandler method)
	    {
			switch (type)
			{
				case TriggerType.TURN_END:
					EndTurnTrigger += method;
					return;
				case TriggerType.TURN_START:
					TurnStartTrigger += method;
					return;
				case TriggerType.DEATH:
					DeathTrigger += method;
					return;
				case TriggerType.INSPIRE:
					InspireTrigger += method;
					return;
				case TriggerType.DEAL_DAMAGE:
					DealDamageTrigger += method;
					return;
				case TriggerType.TAKE_DAMAGE:
					DamageTrigger += method;
					return;
				case TriggerType.HEAL_INTENT:
                    HealIntentTrigger += method; return;
				case TriggerType.HEAL:
					HealTrigger += method;
					return;
				case TriggerType.LOSE_DIVINE_SHIELD:
					LoseDivineShield += method;
					return;
				case TriggerType.AFTER_ATTACK:
                    AfterAttackTrigger += method;
                    return;
                case TriggerType.ATTACK:
					AttackTrigger += method;
					return;
				case TriggerType.SUMMON:
					SummonTrigger += method;
					return;
				case TriggerType.AFTER_SUMMON:
					AfterSummonTrigger += method;
					return;
				case TriggerType.PLAY_CARD:
					PlayCardTrigger += method;
					return;
				case TriggerType.AFTER_PLAY_CARD:
					AfterPlayMinionTrigger += method;
					return;
				case TriggerType.PLAY_MINION:
					PlayMinionTrigger += method;
					return;
				case TriggerType.AFTER_PLAY_MINION:
					AfterPlayMinionTrigger += method;
					return;
				case TriggerType.CAST_SPELL:
					CastSpellTrigger += method;
					return;
				case TriggerType.AFTER_CAST:
					AfterCastTrigger += method;
					return;
				case TriggerType.SECRET_REVEALED:
					SecretRevealedTrigger += method;
					return;
				case TriggerType.ZONE:
					ZoneTrigger += method;
					return;
				case TriggerType.DISCARD:
					DiscardTrigger += method;
					return;
				case TriggerType.GAME_START:
					GameStartTrigger += method;
					return;
				case TriggerType.DRAW:
					DrawTrigger += method;
					return;
				case TriggerType.TARGET:
					TargetTrigger += method;
					return;
				case TriggerType.FROZEN:
					FreezeTrigger += method;
					return;
				case TriggerType.ARMOR:
					ArmorTrigger += method;
					return;
                case TriggerType.SHATTER:
                    ShatterTrigger += method;
                    return;
				case TriggerType.CARD_PREPARED:
                    CardPreparedTrigger += method;
                    return;
                case TriggerType.EQUIP_WEAPON:
					EquipWeaponTrigger += method;
					return;
				default:
					throw new System.NotImplementedException();
			}
		}

	    public void RemoveTrigger(TriggerType type, TriggerHandler method)
	    {
		    switch (type)
		    {
			    case TriggerType.TURN_END:
				    EndTurnTrigger -= method;
				    return;
			    case TriggerType.TURN_START:
				    TurnStartTrigger -= method;
				    return;
			    case TriggerType.DEATH:
				    DeathTrigger -= method;
				    return;
			    case TriggerType.INSPIRE:
				    InspireTrigger -= method;
				    return;
			    case TriggerType.DEAL_DAMAGE:
				    DealDamageTrigger -= method;
				    return;
			    case TriggerType.TAKE_DAMAGE:
				    DamageTrigger -= method;
				    return;
			    case TriggerType.HEAL_INTENT:
                    HealIntentTrigger -= method; return;
				case TriggerType.HEAL:
				    HealTrigger -= method;
				    return;
				case TriggerType.LOSE_DIVINE_SHIELD:
				    LoseDivineShield -= method;
				    return;
			    case TriggerType.AFTER_ATTACK:
                    AfterAttackTrigger -= method;
                    return;
                case TriggerType.ATTACK:
				    AttackTrigger -= method;
				    return;
			    case TriggerType.SUMMON:
				    SummonTrigger -= method;
				    return;
			    case TriggerType.AFTER_SUMMON:
				    AfterSummonTrigger -= method;
				    return;
			    case TriggerType.PLAY_CARD:
				    PlayCardTrigger -= method;
				    return;
			    case TriggerType.AFTER_PLAY_CARD:
				    AfterPlayMinionTrigger -= method;
				    return;
			    case TriggerType.PLAY_MINION:
				    PlayMinionTrigger -= method;
				    return;
			    case TriggerType.AFTER_PLAY_MINION:
				    AfterPlayMinionTrigger -= method;
				    return;
			    case TriggerType.CAST_SPELL:
				    CastSpellTrigger -= method;
				    return;
			    case TriggerType.AFTER_CAST:
				    AfterCastTrigger -= method;
				    return;
			    case TriggerType.SECRET_REVEALED:
				    SecretRevealedTrigger -= method;
				    return;
			    case TriggerType.ZONE:
				    ZoneTrigger -= method;
				    return;
			    case TriggerType.DISCARD:
				    DiscardTrigger -= method;
				    return;
			    case TriggerType.GAME_START:
				    GameStartTrigger -= method;
				    return;
			    case TriggerType.DRAW:
				    DrawTrigger -= method;
				    return;
			    case TriggerType.TARGET:
				    TargetTrigger -= method;
				    return;
			    case TriggerType.FROZEN:
				    FreezeTrigger -= method;
				    return;
			    case TriggerType.ARMOR:
				    ArmorTrigger -= method;
				    return;
                case TriggerType.SHATTER:
                    ShatterTrigger -= method;
                    return;
			    case TriggerType.CARD_PREPARED:
                    CardPreparedTrigger -= method;
                    return;
                case TriggerType.EQUIP_WEAPON:
				    EquipWeaponTrigger -= method;
				    return;
			    default:
				    throw new System.NotImplementedException();
		    }
	    }
    }
}
