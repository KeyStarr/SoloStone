using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    // Derived pages are immutable after publication; Fork shares them, never a writable builder.
    internal sealed class HistoryPaymentPrefix
    {
        internal readonly int ControllerId,Mana,Plays,TwoManaPlays,Deaths,Turns;
        internal HistoryPaymentPrefix(int controller,int mana=0,int plays=0,int two=0,int deaths=0,int turns=0)
        { ControllerId=controller;Mana=mana;Plays=plays;TwoManaPlays=two;Deaths=deaths;Turns=turns; }
    }
    internal sealed class HistoryResidenceProgress
    {
        internal readonly ResidenceChangedFact Entry;
        internal readonly HistoryResidenceProgress Previous;
        internal readonly long Exit;
        internal readonly int EntryMana,ExitMana,OwnPlayMana;
        internal HistoryResidenceProgress(ResidenceChangedFact entry,HistoryResidenceProgress previous,long exit,int entryMana,int exitMana,int own)
        { Entry=entry;Previous=previous;Exit=exit;EntryMana=entryMana;ExitMana=exitMana;OwnPlayMana=own; }
    }
    internal sealed class HistoryEntityProgress
    {
        internal readonly HistoryResidenceProgress Hand,Deck;
        internal readonly int Lotus1,Lotus2,OwnLotus1,OwnLotus2;
        internal HistoryEntityProgress(HistoryResidenceProgress hand=null,HistoryResidenceProgress deck=null,int lotus1=0,int lotus2=0,int own1=0,int own2=0)
        { Hand=hand;Deck=deck;Lotus1=lotus1;Lotus2=lotus2;OwnLotus1=own1;OwnLotus2=own2; }
        internal HistoryEntityProgress Residence(Zone zone,HistoryResidenceProgress value)=>new HistoryEntityProgress(
            zone==Zone.HAND?value:Hand,zone==Zone.DECK?value:Deck,Lotus1,Lotus2,OwnLotus1,OwnLotus2);
        internal HistoryEntityProgress Play(bool first,bool own)=>new HistoryEntityProgress(Hand,Deck,
            Lotus1+(first?1:0),Lotus2+(first?0:1),OwnLotus1+(first && own?1:0),OwnLotus2+(!first && own?1:0));
        internal bool Resident(int owner)=>(Hand!=null && Hand.Exit==0 && Hand.Entry.ControllerId==owner)
            || (Deck!=null && Deck.Exit==0 && Deck.Entry.ControllerId==owner);
    }
    internal sealed class HistoryResolutionIndex
    {
        internal readonly HistoryEntityProgress[][] Pages;
        internal readonly int[] Active;
        internal readonly HistoryPaymentPrefix First,Second;
        internal HistoryResolutionIndex(HistoryEntityProgress[][] pages=null,int[] active=null,HistoryPaymentPrefix first=null,HistoryPaymentPrefix second=null)
        { Pages=pages??Array.Empty<HistoryEntityProgress[]>();Active=active??Array.Empty<int>();First=first;Second=second; }
        internal HistoryEntityProgress Entity(int id)=>id>0 && id/64<Pages.Length?Pages[id/64]?[id%64]:null;
        internal HistoryPaymentPrefix Prefix(int owner)=>First?.ControllerId==owner?First:Second?.ControllerId==owner?Second:null;
    }
    public sealed partial class HistoryFacts
    {
        // Rebuilt into fresh pages only after snapshot facts validate; derived state is not serialized.
        [NonSerialized] private List<HistoryResolutionIndex[]> _indexChunks=new List<HistoryResolutionIndex[]>();
        [NonSerialized] private HistoryResolutionIndex[] _indexTail=new HistoryResolutionIndex[ChunkSize];
        private int _projectionOwner;
        internal int ResidenceEntity(Entities.IPlayable entity,Zone zone,bool entered)
        {
            if(!_projected) return entity.Id;
            if(entity is Entities.Unknown || entity.Controller.Id!=_projectionOwner) return 0;
            if(entered) return entity.Id;
            // A newly visible actor cannot recover its erased residence across a viewer change or reveal.
            var progress=IndexAt(Count)?.Entity(entity.Id);
            var residence=zone==Zone.HAND?progress?.Hand:progress?.Deck;
            return residence!=null && residence.Exit==0 && residence.Entry.ControllerId==entity.Controller.Id?entity.Id:0;
        }
        internal HistoryFact RecordAt(long sequence)
        {
            if(sequence<1 || sequence>Count) throw new ArgumentOutOfRangeException(nameof(sequence));
            long offset=sequence-1;int chunk=(int)(offset/ChunkSize),slot=(int)(offset%ChunkSize);
            return chunk<_chunks.Count?_chunks[chunk][slot]:_tail[slot];
        }
        internal HistoryResolutionIndex IndexAt(long sequence)
        {
            if(sequence<=0) return null;
            long offset=sequence-1;int chunk=(int)(offset/ChunkSize),slot=(int)(offset%ChunkSize);
            return chunk<_indexChunks.Count?_indexChunks[chunk][slot]:_indexTail[slot];
        }
        internal void RequireResidenceIdentity(int entity,HistoryEntityProgress progress)
        {
            if(_projected && (entity==0 || progress==null
                || progress.Hand?.Entry.ControllerId!=_projectionOwner && progress.Deck?.Entry.ControllerId!=_projectionOwner))
                throw new NotSupportedException("Private historical residence is unavailable in a fair view.");
        }
        private HistoryResolutionIndex IndexRecord(HistoryFact fact)
        {
            var previous=IndexAt(fact.Sequence-1)??new HistoryResolutionIndex();
            if(!(fact is ResidenceChangedFact) && !(fact is ManaSpentFact) && !(fact is CardPlayedFact) && !(fact is MinionDiedFact) && !(fact is TurnStartedFact)) return previous;
            var first=previous.First;var second=previous.Second;
            if(previous.Prefix(fact.ControllerId)==null)
            {
                if(first==null) first=new HistoryPaymentPrefix(fact.ControllerId);
                else if(second==null) second=new HistoryPaymentPrefix(fact.ControllerId);
                else throw new InvalidOperationException("History has more than two payment controllers.");
            }
            bool isFirst=first.ControllerId==fact.ControllerId;
            var prefix=isFirst?first:second;
            var pages=previous.Pages;var active=previous.Active;
            HashSet<int> changed=null;
            void Put(int id,HistoryEntityProgress progress)
            {
                if(changed==null) {pages=(HistoryEntityProgress[][])pages.Clone();changed=new HashSet<int>();}
                int page=id/64;
                if(page>=pages.Length) Array.Resize(ref pages,page+1);
                if(changed.Add(page)) pages[page]=pages[page]==null?new HistoryEntityProgress[64]:(HistoryEntityProgress[])pages[page].Clone();
                pages[page][id%64]=progress;
            }
            HistoryEntityProgress Get(int id)=>id>0 && id/64<pages.Length?pages[id/64]?[id%64]:null;
            if(fact is ResidenceChangedFact r && r.EntityId!=0)
            {
                var entity=Get(r.EntityId)??new HistoryEntityProgress();
                var old=r.Zone==Zone.HAND?entity.Hand:entity.Deck;
                if(!r.Entered && (old==null || old.Exit!=0 || old.Entry.ControllerId!=r.ControllerId))
                    throw new InvalidOperationException("Identified residence exit requires a matching open entry.");
                var value=r.Entered?new HistoryResidenceProgress(r,old,0,prefix.Mana,0,0)
                    :new HistoryResidenceProgress(old.Entry,old.Previous,r.Sequence,old.EntryMana,prefix.Mana,old.OwnPlayMana);
                Put(r.EntityId,entity.Residence(r.Zone,value));
                active=r.Entered?active.Concat(new[]{r.EntityId}).ToArray():active.Where(id=>id!=r.EntityId).ToArray();
            }
            if(fact is ManaSpentFact payment)
            {
                prefix=new HistoryPaymentPrefix(prefix.ControllerId,prefix.Mana+payment.Mana,prefix.Plays,prefix.TwoManaPlays,prefix.Deaths,prefix.Turns);
                var entity=Get(payment.EntityId);var hand=entity?.Hand;
                if(payment.ActionKind==HistoryPaymentKind.Play && hand!=null && hand.Exit==0 && hand.Entry.ControllerId==payment.ControllerId)
                    Put(payment.EntityId,entity.Residence(Zone.HAND,new HistoryResidenceProgress(hand.Entry,hand.Previous,0,hand.EntryMana,0,hand.OwnPlayMana+payment.Mana)));
            }
            if(fact is CardPlayedFact play)
            {
                var paid=(ManaSpentFact)RecordAt(play.PlayId);
                bool two=paid.Mana==2 && paid.Health==0;
                prefix=new HistoryPaymentPrefix(prefix.ControllerId,prefix.Mana,prefix.Plays+1,prefix.TwoManaPlays+(two?1:0),prefix.Deaths,prefix.Turns);
                if(two)
                {
                    // Membership is measured at payment, publication only after the linked play commits.
                    var atPayment=IndexAt(paid.Sequence);
                    foreach(int id in atPayment.Active)
                        if(atPayment.Entity(id).Resident(play.ControllerId))
                            Put(id,Get(id).Play(isFirst,id==play.EntityId));
                }
            }
            if(fact is MinionDiedFact || fact is TurnStartedFact)
                prefix=new HistoryPaymentPrefix(prefix.ControllerId,prefix.Mana,prefix.Plays,prefix.TwoManaPlays,
                    prefix.Deaths+(fact is MinionDiedFact?1:0),fact is TurnStartedFact turn?turn.TurnsTaken:prefix.Turns);
            return new HistoryResolutionIndex(pages,active,isFirst?prefix:first,isFirst?second:prefix);
        }
        private void RebuildResolutionIndex()
        {
            _indexChunks=new List<HistoryResolutionIndex[]>();_indexTail=new HistoryResolutionIndex[ChunkSize];
            int slot=0;
            foreach(var fact in Records)
            {
                _indexTail[slot++]=IndexRecord(fact);
                if(slot==ChunkSize) {_indexChunks.Add(_indexTail);_indexTail=new HistoryResolutionIndex[ChunkSize];slot=0;}
            }
        }
    }
    public sealed partial class HistoryQuery
    {
        /// <summary>Committed friendly deaths before this query's cursor; constant index work.</summary>
        public int FriendlyDeathCount(int controllerId) => ResolutionIndex()?.Prefix(controllerId)?.Deaths ?? 0;

        /// <summary>Owner starts in the latest hand residence, including a closed residence at resolution.</summary>
        public int OwnerTurnsStartedWhileHolding(int entityId,int controllerId)
        {
            var index=ResolutionIndex();var hand=ResidenceProgress(index,entityId)?.Hand;
            if(hand==null || hand.Entry.ControllerId!=controllerId) return 0;
            EntriesExamined++;
            int entry=_store.IndexAt(hand.Entry.Sequence)?.Prefix(controllerId)?.Turns??0;
            if(hand.Exit!=0) { EntriesExamined++;index=_store.IndexAt(hand.Exit); }
            return (index?.Prefix(controllerId)?.Turns??0)-entry;
        }

        private HistoryResolutionIndex ResolutionIndex()
        { EntriesExamined++;return _store.IndexAt(Math.Min(_before-1,_store.Count)); }
        private HistoryEntityProgress ResidenceProgress(HistoryResolutionIndex index,int entity)
        {
            EntriesExamined++;
            var progress=index?.Entity(entity);_store.RequireResidenceIdentity(entity,progress);return progress;
        }
    }
}
