using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Model
{
    public enum HistoryDeathCause { None, Combat, SpellOrEffect, Poisonous, Destroy }
    public enum HistoryDamageCause { Combat, SpellOrEffect, HeroPower, Fatigue }
    public enum HistoryBurnReason { FullHand }
    public enum HistoryKeyword { Frozen }
    public enum HistoryBoardLeaveReason { Other }
    public enum HistoryPlayerActionKind { PlayCard, MinionAttack, HeroAttack, HeroPower, TradeCard, PrepareCard, ActivateLocation, ActivateOldGod }

    public sealed class CardDiscardedFact : HistoryCardFact
    {
        internal CardDiscardedFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o)
            : base(s,t,c,a,e,d,card,o) { }
        internal override string Payload() => CardText();
    }
    public sealed class MinionRebornFact : HistoryCardFact
    {
        public long SourceIncarnation { get; }
        public int ReturnedEntityId { get; }
        public long ReturnedIncarnation { get; }
        internal MinionRebornFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,
            long sourceIncarnation,int returnedEntity,long returnedIncarnation)
            : base(s,t,c,a,e,d,card,o)
        { SourceIncarnation=sourceIncarnation;ReturnedEntityId=returnedEntity;ReturnedIncarnation=returnedIncarnation; }
        internal override string Payload() => FormattableString.Invariant($"{CardText()},{SourceIncarnation},{ReturnedEntityId},{ReturnedIncarnation}");
    }
    public sealed class CardPlayCompletedFact : HistoryFact
    {
        public long PlayId { get; }
        internal CardPlayCompletedFact(long s,int t,int c,int a,long play) : base(s,t,c,a) { PlayId=play; }
        internal override string Payload() => PlayId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public sealed class ControlChangedFact : HistoryFact
    {
        public int EntityId { get; } public long FromIncarnation { get; } public long ToIncarnation { get; }
        public int FromControllerId { get; } public int ToControllerId { get; }
        public int SourceId { get; } public int SourceControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal ControlChangedFact(long s,int t,int c,int a,int entity,long fromInc,long toInc,int from,int to,int source,int sourceController,long cast,long resolution)
            : base(s,t,c,a) { EntityId=entity;FromIncarnation=fromInc;ToIncarnation=toInc;FromControllerId=from;ToControllerId=to;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{FromIncarnation},{ToIncarnation},{FromControllerId},{ToControllerId},{SourceId},{SourceControllerId},{CastId},{ResolutionId}");
    }
    public sealed class CardBurnedFact : HistoryFact
    {
        public int CardId { get; } public int SourceId { get; } public int SourceControllerId { get; }
        public long CastId { get; } public long ResolutionId { get; } public HistoryBurnReason Reason { get; }
        internal CardBurnedFact(long s,int t,int c,int a,int card,int source,int sourceController,long cast,long resolution,HistoryBurnReason reason)
            : base(s,t,c,a) { CardId=card;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution;Reason=reason; }
        internal override string Payload()=>FormattableString.Invariant($"{CardId},{SourceId},{SourceControllerId},{CastId},{ResolutionId},{(int)Reason}");
    }
    public sealed class KeywordAppliedFact : HistoryFact
    {
        public int TargetId { get; } public int SourceId { get; } public int SourceControllerId { get; }
        public long CastId { get; } public long ResolutionId { get; } public HistoryKeyword Keyword { get; } public bool ChangedFromFalse { get; }
        internal KeywordAppliedFact(long s,int t,int c,int a,int target,int source,int sourceController,long cast,long resolution,HistoryKeyword keyword,bool changed)
            : base(s,t,c,a) { TargetId=target;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution;Keyword=keyword;ChangedFromFalse=changed; }
        internal override string Payload()=>FormattableString.Invariant($"{TargetId},{SourceId},{SourceControllerId},{CastId},{ResolutionId},{(int)Keyword},{(ChangedFromFalse?1:0)}");
    }
    public sealed class CorpseSpentFact : HistoryFact
    {
        public int Amount { get; } public int SourceId { get; } public int SourceControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal CorpseSpentFact(long s,int t,int c,int a,int amount,int source,int sourceController,long cast,long resolution)
            : base(s,t,c,a) { Amount=amount;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{Amount},{SourceId},{SourceControllerId},{CastId},{ResolutionId}");
    }
    public sealed class DeathrattleResolvedFact : HistoryFact
    {
        // Zero incarnation denotes an explicit activation, not a minion death.
        public int EntityId { get; } public long Incarnation { get; } public int EffectSourceId { get; } public long ResolutionId { get; }
        internal DeathrattleResolvedFact(long s,int t,int c,int a,int entity,long incarnation,int effectSource,long resolution)
            : base(s,t,c,a) { EntityId=entity;Incarnation=incarnation;EffectSourceId=effectSource;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{Incarnation},{EffectSourceId},{ResolutionId}");
    }
    public sealed class SpellCounteredFact : HistoryFact
    {
        public int SpellEntityId { get; } public int BaseCost { get; } public int CasterControllerId { get; }
        public int CounterSourceId { get; } public int CounterControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal SpellCounteredFact(long s,int t,int c,int a,int spell,int cost,int caster,int source,int counter,long cast,long resolution)
            : base(s,t,c,a) { SpellEntityId=spell;BaseCost=cost;CasterControllerId=caster;CounterSourceId=source;CounterControllerId=counter;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{SpellEntityId},{BaseCost},{CasterControllerId},{CounterSourceId},{CounterControllerId},{CastId},{ResolutionId}");
    }
    public sealed class ExtraTurnGrantedFact : HistoryFact
    {
        public long OperationId { get; } public int SourceId { get; } public int SourceControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal ExtraTurnGrantedFact(long s,int t,int c,int a,long operation,int source,int sourceController,long cast,long resolution)
            : base(s,t,c,a) { OperationId=operation;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{OperationId},{SourceId},{SourceControllerId},{CastId},{ResolutionId}");
    }
    public sealed class ExtraTurnStartedFact : HistoryFact
    {
        public long OperationId { get; } public int GrantSourceId { get; } public long GrantResolutionId { get; }
        internal ExtraTurnStartedFact(long s,int t,int c,int a,long operation,int source,long resolution)
            : base(s,t,c,a) { OperationId=operation;GrantSourceId=source;GrantResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{OperationId},{GrantSourceId},{GrantResolutionId}");
    }
    public sealed class ComboActivatedFact : HistoryFact
    {
        public int CardEntityId { get; } public long PlayId { get; } public long ResolutionId { get; }
        internal ComboActivatedFact(long s,int t,int c,int a,int card,long play,long resolution)
            : base(s,t,c,a) { CardEntityId=card;PlayId=play;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{CardEntityId},{PlayId},{ResolutionId}");
    }
    public sealed class MaxManaChangedFact : HistoryFact
    {
        public int OldMaximum { get; } public int NewMaximum { get; } public bool Temporary { get; }
        public int SourceId { get; } public int SourceControllerId { get; } public long ResolutionId { get; }
        internal MaxManaChangedFact(long s,int t,int c,int a,int oldMaximum,int newMaximum,bool temporary,int source,int sourceController,long resolution)
            : base(s,t,c,a) { OldMaximum=oldMaximum;NewMaximum=newMaximum;Temporary=temporary;SourceId=source;SourceControllerId=sourceController;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{OldMaximum},{NewMaximum},{(Temporary?1:0)},{SourceId},{SourceControllerId},{ResolutionId}");
    }
    public sealed class HeroFatalFact : HistoryFact
    {
        public int HeroId { get; } public int SourceId { get; } public int SourceControllerId { get; }
        public HistoryDeathCause Cause { get; } public long CastId { get; } public long ResolutionId { get; } public long FatalDamageSequence { get; }
        internal HeroFatalFact(long s,int t,int c,int a,int hero,int source,int sourceController,HistoryDeathCause cause,long cast,long resolution,long damageSequence)
            : base(s,t,c,a) { HeroId=hero;SourceId=source;SourceControllerId=sourceController;Cause=cause;CastId=cast;ResolutionId=resolution;FatalDamageSequence=damageSequence; }
        internal override string Payload()=>FormattableString.Invariant($"{HeroId},{SourceId},{SourceControllerId},{(int)Cause},{CastId},{ResolutionId},{FatalDamageSequence}");
    }
    public sealed class SpellBodyStartedFact : HistoryFact
    {
        public long CastId { get; } public int SpellEntityId { get; } public long ResolutionId { get; }
        internal SpellBodyStartedFact(long s,int t,int c,int a,long cast,int spell,long resolution):base(s,t,c,a)
        { CastId=cast;SpellEntityId=spell;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{CastId},{SpellEntityId},{ResolutionId}");
    }
    public sealed class MinionLeftPlayFact : HistoryFact
    {
        public int EntityId { get; } public long Incarnation { get; } public int SourceId { get; } public int SourceControllerId { get; }
        public long CastId { get; } public long ResolutionId { get; } public HistoryBoardLeaveReason Reason { get; }
        internal MinionLeftPlayFact(long s,int t,int c,int a,int entity,long incarnation,int source,int sourceController,long cast,long resolution,HistoryBoardLeaveReason reason):base(s,t,c,a)
        { EntityId=entity;Incarnation=incarnation;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution;Reason=reason; }
        internal override string Payload()=>FormattableString.Invariant($"{EntityId},{Incarnation},{SourceId},{SourceControllerId},{CastId},{ResolutionId},{(int)Reason}");
    }
    public sealed class MinionFormChangedFact : HistoryFact
    {
        public int OldEntityId { get; } public long OldIncarnation { get; } public HistoryDefinition OldDefinition { get; }
        public int NewEntityId { get; } public long NewIncarnation { get; } public HistoryDefinition NewDefinition { get; }
        public int SourceId { get; } public int SourceControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal MinionFormChangedFact(long s,int t,int c,int a,int oldEntity,long oldIncarnation,HistoryDefinition oldDefinition,int newEntity,long newIncarnation,HistoryDefinition newDefinition,int source,int sourceController,long cast,long resolution):base(s,t,c,a)
        { OldEntityId=oldEntity;OldIncarnation=oldIncarnation;OldDefinition=oldDefinition;NewEntityId=newEntity;NewIncarnation=newIncarnation;NewDefinition=newDefinition;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{OldEntityId},{OldIncarnation},{OldDefinition.Data},{NewEntityId},{NewIncarnation},{NewDefinition.Data},{SourceId},{SourceControllerId},{CastId},{ResolutionId}");
    }
    public sealed class MinionAwakenedFact : HistoryCardFact
    {
        public long Incarnation { get; } public int SourceId { get; } public int SourceControllerId { get; } public long ResolutionId { get; }
        internal MinionAwakenedFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long incarnation,int source,int sourceController,long resolution):base(s,t,c,a,e,d,card,o)
        { Incarnation=incarnation;SourceId=source;SourceControllerId=sourceController;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{CardText()},{Incarnation},{SourceId},{SourceControllerId},{ResolutionId}");
    }
    public sealed class CardTransferredFact : HistoryCardFact
    {
        public long TransferId { get; } public int FromControllerId { get; } public Zone FromZone { get; }
        public int SourceId { get; } public int SourceControllerId { get; } public long CastId { get; } public long ResolutionId { get; }
        internal CardTransferredFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long transfer,int from,Zone fromZone,int source,int sourceController,long cast,long resolution):base(s,t,c,a,e,d,card,o)
        { TransferId=transfer;FromControllerId=from;FromZone=fromZone;SourceId=source;SourceControllerId=sourceController;CastId=cast;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{CardText()},{TransferId},{FromControllerId},{(int)FromZone},{SourceId},{SourceControllerId},{CastId},{ResolutionId}");
    }
    public sealed class MatchTerminalFact : HistoryFact
    {
        public int CurrentControllerId { get; } public int Player1Id { get; } public PlayState Player1State { get; }
        public int Player1Health { get; } public int Player1MaxHealth { get; } public int Player1Armor { get; } public int Player1Hand { get; } public int Player1Deck { get; } public int Player1Board { get; }
        public int Player2Id { get; } public PlayState Player2State { get; } public int Player2Health { get; } public int Player2MaxHealth { get; } public int Player2Armor { get; } public int Player2Hand { get; } public int Player2Deck { get; } public int Player2Board { get; }
        internal MatchTerminalFact(long s,int t,int c,int a,Controller p1,Controller p2):base(s,t,c,a)
        { CurrentControllerId=a;Player1Id=p1.Id;Player1State=p1.PlayState;Player1Health=p1.Hero.Health;Player1MaxHealth=p1.Hero.BaseHealth;Player1Armor=p1.Hero.Armor;Player1Hand=p1.HandZone.Count;Player1Deck=p1.DeckZone.Count;Player1Board=p1.BoardZone.Count;Player2Id=p2.Id;Player2State=p2.PlayState;Player2Health=p2.Hero.Health;Player2MaxHealth=p2.Hero.BaseHealth;Player2Armor=p2.Hero.Armor;Player2Hand=p2.HandZone.Count;Player2Deck=p2.DeckZone.Count;Player2Board=p2.BoardZone.Count; }
        internal override string Payload()=>FormattableString.Invariant($"{CurrentControllerId},{Player1Id},{(int)Player1State},{Player1Health},{Player1MaxHealth},{Player1Armor},{Player1Hand},{Player1Deck},{Player1Board},{Player2Id},{(int)Player2State},{Player2Health},{Player2MaxHealth},{Player2Armor},{Player2Hand},{Player2Deck},{Player2Board}");
    }
    public sealed class PlayerActionFact : HistoryFact
    {
        public HistoryPlayerActionKind Kind { get; } public int SourceId { get; } public int TargetId { get; } public long RelatedPlayId { get; }
        internal PlayerActionFact(long s,int t,int c,int a,HistoryPlayerActionKind kind,int source,int target,long play):base(s,t,c,a)
        { Kind=kind;SourceId=source;TargetId=target;RelatedPlayId=play; }
        internal override string Payload()=>FormattableString.Invariant($"{(int)Kind},{SourceId},{TargetId},{RelatedPlayId}");
    }
    public sealed class CombatStartedFact : HistoryFact
    {
        public long AttackId { get; } public int AttackerId { get; } public CardType AttackerType { get; }
        public int TargetId { get; } public int TargetControllerId { get; } public CardType TargetType { get; }
        public int AttackDamage { get; } public long AttackerIncarnation { get; } public HistoryDefinition AttackerDefinition { get; }
        public int TargetPreHitHealth { get; } public int TargetPreHitArmor { get; } public long ResolutionId { get; }
        internal CombatStartedFact(long s,int t,int c,int a,long attack,int attacker,CardType attackerType,int target,int targetController,CardType targetType,
            int damage,long incarnation,HistoryDefinition definition,int health,int armor,long resolution):base(s,t,c,a)
        { AttackId=attack;AttackerId=attacker;AttackerType=attackerType;TargetId=target;TargetControllerId=targetController;TargetType=targetType;
            AttackDamage=damage;AttackerIncarnation=incarnation;AttackerDefinition=definition;TargetPreHitHealth=health;TargetPreHitArmor=armor;ResolutionId=resolution; }
        internal override string Payload()=>FormattableString.Invariant($"{AttackId},{AttackerId},{(int)AttackerType},{TargetId},{TargetControllerId},{(int)TargetType},{AttackDamage},{AttackerIncarnation},{AttackerDefinition.Data},{TargetPreHitHealth},{TargetPreHitArmor},{ResolutionId}");
    }
    public sealed partial class HistoryQuery
    {
        public IEnumerable<CardDiscardedFact> Discards(HistoryWindow? window=null) => Events<CardDiscardedFact>(window);
        public IEnumerable<MinionRebornFact> Reborns(HistoryWindow? window=null) => Events<MinionRebornFact>(window);
        public IEnumerable<ControlChangedFact> ControlChanges(HistoryWindow? window=null)=>Events<ControlChangedFact>(window);
        public IEnumerable<CardBurnedFact> Burns(HistoryWindow? window=null)=>Events<CardBurnedFact>(window);
        public IEnumerable<KeywordAppliedFact> KeywordsApplied(HistoryWindow? window=null)=>Events<KeywordAppliedFact>(window);
        public IEnumerable<CorpseSpentFact> CorpsesSpent(HistoryWindow? window=null)=>Events<CorpseSpentFact>(window);
        public IEnumerable<DeathrattleResolvedFact> DeathrattlesResolved(HistoryWindow? window=null)=>Events<DeathrattleResolvedFact>(window);
        public IEnumerable<SpellCounteredFact> Counters(HistoryWindow? window=null)=>Events<SpellCounteredFact>(window);
        public IEnumerable<ExtraTurnStartedFact> ExtraTurnsStarted(HistoryWindow? window=null)=>Events<ExtraTurnStartedFact>(window);
        public IEnumerable<ComboActivatedFact> Combos(HistoryWindow? window=null)=>Events<ComboActivatedFact>(window);
        public IEnumerable<MaxManaChangedFact> MaxManaChanges(HistoryWindow? window=null)=>Events<MaxManaChangedFact>(window);
        public IEnumerable<HeroFatalFact> HeroFatals(HistoryWindow? window=null)=>Events<HeroFatalFact>(window);
        public IEnumerable<MinionLeftPlayFact> MinionsLeftPlay(HistoryWindow? window=null)=>Events<MinionLeftPlayFact>(window);
        public IEnumerable<MinionFormChangedFact> FormChanges(HistoryWindow? window=null)=>Events<MinionFormChangedFact>(window);
        public IEnumerable<MinionAwakenedFact> Awakenings(HistoryWindow? window=null)=>Events<MinionAwakenedFact>(window);
        public IEnumerable<CardTransferredFact> Transfers(HistoryWindow? window=null)=>Events<CardTransferredFact>(window);
        public IEnumerable<PlayerActionFact> PlayerActions(HistoryWindow? window=null)=>Events<PlayerActionFact>(window);
        public IEnumerable<CombatStartedFact> CombatStarts(HistoryWindow? window=null)=>Events<CombatStartedFact>(window);
        public IEnumerable<CardPlayedFact> CompletedPlays(HistoryWindow? window=null)
        {
            var completed = new HashSet<long>(Events<CardPlayCompletedFact>(window).Select(f=>f.PlayId));
            return Plays().Where(f=>completed.Contains(f.PlayId));
        }
        public HistoryWindow SincePreviousTurnEnded(int controllerId)
        {
            var turns=Events<TurnStartedFact>().ToArray();
            var previous=turns.LastOrDefault(f=>f.ControllerId==controllerId && f.Turn<_store.CurrentTurn);
            long start=previous==null ? 0 : turns.FirstOrDefault(f=>f.Sequence>previous.Sequence)?.Sequence ?? long.MaxValue;
            return new HistoryWindow(start,long.MaxValue);
        }
        public CardPlayedFact LatestPlay(int entityId) => Newest<CardPlayedFact>((f,id)=>f.EntityId==id,entityId);
        /// <summary>Whether a completion fact for this play exists (it is always appended after the play itself).</summary>
        internal bool PlayCompleted(CardPlayedFact play) => Newest<CardPlayCompletedFact>((f,id)=>f.PlayId==id,play.PlayId,play.Sequence)!=null;
        /// <summary>Newest-first scan: the fact a forward LastOrDefault over Events&lt;T&gt; picks, without walking the whole ledger.
        /// Facts at or before <paramref name="after"/> cannot match. The match delegates must not capture.</summary>
        private T Newest<T>(Func<T,long,bool> match,long arg,long after=0) where T:HistoryFact
        {
            var tail=_store.TailChunk;
            for(int i=_store.TailCount-1;i>=0;i--)
            {
                var fact=tail[i]; EntriesExamined++;
                if(fact.Sequence<=after) return null;
                if(fact is T value && fact.Sequence<_before && match(value,arg)) return value;
            }
            var chunks=_store.PublishedChunks;
            for(int c=chunks.Count-1;c>=0;c--)
            {
                var chunk=chunks[c];
                if(chunk[chunk.Length-1].Sequence<=after) return null;
                for(int i=chunk.Length-1;i>=0;i--)
                {
                    var fact=chunk[i]; EntriesExamined++;
                    if(fact.Sequence<=after) return null;
                    if(fact is T value && fact.Sequence<_before && match(value,arg)) return value;
                }
            }
            return null;
        }
    }
    internal sealed partial class HistoryRecorder
    {
        internal void Discarded(IPlayable e)
        {
            if (Store.IsProjected && e is Unknown)
                throw new NotSupportedException("Private historical card definition is unavailable in a fair view.");
            Store.Append(new CardDiscardedFact(Seq,Turn,e.Controller.Id,Active,
                e.Id,Store.Definition(e.Card),e.Card,Original(e)));
        }
        private Minion _rebornSource,_rebornEntity;
        internal IDisposable Rebirth(Minion source,Minion returned)
        {
            var previousSource=_rebornSource;var previousEntity=_rebornEntity;
            _rebornSource=source;_rebornEntity=returned;
            return new Cleanup(()=>{_rebornSource=previousSource;_rebornEntity=previousEntity;});
        }
        private void RecordRebirth(Minion entity)
        {
            if(entity!=_rebornEntity) return;
            var source=_rebornSource;_rebornEntity=null;
            var sourceIdentity=Store.Query.Deaths().LastOrDefault(f=>f.EntityId==source.Id);
            var returnedIdentity=Store.Board(entity.Id);
            // The fact itself is gameplay input (CAP_806 Raith van Geist reads Query.Reborns), so it is always appended, as on
            // main; only its incarnation links are achievement evidence (0 when unknown, and the evidence is invalidated).
            if(sourceIdentity==null || returnedIdentity==null) Store.InvalidateEvidence("Missing returned Reborn incarnation.");
            Store.Append(new MinionRebornFact(Seq,Turn,entity.Controller.Id,Active,source.Id,
                Store.Definition(source.Card),source.Card,Store.Origin(source.Id,entity.Controller.Id),sourceIdentity?.Incarnation??0,entity.Id,returnedIdentity?.Incarnation??0));
        }
        internal void PlayCompleted(IPlayable source)
        {
            var play=Store.Query.LatestPlay(source.Id);
            if(play==null || Store.Query.PlayCompleted(play)) return;
            Store.Append(new CardPlayCompletedFact(Seq,Turn,play.ControllerId,Active,play.PlayId));
        }

        private long CastId(IPlayable source)
        {
            if(source==null) return 0;
            for(var cast=_cast;cast!=null;cast=cast.Previous)
                if(cast.Entity.Id==source.Id) return cast.Id;
            return 0;
        }
        private IEntity HistorySource(IEntity source)=>source??_executingSource;
        internal void CaptureFatal(ICharacter victim,IPlayable source,HistoryDeathCause cause,long damageSequence)
        {
            long cast=CastId(source);long resolution=_game.Resolution.Current.SequenceId;
            if(victim is Minion minion)
            {
                if(minion.PendingHistoryDeathCause!=HistoryDeathCause.None) return;
                minion.SetHistoryDeathCause(source?.Id??0,source?.Controller?.Id??0,cause,cast,resolution,damageSequence);
            }
            else if(victim is Hero)
                Store.Append(new HeroFatalFact(Seq,Turn,victim.Controller.Id,Active,victim.Id,source?.Id??0,source?.Controller?.Id??0,cause,cast,resolution,damageSequence));
        }
        internal void CaptureDestroy(IPlayable victim,IEntity source)
        {
            if(victim is Minion minion && minion.PendingHistoryDeathCause==HistoryDeathCause.None)
                minion.SetHistoryDeathCause(source?.Id??0,source?.Controller?.Id??0,HistoryDeathCause.Destroy,CastId(source as IPlayable),_game.Resolution.Current.SequenceId);
            else if(victim is Hero hero && !hero.ToBeDestroyed)
                Store.Append(new HeroFatalFact(Seq,Turn,hero.Controller.Id,Active,hero.Id,source?.Id??0,source?.Controller?.Id??0,HistoryDeathCause.Destroy,CastId(source as IPlayable),_game.Resolution.Current.SequenceId,0));
        }
        internal void Freeze(ICharacter target,IEntity source)
        {
            bool wasFrozen=target.IsFrozen;target.IsFrozen=true;if(!wasFrozen && target.IsFrozen) Frozen(target,source);
        }
        // Records only: callers that write FROZEN through the tag indexer raise their own freeze event.
        internal void Frozen(ICharacter target,IEntity source)
        {
            var actual=HistorySource(source);Store.Append(new KeywordAppliedFact(Seq,Turn,target.Controller.Id,Active,target.Id,actual?.Id??0,
                actual?.Controller?.Id??0,CastId(actual as IPlayable),_game.Resolution.Current.SequenceId,HistoryKeyword.Frozen,true));
        }
        internal void CardBurned(IPlayable card)
        {
            if(_handEntity!=card.Id || _handKind!=HistoryHandEntryKind.Draw && _handKind!=HistoryHandEntryKind.TurnStartDraw) return;
            var source=_executingSource;Store.Append(new CardBurnedFact(Seq,Turn,card.Controller.Id,Active,card.Id,source?.Id??0,source?.Controller?.Id??0,
                CastId(source as IPlayable),_game.Resolution.Current.SequenceId,HistoryBurnReason.FullHand));
        }
        internal void CorpsesSpent(Controller controller,int amount,IEntity source=null)
        {
            var actual=HistorySource(source);Store.Append(new CorpseSpentFact(Seq,Turn,controller.Id,Active,amount,actual?.Id??0,actual?.Controller?.Id??0,
                CastId(actual as IPlayable),_game.Resolution.Current.SequenceId));
        }
        internal void DeathrattleResolved(int deathEntityId,IEntity effectSource,Controller controller,bool manual=false)
        {
            if(manual) {
                if(_game.IdEntityDic.TryGetValue(deathEntityId,out var manualEntity) && manualEntity is Minion)
                    Store.Append(new DeathrattleResolvedFact(Seq,Turn,controller.Id,Active,deathEntityId,0,effectSource?.Id??0,_game.Resolution.Current.SequenceId));
                return;
            }
            var death=Store.Query.Deaths().LastOrDefault(f=>f.EntityId==deathEntityId);
            if(_game.IdEntityDic.TryGetValue(deathEntityId,out var entity) && !(entity is Minion)) return;
            if(death==null) { Store.InvalidateEvidence("Deathrattle has no linked death fact: entity "+deathEntityId+".");return; }
            Store.Append(new DeathrattleResolvedFact(Seq,Turn,controller.Id,Active,deathEntityId,death.Incarnation,effectSource?.Id??0,_game.Resolution.Current.SequenceId));
        }
        internal void SpellCountered(Spell spell,IEntity source)
        {
            var cast=_cast;
            while(cast!=null && cast.Entity.Id!=spell.Id) cast=cast.Previous;
            if(cast==null || cast.Finished) return;
            var actual=HistorySource(source);
            Store.Append(new SpellCounteredFact(Seq,Turn,actual?.Controller?.Id??spell.Controller.Id,Active,spell.Id,spell.Card.Cost,spell.Controller.Id,
                actual?.Id??0,actual?.Controller?.Id??0,cast.Id,_game.Resolution.Current.SequenceId));
        }
        internal long AllocateOperation()=>Store.NextOperation++;
        internal void ExtraTurnGranted(long operation,Controller owner,IEntity source)
        {
            var actual=HistorySource(source);Store.Append(new ExtraTurnGrantedFact(Seq,Turn,owner.Id,Active,operation,actual?.Id??0,actual?.Controller?.Id??0,
                CastId(actual as IPlayable),_game.Resolution.Current.SequenceId));
        }
        internal void ExtraTurnStarted(long operation)
        {
            var grant=Store.Query.Events<ExtraTurnGrantedFact>().Single(f=>f.OperationId==operation);
            Store.Append(new ExtraTurnStartedFact(Seq,Turn,grant.ControllerId,grant.ControllerId,operation,grant.SourceId,grant.ResolutionId));
        }
        internal void ComboActivated(IPlayable card)
        {
            var play=Store.Query.LatestPlay(card.Id);
            if(play==null || Store.Query.Events<ComboActivatedFact>().Any(f=>f.PlayId==play.PlayId)) return;
            Store.Append(new ComboActivatedFact(Seq,Turn,card.Controller.Id,Active,card.Id,play.PlayId,_game.Resolution.Current.SequenceId));
        }
        internal void BaseManaChanged(Controller controller,int oldMaximum,IEntity source=null)
        {
            if(oldMaximum==controller.BaseMana) return;
            var actual=HistorySource(source);Store.Append(new MaxManaChangedFact(Seq,Turn,controller.Id,Active,oldMaximum,controller.BaseMana,false,
                actual?.Id??0,actual?.Controller?.Id??0,_game.Resolution.Current.SequenceId));
        }
        internal void SpellBodyStarted(Spell spell)
        {
            var cast=_cast;
            while(cast!=null && cast.Entity.Id!=spell.Id) cast=cast.Previous;
            if(cast==null || cast.Finished) { Store.InvalidateEvidence("Spell body has no active cast.");return; }
            Store.Append(new SpellBodyStartedFact(Seq,Turn,spell.Controller.Id,Active,cast.Id,spell.Id,_game.Resolution.Current.SequenceId));
        }
        internal void FormChanged(IPlayable entity,Card oldCard)
        {
            if(!(entity is Minion) || oldCard==entity.Card) return;
            var board=Store.Board(entity.Id);if(board==null) return;
            var source=_executingSource;Store.Append(new MinionFormChangedFact(Seq,Turn,entity.Controller.Id,Active,entity.Id,board.Incarnation,
                Store.Definition(oldCard),entity.Id,board.Incarnation,Store.Definition(entity.Card),source?.Id??0,source?.Controller?.Id??0,
                CastId(source as IPlayable),_game.Resolution.Current.SequenceId));
        }
        internal void TransformReplacement(Minion oldEntity,Minion newEntity)
        {
            var oldBoard=Store.Board(oldEntity.Id);
            Store.RemoveBoard(oldEntity.Id);BoardEntered(newEntity,false);
            var newBoard=Store.Board(newEntity.Id);var source=_executingSource;
            if(oldBoard==null || newBoard==null) { Store.InvalidateEvidence("Missing transformed board identity.");return; }
            Store.Append(new MinionFormChangedFact(Seq,Turn,newEntity.Controller.Id,Active,oldEntity.Id,oldBoard.Incarnation,Store.Definition(oldEntity.Card),
                newEntity.Id,newBoard.Incarnation,Store.Definition(newEntity.Card),source?.Id??0,source?.Controller?.Id??0,CastId(source as IPlayable),_game.Resolution.Current.SequenceId));
        }
        internal void Awakened(Minion minion,IEntity source=null)
        {
            var board=Store.Board(minion.Id);var actual=HistorySource(source);
            if(board==null) { Store.InvalidateEvidence("Missing awakened board identity.");return; }
            Store.Append(new MinionAwakenedFact(Seq,Turn,minion.Controller.Id,Active,minion.Id,Store.Definition(minion.Card),minion.Card,Original(minion),
                board.Incarnation,actual?.Id??0,actual?.Controller?.Id??0,_game.Resolution.Current.SequenceId));
        }
        internal void Terminal()
        {
            if(Store.Query.Events<MatchTerminalFact>().Any()) return;
            Store.Append(new MatchTerminalFact(Seq,Turn,_game.CurrentPlayer.Id,Active,_game.Player1,_game.Player2));
        }
        private PlayerActionScope _playerAction;
        internal PlayerActionScope BeginPlayerAction(PlayerTask task,bool enabled)
        {
            var scope=new PlayerActionScope(this,_playerAction,task,enabled);_playerAction=scope;return scope;
        }
        internal void AcceptPlayerAction(IPlayable source)
        { _playerAction?.Accept(source); }
        internal sealed class PlayerActionScope:IDisposable
        {
            private readonly HistoryRecorder _owner;
            private readonly PlayerActionScope _previous;
            private readonly HistoryPlayerActionKind? _kind;
            private readonly int _controller,_source,_target;
            private bool _accepted,_finished;
            internal PlayerActionScope(HistoryRecorder owner,PlayerActionScope previous,PlayerTask task,bool enabled)
            {
                _owner=owner;_previous=previous;
                _kind=enabled ? task switch {
                PlayCardTask=>HistoryPlayerActionKind.PlayCard,MinionAttackTask=>HistoryPlayerActionKind.MinionAttack,
                HeroAttackTask=>HistoryPlayerActionKind.HeroAttack,HeroPowerTask=>HistoryPlayerActionKind.HeroPower,
                TradeCardTask=>HistoryPlayerActionKind.TradeCard,PrepareCardTask=>HistoryPlayerActionKind.PrepareCard,
                ActivateLocationTask=>HistoryPlayerActionKind.ActivateLocation,ActivateOldGodTask=>HistoryPlayerActionKind.ActivateOldGod,_=>null } : null;
                _controller=task.Controller.Id;
                _source=(task switch { HeroAttackTask=>task.Controller.Hero,HeroPowerTask=>task.Source??task.Controller.Hero.HeroPower,_=>task.Source })?.Id??0;
                _target=task.ActionTarget?.Id??0;
            }
            // Achievement bookkeeping only: an unexpected shape invalidates the match's evidence, never the action.
            internal void Accept(IPlayable source)
            {
                if(_kind==null || source?.Id!=_source) return;
                // The first acceptance is the player's action; a same-source attack/payment inside it is an effect.
                if(_accepted) return;
                long play=_kind==HistoryPlayerActionKind.PlayCard ? _owner._paymentPlay : 0;
                if(_kind==HistoryPlayerActionKind.PlayCard && play==0)
                { _owner.Store.InvalidateEvidence("Accepted play has no committed payment fact.");return; }
                _owner.Store.Append(new PlayerActionFact(_owner.Seq,_owner.Turn,_controller,_owner.Active,_kind.Value,_source,_target,play));
                _accepted=true;
            }
            internal void Finish(bool result)
            {
                if(_kind!=null && _accepted!=result)
                    _owner.Store.InvalidateEvidence(result ? "Accepted player action was not recorded." : "Rejected player action was recorded.");
                _finished=true;
            }
            internal void Abort() { _finished=true; }
            public void Dispose()
            {
                _owner._playerAction=_previous;
                if(_kind!=null && !_finished && _accepted) _owner.Store.InvalidateEvidence("Accepted player action did not finish.");
            }
        }
        private TransferScope _transfer;
        internal IDisposable CardTransfer(IPlayable card,int fromController,Zone fromZone,IEntity source=null)
        {
            var scope=new TransferScope(this,_transfer,card,fromController,fromZone,HistorySource(source));_transfer=scope;return scope;
        }
        private void RecordTransfer(IPlayable card)
        {
            var scope=_transfer;if(scope==null || scope.Card.Id!=card.Id || scope.FromController==card.Controller.Id) return;
            Store.Append(new CardTransferredFact(Seq,Turn,card.Controller.Id,Active,card.Id,Store.Definition(card.Card),card.Card,Original(card),
                Store.NextOperation++,scope.FromController,scope.FromZone,scope.Source?.Id??0,scope.Source?.Controller?.Id??0,
                CastId(scope.Source as IPlayable),_game.Resolution.Current.SequenceId));
        }
        private sealed class TransferScope:IDisposable
        {
            private readonly HistoryRecorder _owner;private readonly TransferScope _previous;
            private readonly IDisposable _handEntry;
            internal readonly IPlayable Card;internal readonly int FromController;internal readonly Zone FromZone;internal readonly IEntity Source;
            internal TransferScope(HistoryRecorder owner,TransferScope previous,IPlayable card,int from,Zone zone,IEntity source)
            { _owner=owner;_previous=previous;Card=card;FromController=from;FromZone=zone;Source=source;
                _handEntry=owner.HandEntry(card,HistoryHandEntryKind.Other,zone); }
            public void Dispose() { _owner._transfer=_previous;_handEntry.Dispose(); }
        }
    }
}
