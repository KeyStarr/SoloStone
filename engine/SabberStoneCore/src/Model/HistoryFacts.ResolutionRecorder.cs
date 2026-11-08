using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    internal sealed partial class HistoryRecorder
    {
        private int _healthMutation,_paymentEntity,_optionSpell;
        private long _paymentPlay;
        private bool _playerAttack,_fatigue;
        private AttackScope _attack;
        private IEntity _executingSource;
        internal IDisposable Executing(IEntity source)
        { var previous=_executingSource;_executingSource=source;return new Cleanup(()=>_executingSource=previous); }
        internal IDisposable SuppressHealth()
        { _healthMutation++;return new Cleanup(()=>_healthMutation--); }
        internal void HealthSet(Character entity,int oldHealth)
        { if(_healthMutation==0) HealthChanged(entity,oldHealth,HistoryHealthChangeReason.Set); }
        internal void HealthChanged(ICharacter entity,int oldHealth,HistoryHealthChangeReason reason)
        {
            if(entity is Hero && ReferenceEquals(entity.Controller.Hero,entity) && oldHealth!=entity.Health)
                Store.Append(new HeroHealthChangedFact(Seq,Turn,entity.Controller.Id,Active,entity.Id,oldHealth,entity.Health,reason));
            if (entity is Hero hero) hero.ResolveFullHealthGrants();
        }
        internal IDisposable Fatigue()
        { bool previous=_fatigue;_fatigue=true;return new Cleanup(()=>_fatigue=previous); }
        internal long DamageResolved(IPlayable source,ICharacter victim,int preHealth,int armorDamage,int healthDamage,bool combat)
        {
            armorDamage=Math.Max(0,armorDamage);healthDamage=Math.Max(0,healthDamage);
            if(armorDamage+healthDamage==0) return 0;
            long castId=0;
            if(source is Spell)
                for(var cast=_cast;cast!=null;cast=cast.Previous)
                    if(cast.Entity.Id==source.Id) {castId=cast.Id;break;}
            bool fatigue=_fatigue;if(fatigue)_fatigue=false;
            var cause=fatigue?HistoryDamageCause.Fatigue:combat?HistoryDamageCause.Combat:
                source.Card.Type==CardType.HERO_POWER?HistoryDamageCause.HeroPower:HistoryDamageCause.SpellOrEffect;
            long attackId=combat?(_attack?.CombatAttackId??0):0;
            if(combat && attackId==0) Store.InvalidateEvidence("Combat damage has no active history attack.");
            long damageSequence=Seq;
            Store.Append(new DamageResolvedHistoryFact(damageSequence,Turn,source.Controller.Id,Active,source.Id,source.Card.Type,castId,attackId,
                _game.Resolution.Current.SequenceId,cause,
                victim.Id,victim.Controller.Id,victim.Card.Type,armorDamage,healthDamage,preHealth));
            HealthChanged(victim,preHealth,HistoryHealthChangeReason.Damage);
            return damageSequence;
        }
        internal void ArmorGained(ICharacter entity,int amount)
        { if(amount>0) Store.Append(new ArmorGainedFact(Seq,Turn,entity.Controller.Id,Active,entity.Id,amount)); }
        internal IDisposable PlayPayment(IPlayable source)
        {
            int previousEntity=_paymentEntity;long previousPlay=_paymentPlay;_paymentEntity=source.Id;_paymentPlay=0;
            return new Cleanup(()=>{_paymentEntity=previousEntity;_paymentPlay=previousPlay;});
        }
        internal void Paid(Controller payer,IPlayable source,int mana,int health=0,HistoryPaymentKind? action=null)
        {
            var kind=action??(source is HeroPower?HistoryPaymentKind.HeroPower:_paymentEntity==source.Id?HistoryPaymentKind.Play:HistoryPaymentKind.Other);
            long play=kind==HistoryPaymentKind.Play?Seq:0;
            if(play!=0) _paymentPlay=play;
            Store.Append(new ManaSpentFact(Seq,Turn,payer.Id,Active,source.Id,mana,health,kind,play,mana>0 && payer.RemainingMana==0));
            if(kind is HistoryPaymentKind.Play or HistoryPaymentKind.HeroPower or HistoryPaymentKind.Trade or HistoryPaymentKind.Prepare)
                AcceptPlayerAction(source);
            if(mana>0 && payer.RemainingMana==0 && _game.TriggerManager.Events.HasSubscribers<Events.LastManaSpentEvent>())
            {
                _game.TaskQueue.StartEvent();
                try { _game.TriggerManager.Events.Raise(new Events.LastManaSpentEvent(payer)); _game.ProcessTasks(); }
                finally { _game.TaskQueue.EndEvent(); }
            }
        }
        internal void Residence(IPlayable entity,Zone zone,bool entered)
        {
            if(zone==Zone.HAND || zone==Zone.DECK)
                Store.Append(new ResidenceChangedFact(Seq,Turn,entity.Controller.Id,Active,Store.ResidenceEntity(entity,zone,entered),zone,entered));
        }
        internal void ResidenceReplaced(IPlayable oldEntity,IPlayable newEntity,Zone zone)
        { if(oldEntity.Id!=newEntity.Id) { Residence(oldEntity,zone,false);Residence(newEntity,zone,true); } }
        internal IDisposable PlayerAttack()
        { bool previous=_playerAttack;_playerAttack=true;return new Cleanup(()=>_playerAttack=previous); }
        internal AttackScope Attack(ICharacter source,ICharacter target)
        {
            var scope=new AttackScope(this,_attack,source,target,_playerAttack?HistoryAttackOrigin.Player:HistoryAttackOrigin.Forced);
            _playerAttack=false;_attack=scope;return scope;
        }
        internal void CombatTarget(ICharacter target)=>_attack?.BeginCombat(target);
        internal sealed class AttackScope:IDisposable
        {
            private readonly HistoryRecorder _owner;
            private readonly AttackScope _previous;
            private readonly ICharacter _source;
            private readonly HistoryAttackOrigin _origin;
            private readonly AttackStartedFact _start;
            private CombatStartedFact _combat;
            private ICharacter _target;
            private bool _finished;
            internal bool IsForced => _origin == HistoryAttackOrigin.Forced;
            internal long AttackId=>_start.AttackId;
            internal long CombatAttackId
            { get { if(_combat!=null) return _start.AttackId;_owner.Store.InvalidateEvidence("Combat damage preceded combat start.");return 0; } }
            internal AttackScope(HistoryRecorder owner,AttackScope previous,ICharacter source,ICharacter target,HistoryAttackOrigin origin)
            {
                _owner=owner;_previous=previous;_source=source;_target=target;_origin=origin;
                var board=source is Minion?owner.Store.Board(source.Id):null;
                _start=new AttackStartedFact(owner.Seq,owner.Turn,source.Controller.Id,owner.Active,source.Id,source.Card.Type,target.Id,target.Controller.Id,target.Card.Type,origin,
                    source.AttackDamage,board?.Incarnation??0,owner.Store.Definition(source.Card),target.Health,(target as Hero)?.Armor??0,owner._game.Resolution.Current.SequenceId);
                owner.Store.Append(_start);
            }
            internal void SetTarget(ICharacter target) { _target=target; }
            internal void BeginCombat(ICharacter target)
            {
                if(_combat!=null) { _owner.Store.InvalidateEvidence("History combat begun twice.");return; }
                _target=target;var board=_source is Minion?_owner.Store.Board(_source.Id):null;
                _combat=new CombatStartedFact(_owner.Seq,_owner.Turn,_source.Controller.Id,_owner.Active,_start.AttackId,_source.Id,_source.Card.Type,target.Id,target.Controller.Id,target.Card.Type,
                    _source.AttackDamage,board?.Incarnation??0,_owner.Store.Definition(_source.Card),target.Health,(target as Hero)?.Armor??0,_owner._game.Resolution.Current.SequenceId);
                _owner.Store.Append(_combat);
            }
            internal void Finish(bool reached)
            {
                if(_finished) throw new InvalidOperationException("History attack finished twice.");
                _owner.Store.Append(new AttackFinishedFact(_owner.Seq,_owner.Turn,_start.ControllerId,_owner.Active,_start.AttackId,
                    _start.AttackerId,_start.AttackerType,_target.Id,_target.Controller.Id,_target.Card.Type,_start.Origin,reached));
                _finished=true;
            }
            public void Dispose()=>_owner._attack=_previous;
        }
        internal IDisposable SpellOption(Spell spell)
        { int previous=_optionSpell;_optionSpell=spell.Id;return new Cleanup(()=>_optionSpell=previous); }
        private QueuedSpellActivation _activation;
        private int _activationProducer;
        internal IDisposable RepeatActivation(IPlayable spell)
        { int previous=_activationProducer;_activationProducer=spell.Id;return new Cleanup(()=>_activationProducer=previous); }
        internal IDisposable Activation(Playable entity,PowerActivation activation,IPlayable source,out QueuedSpellActivation queued)
        {
            queued=null;
            if(entity is Spell spell && (activation==PowerActivation.POWER || activation==PowerActivation.COMBO))
            {
                if(source!=null && source.ChooseOne && source.ChooseOnePlayables!=null && Array.Exists(source.ChooseOnePlayables,p=>p.Id==spell.Id))
                    queued=_activation;
                else if(_activationProducer==spell.Id || _executingSource?.Card.Id=="UNG_843")
                    queued=new QueuedSpellActivation(spell,HistoryCastKind.Repeat);
                else if(_optionSpell!=spell.Id && !(_cast!=null && _cast.Entity.Id==spell.Id && !_cast.Finished))
                    throw new InvalidOperationException("Missing history spell activation producer annotation.");
            }
            var previous=_activation;_activation=queued;return new Cleanup(()=>_activation=previous);
        }
        internal QueuedCastScope QueuedCast(QueuedSpellActivation queued)=>queued==null?null:new QueuedCastScope(this,queued);
        internal sealed class QueuedCastScope:IDisposable
        {
            private readonly HistoryRecorder _owner;
            private readonly QueuedSpellActivation _queued;
            private readonly CastScope _previous;
            internal QueuedCastScope(HistoryRecorder owner,QueuedSpellActivation queued)
            {
                _owner=owner;_queued=queued;_previous=owner._cast;
                if(queued.Invocation==null) queued.Invocation=owner.Cast(queued.Entity,queued.Kind);
                else owner._cast=queued.Invocation;
            }
            internal void Complete() { if(--_queued.Bodies==0) _queued.Invocation.Complete(); }
            public void Dispose()=>_owner._cast=_previous;
        }
    }
    // Invocation metadata shares one cast across queued Choose Both bodies; never adds queue entries.
    public sealed class QueuedSpellActivation
    {
        internal readonly Spell Entity;
        internal readonly HistoryCastKind Kind;
        internal int Bodies;
        [NonSerialized] internal HistoryRecorder.CastScope Invocation;
        internal QueuedSpellActivation(Spell entity,HistoryCastKind kind) { Entity=entity;Kind=kind; }
    }
}
