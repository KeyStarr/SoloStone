using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using SabberStoneCore.Model.HistoryIndex;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Snapshots;

namespace SabberStoneCore.Model
{
	public enum HistoryCastKind { Player, Effect, Drawn, Repeat }
	public enum HistoryCastOutcome { Started, Countered, Completed }
	public enum HistoryHandEntryKind { Draw, Generated, Other, TurnStartDraw }

	/// <summary>Frozen definition, encoded by the same closed Card recipe as snapshot v2 (including composed cards).</summary>
	public sealed class HistoryDefinition
	{
		private readonly Card _card;
		[NonSerialized] private readonly Card _source;
		public string Data => SnapshotCodec.EncodeDefinition(_card);
		public int DbfId => _card.AssetId;
		internal HistoryDefinition(Card card)
		{
			_source = card;
			_card = ReferenceEquals(card, Cards.FromId(card.Id)) ? card : Freeze(card);
		}
		// Match the snapshot graph, retaining only immutable catalogue/callback recipe identities.
		private static Card Freeze(Card card)
		{
			var seen = new Dictionary<object, object>(ReferenceEquality.Instance);
			object Copy(object value)
			{
				if (value == null) return null;
				var type = value.GetType();
				if (type.IsPrimitive || type.IsEnum || value is string || value is Delegate || value is System.Type || value is MemberInfo)
					return value;
				if (!ReferenceEquals(value, card) && value is Card nested && ReferenceEquals(nested, Cards.FromId(nested.Id)))
					return value;
				if (seen.TryGetValue(value, out var existing)) return existing;
				var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
				seen.Add(value, clone);
				if (value is Array array)
				{
					for (int i = 0; i < array.Length; i++) ((Array)clone).SetValue(Copy(array.GetValue(i)), i);
				}
				else
					foreach (var field in SnapshotSchema.Fields(type)) field.SetValue(clone, Copy(field.GetValue(value)));
				return clone;
			}
			return (Card)Copy(card);
		}
		internal bool Matches(Card card) => ReferenceEquals(_source,card) || ReferenceEquals(_card,card);
		public Card Materialize() => SnapshotCodec.DecodeDefinition(SnapshotCodec.EncodeDefinition(_card, detached: true));
		internal Card ReadDefinition() => _card;
		internal void Validate()
		{
			if (_card == null || string.IsNullOrEmpty(_card.Id))
				throw new SerializationException("Invalid history card definition.");
			_card.ValidateHistoryDisplays();
			Card installed;
			try { installed = Cards.FromId(_card.Id); }
			catch (KeyNotFoundException e) { throw new SerializationException("Unknown history card definition.", e); }
			if (installed == null || installed.AssetId != DbfId || string.IsNullOrEmpty(Data))
				throw new SerializationException("Invalid history card definition identity.");
		}
	}

	public abstract class HistoryFact
	{
		public long Sequence { get; }
		public int Turn { get; }
		public int ControllerId { get; }
		public int ActiveControllerId { get; }
		internal HistoryFact(long sequence, int turn, int controller, int active)
		{ Sequence = sequence; Turn = turn; ControllerId = controller; ActiveControllerId = active; }
		internal abstract string Payload();
		internal string Text() => FormattableString.Invariant($"{GetType().Name}:{Sequence}:{Turn}:{ControllerId}:{ActiveControllerId}:{Payload()}");
	}
	public sealed class TurnStartedFact : HistoryFact
	{
		public int TurnsTaken { get; }
		public int HeroHealth { get; } public int HeroMaxHealth { get; } public int HeroArmor { get; }
		public int HandCount { get; } public int DeckCount { get; } public int FriendlyMinionCount { get; }
		public int OpposingHeroHealth { get; } public int OpposingHeroArmor { get; }
		public int BaseMana { get; } public int OpposingBaseMana { get; }
		internal TurnStartedFact(long s,int t,int c,int n,int health,int maxHealth,int armor,int hand,int deck,int board,
			int opposingHealth,int opposingArmor,int baseMana,int opposingBaseMana) : base(s,t,c,c)
		{ TurnsTaken=n;HeroHealth=health;HeroMaxHealth=maxHealth;HeroArmor=armor;HandCount=hand;DeckCount=deck;FriendlyMinionCount=board;
			OpposingHeroHealth=opposingHealth;OpposingHeroArmor=opposingArmor;BaseMana=baseMana;OpposingBaseMana=opposingBaseMana; }
		internal override string Payload() => FormattableString.Invariant($"{TurnsTaken},{HeroHealth},{HeroMaxHealth},{HeroArmor},{HandCount},{DeckCount},{FriendlyMinionCount},{OpposingHeroHealth},{OpposingHeroArmor},{BaseMana},{OpposingBaseMana}");
	}
	public abstract class HistoryCardFact : HistoryFact
	{
		public int EntityId { get; }
		private readonly HistoryDefinition _definition;
		public bool DefinitionKnown => _definition != null;
		public HistoryDefinition Definition => DefinitionKnown ? _definition
			: throw new NotSupportedException("Private historical card definition is unavailable in a fair view.");
		public int DbfId => Definition.DbfId;
		public CardType CardType { get; }
		public int SpellSchool { get; }
		private readonly Race[] _races;
		public IReadOnlyList<Race> Races => Array.AsReadOnly(_races);
		private readonly bool _startedInDeck, _originKnown;
		public bool OriginKnown => _originKnown;
		public bool StartedInDeck => !_originKnown ? throw new NotSupportedException("Private historical origin is unavailable in a fair view.") : _startedInDeck;
		internal HistoryCardFact(long s, int t, int c, int a, int entity, HistoryDefinition definition, Card card, bool? original)
			: base(s,t,c,a)
		{ EntityId=entity; _definition=definition; CardType=card?.Type??CardType.SPELL; SpellSchool=card?[GameTag.SPELL_SCHOOL]??0; _races=card?.Races.ToArray()??Array.Empty<Race>(); _startedInDeck=original??false; _originKnown=original.HasValue; }
		internal string CardText() => FormattableString.Invariant($"{EntityId},{(DefinitionKnown ? Definition.Data : "?")},{(int)CardType},{SpellSchool},{string.Join("/", _races.Select(r => ((int)r).ToString(CultureInfo.InvariantCulture)))},{(_originKnown ? (_startedInDeck ? 1 : 0) : -1)}");
	}
	public sealed class CardPlayedFact : HistoryCardFact
	{
		public long PlayId { get; }
        public int PaidCost { get; }
        public int HandPosition { get; }
        public int HandCount { get; }
		internal CardPlayedFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long playId,int paidCost,int handPosition=-1,int handCount=0) : base(s,t,c,a,e,d,card,o) { PlayId=playId; PaidCost=paidCost; HandPosition=handPosition; HandCount=handCount; }
		internal override string Payload() => CardText()+","+PlayId.ToString(CultureInfo.InvariantCulture)+","+PaidCost.ToString(CultureInfo.InvariantCulture)+FormattableString.Invariant($",{HandPosition},{HandCount}");
	}
	public sealed class SpellCastFact : HistoryCardFact
	{
		public long CastId { get; }
		public HistoryCastKind CasterKind { get; }
		public HistoryCastOutcome Outcome { get; }
		internal SpellCastFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long id,HistoryCastKind kind,HistoryCastOutcome outcome)
			: base(s,t,c,a,e,d,card,o) { CastId=id; CasterKind=kind; Outcome=outcome; }
		internal override string Payload() => FormattableString.Invariant($"{CardText()},{CastId},{(int)CasterKind},{(int)Outcome}");
	}
	public sealed class MinionSummonedFact : HistoryCardFact
	{
		public long Incarnation { get; }
		public long FromPlayId { get; }
		internal MinionSummonedFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long incarnation,long play)
			: base(s,t,c,a,e,d,card,o) { Incarnation=incarnation; FromPlayId=play; }
		internal override string Payload() => FormattableString.Invariant($"{CardText()},{Incarnation},{FromPlayId}");
	}
	public sealed class MinionDiedFact : HistoryCardFact
	{
		public long Incarnation { get; }
		public long FromPlayId { get; }
		public int PlayedTurn { get; }
		public bool IsTreant { get; }
        public int CostAtDeath { get; }
		public int SourceId { get; } public int SourceControllerId { get; } public HistoryDeathCause Cause { get; }
		public long CastId { get; } public long ResolutionId { get; } public long FatalDamageSequence { get; }
		internal MinionDiedFact(long s,int t,int c,int a,int e,HistoryDefinition d,Card card,bool? o,long incarnation,long play,int playedTurn,bool treant,int costAtDeath,
			int source,int sourceController,HistoryDeathCause cause,long cast,long resolution,long damageSequence)
			: base(s,t,c,a,e,d,card,o) { Incarnation=incarnation;FromPlayId=play;PlayedTurn=playedTurn;IsTreant=treant;CostAtDeath=costAtDeath;
				SourceId=source;SourceControllerId=sourceController;Cause=cause;CastId=cast;ResolutionId=resolution;FatalDamageSequence=damageSequence; }
		internal override string Payload() => FormattableString.Invariant($"{CardText()},{Incarnation},{FromPlayId},{PlayedTurn},{(IsTreant?1:0)},{CostAtDeath},{SourceId},{SourceControllerId},{(int)Cause},{CastId},{ResolutionId},{FatalDamageSequence}");
	}
	public sealed class HandEnteredFact : HistoryFact
	{
		public int EntityId { get; }
		public HistoryHandEntryKind EntryKind { get; }
        public int SourceId { get; }
        public Zone FromZone { get; }
		internal HandEnteredFact(long s,int t,int c,int a,int e,HistoryHandEntryKind k,int source=0,Zone from=Zone.INVALID) : base(s,t,c,a) { EntityId=e; EntryKind=k; SourceId=source; FromZone=from; }
		internal override string Payload() => FormattableString.Invariant($"{EntityId},{(int)EntryKind},{SourceId},{(int)FromZone}");
	}
	/// <summary>One member per successful insertion; IsFirst counts the semantic operation once.</summary>
	public sealed class ShuffleOperationFact : HistoryFact
	{
		public long OperationId { get; }
		public int EntityId { get; }
		public int DestinationControllerId { get; }
		public bool IsFirst { get; }
		internal ShuffleOperationFact(long s,int t,int c,int a,long id,int e,int destination,bool first) : base(s,t,c,a)
		{ OperationId=id; EntityId=e; DestinationControllerId=destination; IsFirst=first; }
		internal override string Payload() => FormattableString.Invariant($"{OperationId},{EntityId},{DestinationControllerId},{(IsFirst?1:0)}");
	}
	public sealed class DiscoverCompletedFact : HistoryFact
	{
		public long OperationId { get; }
		private readonly int[] _selected;
		private readonly HistoryDefinition[] _definitions;
		internal IReadOnlyList<int> SelectionIds => Array.AsReadOnly(_selected);
		public bool EntityIdsKnown => _selected.All(id=>id!=0);
		public IReadOnlyList<int> SelectedEntityIds => EntityIdsKnown ? SelectionIds
			: throw new NotSupportedException("Private Discover selection entity links are unavailable in a fair view.");
		public bool DefinitionsKnown => _definitions != null;
		public IReadOnlyList<HistoryDefinition> SelectedDefinitions => DefinitionsKnown ? Array.AsReadOnly(_definitions)
			: throw new NotSupportedException("Private Discover selection definitions are unavailable in a fair view.");
		public bool KeepAll { get; }
		internal DiscoverCompletedFact(long s,int t,int c,int a,long id,IEnumerable<int> selected,
			HistoryDefinition[] definitions, bool keepAll = false) : base(s,t,c,a)
		{ OperationId=id; _selected=selected.ToArray(); _definitions=definitions; KeepAll=keepAll; }
		internal override string Payload() => FormattableString.Invariant($"{OperationId},{(KeepAll?1:0)},{string.Join("/",_selected.Select(id=>id.ToString(CultureInfo.InvariantCulture)))},{(DefinitionsKnown ? string.Join("/",_definitions.Select(d=>d.Data)) : "?")}");
	}

	public readonly struct HistoryWindow
	{
		internal readonly int? Turn;
        private readonly long _from,_until;
        internal readonly long ExcludedPlayId;
        internal HistoryWindow(int turn) { Turn=turn;_from=0;_until=long.MaxValue;ExcludedPlayId=0; }
        internal HistoryWindow(long from,long until,long excludePlay=0) { Turn=null;_from=from;_until=until;ExcludedPlayId=excludePlay; }
        private HistoryWindow(int? turn,long from,long until,long excludePlay) { Turn=turn;_from=from;_until=until;ExcludedPlayId=excludePlay; }
        public HistoryWindow ExcludingPlay(long playId)=>new HistoryWindow(Turn,_from,_until,playId);
        public bool Contains(long sequence)=>sequence>=_from && sequence<_until;
        internal bool Includes(HistoryFact fact)
        {
            if(Turn.HasValue && fact.Turn!=Turn.Value || !Contains(fact.Sequence)) return false;
            if(ExcludedPlayId==0) return true;
            if(fact is ManaSpentFact payment && payment.RelatedPlayId==ExcludedPlayId) return false;
            if(fact is CardPlayCompletedFact completed && completed.PlayId==ExcludedPlayId) return false;
            return !(fact is CardPlayedFact play && play.PlayId==ExcludedPlayId);
        }
	}
	/// <summary>Read-only queries never enroll consumers. Entity-private queries on a fair view fail explicitly.</summary>
	public sealed partial class HistoryQuery
	{
		private readonly HistoryFacts _store;
		private readonly long _before;
		internal HistoryQuery(HistoryFacts store,long before=long.MaxValue) { _store=store; _before=before; }
		public HistoryWindow ThisTurn => new HistoryWindow(_store.CurrentTurn);
		public HistoryWindow PreviousTurnOf(int controllerId)
		{
			var turns=Events<TurnStartedFact>().Where(f=>f.ControllerId==controllerId).ToArray();
			return new HistoryWindow(turns.Where(f=>f.Turn<_store.CurrentTurn).LastOrDefault()?.Turn ?? -1);
		}
		public int TurnsTaken(int controllerId) => Newest<TurnStartedFact>((f,id)=>f.ControllerId==id,controllerId)?.TurnsTaken ?? 0;
		/// <summary>Ledger/index entries examined by this query object, for deterministic cost diagnostics.</summary>
		public long EntriesExamined { get; private set; }
		public IEnumerable<T> Events<T>(HistoryWindow? window=null) where T:HistoryFact
		{
			// Chunks are immutable: skip those whose kinds or turn range cannot match. A query cost O(match length) per
			// call before this, and the search asks several times per node. The per-fact test below still decides.
			ulong mask=HistoryKinds<T>.Mask; int? turn=window.HasValue?window.Value.Turn:null;
			int c=0;
			foreach(var chunk in _store.PublishedChunks)
			{
				var summary=_store.Summary(c++);
				if((summary.Kinds&mask)==0 || turn.HasValue && (summary.MaxTurn<turn.Value || summary.MinTurn>turn.Value)) { EntriesExamined+=chunk.Length; continue; }
				for(int i=0;i<chunk.Length;i++)
				{
					var fact=chunk[i];
					EntriesExamined++;
					if(fact is T value && fact.Sequence<_before && (!window.HasValue || window.Value.Includes(fact))) yield return value;
				}
			}
			for(int i=0;i<_store.TailCount;i++)
			{
				var fact=_store.TailChunk[i];
				EntriesExamined++;
				if(fact is T value && fact.Sequence<_before && (!window.HasValue || window.Value.Includes(fact))) yield return value;
			}
		}
		public IEnumerable<CardPlayedFact> Plays(HistoryWindow? window=null) => Events<CardPlayedFact>(window);
		public IEnumerable<SpellCastFact> Casts(HistoryWindow? window=null) => Events<SpellCastFact>(window);
		public IEnumerable<MinionSummonedFact> Summons(HistoryWindow? window=null) => Events<MinionSummonedFact>(window);
		public IEnumerable<MinionDiedFact> Deaths(HistoryWindow? window=null) => Events<MinionDiedFact>(window);
		public IEnumerable<HandEnteredFact> HandEntries(HistoryWindow? window=null) => Events<HandEnteredFact>(window);
		public IEnumerable<ShuffleOperationFact> Shuffles(HistoryWindow? window=null) => Events<ShuffleOperationFact>(window).Where(f=>f.IsFirst);
		public IEnumerable<DiscoverCompletedFact> Discoveries(HistoryWindow? window=null) => Events<DiscoverCompletedFact>(window);
		public bool StartedInDeck(int entityId,int ownerId) => _store.StartedInDeck(entityId,ownerId);
		/// <summary>Non-throwing origin read: null where a fair view erased the entity's origin.</summary>
		public bool? KnownOrigin(int entityId,int ownerId) => _store.Origin(entityId,ownerId);
		public HistoryQuery BeforeThisPlay(long playId) => new HistoryQuery(_store,Math.Min(_before,_store.Query.Plays().Single(f=>f.PlayId==playId).Sequence));
		/// <summary>A fair view keeps an entity's entries only when all are fair (own known hand) and appends its own
		/// search entries, so a found latest entry is exact; a missing one may be erased and still fails closed.</summary>
		public HandEnteredFact LatestHandEntry(int entityId)
		{
			var latest=entityId==0 ? null : HandEntries().LastOrDefault(f=>f.EntityId==entityId);
			if(latest==null) _store.RequirePrivateIdentity();
			return latest;
		}
	}

	/// <summary>Append-only 64-slot immutable chunks. Fork copies only pointers and the bounded unpublished tail.</summary>
	public sealed partial class HistoryFacts
	{
		// Derived from the immutable chunks, never saved; a loaded snapshot rebuilds it on the first query.
		[NonSerialized] private List<HistoryChunkSummary> _summaries;
		internal List<HistoryFact[]> PublishedChunks=>_chunks;
		internal HistoryFact[] TailChunk=>_tail;
		internal int TailCount=>_tailCount;
		internal HistoryChunkSummary Summary(int chunk)
		{
			if(_summaries==null) _summaries=new List<HistoryChunkSummary>(_chunks.Count);
			while(_summaries.Count<=chunk)
			{
				var summary=new HistoryChunkSummary { MinTurn=int.MaxValue,MaxTurn=int.MinValue };
				foreach(var fact in _chunks[_summaries.Count])
				{
					summary.Kinds|=HistoryKinds.Of(fact.GetType());
					summary.MinTurn=Math.Min(summary.MinTurn,fact.Turn); summary.MaxTurn=Math.Max(summary.MaxTurn,fact.Turn);
				}
				_summaries.Add(summary);
			}
			return _summaries[chunk];
		}
		private const int ChunkSize=64;
		private int _version=1;
		private List<HistoryFact[]> _chunks=new List<HistoryFact[]>();
		private HistoryFact[] _tail=new HistoryFact[ChunkSize];
		private int _tailCount;
		private long _nextSequence=1;
		internal long NextOperation=1, NextCast=1, NextIncarnation=1;
		internal int CurrentTurn;
		private Dictionary<int,int> _initial=new Dictionary<int,int>();
		private HashSet<int> _unavailableOrigins=new HashSet<int>();
		private string[] _consumers=Array.Empty<string>();
		private HistoryDefinition[] _definitions=Array.Empty<HistoryDefinition>();
		private bool _initialFrozen, _projected;
		internal bool IsProjected => _projected;
		private Dictionary<int,HistoryBoardIdentity> _board=new Dictionary<int,HistoryBoardIdentity>();
		private List<long> _futureTurnOperations=new List<long>();
		private List<long> _currentExtraTurnOperations=new List<long>();
		private long _startedTurnOperation;
		// Live-only verdict: once achievement evidence fails validation the match stays ineligible (sticky across Rewind).
		[NonSerialized] private string _evidenceFailure;
		/// <summary>False when achievement-evidence links/shapes failed during live play; gameplay continued.</summary>
		public bool EvidenceValid => _evidenceFailure==null;
		public string EvidenceFailure => _evidenceFailure;
		/// <summary>Test/diagnostic observer of first invalidations: suites fail a normal game that silently loses its evidence.</summary>
		internal static Action<string> EvidenceInvalidated;
		/// <summary>Marks this match's achievement evidence invalid; returns true only on the first failure.</summary>
		internal bool InvalidateEvidence(string reason)
		{
			if(_evidenceFailure!=null) return false;
			_evidenceFailure=string.IsNullOrEmpty(reason)?"invalid achievement evidence":reason;
			EvidenceInvalidated?.Invoke(_evidenceFailure);return true;
		}
		internal HistoryBoardIdentity Board(int id)=>_board.TryGetValue(id,out var value)?value:null;
		internal void SetBoard(int id,HistoryBoardIdentity value)=>_board[id]=value;
		internal void RemoveBoard(int id)=>_board.Remove(id);
		internal void PrependFutureTurnOperations(IEnumerable<long> operations)
		{
			var added=operations.ToArray();
			if(added.Any(operation=>operation<0)) throw new ArgumentException("Invalid future-turn provenance.");
			_futureTurnOperations.InsertRange(0,added);
		}
		internal long TakeFutureTurnOperation()
		{
			if(_futureTurnOperations.Count==0) throw new InvalidOperationException("Missing future-turn provenance.");
			long operation=_futureTurnOperations[0];_futureTurnOperations.RemoveAt(0);return operation;
		}
		internal void AddCurrentExtraTurnOperation(long operation)
		{
			if(operation<=0) throw new ArgumentException("Invalid current extra-turn provenance.");
			_currentExtraTurnOperations.Add(operation);
		}
		internal long TakeCurrentExtraTurnOperation()
		{
			if(_currentExtraTurnOperations.Count==0) return 0;
			long operation=_currentExtraTurnOperations[0];_currentExtraTurnOperations.RemoveAt(0);return operation;
		}
		internal long[] MoveCurrentExtraTurnOperations(int count)
		{
			if(count<0 || _currentExtraTurnOperations.Count>count) throw new InvalidOperationException("Invalid current extra-turn provenance.");
			var result=new long[count];_currentExtraTurnOperations.CopyTo(result);_currentExtraTurnOperations.Clear();return result;
		}
		internal void MarkStartedTurnOperation(long operation)
		{
			if(operation<0 || _startedTurnOperation!=0) throw new InvalidOperationException("Invalid started-turn provenance.");
			_startedTurnOperation=operation;
		}
		internal long ConsumeStartedTurnOperation()
		{ long operation=_startedTurnOperation;_startedTurnOperation=0;return operation; }
		internal void ValidateFutureTurnOperations(Game game,int ownerCount,bool strictEvidence=true)
		{
			var grants=Records.OfType<ExtraTurnGrantedFact>().Select(f=>f.OperationId).ToHashSet();
			if(_futureTurnOperations==null || _futureTurnOperations.Count!=ownerCount || _startedTurnOperation<0
				|| _currentExtraTurnOperations==null || _currentExtraTurnOperations.Count>Math.Max(0,game.CurrentPlayer.NumTurnsLeft-1)
				|| _currentExtraTurnOperations.Any(operation=>operation<=0 || !grants.Contains(operation))
				|| _futureTurnOperations.Any(operation=>operation<0 || operation!=0 && !grants.Contains(operation))
				|| _startedTurnOperation!=0 && !grants.Contains(_startedTurnOperation))
			{
				// Extra-turn provenance is achievement evidence; TurnQueueRules validates the gameplay queue itself.
				if(strictEvidence) throw new SerializationException("Invalid future-turn provenance.");
				InvalidateEvidence("Invalid future-turn provenance.");
			}
		}
		public bool HasConsumers => _consumers.Length!=0;
		public int Count => checked((int)(_nextSequence-1));
		public HistoryQuery Query => new HistoryQuery(this);
		public IEnumerable<HistoryFact> Records
		{
			get { foreach(var chunk in _chunks) foreach(var fact in chunk) yield return fact;
				for(int i=0;i<_tailCount;i++) yield return _tail[i]; }
		}
        /// <summary>Constant-time size check for bounded consumers; no history enumeration.</summary>
        public long RecordCount => _nextSequence - 1;
		internal long Sequence => _nextSequence;
		internal void Append(HistoryFact fact)
		{
			if(fact.Sequence!=_nextSequence) throw new InvalidOperationException("History sequence mismatch.");
			_indexTail[_tailCount]=IndexRecord(fact);
			_tail[_tailCount++]=fact; _nextSequence++;
			if(_tailCount==ChunkSize) { _chunks.Add(_tail); _tail=new HistoryFact[ChunkSize]; _indexChunks.Add(_indexTail);_indexTail=new HistoryResolutionIndex[ChunkSize];_tailCount=0; }
		}
		internal HistoryDefinition Definition(Card card)
		{
			var existing=_definitions.FirstOrDefault(d=>d.Matches(card));
			if(existing!=null) return existing;
			var definition=new HistoryDefinition(card);
			_definitions=_definitions.Concat(new[]{definition}).ToArray(); return definition;
		}
		internal void Enroll(Card card) { if(card.ConsumesHistory && !_consumers.Contains(card.Id)) _consumers=_consumers.Concat(new[]{card.Id}).ToArray(); }
		internal void FreezeInitial(Game game)
		{
			if(_initialFrozen) return;
			foreach(var p in new[]{game.Player1,game.Player2}) foreach(var e in p.DeckZone) _initial.Add(e.Id,p.Id);
			_initialFrozen=true;
		}
		internal void RequirePrivateIdentity() { if(_projected) throw new NotSupportedException("Private historical identity is unavailable in a fair view."); }
		internal bool WasInitial(int entity)=>_initial.ContainsKey(entity);
		internal bool StartedInDeck(int entity,int owner)
		{
			var origin=Origin(entity,owner);
			return origin ?? throw new NotSupportedException("Private historical origin is unavailable in a fair view.");
		}
		/// <summary>Fair views only: a search copy states its owner-belief origin for entities whose real origin was erased
		/// (sampled own-deck slots, masked cards). Known origins cannot be replaced; authoritative stores reject this.</summary>
		public void AssumeOrigins(IEnumerable<KeyValuePair<int,int>> startedInDeck, IEnumerable<int> foreign)
		{
			if(!_projected) throw new InvalidOperationException("Origin assumptions require a fair view.");
			var initial=startedInDeck?.ToArray() ?? Array.Empty<KeyValuePair<int,int>>();
			var others=foreign?.ToArray() ?? Array.Empty<int>();
			var ids=initial.Select(p=>p.Key).Concat(others).ToArray();
			if(ids.Distinct().Count()!=ids.Length || !ids.All(_unavailableOrigins.Contains))
				throw new InvalidOperationException("Only erased origins can be assumed, once each.");
			if(ids.Length==0) return;
			// Forks share these collections; copy before writing.
			var unavailable=new HashSet<int>(_unavailableOrigins); unavailable.ExceptWith(ids);
			var merged=new Dictionary<int,int>(_initial);
			foreach(var p in initial) merged.Add(p.Key,p.Value);
			_unavailableOrigins=unavailable; _initial=merged;
		}
		internal bool? Origin(int entity,int owner) => _projected && (entity==0 || _unavailableOrigins.Contains(entity))
			? (bool?)null : _initial.TryGetValue(entity,out int c) && c==owner;
		public HistoryFacts Fork() => new HistoryFacts {
			_version=_version, _chunks=new List<HistoryFact[]>(_chunks), _tail=(HistoryFact[])_tail.Clone(), _tailCount=_tailCount,
			_summaries=_summaries==null?null:new List<HistoryChunkSummary>(_summaries),
			_indexChunks=new List<HistoryResolutionIndex[]>(_indexChunks),_indexTail=(HistoryResolutionIndex[])_indexTail.Clone(),_projectionOwner=_projectionOwner,
			_nextSequence=_nextSequence, NextOperation=NextOperation,NextCast=NextCast,NextIncarnation=NextIncarnation,CurrentTurn=CurrentTurn,
			_board=new Dictionary<int,HistoryBoardIdentity>(_board),_futureTurnOperations=new List<long>(_futureTurnOperations),
			_currentExtraTurnOperations=new List<long>(_currentExtraTurnOperations),_startedTurnOperation=_startedTurnOperation,
			_initial=_initialFrozen?_initial:new Dictionary<int,int>(_initial),_consumers=_consumers,
			_definitions=_definitions,_unavailableOrigins=_unavailableOrigins,_initialFrozen=_initialFrozen,_projected=_projected,_evidenceFailure=_evidenceFailure };
		public string CanonicalText()
		{
			var text=new StringBuilder();
			text.AppendFormat(CultureInfo.InvariantCulture,"{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}\n",_version,_nextSequence,NextOperation,NextCast,NextIncarnation,CurrentTurn,_initialFrozen?1:0,_projected?1:0);
			if(_projected)
			{
				text.Append("viewer:").Append(_projectionOwner.ToString(CultureInfo.InvariantCulture)).Append('\n');
				foreach(var id in _unavailableOrigins.OrderBy(id=>id)) text.Append("unknown-origin:").Append(id.ToString(CultureInfo.InvariantCulture)).Append('\n');
			}
			foreach(var c in _consumers.OrderBy(c=>c,StringComparer.Ordinal)) text.Append("consumer:").Append(c).Append('\n');
			foreach(var p in _initial.OrderBy(p=>p.Key)) text.Append("initial:").Append(p.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
			foreach(var p in _board.OrderBy(p=>p.Key)) text.Append("board:").Append(p.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.Incarnation.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.PlayId.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.PlayedTurn.ToString(CultureInfo.InvariantCulture)).Append('\n');
			text.Append("future-turns:").Append(_startedTurnOperation.ToString(CultureInfo.InvariantCulture)).Append(':')
				.Append(string.Join("/",_currentExtraTurnOperations.Select(o=>o.ToString(CultureInfo.InvariantCulture)))).Append(':')
				.Append(string.Join("/",_futureTurnOperations.Select(o=>o.ToString(CultureInfo.InvariantCulture)))).Append('\n');
			foreach(var f in Records) text.Append(f.Text()).Append('\n');
			return text.ToString();
		}
		/// <summary>strictEvidence=false (live Rewind/Sands bytes this process saved): achievement-only fact shapes and causality
		/// links downgrade to <see cref="EvidenceValid"/>=false. Core ledger, residence, payment and privacy checks always throw.</summary>
		internal void Validate(Game game,bool strictEvidence=true)
		{
			void Require(bool ok) { if(!ok) throw new SerializationException("Invalid history facts snapshot."); }
			bool Owner(int id)=>id==game.Player1.Id || id==game.Player2.Id;
			Require(_version==1 && _chunks!=null && _tail!=null && _tail.Length==ChunkSize && _tailCount>=0 && _tailCount<ChunkSize);
			Require(_chunks.All(c=>c!=null && c.Length==ChunkSize) && _tail.Skip(_tailCount).All(f=>f==null));
			Require(_initial!=null && _consumers!=null && _definitions!=null && _initialFrozen && CurrentTurn>=0 && CurrentTurn<=game.Turn);
			Require(_unavailableOrigins!=null && (_projected || _unavailableOrigins.Count==0) && _unavailableOrigins.All(id=>id>0 && game.IdEntityDic.ContainsKey(id)) && !_initial.Keys.Any(_unavailableOrigins.Contains));
			Require(_initial.All(p=>Owner(p.Value) && game.IdEntityDic.ContainsKey(p.Key)) && _consumers.All(c=>!string.IsNullOrEmpty(c)));
			Require(_definitions.All(d=>d!=null) && _definitions.Distinct().Count()==_definitions.Length);
			var cards=new Dictionary<HistoryDefinition,Card>();
			foreach(var d in _definitions) { d.Validate(); cards.Add(d,d.ReadDefinition()); }
			Require(_board!=null && _board.All(p=>game.IdEntityDic.TryGetValue(p.Key,out var entity) && entity is Entities.Minion
				&& entity.Zone==entity.Controller.BoardZone && p.Value!=null && p.Value.Incarnation>0 && p.Value.Incarnation<NextIncarnation));
			var plays=new Dictionary<long,CardPlayedFact>(); var casts=new Dictionary<long,SpellCastFact>();
			var completedPlays=new HashSet<long>();
            var playSummons=new HashSet<long>(); var summons=new Dictionary<long,MinionSummonedFact>(); var operations=new HashSet<long>(); var dead=new HashSet<long>(); var turns=new Dictionary<int,int>();
			long sequence=1; int lastTurn=0, active=0;
			// Achievement-only parts of gameplay facts: strict loads reject them, live Rewind/Sands downgrades to invalid evidence.
			void Evidence(bool ok,HistoryFact fact)
			{
				if(ok) return;
				if(strictEvidence) Require(false);
				InvalidateEvidence(FormattableString.Invariant($"Invalid history evidence shape: {fact.GetType().Name}@{fact.Sequence}."));
			}
			foreach(var f in Records)
			{
				Require(f!=null && f.Sequence==sequence++ && Owner(f.ControllerId) && (Owner(f.ActiveControllerId)||f.ActiveControllerId==0) && f.Turn>=0 && f.Turn<=game.Turn);
				if(f is TurnStartedFact begun) Require(begun.ActiveControllerId==begun.ControllerId && begun.HeroHealth>0 && begun.HeroMaxHealth>0
					&& begun.HeroArmor>=0 && begun.HandCount>=0 && begun.DeckCount>=0 && begun.FriendlyMinionCount>=0 && begun.FriendlyMinionCount<=Game.MAX_MINIONS_ON_BOARD
					&& begun.OpposingHeroHealth>0 && begun.OpposingHeroArmor>=0 && begun.BaseMana>=0 && begun.OpposingBaseMana>=0);
				else Require(lastTurn==0
					? f.Turn<=1 && (f.Turn==0 && f.ActiveControllerId==0 || f.ActiveControllerId==game.FirstPlayer.Id)
					: f.Turn==lastTurn && f.ActiveControllerId==active);
				if(f is HistoryCardFact card) Require(card.DefinitionKnown
					? _definitions.Contains(card.Definition) && (card.EntityId==0 && _projected || card.EntityId>0 && game.IdEntityDic.ContainsKey(card.EntityId)) && cards.TryGetValue(card.Definition,out var definition) && card.CardType==definition.Type && card.SpellSchool==definition[GameTag.SPELL_SCHOOL] && card.Races.SequenceEqual(definition.Races) && (_projected || card.OriginKnown && card.StartedInDeck==(_initial.TryGetValue(card.EntityId,out int owner) && owner==card.ControllerId))
					: _projected && card.ControllerId!=_projectionOwner && card.EntityId==0 && !card.OriginKnown && card.CardType==CardType.SPELL && card.SpellSchool==0 && card.Races.Count==0 && (card is CardPlayedFact || card is SpellCastFact || card is CardTransferredFact));
				if(f is TurnStartedFact turn) { turns.TryGetValue(turn.ControllerId,out int n); Require(turn.Turn>lastTurn && turn.TurnsTaken==n+1); turns[turn.ControllerId]=n+1; lastTurn=turn.Turn; active=turn.ControllerId; }
				else if(f is CardPlayedFact play) { Require(play.HandCount>=0 && play.HandPosition>=-1 && play.HandPosition<Math.Max(1,play.HandCount) && play.PaidCost>=0 && play.PlayId>0 && play.PlayId<=play.Sequence); plays.Add(play.PlayId,play); }
				else if(f is SpellCastFact cast)
				{
					Require(cast.CardType==CardType.SPELL && cast.CastId>0 && cast.CastId<NextCast && Enum.IsDefined(typeof(HistoryCastKind),cast.CasterKind));
					if(cast.Outcome==HistoryCastOutcome.Started) { Require(!casts.ContainsKey(cast.CastId)); casts.Add(cast.CastId,cast); }
					else { Require((cast.Outcome==HistoryCastOutcome.Completed||cast.Outcome==HistoryCastOutcome.Countered) && casts.TryGetValue(cast.CastId,out var start) && start.Outcome==HistoryCastOutcome.Started && start.EntityId==cast.EntityId && start.CasterKind==cast.CasterKind && start.ControllerId==cast.ControllerId && start.DefinitionKnown==cast.DefinitionKnown && (!cast.DefinitionKnown || start.Definition.Data==cast.Definition.Data)); casts[cast.CastId]=cast; }
				}
				else if(f is MinionSummonedFact summon)
				{ Require(summon.CardType==CardType.MINION && summon.Incarnation>0 && summon.Incarnation<NextIncarnation && !summons.ContainsKey(summon.Incarnation));
				  Require(summon.FromPlayId==0 || plays.TryGetValue(summon.FromPlayId,out var played) && played.EntityId==summon.EntityId && played.CardType==CardType.MINION && playSummons.Add(summon.FromPlayId)); summons.Add(summon.Incarnation,summon); }
				else if(f is MinionDiedFact death)
				{ Require(death.CostAtDeath>=0 && death.CardType==CardType.MINION && death.Incarnation>0 && death.Incarnation<NextIncarnation && death.PlayedTurn>=0 && death.PlayedTurn<=death.Turn && dead.Add(death.Incarnation));
				  Require(!summons.TryGetValue(death.Incarnation,out var body) || body.EntityId==death.EntityId);
				  Require(death.FromPlayId==0 || plays.TryGetValue(death.FromPlayId,out var played) && played.EntityId==death.EntityId && played.Turn==death.PlayedTurn);
				  Require(Enum.IsDefined(typeof(HistoryDeathCause),death.Cause) && (death.Cause==HistoryDeathCause.None
					? death.SourceId==0 && death.SourceControllerId==0 && death.CastId==0 && death.ResolutionId==0 && death.FatalDamageSequence==0
					: (death.SourceId==0 || game.IdEntityDic.ContainsKey(death.SourceId)) && (death.SourceControllerId==0 || Owner(death.SourceControllerId))
						&& death.ResolutionId>=0 && death.CastId>=0 && death.CastId<NextCast
						&& (death.Cause==HistoryDeathCause.Destroy ? death.FatalDamageSequence==0 : death.FatalDamageSequence>0 && death.FatalDamageSequence<death.Sequence))); }
				else if(f is CardPlayCompletedFact completed) Require(plays.TryGetValue(completed.PlayId,out var prior)
                    && prior.ControllerId==completed.ControllerId && completedPlays.Add(completed.PlayId) && prior.Sequence<completed.Sequence);
                else if(f is CardDiscardedFact) { }
                // Reborn feeds Raith van Geist (main checked only its type); its incarnation and entity links are evidence.
                else if(f is MinionRebornFact reborn) { Require(reborn.CardType==CardType.MINION);
                    Evidence(reborn.SourceIncarnation>0 && reborn.SourceIncarnation<NextIncarnation && reborn.ReturnedIncarnation>0 && reborn.ReturnedIncarnation<NextIncarnation
                        && (reborn.ReturnedEntityId==0 && _projected || game.IdEntityDic.ContainsKey(reborn.ReturnedEntityId)),f); }
                // Awakened has no gameplay reader: evidence only.
                else if(f is MinionAwakenedFact awakened) Evidence(awakened.CardType==CardType.MINION && awakened.Incarnation>0 && awakened.Incarnation<NextIncarnation
                    && (awakened.SourceId==0 || game.IdEntityDic.ContainsKey(awakened.SourceId)) && (awakened.SourceControllerId==0 || Owner(awakened.SourceControllerId)) && awakened.ResolutionId>=0,f);
                else if(f is CardTransferredFact transfer) Require(transfer.TransferId>0 && transfer.TransferId<NextOperation && Owner(transfer.FromControllerId)
                    && transfer.FromControllerId!=transfer.ControllerId && (transfer.FromZone==Zone.HAND || transfer.FromZone==Zone.DECK)
                    && (transfer.SourceId==0 || game.IdEntityDic.ContainsKey(transfer.SourceId)) && (transfer.SourceControllerId==0 || Owner(transfer.SourceControllerId))
                    && transfer.CastId>=0 && transfer.CastId<NextCast && transfer.ResolutionId>=0);
                else if(f is HandEnteredFact hand) Require((hand.SourceId==0 || game.IdEntityDic.ContainsKey(hand.SourceId))
                    && Enum.IsDefined(typeof(Zone),hand.FromZone) && (hand.EntityId==0 && _projected || game.IdEntityDic.ContainsKey(hand.EntityId)) && Enum.IsDefined(typeof(HistoryHandEntryKind),hand.EntryKind));
				else if(f is ShuffleOperationFact shuffle)
				{ Require(shuffle.OperationId>0 && shuffle.OperationId<NextOperation && Owner(shuffle.DestinationControllerId) && (shuffle.EntityId==0 && _projected || game.IdEntityDic.ContainsKey(shuffle.EntityId)));
				  Require(shuffle.IsFirst ? operations.Add(shuffle.OperationId) : operations.Contains(shuffle.OperationId)); }
				else if(f is DiscoverCompletedFact discover) Require(discover.OperationId>0 && discover.OperationId<NextOperation && operations.Add(discover.OperationId) && (discover.SelectionIds.Count>0 || discover.KeepAll && (!discover.DefinitionsKnown && _projected || discover.SelectedDefinitions.Count>0)) && discover.SelectionIds.All(e=>e==0 && _projected || game.IdEntityDic.ContainsKey(e)) && (discover.DefinitionsKnown ? discover.SelectedDefinitions.Count>0 && discover.SelectedDefinitions.All(d=>d!=null && cards.ContainsKey(d)) : _projected));
				else if(!ValidateResolutionFact(f,game))
				{
					// Residence and payments feed gameplay indexes and fair-view privacy.
					if(strictEvidence || f is ResidenceChangedFact || f is ManaSpentFact) Require(false);
					InvalidateEvidence(FormattableString.Invariant($"Invalid history evidence shape: {f.GetType().Name}@{f.Sequence}."));
				}
			}
			var records=Records.ToArray();
			ValidateResidenceLinks(game,records);
			if(strictEvidence) ValidateResolutionLinks(game,records);
			else if(EvidenceValid)
			{
				try { ValidateResolutionLinks(game,records); }
				catch(Exception e) when (!(e is OutOfMemoryException)) { InvalidateEvidence(e.GetType().Name+": "+e.Message); }
			}
			Require(_projected?Owner(_projectionOwner):_projectionOwner==0);
			// Hard, as on main: the index feeds gameplay (death counts, held-mana and cost rules), like CardPlayCompleted and open casts.
			RebuildResolutionIndex();
			foreach(var entry in _board.Values)
				Require(entry.PlayId==0 && entry.PlayedTurn==0 || plays.TryGetValue(entry.PlayId,out var played) && played.Turn==entry.PlayedTurn);
			foreach(var player in new[]{game.Player1,game.Player2})
			{
				foreach(var minion in player.BoardZone) Require(_board.ContainsKey(minion.Id));
				for(var choice=player.Choice;choice!=null;choice=choice.NextChoice)
				{
					Require(choice.HistoryDiscoverId==0 || choice.HistoryDiscoverId>0 && choice.HistoryDiscoverId<NextOperation && !operations.Contains(choice.HistoryDiscoverId));
					Require(!choice.HistoryOptionResolution || choice.ChoiceAction==ChoiceAction.CAST && choice.HistoryDiscoverId==0);
				}
			}
			Require(_nextSequence==sequence && CurrentTurn==lastTurn && NextOperation>0 && NextCast>0 && NextIncarnation>0 && casts.Values.All(c=>c.Outcome!=HistoryCastOutcome.Started));
		}
		/// <summary>Retain visible origins and own event-time play origins; erase hidden current identity links.</summary>
		public HistoryFacts ProjectForFairView(Game game, int ownerId, IReadOnlyCollection<int> publiclyKnownHand = null)
		{
			var owner = game.Player1.PlayerId==ownerId ? game.Player1 : game.Player2;
			var knownHand = new HashSet<int>(owner.HandZone.Select(e=>e.Id));
            // Caller supplies ids reconstructed from public observations, never arbitrary hidden identities.
            var publicHand = new HashSet<int>(publiclyKnownHand ?? Array.Empty<int>());
            publicHand.IntersectWith(owner.Opponent.HandZone.Select(e=>e.Id));
            knownHand.UnionWith(publicHand);
			// Bound display offers expose the same own-DECK identities as direct picks; retain their residence links too.
			var offered = new HashSet<int>(owner.Choice?.Choices ?? new List<int>());
			foreach(int id in offered.ToArray())
			{
				int bound=game.IdEntityDic[id][Actions.DiscoverRealRules.BoundEntity];
				if(bound!=0) offered.Add(bound);
			}
			var offers = owner.DeckZone.Where(e=>offered.Contains(e.Id)).ToArray();
			var hidden = new HashSet<int>(game.Player1.DeckZone.Concat(game.Player2.DeckZone).Except(offers)
				.Concat(owner.Opponent.HandZone.Where(e=>!publicHand.Contains(e.Id)))
				.Concat(owner.Opponent.SecretZone.Where(e=>!e.IsQuest && !e.IsAura && e[GameTag.REVEALED]==0))
				.Concat(owner.Opponent.SetasideZone.Where(e=>e.Card.IsSecret && e[GameTag.REVEALED]==0)).Select(e=>e.Id));
			var visibleEntities = new[]{game.Player1,game.Player2}.SelectMany(p=>p.BoardZone.Slots
				.Concat(p.GraveyardZone)
				.Concat(new Entities.IPlayable[]{p.Hero,p.Hero.HeroPower,p.Hero.Weapon}.Where(e=>e!=null))
				.Concat(p.SecretZone.Where(e=>p==owner || e.IsQuest || e.IsAura || e[GameTag.REVEALED]!=0))
				.Concat(p.SetasideZone.Where(e=>e[GameTag.REVEALED]!=0)))
				.Concat(owner.HandZone).Concat(owner.Opponent.HandZone.Where(e=>publicHand.Contains(e.Id))).Concat(offers).ToArray();
			var visible = new HashSet<int>(visibleEntities.Select(e=>e.Id)
				.Concat(Records.OfType<HistoryCardFact>().Where(f=>!hidden.Contains(f.EntityId)).Select(f=>f.EntityId)));
            visible.ExceptWith(hidden);
			// A face-down play is not a public appearance, including for Discover and consumer indexes.
			bool PrivateDefinition(HistoryCardFact f) => !f.DefinitionKnown || f.ControllerId!=owner.Id && !(f is CardDiscardedFact) && hidden.Contains(f.EntityId)
				&& (f.Definition.ReadDefinition().IsSecret || owner.Opponent.SecretZone.Any(e=>e.Id==f.EntityId && !e.IsQuest && !e.IsAura));
            var publicCards = Records.OfType<HistoryCardFact>().Where(f=>!PrivateDefinition(f)).ToLookup(f=>f.EntityId);
			bool KnownSelection(DiscoverCompletedFact discover) => discover.DefinitionsKnown && (discover.ControllerId==owner.Id
				|| discover.SelectionIds.Count==discover.SelectedDefinitions.Count && discover.SelectionIds
					.Zip(discover.SelectedDefinitions,(id,definition)=>publicCards[id].Any(f=>f.Sequence>discover.Sequence && f.Definition.Data==definition.Data)).All(known=>known));
			var publicDefinitions = new HashSet<string>(Records.OfType<HistoryCardFact>().Where(f=>!PrivateDefinition(f)).Select(f=>f.Definition.ReadDefinition().Id)
				.Concat(visibleEntities.Select(e=>e.Card.Id)));
			var result=new HistoryFacts { NextOperation=NextOperation,NextCast=NextCast,NextIncarnation=NextIncarnation,
				CurrentTurn=CurrentTurn,_initialFrozen=_initialFrozen,_projected=true,_projectionOwner=owner.Id,_board=new Dictionary<int,HistoryBoardIdentity>(_board),
				_futureTurnOperations=new List<long>(_futureTurnOperations),_currentExtraTurnOperations=new List<long>(_currentExtraTurnOperations),
				_startedTurnOperation=_startedTurnOperation,
				_consumers=_consumers,_definitions=_definitions,
				_initial=_initial.Where(p=>visible.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
				_unavailableOrigins=new HashSet<int>(game.IdEntityDic.Select(pair=>pair.Key).Where(id=>!visible.Contains(id))) };
			foreach(var f in Records)
			{
				HistoryFact projected=ProjectResolutionFact(f,hidden,owner.Id);
				if(f is HistoryCardFact card && (hidden.Contains(card.EntityId) || !card.DefinitionKnown))
				{
					var retained=PrivateDefinition(card)?null:card.Definition;
					var definition=retained?.ReadDefinition();
					bool? origin=card.ControllerId==owner.Id && card.OriginKnown ? card.StartedInDeck : (bool?)null;
					if(card is CardPlayedFact) projected=new CardPlayedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,((CardPlayedFact)card).PlayId,((CardPlayedFact)card).PaidCost,((CardPlayedFact)card).HandPosition,((CardPlayedFact)card).HandCount);
					else if(card is SpellCastFact cast) projected=new SpellCastFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,cast.CastId,cast.CasterKind,cast.Outcome);
					else if(card is MinionSummonedFact summon) projected=new MinionSummonedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,summon.Incarnation,summon.FromPlayId);
					else if(card is CardDiscardedFact) projected=new CardDiscardedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin);
                    else if(card is MinionRebornFact reborn) projected=new MinionRebornFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,
                        reborn.SourceIncarnation,hidden.Contains(reborn.ReturnedEntityId)?0:reborn.ReturnedEntityId,reborn.ReturnedIncarnation);
                    else if(card is MinionAwakenedFact awakened) projected=new MinionAwakenedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,
                        awakened.Incarnation,hidden.Contains(awakened.SourceId)?0:awakened.SourceId,awakened.SourceControllerId,awakened.ResolutionId);
                    else if(card is CardTransferredFact transfer) projected=new CardTransferredFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,
                        transfer.ControllerId==owner.Id?retained:null,transfer.ControllerId==owner.Id?definition:null,origin,transfer.TransferId,transfer.FromControllerId,transfer.FromZone,
                        hidden.Contains(transfer.SourceId)?0:transfer.SourceId,transfer.SourceControllerId,transfer.CastId,transfer.ResolutionId);
                    else if(card is MinionDiedFact death) projected=new MinionDiedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,0,retained,definition,origin,death.Incarnation,death.FromPlayId,death.PlayedTurn,death.IsTreant,death.CostAtDeath,
                        hidden.Contains(death.SourceId)?0:death.SourceId,death.SourceControllerId,death.Cause,death.CastId,death.ResolutionId,death.FatalDamageSequence);
				}
				if(f is HandEnteredFact hand) projected=new HandEnteredFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,knownHand.Contains(hand.EntityId)?hand.EntityId:0,hand.EntryKind,hidden.Contains(hand.SourceId)?0:hand.SourceId,hand.FromZone);
				if(f is ShuffleOperationFact shuffle) projected=new ShuffleOperationFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,shuffle.OperationId,0,shuffle.DestinationControllerId,shuffle.IsFirst);
				if(f is DiscoverCompletedFact discover)
					projected=new DiscoverCompletedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,discover.OperationId,
						discover.SelectionIds.Select(id=>!hidden.Contains(id) && (discover.ControllerId==owner.Id || publicCards[id].Any())?id:0),
						KnownSelection(discover) ? discover.SelectedDefinitions.ToArray() : null,
						discover.KeepAll);
				result.Append(projected);
			}
			result._definitions = result.Records.OfType<HistoryCardFact>().Where(f=>f.DefinitionKnown).Select(f=>f.Definition)
				.Concat(result.Records.OfType<AttackStartedFact>().Select(f=>f.AttackerDefinition))
				.Concat(result.Records.OfType<CombatStartedFact>().Select(f=>f.AttackerDefinition))
				.Concat(result.Records.OfType<MinionFormChangedFact>().SelectMany(f=>new[]{f.OldDefinition,f.NewDefinition}))
				.Concat(result.Query.Discoveries().Where(f=>f.DefinitionsKnown).SelectMany(f=>f.SelectedDefinitions)).Distinct().ToArray();
			publicDefinitions.UnionWith(result.Query.Discoveries().Where(f=>f.DefinitionsKnown)
				.SelectMany(f=>f.SelectedDefinitions).Select(d=>d.ReadDefinition().Id));
			result._consumers=_consumers.Where(publicDefinitions.Contains).ToArray();
			return result;
		}
	}
}

// Outside the snapshot schema's discovered namespaces: a new type in SabberStoneCore.Model would renumber published type IDs.
namespace SabberStoneCore.Model.HistoryIndex
{
	// One bit per concrete fact type, for the chunk summaries. There are fewer than 64 types; a shared bit would only skip less.
	internal static class HistoryKinds
	{
		private static readonly Dictionary<System.Type,ulong> Bits=Build();
		private static Dictionary<System.Type,ulong> Build()
		{
			var bits=new Dictionary<System.Type,ulong>(); int next=0;
			foreach(var type in typeof(HistoryFact).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(HistoryFact).IsAssignableFrom(t)).OrderBy(t=>t.FullName,StringComparer.Ordinal))
				bits[type]=1UL<<(next++%64);
			return bits;
		}
		internal static ulong Of(System.Type type) => Bits.TryGetValue(type,out var bit)?bit:ulong.MaxValue;
		internal static ulong MaskFor(System.Type target)
		{
			ulong mask=0;
			foreach(var pair in Bits) if(target.IsAssignableFrom(pair.Key)) mask|=pair.Value;
			return mask;
		}
	}
	internal static class HistoryKinds<T> { internal static readonly ulong Mask=HistoryKinds.MaskFor(typeof(T)); }
	internal struct HistoryChunkSummary { internal ulong Kinds; internal int MinTurn,MaxTurn; }
}
