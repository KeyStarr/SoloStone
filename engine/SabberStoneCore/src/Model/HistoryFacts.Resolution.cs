using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    public enum HistoryHealthChangeReason { Damage, Heal, Payment, Set, Replacement }
    public enum HistoryPaymentKind { Play, HeroPower, Trade, Prepare, Other }
    public enum HistoryAttackOrigin { Player, Forced }

    // Separate from the existing transient quest DamageResolvedFact.
    public sealed class DamageResolvedHistoryFact : HistoryFact
    {
        public int SourceId { get; }
        public int SourceControllerId => ControllerId;
        public CardType SourceKind { get; }
        public long CastId { get; }
        public long AttackId { get; }
        public long ResolutionId { get; }
        public HistoryDamageCause Cause { get; }
        public int VictimId { get; }
        public int VictimControllerId { get; }
        public CardType VictimType { get; }
        public int Amount => ArmorDamage + HealthDamage;
        public int ArmorDamage { get; }
        public int HealthDamage { get; }
        public int PreHitHealth { get; }
        internal DamageResolvedHistoryFact(long s,int t,int c,int a,int source,CardType kind,long cast,long attack,long resolution,HistoryDamageCause cause,
            int victim,int victimController,CardType victimType,int armor,int health,int preHealth) : base(s,t,c,a)
        { SourceId=source;SourceKind=kind;CastId=cast;AttackId=attack;ResolutionId=resolution;Cause=cause;VictimId=victim;VictimControllerId=victimController;VictimType=victimType;
          ArmorDamage=armor;HealthDamage=health;PreHitHealth=preHealth; }
        internal override string Payload()=>FormattableString.Invariant($"{SourceId},{(int)SourceKind},{CastId},{AttackId},{ResolutionId},{(int)Cause},{VictimId},{VictimControllerId},{(int)VictimType},{ArmorDamage},{HealthDamage},{PreHitHealth}");
    }
    public sealed class HeroHealthChangedFact : HistoryFact
    {
        public int HeroId { get; }
        public int OldHealth { get; }
        public int NewHealth { get; }
        public HistoryHealthChangeReason Reason { get; }
        internal HeroHealthChangedFact(long s,int t,int c,int a,int hero,int oldHealth,int newHealth,HistoryHealthChangeReason reason) : base(s,t,c,a)
        { HeroId=hero;OldHealth=oldHealth;NewHealth=newHealth;Reason=reason; }
        internal override string Payload()=>FormattableString.Invariant($"{HeroId},{OldHealth},{NewHealth},{(int)Reason}");
    }
    public sealed class ArmorGainedFact : HistoryFact
    {
        public int EntityId { get; }
        public int Amount { get; }
        internal ArmorGainedFact(long s,int t,int c,int a,int entity,int amount) : base(s,t,c,a) { EntityId=entity;Amount=amount; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{Amount}");
    }
    public abstract class AttackHistoryFact : HistoryFact
    {
        public long AttackId { get; }
        public int AttackerId { get; }
        public CardType AttackerType { get; }
        public int TargetId { get; }
        public int TargetControllerId { get; }
        public CardType TargetType { get; }
        public HistoryAttackOrigin Origin { get; }
        internal AttackHistoryFact(long s,int t,int c,int a,long id,int attacker,CardType type,int target,int targetController,CardType targetType,HistoryAttackOrigin origin) : base(s,t,c,a)
        { AttackId=id;AttackerId=attacker;AttackerType=type;TargetId=target;TargetControllerId=targetController;TargetType=targetType;Origin=origin; }
        internal string AttackText()=>FormattableString.Invariant($"{AttackId},{AttackerId},{(int)AttackerType},{TargetId},{TargetControllerId},{(int)TargetType},{(int)Origin}");
    }
    public sealed class AttackStartedFact : AttackHistoryFact
    {
        public int AttackDamage { get; } public long AttackerIncarnation { get; } public HistoryDefinition AttackerDefinition { get; }
        public int TargetPreHitHealth { get; } public int TargetPreHitArmor { get; } public long ResolutionId { get; }
        internal AttackStartedFact(long s,int t,int c,int a,int attacker,CardType type,int target,int targetController,CardType targetType,HistoryAttackOrigin origin,
            int attackDamage,long incarnation,HistoryDefinition definition,int targetHealth,int targetArmor,long resolution)
            : base(s,t,c,a,s,attacker,type,target,targetController,targetType,origin)
        { AttackDamage=attackDamage;AttackerIncarnation=incarnation;AttackerDefinition=definition;TargetPreHitHealth=targetHealth;TargetPreHitArmor=targetArmor;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{AttackText()},{AttackDamage},{AttackerIncarnation},{AttackerDefinition.Data},{TargetPreHitHealth},{TargetPreHitArmor},{ResolutionId}");
    }
    public sealed class AttackFinishedFact : AttackHistoryFact
    {
        public bool ReachedCombat { get; }
        public bool Cancelled => !ReachedCombat;
        internal AttackFinishedFact(long s,int t,int c,int a,long id,int attacker,CardType type,int target,int targetController,CardType targetType,HistoryAttackOrigin origin,bool reached)
            : base(s,t,c,a,id,attacker,type,target,targetController,targetType,origin) { ReachedCombat=reached; }
        internal override string Payload()=>AttackText()+","+(ReachedCombat?"1":"0");
    }
    public sealed class ManaSpentFact : HistoryFact
    {
        public int EntityId { get; }
        public int Mana { get; }
        public int Health { get; }
        public HistoryPaymentKind ActionKind { get; }
        public long RelatedPlayId { get; }
        public bool SpentLastMana { get; }
        internal ManaSpentFact(long s,int t,int c,int a,int entity,int mana,int health,HistoryPaymentKind kind,long play,bool last=false) : base(s,t,c,a)
        { EntityId=entity;Mana=mana;Health=health;ActionKind=kind;RelatedPlayId=play;SpentLastMana=last; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{Mana},{Health},{(int)ActionKind},{RelatedPlayId},{(SpentLastMana?1:0)}");
    }
    public sealed class ResidenceChangedFact : HistoryFact
    {
        public int EntityId { get; }
        public Zone Zone { get; }
        public bool Entered { get; }
        internal ResidenceChangedFact(long s,int t,int c,int a,int entity,Zone zone,bool entered) : base(s,t,c,a)
        { EntityId=entity;Zone=zone;Entered=entered; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{(int)Zone},{(Entered?1:0)}");
    }
    public readonly struct HistoryResidence
    {
        public int EntityId { get; }
        public int ControllerId { get; }
        public Zone Zone { get; }
        public long EntrySequence { get; }
        public long? ExitSequence { get; }
        internal HistoryResidence(ResidenceChangedFact entry,long? exit)
        { EntityId=entry.EntityId;ControllerId=entry.ControllerId;Zone=entry.Zone;EntrySequence=entry.Sequence;ExitSequence=exit; }
        public HistoryWindow Window => new HistoryWindow(EntrySequence,ExitSequence??long.MaxValue);
    }
    public sealed partial class HistoryQuery
    {
        public IEnumerable<DamageResolvedHistoryFact> Damage(HistoryWindow? window=null)=>Events<DamageResolvedHistoryFact>(window);
        public IEnumerable<HeroHealthChangedFact> HealthChanges(HistoryWindow? window=null)=>Events<HeroHealthChangedFact>(window);
        public IEnumerable<ArmorGainedFact> ArmorGains(HistoryWindow? window=null)=>Events<ArmorGainedFact>(window);
        public IEnumerable<AttackStartedFact> AttacksStarted(HistoryWindow? window=null)=>Events<AttackStartedFact>(window);
        public IEnumerable<AttackFinishedFact> AttacksFinished(HistoryWindow? window=null)=>Events<AttackFinishedFact>(window);
        public IEnumerable<ManaSpentFact> Payments(HistoryWindow? window=null)=>Events<ManaSpentFact>(window);
        public int HeroDamageEventCount(int victimControllerId,HistoryWindow window)=>Damage(window).Count(f=>f.VictimType==CardType.HERO && f.VictimControllerId==victimControllerId);
        public bool HeroTookDamage(int controllerId,HistoryWindow window)=>HeroDamageEventCount(controllerId,window)>0;
        public bool HeroHealthChanged(int controllerId,HistoryWindow window)=>HealthChanges(window).Any(f=>f.ControllerId==controllerId);
        public bool AnySpellDamage(int controllerId,HistoryWindow window)=>Damage(window).Any(f=>f.ControllerId==controllerId && f.SourceKind==CardType.SPELL);
        public int SpellDamageAmount(int controllerId,HistoryWindow window)=>Damage(window).Where(f=>f.ControllerId==controllerId && f.SourceKind==CardType.SPELL).Aggregate(0,(sum,f)=>Actions.DamageArithmetic.Add(sum,f.Amount));
        public int ArmorGainedAmount(int controllerId,HistoryWindow window)=>ArmorGains(window).Where(f=>f.ControllerId==controllerId).Sum(f=>f.Amount);
        public int FriendlyAttacksReachedCombat(int controllerId)=>AttacksFinished().Count(f=>f.ControllerId==controllerId && f.ReachedCombat);
        public IEnumerable<HistoryResidence> Residences(int entityId,Zone zone)
        {
            if(zone!=Zone.HAND && zone!=Zone.DECK) throw new ArgumentOutOfRangeException(nameof(zone));
            var progress=ResidenceProgress(ResolutionIndex(),entityId);
            return ReadResidences(zone==Zone.HAND?progress?.Hand:progress?.Deck);
        }
        private IEnumerable<HistoryResidence> ReadResidences(HistoryResidenceProgress node)
        {
            var result=new List<HistoryResidence>();
            for(;node!=null;node=node.Previous)
            { EntriesExamined++;result.Add(new HistoryResidence(node.Entry,node.Exit==0?(long?)null:node.Exit)); }
            result.Reverse();return result;
        }
        public HistoryWindow Residence(int entityId,Zone zone,long atSequence=long.MaxValue)
        {
            if(zone!=Zone.HAND && zone!=Zone.DECK) throw new ArgumentOutOfRangeException(nameof(zone));
            var progress=ResidenceProgress(ResolutionIndex(),entityId);
            var node=zone==Zone.HAND?progress?.Hand:progress?.Deck;
            if(atSequence!=long.MaxValue)
                while(node!=null && !(atSequence>=node.Entry.Sequence && (node.Exit==0 || atSequence<node.Exit)))
                { EntriesExamined++;node=node.Previous; }
            return node==null?new HistoryWindow(0,0):new HistoryWindow(node.Entry.Sequence,node.Exit==0?long.MaxValue:node.Exit);
        }
        public HistoryWindow HeldBeforeThisPlay(int entityId,long playId)
        {
            EntriesExamined++;
            if(!(_store.RecordAt(playId) is ManaSpentFact payment) || payment.ActionKind!=HistoryPaymentKind.Play)
                throw new ArgumentOutOfRangeException(nameof(playId));
            return Residence(entityId,Zone.HAND,payment.Sequence).ExcludingPlay(playId);
        }
        public int ManaSpentWhileHolding(int entityId,int controllerId,bool excludeOwnPlay=true)
        {
            var index=ResolutionIndex();var hand=ResidenceProgress(index,entityId)?.Hand;
            if(hand==null || hand.Entry.ControllerId!=controllerId) return 0;
            return (hand.Exit==0?index.Prefix(controllerId).Mana:hand.ExitMana)-hand.EntryMana-(excludeOwnPlay?hand.OwnPlayMana:0);
        }
        public int TwoManaPlaysWhileInHandOrDeck(int entityId,int controllerId,bool excludeOwnPlay=true)
        {
            var index=ResolutionIndex();var entity=ResidenceProgress(index,entityId);
            if(entity==null || index.Prefix(controllerId)==null) return 0;
            bool first=index.First.ControllerId==controllerId;
            return first?entity.Lotus1-(excludeOwnPlay?entity.OwnLotus1:0):entity.Lotus2-(excludeOwnPlay?entity.OwnLotus2:0);
        }
    }
}
