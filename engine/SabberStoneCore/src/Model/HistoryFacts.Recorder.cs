using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
	public partial class Game
	{
		public HistoryFacts HistoryFacts { get; private set; } = new HistoryFacts();
		[NonSerialized] private HistoryRecorder _facts;
		internal HistoryRecorder Facts => _facts ?? (_facts=new HistoryRecorder(this));
		/// <summary>Replace only the simulation clone root; masking is not gameplay.</summary>
		public void ProjectHistoryForFairView(int ownerId, System.Collections.Generic.IReadOnlyCollection<int> publiclyKnownHand = null)
		{
			if(ownerId!=Player1.PlayerId && ownerId!=Player2.PlayerId) throw new ArgumentOutOfRangeException(nameof(ownerId));
			HistoryFacts=HistoryFacts.ProjectForFairView(this, ownerId, publiclyKnownHand);
		}
	}
	// The only gameplay writer. Scopes are invocation-local, never cloned or hydrated.
	internal sealed partial class HistoryRecorder
	{
		private readonly Game _game;
		private HistoryFacts Store=>_game.HistoryFacts;
		private int Active=>_game.CurrentPlayer?.Id ?? 0;
		private long Seq=>Store.Sequence;
		private int Turn=>_game.Turn;
		private int _playing, _otherBoardEntry, _handEntity, _moving, _turnStartDraw;
		private HistoryBoardIdentity _movedBoard;
		private bool _playBoardPending;
		private int _choiceOffers;
        internal bool CreatingChoiceOffers => _choiceOffers != 0;
		private HistoryHandEntryKind _handKind;
        private Zone _handFrom;
		private ShuffleScope _shuffle;
		private CastScope _cast;
		internal HistoryRecorder(Game game) { _game=game; }
		/// <summary>Entity of the hand play in flight (0 outside a play); its own play fact is already recorded.</summary>
		internal int PlayingEntity => _playing;
		private bool? Original(IPlayable e)=>Store.Origin(e.Id,e.Controller.Id);
		internal void EntityCreated(Card card) { if(_choiceOffers==0) Store.Enroll(card); }
		internal void AdmitDefinition(Card card) => Store.Enroll(card);
		internal IDisposable ChoiceOffers()
		{ _choiceOffers++; return new Cleanup(()=>_choiceOffers--); }
		internal void TurnStarted()
		{
			Store.CurrentTurn=Turn;
			var c=_game.CurrentPlayer;var opposing=c.Opponent;
			Store.Append(new TurnStartedFact(Seq,Turn,Active,Store.Query.TurnsTaken(Active)+1,c.Hero.Health,c.Hero.BaseHealth,c.Hero.Armor,
				c.HandZone.Count,c.DeckZone.Count,c.BoardZone.Count,opposing.Hero.Health,opposing.Hero.Armor,c.BaseMana,opposing.BaseMana));
		}
		internal IDisposable TurnStartDraw()
		{ _turnStartDraw++;return new Cleanup(()=>_turnStartDraw--); }
		internal IDisposable Play(IPlayable e)
		{
			if(_paymentPlay==0) throw new InvalidOperationException("Missing history play payment annotation.");
			int old=_playing; bool pending=_playBoardPending; _playing=e.Id; _playBoardPending=true;
			Store.Append(new CardPlayedFact(Seq,Turn,e.Controller.Id,Active,e.Id,Store.Definition(e.Card),e.Card,Original(e),_paymentPlay,e[GameTag.TAG_LAST_KNOWN_COST_IN_HAND],e.Controller.PlayHistory.Last().HandPosition,e.Controller.PlayHistory.Last().HandCount));
			return new Cleanup(()=>{_playing=old;_playBoardPending=pending;});
		}
		internal IDisposable ControlTransfer(Minion entity,IEntity source)
		{
			int oldEntity=_moving; var oldBoard=_movedBoard;
			int from=entity.Controller.Id;var moved=Store.Board(entity.Id);
			_moving=entity.Id; _movedBoard=Store.Board(entity.Id); _otherBoardEntry++;
			return new Cleanup(()=>{
				var current=Store.Board(entity.Id);
				if(moved!=null && current!=null && from!=entity.Controller.Id)
				{
					var actual=source??_executingSource;long cast=CastId(actual as IPlayable);
					Store.Append(new ControlChangedFact(Seq,Turn,entity.Controller.Id,Active,entity.Id,moved.Incarnation,current.Incarnation,from,entity.Controller.Id,
						actual?.Id??0,actual?.Controller?.Id??0,cast,_game.Resolution.Current.SequenceId));
				}
				_moving=oldEntity;_movedBoard=oldBoard;_otherBoardEntry--;
			});
		}
		internal void BoardEntered(Minion e,bool summon=true)
		{
			long incarnation=Store.NextIncarnation++;
			var play=_playing==e.Id && _playBoardPending ? Store.Query.Plays().LastOrDefault(f=>f.EntityId==e.Id) : null;
			if(play!=null) _playBoardPending=false;
			var previous=_moving==e.Id?_movedBoard:null;
			Store.SetBoard(e.Id,new HistoryBoardIdentity(incarnation,play?.PlayId??previous?.PlayId??0,play?.Turn??previous?.PlayedTurn??0));
			if(summon && _otherBoardEntry==0)
            {
				Store.Append(new MinionSummonedFact(Seq,Turn,e.Controller.Id,Active,e.Id,Store.Definition(e.Card),e.Card,Original(e),incarnation,play?.PlayId??0));
                Actions.OldGodRules.RecordSummon(e);
            }
            RecordRebirth(e);
		}
		internal void BoardLeft(Minion e)
		{
			var board=Store.Board(e.Id);if(board==null) return;
			if(_moving!=e.Id)
			{
				var source=_executingSource;Store.Append(new MinionLeftPlayFact(Seq,Turn,e.Controller.Id,Active,e.Id,board.Incarnation,source?.Id??0,
					source?.Controller?.Id??0,CastId(source as IPlayable),_game.Resolution.Current.SequenceId,HistoryBoardLeaveReason.Other));
			}
			Store.RemoveBoard(e.Id);
		}
		internal void Died(Minion e, int costAtDeath)
		{
			var board=Store.Board(e.Id) ?? throw new InvalidOperationException($"Missing history board-entry annotation: {e.Card.Id} entity={e.Id} zone={e.Zone?.Type} turn={Turn}.");
			Store.Append(new MinionDiedFact(Seq,Turn,e.Controller.Id,Active,e.Id,Store.Definition(e.Card),e.Card,Original(e),board.Incarnation,board.PlayId,board.PlayedTurn,e.Card.Name=="Treant",costAtDeath,
				e.HistoryDeathSourceId,e.HistoryDeathSourceControllerId,e.PendingHistoryDeathCause,e.HistoryDeathCastId,e.HistoryDeathResolutionId,e.HistoryDeathDamageSequence));
			Store.RemoveBoard(e.Id);e.ClearHistoryDeathCause();
		}
		internal IDisposable HandEntry(IPlayable e,HistoryHandEntryKind kind,Zone from=Zone.INVALID)
		{
			int old=_handEntity; var oldKind=_handKind; var oldFrom=_handFrom; _handEntity=e.Id;
			_handKind=_turnStartDraw!=0 && kind==HistoryHandEntryKind.Draw?HistoryHandEntryKind.TurnStartDraw:kind; _handFrom=from;
			return new Cleanup(()=>{_handEntity=old;_handKind=oldKind;_handFrom=oldFrom;});
		}
        internal HistoryHandEntryKind HandEntryKind(IPlayable e) =>
            _handEntity==e.Id ? _handKind : Store.WasInitial(e.Id) || Store.Query.HandEntries().Any(f=>f.EntityId==e.Id) || Store.Query.Summons().Any(f=>f.EntityId==e.Id)
				|| Store.Query.Casts().Any(f=>f.EntityId==e.Id) || Store.Query.Events<ShuffleOperationFact>().Any(f=>f.EntityId==e.Id)
				? HistoryHandEntryKind.Other : HistoryHandEntryKind.Generated;
		internal void HandEntered(IPlayable e,Zone from=Zone.INVALID)
		{
            var kind = HandEntryKind(e);
			Store.Append(new HandEnteredFact(Seq,Turn,e.Controller.Id,Active,e.Id,kind,_executingSource?.Id ?? 0,_handEntity==e.Id && _handFrom!=Zone.INVALID ? _handFrom : from));
			RecordTransfer(e);
		}
		internal ShuffleScope Shuffle(int controllerId)
		{ var scope=new ShuffleScope(this,_shuffle,controllerId); _shuffle=scope; return scope; }
		internal IDisposable EnsureShuffle(int controllerId) => _shuffle==null ? Shuffle(controllerId) : null;
		internal void RequireShuffle() { if(_shuffle==null) throw new InvalidOperationException("Missing history shuffle producer annotation."); }
		internal void Shuffled(IPlayable e)
		{
			var scope=_shuffle ?? throw new InvalidOperationException("Missing history shuffle producer annotation.");
			if(scope.ControllerId==0) return;
			bool first=scope.Id==0;
			if(first) scope.Id=Store.NextOperation++;
			Store.Append(new ShuffleOperationFact(Seq,Turn,scope.ControllerId,Active,scope.Id,e.Id,e.Controller.Id,first));
		}
		internal IDisposable SuspendShuffle()
		{ var old=_shuffle; _shuffle=null; return new Cleanup(()=>_shuffle=old); }
		internal void MarkDiscover(Choice choice) { choice.HistoryDiscoverId=Store.NextOperation++; }
		internal void MarkDiscover(Controller controller)
		{
			var choice=controller.Choice;
			while(choice.NextChoice!=null) choice=choice.NextChoice;
			MarkDiscover(choice);
		}
		internal void Discover(Choice choice,int selected)
		{
			if(choice.HistoryDiscoverId==0 || Actions.DiscoverRealRules.IsRefresh(choice,selected)) return;
			// A fair-view simulation can pick a hidden deck offer (e.g. Tracking); its definition is unknowable.
			var definitions=Store.IsProjected && _game.IdEntityDic[selected] is Unknown ? null : new[]{Store.Definition(_game.IdEntityDic[selected].Card)};
			Store.Append(new DiscoverCompletedFact(Seq,Turn,choice.Controller.Id,Active,choice.HistoryDiscoverId,new[]{selected},definitions));
			choice.HistoryDiscoverId=0;
            if (_game.History) _game.PowerHistory.Add(Kettle.PowerHistoryBuilder.TagChange(choice.Controller.Id, Actions.DiscoverRealRules.CompletionEvent, 1));
		}
		internal void DiscoverAll(int controllerId,Card[] selected)
        {
            Store.Append(new DiscoverCompletedFact(Seq,Turn,controllerId,Active,Store.NextOperation++,Array.Empty<int>(),selected.Select(Store.Definition).ToArray(),true));
            if (_game.History) _game.PowerHistory.Add(Kettle.PowerHistoryBuilder.TagChange(controllerId, Actions.DiscoverRealRules.CompletionEvent, 1));
        }
		internal CastScope Cast(Spell e,HistoryCastKind kind,bool claimBody=false)
		{
			if(claimBody && _cast!=null && _cast.Entity.Id==e.Id && _cast.AllowBodyClaim && !_cast.BodyClaimed && !_cast.Finished)
			{ _cast.BodyClaimed=true; return new CastScope(this,_cast); }
			if(!Enum.IsDefined(typeof(HistoryCastKind),kind)) throw new ArgumentOutOfRangeException(nameof(kind));
			var scope=new CastScope(this,_cast,e,kind,Store.NextCast++); scope.BodyClaimed=claimBody; _cast=scope;
			RecordCast(scope,HistoryCastOutcome.Started); return scope;
		}
		private void RecordCast(CastScope cast,HistoryCastOutcome outcome)
		{ var e=cast.Entity; Store.Append(new SpellCastFact(Seq,Turn,cast.ControllerId,Active,e.Id,cast.Definition,cast.Card,cast.Original,cast.Id,cast.Kind,outcome)); }
		internal sealed class ShuffleScope:IDisposable
		{
			private readonly HistoryRecorder _owner; private readonly ShuffleScope _previous;
			internal readonly int ControllerId; internal long Id;
			internal ShuffleScope(HistoryRecorder owner,ShuffleScope previous,int controller) { _owner=owner;_previous=previous;ControllerId=controller; }
			public void Dispose()=>_owner._shuffle=_previous;
		}
		internal sealed class CastScope:IDisposable
		{
			private readonly HistoryRecorder _owner; private readonly CastScope _previous,_borrowed;
			internal readonly Spell Entity; internal readonly Card Card; internal readonly HistoryDefinition Definition;
			internal readonly bool? Original; internal readonly HistoryCastKind Kind; internal readonly long Id;
			internal readonly int ControllerId;

            private readonly Actions.AuraSpellRules.CastWitness _auraWitness;
			internal bool BodyClaimed,Finished,AllowBodyClaim;
            internal CastScope Previous=>_previous;
			internal CastScope(HistoryRecorder owner,CastScope borrowed) { _owner=owner;_borrowed=borrowed; }
			internal CastScope(HistoryRecorder owner,CastScope previous,Spell e,HistoryCastKind kind,long id)
			{ _owner=owner;_previous=previous;Entity=e;Card=e.Card;ControllerId=e.Controller.Id;Definition=owner.Store.Definition(e.Card);Original=owner.Original(e);Kind=kind;Id=id;
                _auraWitness=Actions.AuraSpellRules.BeginCast(e,kind); }
			internal void Complete(HistoryCastOutcome outcome=HistoryCastOutcome.Completed)
			{ if(_borrowed!=null) return; if(Finished) return; _owner.RecordCast(this,outcome);Finished=true;
                if(outcome==HistoryCastOutcome.Completed) Actions.AuraSpellRules.CompleteCast(Entity,_auraWitness); }
			public void Dispose() { if(_borrowed!=null) return; _owner._cast=_previous; }
		}
		private sealed class Cleanup:IDisposable
		{ private readonly Action _cleanup; internal Cleanup(Action cleanup) { _cleanup=cleanup; } public void Dispose()=>_cleanup(); }
	}
	internal sealed class HistoryBoardIdentity
	{
		internal readonly long Incarnation,PlayId; internal readonly int PlayedTurn;
		internal HistoryBoardIdentity(long incarnation,long playId,int playedTurn) { Incarnation=incarnation;PlayId=playId;PlayedTurn=playedTurn; }
	}
}
