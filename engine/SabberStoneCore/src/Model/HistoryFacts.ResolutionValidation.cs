using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    public sealed partial class HistoryFacts
    {
        private bool ValidateResolutionFact(HistoryFact fact,Game game)
        {
            bool Entity(int id)=>id==0 && _projected || id>0 && game.IdEntityDic.ContainsKey(id);
            bool OptionalEntity(int id)=>id==0||Entity(id);
            bool Owner(int id)=>id==game.Player1.Id || id==game.Player2.Id;
            bool OptionalOwner(int id)=>id==0||Owner(id);
            bool Character(CardType type)=>type==CardType.HERO || type==CardType.MINION;
            if(fact is DamageResolvedHistoryFact d)
                return Entity(d.SourceId) && Enum.IsDefined(typeof(CardType),d.SourceKind) && d.CastId>=0 && d.CastId<NextCast && d.AttackId>=0
                    && d.ResolutionId>=0 && Enum.IsDefined(typeof(HistoryDamageCause),d.Cause)
                    && (d.Cause==HistoryDamageCause.Combat)==(d.AttackId!=0)
                    && (d.CastId==0 || d.SourceKind==CardType.SPELL) && Entity(d.VictimId) && Owner(d.VictimControllerId)
                    && Character(d.VictimType) && d.ArmorDamage>=0 && d.HealthDamage>=0 && (long)d.ArmorDamage+d.HealthDamage>0
                    && (long)d.ArmorDamage+d.HealthDamage<=int.MaxValue && (d.VictimType==CardType.HERO || d.ArmorDamage==0);
            if(fact is HeroHealthChangedFact h)
                return Entity(h.HeroId) && h.OldHealth!=h.NewHealth && Enum.IsDefined(typeof(HistoryHealthChangeReason),h.Reason);
            if(fact is ArmorGainedFact armor) return Entity(armor.EntityId) && armor.Amount>0;
            if(fact is AttackHistoryFact attack)
                return attack.AttackId>0 && attack.AttackId<=attack.Sequence && Entity(attack.AttackerId) && Character(attack.AttackerType)
                    && Entity(attack.TargetId) && Owner(attack.TargetControllerId) && Character(attack.TargetType)
                    && Enum.IsDefined(typeof(HistoryAttackOrigin),attack.Origin)
                    && (!(attack is AttackStartedFact started) || started.AttackDamage>=0 && started.AttackerIncarnation>=0 && started.TargetPreHitHealth>0
                        && started.TargetPreHitArmor>=0 && started.ResolutionId>=0 && started.AttackerDefinition!=null && _definitions.Contains(started.AttackerDefinition));
            if(fact is ManaSpentFact payment)
                return Entity(payment.EntityId) && payment.Mana>=0 && (!payment.SpentLastMana || payment.Mana>0) && payment.Health>=0 && (payment.Health==0 || payment.Mana==0)
                    && Enum.IsDefined(typeof(HistoryPaymentKind),payment.ActionKind)
                    && (payment.ActionKind==HistoryPaymentKind.Play ? payment.RelatedPlayId==payment.Sequence : payment.RelatedPlayId==0);
            if(fact is ResidenceChangedFact residence)
                return Entity(residence.EntityId) && (residence.Zone==Zone.HAND || residence.Zone==Zone.DECK);
            if(fact is ControlChangedFact control) return Entity(control.EntityId) && control.FromIncarnation>0 && control.ToIncarnation>0
                && control.FromIncarnation!=control.ToIncarnation && Owner(control.FromControllerId) && Owner(control.ToControllerId)
                && control.FromControllerId!=control.ToControllerId && OptionalEntity(control.SourceId) && OptionalOwner(control.SourceControllerId)
                && control.CastId>=0 && control.CastId<NextCast && control.ResolutionId>=0;
            if(fact is CardBurnedFact burn) return Entity(burn.CardId) && OptionalEntity(burn.SourceId) && OptionalOwner(burn.SourceControllerId)
                && burn.CastId>=0 && burn.CastId<NextCast && burn.ResolutionId>=0 && Enum.IsDefined(typeof(HistoryBurnReason),burn.Reason);
            if(fact is KeywordAppliedFact keyword) return Entity(keyword.TargetId) && OptionalEntity(keyword.SourceId) && OptionalOwner(keyword.SourceControllerId)
                && keyword.CastId>=0 && keyword.CastId<NextCast && keyword.ResolutionId>=0 && keyword.ChangedFromFalse && Enum.IsDefined(typeof(HistoryKeyword),keyword.Keyword);
            if(fact is CorpseSpentFact corpse) return corpse.Amount>0 && OptionalEntity(corpse.SourceId) && OptionalOwner(corpse.SourceControllerId)
                && corpse.CastId>=0 && corpse.CastId<NextCast && corpse.ResolutionId>=0;
            if(fact is DeathrattleResolvedFact deathrattle) return Entity(deathrattle.EntityId) && deathrattle.Incarnation>=0
                && deathrattle.Incarnation<NextIncarnation && OptionalEntity(deathrattle.EffectSourceId) && deathrattle.ResolutionId>=0;
            if(fact is SpellCounteredFact counter) return Entity(counter.SpellEntityId) && counter.BaseCost>=0 && Owner(counter.CasterControllerId)
                && Entity(counter.CounterSourceId) && Owner(counter.CounterControllerId) && counter.CasterControllerId!=counter.CounterControllerId
                && counter.CastId>0 && counter.CastId<NextCast && counter.ResolutionId>=0;
            if(fact is ExtraTurnGrantedFact grant) return grant.OperationId>0 && grant.OperationId<NextOperation && OptionalEntity(grant.SourceId)
                && OptionalOwner(grant.SourceControllerId) && grant.CastId>=0 && grant.CastId<NextCast && grant.ResolutionId>=0;
            if(fact is ExtraTurnStartedFact startedTurn) return startedTurn.OperationId>0 && startedTurn.OperationId<NextOperation
                && OptionalEntity(startedTurn.GrantSourceId) && startedTurn.GrantResolutionId>=0;
            if(fact is ComboActivatedFact combo) return Entity(combo.CardEntityId) && combo.PlayId>0 && combo.ResolutionId>=0;
            if(fact is MaxManaChangedFact mana) return mana.OldMaximum>=0 && mana.NewMaximum>=0 && mana.OldMaximum!=mana.NewMaximum && !mana.Temporary
                && OptionalEntity(mana.SourceId) && OptionalOwner(mana.SourceControllerId) && mana.ResolutionId>=0;
            if(fact is HeroFatalFact fatal) return Entity(fatal.HeroId) && OptionalEntity(fatal.SourceId) && OptionalOwner(fatal.SourceControllerId)
                && fatal.Cause!=HistoryDeathCause.None && Enum.IsDefined(typeof(HistoryDeathCause),fatal.Cause)
                && fatal.CastId>=0 && fatal.CastId<NextCast && fatal.ResolutionId>=0
                && (fatal.Cause==HistoryDeathCause.Destroy ? fatal.FatalDamageSequence==0 : fatal.FatalDamageSequence>0 && fatal.FatalDamageSequence<fatal.Sequence);
            if(fact is SpellBodyStartedFact body) return body.CastId>0 && body.CastId<NextCast && Entity(body.SpellEntityId) && body.ResolutionId>=0;
            if(fact is MinionLeftPlayFact left) return Entity(left.EntityId) && left.Incarnation>0 && left.Incarnation<NextIncarnation
                && OptionalEntity(left.SourceId) && OptionalOwner(left.SourceControllerId) && left.CastId>=0 && left.CastId<NextCast && left.ResolutionId>=0
                && Enum.IsDefined(typeof(HistoryBoardLeaveReason),left.Reason);
            if(fact is MinionFormChangedFact form) return Entity(form.OldEntityId) && Entity(form.NewEntityId)
                && form.OldIncarnation>0 && form.OldIncarnation<NextIncarnation && form.NewIncarnation>0 && form.NewIncarnation<NextIncarnation
                && form.OldDefinition!=null && form.NewDefinition!=null && _definitions.Contains(form.OldDefinition) && _definitions.Contains(form.NewDefinition)
                && OptionalEntity(form.SourceId) && OptionalOwner(form.SourceControllerId) && form.CastId>=0 && form.CastId<NextCast && form.ResolutionId>=0;
            if(fact is MatchTerminalFact terminal) return terminal.CurrentControllerId==terminal.ControllerId && Owner(terminal.Player1Id) && Owner(terminal.Player2Id)
                && terminal.Player1Id!=terminal.Player2Id && Enum.IsDefined(typeof(PlayState),terminal.Player1State) && Enum.IsDefined(typeof(PlayState),terminal.Player2State)
                && terminal.Player1Health<=terminal.Player1MaxHealth && terminal.Player2Health<=terminal.Player2MaxHealth
                && terminal.Player1Armor>=0 && terminal.Player2Armor>=0 && terminal.Player1Hand>=0 && terminal.Player2Hand>=0
                && terminal.Player1Deck>=0 && terminal.Player2Deck>=0 && terminal.Player1Board>=0 && terminal.Player1Board<=Game.MAX_MINIONS_ON_BOARD
                && terminal.Player2Board>=0 && terminal.Player2Board<=Game.MAX_MINIONS_ON_BOARD;
            if(fact is PlayerActionFact action) return Enum.IsDefined(typeof(HistoryPlayerActionKind),action.Kind)
                && OptionalEntity(action.SourceId) && OptionalEntity(action.TargetId)
                && (action.Kind==HistoryPlayerActionKind.PlayCard ? action.RelatedPlayId>0 : action.RelatedPlayId==0)
                && (action.SourceId!=0 || _projected);
            if(fact is CombatStartedFact combat) return combat.AttackId>0 && combat.AttackId<combat.Sequence && Entity(combat.AttackerId)
                && Character(combat.AttackerType) && Entity(combat.TargetId) && Owner(combat.TargetControllerId) && Character(combat.TargetType)
                && combat.AttackDamage>=0 && combat.AttackerIncarnation>=0 && combat.AttackerDefinition!=null && _definitions.Contains(combat.AttackerDefinition)
                && combat.TargetPreHitHealth>0 && combat.TargetPreHitArmor>=0 && combat.ResolutionId>=0;
            return false;
        }
        /// <summary>Hand/deck residence feeds gameplay indexes and fair-view privacy; always fails closed.</summary>
        private void ValidateResidenceLinks(Game game,HistoryFact[] records)
        {
            void Require(bool value) { if(!value) throw new SerializationException("Invalid history residence links."); }
            var residence=new Dictionary<int,ResidenceChangedFact>();
            foreach(var f in records)
                if(f is ResidenceChangedFact r && (!_projected || r.EntityId!=0))
                {
                    if(r.Entered) { Require(!residence.ContainsKey(r.EntityId));residence.Add(r.EntityId,r); }
                    else { Require(residence.TryGetValue(r.EntityId,out var entry) && entry.Zone==r.Zone && entry.ControllerId==r.ControllerId);residence.Remove(r.EntityId); }
                }
            foreach(var entry in residence.Values)
            {
                Require(game.IdEntityDic.TryGetValue(entry.EntityId,out var entity));
                Require(entity.Zone!=null && entity.Zone.Type==entry.Zone && entity.Controller.Id==entry.ControllerId);
            }
            if(!_projected)
            {
                foreach(var player in new[]{game.Player1,game.Player2})
                    foreach(var entity in player.HandZone.Concat(player.DeckZone)) Require(residence.ContainsKey(entity.Id));
            }
        }
        /// <summary>Achievement-evidence causality links. Live Rewind/Sands validation downgrades a failure to invalid evidence.</summary>
        private void ValidateResolutionLinks(Game game,HistoryFact[] records)
        {
            void Require(bool value) { if(!value) throw new SerializationException("Invalid history resolution links."); }
            bool PaymentDelta(HeroHealthChangedFact change, ManaSpentFact paid) => paid.Health > 0
                && (change.ControllerId == paid.ControllerId
                    ? (long)change.OldHealth - change.NewHealth == paid.Health
                        || change.ActiveControllerId == change.ControllerId && (long)change.NewHealth - change.OldHealth == paid.Health
                    : paid.ActionKind == HistoryPaymentKind.Play && paid.Health <= 10 && change.ActiveControllerId == paid.ControllerId
                        && (long)change.OldHealth - change.NewHealth == paid.Health);
            var casts=records.OfType<SpellCastFact>().Where(f=>f.Outcome==HistoryCastOutcome.Started).ToDictionary(f=>f.CastId);
            var castEnds=records.OfType<SpellCastFact>().Where(f=>f.Outcome!=HistoryCastOutcome.Started).ToDictionary(f=>f.CastId);
            // Sequence n is records[n-1]; a fatal hit is looked up by its sequence instead of scanning all damage.
            DamageResolvedHistoryFact DamageAt(long sequence)=>sequence>=1 && sequence<=records.Length ? records[sequence-1] as DamageResolvedHistoryFact : null;
            bool CastEncloses(long castId,long sequence)=>castId==0 || casts.TryGetValue(castId,out var castStart)
                && castEnds.TryGetValue(castId,out var castEnd) && castStart.Sequence<sequence && sequence<castEnd.Sequence;
            var plays=records.OfType<CardPlayedFact>().ToDictionary(f=>f.PlayId);
            var payments=records.OfType<ManaSpentFact>().Where(f=>f.ActionKind==HistoryPaymentKind.Play).ToDictionary(f=>f.RelatedPlayId);
            var attacks=new Dictionary<long,AttackStartedFact>();var combats=new Dictionary<long,CombatStartedFact>();var finished=new HashSet<long>();
            var extraTurns=new Dictionary<long,ExtraTurnGrantedFact>();var startedExtraTurns=new HashSet<long>();
            var board=new Dictionary<int,(long Incarnation,int Controller)>();
            var deaths=new Dictionary<long,MinionDiedFact>();
            var transfers=new HashSet<long>();
            var actionPlays=new HashSet<long>();
            foreach(var f in records)
            {
                if(f is DamageResolvedHistoryFact d)
                {
                    Require(CastEncloses(d.CastId,d.Sequence));
                    Require(d.AttackId==0 || combats.TryGetValue(d.AttackId,out var damageCombat) && damageCombat.Sequence<d.Sequence
                        && (damageCombat.AttackerId==d.SourceId || damageCombat.TargetId==d.SourceId));
                    if(d.VictimType==CardType.HERO && d.HealthDamage>0)
                        Require(d.Sequence<records.Length && records[d.Sequence] is HeroHealthChangedFact h && h.Reason==HistoryHealthChangeReason.Damage
                            && h.HeroId==d.VictimId && h.ControllerId==d.VictimControllerId && h.OldHealth==d.PreHitHealth && (long)h.OldHealth-h.NewHealth==d.HealthDamage);
                }
                if(f is HeroHealthChangedFact health && health.Reason==HistoryHealthChangeReason.Damage)
                    Require(health.Sequence>1 && records[health.Sequence-2] is DamageResolvedHistoryFact damage && damage.HealthDamage>0);
                if(f is AttackStartedFact start) { Require(start.AttackId==start.Sequence);attacks.Add(start.AttackId,start); }
                if(f is CombatStartedFact combat) Require(attacks.TryGetValue(combat.AttackId,out var declared) && declared.Sequence<combat.Sequence
                    && declared.AttackerId==combat.AttackerId && declared.ControllerId==combat.ControllerId && declared.AttackerType==combat.AttackerType
                    && declared.Turn==combat.Turn && declared.ActiveControllerId==combat.ActiveControllerId && combats.TryAdd(combat.AttackId,combat));
                if(f is AttackFinishedFact end)
                    Require(attacks.TryGetValue(end.AttackId,out var begun) && finished.Add(end.AttackId) && begun.Sequence<end.Sequence
                        && begun.AttackerId==end.AttackerId && begun.ControllerId==end.ControllerId && begun.AttackerType==end.AttackerType && begun.Origin==end.Origin
                        && (!end.ReachedCombat && !combats.ContainsKey(end.AttackId) || end.ReachedCombat && combats.TryGetValue(end.AttackId,out var finishedCombat)
                            && finishedCombat.TargetId==end.TargetId && finishedCombat.TargetControllerId==end.TargetControllerId && finishedCombat.TargetType==end.TargetType)
                        && begun.Turn==end.Turn && begun.ActiveControllerId==end.ActiveControllerId);
                if(f is HeroHealthChangedFact paidHealth && paidHealth.Reason==HistoryHealthChangeReason.Payment)
                    Require(paidHealth.Sequence<records.Length && records[paidHealth.Sequence] is ManaSpentFact paid
                        && PaymentDelta(paidHealth, paid));
                if(f is ManaSpentFact healthPayment && healthPayment.Health>0)
                    Require(healthPayment.Sequence>1 && records[healthPayment.Sequence-2] is HeroHealthChangedFact change
                        && change.Reason==HistoryHealthChangeReason.Payment && PaymentDelta(change, healthPayment));
                if(f is ManaSpentFact payment && payment.ActionKind==HistoryPaymentKind.Play)
                    Require(plays.TryGetValue(payment.RelatedPlayId,out var play) && play.Sequence>payment.Sequence && play.EntityId==payment.EntityId && play.ControllerId==payment.ControllerId && play.Turn==payment.Turn);
                if(f is CardPlayedFact played)
                    Require(payments.TryGetValue(played.PlayId,out var paid) && paid.EntityId==played.EntityId);
                if(f is SpellBodyStartedFact body) Require(CastEncloses(body.CastId,body.Sequence) && casts.TryGetValue(body.CastId,out var bodyCast)
                    && bodyCast.EntityId==body.SpellEntityId);
                if(f is SpellCounteredFact counter) Require(casts.TryGetValue(counter.CastId,out var counterCast) && counterCast.Sequence<counter.Sequence
                    && counterCast.EntityId==counter.SpellEntityId && counterCast.ControllerId==counter.CasterControllerId);
                if(f is ComboActivatedFact combo) Require(plays.TryGetValue(combo.PlayId,out var comboPlay) && comboPlay.Sequence<combo.Sequence && comboPlay.EntityId==combo.CardEntityId);
                // Records run in sequence order, so deaths holds exactly the earlier deaths (incarnations are unique).
                if(f is DeathrattleResolvedFact resolved && resolved.Incarnation != 0) Require(deaths.TryGetValue(resolved.Incarnation,out var resolvedDeath)
                    && resolvedDeath.EntityId==resolved.EntityId);
                if(f is ExtraTurnGrantedFact grant) {Require(extraTurns.TryAdd(grant.OperationId,grant));}
                if(f is ExtraTurnStartedFact started) Require(extraTurns.TryGetValue(started.OperationId,out var priorGrant) && priorGrant.Sequence<started.Sequence
                    && priorGrant.ControllerId==started.ControllerId && startedExtraTurns.Add(started.OperationId));

                long linkedCast=f switch {
                    ControlChangedFact x=>x.CastId,CardBurnedFact x=>x.CastId,KeywordAppliedFact x=>x.CastId,CorpseSpentFact x=>x.CastId,
                    SpellCounteredFact x=>x.CastId,ExtraTurnGrantedFact x=>x.CastId,HeroFatalFact x=>x.CastId,MinionLeftPlayFact x=>x.CastId,
                    MinionFormChangedFact x=>x.CastId,CardTransferredFact x=>x.CastId,MinionDiedFact x=>x.CastId,_=>0 };
                // Lethal damage belongs to its cast; death cleanup may run after that cast completes.
                long linkedSequence = f is MinionDiedFact death && death.FatalDamageSequence > 0 ? death.FatalDamageSequence : f.Sequence;
                if (f is MinionDiedFact destroyed && destroyed.Cause == HistoryDeathCause.Destroy && linkedCast != 0)
                    Require(casts.TryGetValue(linkedCast,out var destroyCast) && castEnds.TryGetValue(linkedCast,out var destroyEnd)
                        && destroyCast.EntityId == destroyed.SourceId && destroyCast.ControllerId == destroyed.SourceControllerId
                        && destroyCast.Sequence < destroyed.Sequence && destroyEnd.Outcome == HistoryCastOutcome.Completed);
                else Require(CastEncloses(linkedCast,linkedSequence));

                if(f is MinionSummonedFact summoned && (!_projected || summoned.EntityId!=0))
                { Require(!board.ContainsKey(summoned.EntityId));board.Add(summoned.EntityId,(summoned.Incarnation,summoned.ControllerId)); }
                if(f is ControlChangedFact control && (!_projected || control.EntityId!=0))
                {
                    Require(board.TryGetValue(control.EntityId,out var prior) && prior.Incarnation==control.FromIncarnation && prior.Controller==control.FromControllerId);
                    board[control.EntityId]=(control.ToIncarnation,control.ToControllerId);
                }
                if(f is MinionLeftPlayFact left && (!_projected || left.EntityId!=0))
                { Require(board.TryGetValue(left.EntityId,out var prior) && prior.Incarnation==left.Incarnation && prior.Controller==left.ControllerId);board.Remove(left.EntityId); }
                if(f is MinionDiedFact died)
                {
                    deaths.Add(died.Incarnation,died);
                    if(!_projected || died.EntityId!=0)
                    { Require(board.TryGetValue(died.EntityId,out var prior) && prior.Incarnation==died.Incarnation && prior.Controller==died.ControllerId);board.Remove(died.EntityId); }
                    if(died.Cause is HistoryDeathCause.Combat or HistoryDeathCause.SpellOrEffect or HistoryDeathCause.Poisonous)
                    {
                        var fatal=DamageAt(died.FatalDamageSequence);
                        // A hero's Poisonous weapon is the poison source (Character.PoisonousDamageSource) while the hero deals the hit.
                        bool weaponPoison=died.Cause==HistoryDeathCause.Poisonous && fatal!=null && fatal.Cause==HistoryDamageCause.Combat
                            && fatal.SourceKind==CardType.HERO && died.SourceId!=0 && game.IdEntityDic.TryGetValue(died.SourceId,out var poisonWeapon)
                            && poisonWeapon is Entities.Weapon && poisonWeapon.Controller?.Id==fatal.ControllerId;
                        Require(fatal!=null && fatal.VictimId==died.EntityId && (fatal.SourceId==died.SourceId || weaponPoison) && fatal.ControllerId==died.SourceControllerId
                            && fatal.CastId==died.CastId && fatal.ResolutionId==died.ResolutionId && (died.Cause!=HistoryDeathCause.Combat || fatal.Cause==HistoryDamageCause.Combat));
                    }
                }
                if(f is MinionFormChangedFact form && (!_projected || form.OldEntityId!=0 && form.NewEntityId!=0))
                {
                    Require(board.TryGetValue(form.OldEntityId,out var prior) && prior.Incarnation==form.OldIncarnation && prior.Controller==form.ControllerId);
                    board.Remove(form.OldEntityId);Require(!board.ContainsKey(form.NewEntityId));board.Add(form.NewEntityId,(form.NewIncarnation,form.ControllerId));
                }
                if(f is MinionAwakenedFact awakened && (!_projected || awakened.EntityId!=0))
                    Require(board.TryGetValue(awakened.EntityId,out var prior) && prior.Incarnation==awakened.Incarnation && prior.Controller==awakened.ControllerId);
                if(f is MinionRebornFact reborn && (!_projected || reborn.ReturnedEntityId!=0))
                    Require(deaths.TryGetValue(reborn.SourceIncarnation,out var sourceDeath) && sourceDeath.Sequence<reborn.Sequence
                        && board.TryGetValue(reborn.ReturnedEntityId,out var returned) && returned.Incarnation==reborn.ReturnedIncarnation && returned.Controller==reborn.ControllerId);
                if(f is CardTransferredFact transfer)
                {
                    Require(transfers.Add(transfer.TransferId) && transfer.Sequence>1 && records[transfer.Sequence-2] is HandEnteredFact entered
                        && entered.EntityId==transfer.EntityId && entered.ControllerId==transfer.ControllerId && entered.FromZone==transfer.FromZone);
                }
                if(f is HeroFatalFact heroFatal && heroFatal.Cause!=HistoryDeathCause.Destroy)
                {
                    var fatal=DamageAt(heroFatal.FatalDamageSequence);
                    Require(fatal!=null && fatal.VictimId==heroFatal.HeroId && fatal.SourceId==heroFatal.SourceId && fatal.ControllerId==heroFatal.SourceControllerId
                        && fatal.CastId==heroFatal.CastId && fatal.ResolutionId==heroFatal.ResolutionId && (heroFatal.Cause!=HistoryDeathCause.Combat || fatal.Cause==HistoryDamageCause.Combat));
                }
                if(f is PlayerActionFact action)
                {
                    if(!_projected || action.SourceId!=0)
                    {
                        var source=game.IdEntityDic[action.SourceId];
                        Require(action.Kind switch {
                            HistoryPlayerActionKind.MinionAttack=>source is Entities.Minion,
                            HistoryPlayerActionKind.HeroAttack=>source is Entities.Hero,
                            HistoryPlayerActionKind.HeroPower=>source is Entities.HeroPower,
                            HistoryPlayerActionKind.ActivateLocation=>source is Entities.BoardObject or Entities.Minion,
                            HistoryPlayerActionKind.ActivateOldGod=>source is Entities.Minion,
                            _=>source is Entities.IPlayable });
                    }
                    if(action.Kind==HistoryPlayerActionKind.PlayCard)
                        Require(plays.TryGetValue(action.RelatedPlayId,out var actionPlay)
                            && actionPlay.EntityId==action.SourceId && actionPlay.ControllerId==action.ControllerId && actionPlays.Add(action.RelatedPlayId)
                            && action.Sequence>1 && records[action.Sequence-2] is ManaSpentFact playPayment
                            && playPayment.RelatedPlayId==action.RelatedPlayId && playPayment.ActionKind==HistoryPaymentKind.Play);
                    else if(action.Kind is HistoryPlayerActionKind.HeroPower or HistoryPlayerActionKind.TradeCard or HistoryPlayerActionKind.PrepareCard)
                        Require(action.Sequence>1 && records[action.Sequence-2] is ManaSpentFact actionPayment
                            && actionPayment.EntityId==action.SourceId && actionPayment.ControllerId==action.ControllerId
                            && actionPayment.ActionKind==(action.Kind==HistoryPlayerActionKind.HeroPower ? HistoryPaymentKind.HeroPower
                                : action.Kind==HistoryPlayerActionKind.TradeCard ? HistoryPaymentKind.Trade : HistoryPaymentKind.Prepare));
                    else if(action.Kind is HistoryPlayerActionKind.MinionAttack or HistoryPlayerActionKind.HeroAttack)
                        Require(action.Sequence<records.Length && records[action.Sequence] is AttackStartedFact acceptedAttack
                            && acceptedAttack.AttackerId==action.SourceId && acceptedAttack.ControllerId==action.ControllerId
                            && acceptedAttack.TargetId==action.TargetId && acceptedAttack.Origin==HistoryAttackOrigin.Player);
                }
            }
            Require(attacks.Count==finished.Count);
            Require(board.Count==_board.Count && board.All(pair=>_board.TryGetValue(pair.Key,out var current) && current.Incarnation==pair.Value.Incarnation
                && game.IdEntityDic[pair.Key].Controller.Id==pair.Value.Controller));
        }
        private HistoryFact ProjectResolutionFact(HistoryFact f,HashSet<int> hidden,int owner)
        {
            int Visible(int id)=>hidden.Contains(id)?0:id;
            if(f is ResidenceChangedFact r) return new ResidenceChangedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,r.ControllerId==owner?Visible(r.EntityId):0,r.Zone,r.Entered);
            if(f is ManaSpentFact p) return new ManaSpentFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(p.EntityId),p.Mana,p.Health,p.ActionKind,p.RelatedPlayId,p.SpentLastMana);
            if(f is DamageResolvedHistoryFact d) return new DamageResolvedHistoryFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(d.SourceId),d.SourceKind,d.CastId,d.AttackId,d.ResolutionId,d.Cause,Visible(d.VictimId),d.VictimControllerId,d.VictimType,d.ArmorDamage,d.HealthDamage,d.PreHitHealth);
            if(f is HeroHealthChangedFact h) return new HeroHealthChangedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(h.HeroId),h.OldHealth,h.NewHealth,h.Reason);
            if(f is ArmorGainedFact a) return new ArmorGainedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(a.EntityId),a.Amount);
            if(f is AttackStartedFact s) return new AttackStartedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(s.AttackerId),s.AttackerType,Visible(s.TargetId),s.TargetControllerId,s.TargetType,s.Origin,
                s.AttackDamage,s.AttackerIncarnation,s.AttackerDefinition,s.TargetPreHitHealth,s.TargetPreHitArmor,s.ResolutionId);
            if(f is AttackFinishedFact e) return new AttackFinishedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,e.AttackId,Visible(e.AttackerId),e.AttackerType,Visible(e.TargetId),e.TargetControllerId,e.TargetType,e.Origin,e.ReachedCombat);
            if(f is MinionDiedFact death) return new MinionDiedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(death.EntityId),death.Definition,
                death.Definition.ReadDefinition(),death.OriginKnown?death.StartedInDeck:(bool?)null,death.Incarnation,death.FromPlayId,death.PlayedTurn,death.IsTreant,death.CostAtDeath,
                Visible(death.SourceId),death.SourceControllerId,death.Cause,death.CastId,death.ResolutionId,death.FatalDamageSequence);
            if(f is ControlChangedFact control) return new ControlChangedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(control.EntityId),control.FromIncarnation,control.ToIncarnation,
                control.FromControllerId,control.ToControllerId,Visible(control.SourceId),control.SourceControllerId,control.CastId,control.ResolutionId);
            if(f is CardBurnedFact burn) return new CardBurnedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(burn.CardId),Visible(burn.SourceId),burn.SourceControllerId,burn.CastId,burn.ResolutionId,burn.Reason);
            if(f is KeywordAppliedFact keyword) return new KeywordAppliedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(keyword.TargetId),Visible(keyword.SourceId),keyword.SourceControllerId,keyword.CastId,keyword.ResolutionId,keyword.Keyword,keyword.ChangedFromFalse);
            if(f is CorpseSpentFact corpse) return new CorpseSpentFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,corpse.Amount,Visible(corpse.SourceId),corpse.SourceControllerId,corpse.CastId,corpse.ResolutionId);
            if(f is DeathrattleResolvedFact deathrattle) return new DeathrattleResolvedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(deathrattle.EntityId),deathrattle.Incarnation,Visible(deathrattle.EffectSourceId),deathrattle.ResolutionId);
            if(f is SpellCounteredFact counter) return new SpellCounteredFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(counter.SpellEntityId),counter.BaseCost,counter.CasterControllerId,Visible(counter.CounterSourceId),counter.CounterControllerId,counter.CastId,counter.ResolutionId);
            if(f is ExtraTurnGrantedFact grant) return new ExtraTurnGrantedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,grant.OperationId,Visible(grant.SourceId),grant.SourceControllerId,grant.CastId,grant.ResolutionId);
            if(f is ExtraTurnStartedFact started) return new ExtraTurnStartedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,started.OperationId,Visible(started.GrantSourceId),started.GrantResolutionId);
            if(f is ComboActivatedFact combo) return new ComboActivatedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(combo.CardEntityId),combo.PlayId,combo.ResolutionId);
            if(f is MaxManaChangedFact mana) return new MaxManaChangedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,mana.OldMaximum,mana.NewMaximum,mana.Temporary,Visible(mana.SourceId),mana.SourceControllerId,mana.ResolutionId);
            if(f is HeroFatalFact fatal) return new HeroFatalFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(fatal.HeroId),Visible(fatal.SourceId),fatal.SourceControllerId,fatal.Cause,fatal.CastId,fatal.ResolutionId,fatal.FatalDamageSequence);
            if(f is SpellBodyStartedFact body) return new SpellBodyStartedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,body.CastId,Visible(body.SpellEntityId),body.ResolutionId);
            if(f is MinionLeftPlayFact left) return new MinionLeftPlayFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(left.EntityId),left.Incarnation,Visible(left.SourceId),left.SourceControllerId,left.CastId,left.ResolutionId,left.Reason);
            if(f is MinionFormChangedFact form) return new MinionFormChangedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(form.OldEntityId),form.OldIncarnation,form.OldDefinition,
                Visible(form.NewEntityId),form.NewIncarnation,form.NewDefinition,Visible(form.SourceId),form.SourceControllerId,form.CastId,form.ResolutionId);
            if(f is MinionAwakenedFact awakened) return new MinionAwakenedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,Visible(awakened.EntityId),awakened.Definition,
                awakened.Definition.ReadDefinition(),awakened.OriginKnown?awakened.StartedInDeck:(bool?)null,awakened.Incarnation,Visible(awakened.SourceId),awakened.SourceControllerId,awakened.ResolutionId);
            if(f is CardTransferredFact transfer)
            {
                bool known=transfer.DefinitionKnown && !hidden.Contains(transfer.EntityId);var definition=known?transfer.Definition:null;
                return new CardTransferredFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,known?transfer.EntityId:0,definition,definition?.ReadDefinition(),
                    transfer.ControllerId==owner && transfer.OriginKnown?transfer.StartedInDeck:(bool?)null,transfer.TransferId,transfer.FromControllerId,transfer.FromZone,
                    Visible(transfer.SourceId),transfer.SourceControllerId,transfer.CastId,transfer.ResolutionId);
            }
            if(f is PlayerActionFact action) return new PlayerActionFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,action.Kind,
                Visible(action.SourceId),Visible(action.TargetId),action.RelatedPlayId);
            if(f is CombatStartedFact combat) return new CombatStartedFact(f.Sequence,f.Turn,f.ControllerId,f.ActiveControllerId,combat.AttackId,
                Visible(combat.AttackerId),combat.AttackerType,Visible(combat.TargetId),combat.TargetControllerId,combat.TargetType,combat.AttackDamage,
                combat.AttackerIncarnation,combat.AttackerDefinition,combat.TargetPreHitHealth,combat.TargetPreHitArmor,combat.ResolutionId);
            return f;
        }
    }
}
