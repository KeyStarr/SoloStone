using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    [Flags]
    /// <summary>
    /// ShowOwner / ShowAll: the stored entity is the target of a client entity reference (see
    /// <see cref="PayloadRules.ReferenceId"/>). Only then is it sent named to its host controller / to both
    /// players; every other stored entity stays private.
    /// </summary>
    public enum PayloadPolicy { Copy = 1, Silence = 2, HandExit = 4, Resummon = 8, ShowOwner = 16, ShowAll = 32 }

    /// <summary>Ordered, private offboard entities. All links and policy live in serialized tags.</summary>
    public static class PayloadRules
    {
        public const GameTag Owner = (GameTag)32300, Key = (GameTag)32301, Policy = (GameTag)32302,
            Link = (GameTag)32303, Generation = (GameTag)32304, Value = (GameTag)32305,
            Recipient = (GameTag)32306, Stored = (GameTag)32307;
        public const GameTag CopySourceController = (GameTag)32308, HeldCopyPlayed = (GameTag)32309,
            Tracking = (GameTag)32310;
        public const int WeaponSpell = 18055;
        public static int WeaponSpellDbfId(IPlayable weapon)
        {
            if (weapon.Card.Id != "BE_055t" || weapon[GameTag.SILENCED] != 0) return 0;
            var entries = Entries(weapon, WeaponSpell);
            return entries.Length == 1 && entries[0] is Spell && entries[0].Card.Implemented
                ? entries[0].Card.AssetId : 0;
        }
        /// <summary>Entity reference (tag 2) of the Hammer's spell; related-card tags 1078/1452 keep the dbfId.</summary>
        public static int WeaponSpellReference(IPlayable weapon) => WeaponSpellDbfId(weapon) == 0 ? 0 : Entries(weapon, WeaponSpell)[0].Id;
        public static void CastWeaponSpell(IPlayable weapon)
        {
            int dbfId = WeaponSpellDbfId(weapon);
            if (dbfId == 0 || weapon.Controller.Hero.Weapon != weapon || weapon.Game.State == State.COMPLETE) return;
            var spell = Entity.FromCard(weapon.Controller, Entries(weapon, WeaponSpell).Single().Card,
                new EntityData { [GameTag.CREATOR] = weapon.Id, [GameTag.DISPLAYED_CREATOR] = weapon.Id });
            AutoPlayRules.Play(spell, weapon);
        }
        internal static void WeaponRemoved(Weapon weapon)
        {
            if (weapon?.Card.Id == "BE_055t") Clear(weapon);
        }
        public const GameTag CoinReplacement = (GameTag)32318;
        private const int GeneratedDecision = 18018;
        internal static void CaptureGeneratedDecision(Controller c, Card definition)
        {
            if (definition.Id == "CATA_470t1" || definition.Id == "JAIL_EVENT_100t") StoreCard(c, GeneratedDecision, definition, 0);
        }
        internal static Card DecisionDefinition(Controller c, int dbfId) => dbfId == 127644 || dbfId == 133446
            ? Entries(c, GeneratedDecision).LastOrDefault()?.Card : Cards.FromAssetId(dbfId);
        internal static void ClearGeneratedDecision(Controller c)
        {
            foreach (var entry in Entries(c, GeneratedDecision)) Remove(entry);
        }
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32300 && (int)tag <= 32319 || (int)tag >= CopyPlayHistory.TagBase;
        internal static void ReplaceCoin(IPlayable card)
        {
            var replacement = CoinDefinition(card.Controller, card.Card);
            if (replacement != card.Card)
                Generic.ChangeEntity(card.Controller, card, replacement, false);
        }
        internal static Card CoinDefinition(Controller controller, Card definition)
        {
            if (definition.Id != "GAME_005") return definition;
            int replacement = controller[CoinReplacement];
            return replacement == 127152 || replacement == 127153 || replacement == 127154
                ? Cards.FromAssetId(replacement) : definition;
        }
        public static bool CopiedFromOpponent(IPlayable card) => card[CopySourceController] == card.Controller.Opponent.PlayerId;
        private const int PlayEligibilityTagBase = 0x60000000;
        public static bool HeldOpponentCopy(IPlayable card) => card[HeldCopyPlayed] != 0;
        public static void MarkCopy(IPlayable source, IPlayable copy) => MarkCopy(source.Controller, copy);
        public static void MarkCopy(Controller sourceController, IPlayable copy)
        {
            if ((copy.Game[Tracking] & 2) != 0) copy[CopySourceController] = sourceController.PlayerId;
        }
        internal static void CaptureTracking(Game game)
        {
            // Decide before any copy/death: a later generated consumer needs the earlier events too.
            var pending = new Stack<Card>(game.Player1.DeckCards.Concat(game.Player2.DeckCards));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int tracking = 0;
            while (pending.Count > 0)
            {
                var card = pending.Pop();
                if (!seen.Add(card.Id)) continue;
                if (card.Id == "EDR_491") tracking |= 1;
                if (card.Id == "JAIL_430" || card.Id == "JAIL_432" || card.Id == "JAIL_433" || card.Id == "JAIL_434") tracking |= 2;
                if (tracking == 3) break;
                foreach (string id in card.Entourage) pending.Push(Cards.FromId(id));
                if (game.LegacyPoolReplay) continue;
                var declaration = OfflinePoolDeclarations.Cards.FirstOrDefault(source => source.CardId == card.Id);
                if (declaration == null) continue;
                foreach (var pool in declaration.Pools)
                foreach (var owner in new[] { game.Player1, game.Player2 })
                foreach (var candidate in OfflineCardPools.Candidates(game, owner.HeroClass, pool.Filter, card.AssetId, pool.Destination, OfflineCardPools.PrintedClassMask(card)))
                    if (!seen.Contains(candidate.Id)) pending.Push(candidate);
            }
            // No zero marker/history entry in matches without consumers (including pinned replays).
            if (tracking != 0) game[Tracking] = tracking;
        }
        internal static void CapturePlay(IPlayable card)
        {
            if (card is Minion && card.HasDeathrattle && card.Controller.AppliedEnchantments?.Any(e => e.Card.Id == "BE_852e") == true)
                card.Controller[(GameTag)(PlayEligibilityTagBase + card.Id)] = 1;
        }
        internal static void FinishPlay(Controller controller, IPlayable card) => controller.NativeTags.Remove((GameTag)(PlayEligibilityTagBase + card.Id));
        public static SabberStoneCore.Triggers.Trigger HoldingTrigger() => new SabberStoneCore.Triggers.Trigger(TriggerType.PLAY_CARD) {
            TriggerActivation = TriggerActivation.HAND, TriggerSource = TriggerSource.FRIENDLY,
            Condition = new SabberStoneCore.Conditions.SelfCondition(CopiedFromOpponent),
            SingleTask = new SabberStoneCore.Tasks.SimpleTasks.CustomTask((g,c,s,t,stack) => {
                if (s.Zone == c.HandZone) s[HeldCopyPlayed] = 1;
            })
        };
        public static void CopyAfterPlay(IEntity effect, IPlayable source)
        {
            var c = effect.Controller;
            if (!(source is Minion) || c[(GameTag)(PlayEligibilityTagBase + source.Id)] == 0 || source.Controller != c ||
                source.Zone != c.BoardZone || c.BoardZone.IsFull) return;
            Generic.Copy(c, effect, source, Zone.PLAY, preserveBoardMinionState: true);
        }

        public static bool IsStored(IEntity entity) => entity[Stored] != 0;

        /// <summary>
        /// Entity reference for client text. The pinned client's Reference*Entity builders (CardTextBuilderType
        /// 19, 20 and 23: Treant, Highborne Pupil, Hammer of Archaedas, Horrible Egg, ...) read tag 2/3 as an
        /// ENTITY id and print GameState.GetEntity(id).GetName(); a dbfId, an unknown entity or an unnamed one
        /// prints "another card". The stored entity must therefore be an entity the viewer can name:
        /// see <see cref="SharedWith"/>.
        /// </summary>
        public static int ReferenceId(IPlayable host, int key) => Entries(host, key).FirstOrDefault()?.Id ?? 0;
        // Definitions whose client text builder is an entity-reference builder (pinned DBF CardTextBuilderType 19/20/23)
        // and whose script tag 2 therefore carries an entity id; the value is the DBF builder type. Immutable
        // declaration: tests/native-match/entity-text-builders.tsv fails when an implemented card of that kind is
        // neither declared here nor explicitly classified.
        private static readonly Dictionary<string, int> EntityReferenceCards = new Dictionary<string, int>(StringComparer.Ordinal)
            { ["MEND_046t"] = 23, ["TIME_704t"] = 23, ["BE_055t"] = 23, ["EDR_454t"] = 20,
              ["CATA_563e2"] = 23, ["CATA_897e3"] = 19, ["EDR_494e"] = 19, ["EDR_259e1"] = 19, ["TLC_841e"] = 19,
              ["CATA_567e"] = 19, ["EDR_491e"] = 19, ["BE_731e"] = 19, ["BE_044e"] = 23 };
        /// <summary>
        /// Entity an enchantment's tag 2 projects to the client when its engine tag 2 means something else (Feed Me!'s
        /// tag 2 is the meal's Attack, Overcast's a dbfId the engine keeps). Set at creation so first snapshots carry it.
        /// </summary>
        public const GameTag ReferenceEntity = (GameTag)32319;
        public static bool UsesReferenceTag(Card enchantment) => enchantment != null && ReferenceTagCards.Contains(enchantment.Id);
        // Overcast, Hoarded Gemstone, Feed Me! (reference passed at creation) and the four grants whose engine tag 2 is a dbfId or a
        // never-written value: Ascended and Assembled name a stored copy of the card, the two Deathrattle grants name their donor minion.
        private static readonly HashSet<string> ReferenceTagCards = new HashSet<string>(StringComparer.Ordinal) {
            "CATA_563e2", "CATA_897e3", "EDR_494e", "CATA_567e", "EDR_491e", "BE_731e", "BE_044e" };
        // Engine-defined cards whose client builder resolves tag 2 as an entity (dbf builder 19/20/23) but that do not (yet)
        // send one: their tag 2 holds a dbfId or a stat, so it is never sent. All are Wild-only legacy cards outside the offline
        // pool catalog (the native test fails when one appears there). tests/native-match/entity-text-builders.tsv
        // must list exactly these plus the declared hosts.
        private static readonly HashSet<string> MaskedEntityBuilderCards = new HashSet<string>(StringComparer.Ordinal) {
            "BOT_243e", "DAL_431t", "DRG_031e", "DRG_308e", "GIL_614e2", "ICC_314t1e", "KAR_041e", "LOE_019e", "LOOT_161e",
            "LOOT_520e", "TRL_409e", "ULD_163e", "ULD_286t", "BOTA_231e", "DALA_BOSS_59t3", "GILA_411e" };
        /// <summary>
        /// Grants that name a minion of the board rather than a stored copy (donor of a copied Deathrattle): the reference is
        /// an entity every player has seen, so it needs no store, only the check that this viewer can see that entity.
        /// </summary>
        public static bool NamesPublicEntity(Card card) => card != null && (card.Id == "EDR_491e" || card.Id == "BE_731e");
        public static bool MasksReference(Card card) => card != null && MaskedEntityBuilderCards.Contains(card.Id);
        public static IReadOnlyCollection<string> MaskedEntityBuilderCardIds => MaskedEntityBuilderCards;
        public static bool ReferencesEntity(Card card, GameTag tag) =>
            tag == GameTag.TAG_SCRIPT_DATA_NUM_1 && card != null && EntityReferenceCards.ContainsKey(card.Id);
        /// <summary>DBF CardTextBuilderType of a declared entity-reference definition; 0 otherwise.</summary>
        public static int ReferenceBuilderType(Card card) =>
            card != null && EntityReferenceCards.TryGetValue(card.Id, out int type) ? type : 0;
        public static IReadOnlyCollection<string> EntityReferenceCardIds => EntityReferenceCards.Keys;
        /// <summary>Static HistoryDisplay recipe: entity id of the host's first key-1 payload entry.</summary>
        public static int FirstReference(IPlayable host) => ReferenceId(host, 1);

        /// <summary>
        /// May this viewer's client name the stored entity? ShowAll: public information (e.g. the dragon an
        /// Egg copies). ShowOwner: only the host's controller. Retail 36.0 fixed "Bashana Runetotem revealed
        /// spells to the opponent", so the opponent never gets the identity.
        /// </summary>
        public static bool SharedWith(IEntity stored, int viewerPlayerId)
        {
            int policy = stored[Policy];
            if ((policy & (int)(PayloadPolicy.ShowAll | PayloadPolicy.ShowOwner)) == 0) return false;
            // Owner is cleared on retirement; the entry's own controller is the host's controller at storage time.
            IEntity host = stored[Owner] != 0 && stored.Game.IdEntityDic.TryGetValue(stored[Owner], out var found) ? found : null;
            var zone = (host as IPlayable)?.Zone?.Type;
            // ShowAll is public information, so only while its host is public (on the board): an opponent's hidden copy of
            // an Egg in hand or deck must not tell the human that the copy exists.
            if ((policy & (int)PayloadPolicy.ShowAll) != 0 && host is IPlayable && !(host is Enchantment) && zone == Zone.PLAY) return true;
            // An enchantment host is as public as the board object it is attached to (Ascended: the minion's original identity).
            if ((policy & (int)PayloadPolicy.ShowAll) != 0 && host is Enchantment attachment
                && (attachment.Target as IPlayable)?.Zone?.Type == Zone.PLAY) return true;
            if ((policy & (int)PayloadPolicy.ShowOwner) == 0) return false;
            // ShowOwner: the host's controller, while the host is somewhere that controller can read it. A card shuffled into
            // a deck (e.g. an opposing Treant put in the human's deck) names nothing until it is drawn.
            if (host is IPlayable && !(host is Enchantment) && (zone == Zone.DECK || zone == Zone.SETASIDE || zone == Zone.GRAVEYARD || zone == Zone.REMOVEDFROMGAME)) return false;
            // Both the controller who stored it and the host's current controller must be the viewer: stealing an opposing
            // Treant or copying one into your own deck never teaches the thief what was carved.
            return stored.Controller?.PlayerId == viewerPlayerId && (host?.Controller ?? stored.Controller)?.PlayerId == viewerPlayerId;
        }
        public const GameTag CapturedEffect = (GameTag)32311;
        private const int DeathLedgerKey = 16002, SuppressedDeathTagBase = 0x50000000;
        private static readonly GameTag[] Scripts = { GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2, GameTag.TAG_SCRIPT_DATA_NUM_6 };
        internal static bool CapturedDeathCapability(IEntity owner, GameTag tag) =>
            (tag == DeathContextRules.CaptureDeath || tag == DeathContextRules.PositionAnchor) && HasCapturedDeathCapability(owner, tag);
        // Separate method: C# allocates a lambda's closure on entry, and CapturedDeathCapability runs on every tag read.
        private static bool HasCapturedDeathCapability(IEntity owner, GameTag tag) =>
            owner.AppliedEnchantments?.Any(e => e[CapturedEffect] != 0 && e.Card[tag] != 0) == true;
        internal static Enchantment ExecutionCapture(IEntity owner)
        {
            var actor = owner?.Game?.TaskQueue?.CurrentTarget as Enchantment;
            return actor != null && actor.NativeTags.TryGetValue(CapturedEffect,out var captured) && captured != 0
                && (actor.Target.Id == owner.Id || actor.Id == owner.Id) ? actor : null;
        }
        internal static bool CapturedScript(IEntity owner, GameTag tag, out int value)
        {
            value=0;
            int slot=tag==GameTag.TAG_SCRIPT_DATA_NUM_1?0:tag==GameTag.TAG_SCRIPT_DATA_NUM_2?1:tag==GameTag.TAG_SCRIPT_DATA_NUM_6?2:-1;
            if(slot<0) return false;
            var actor=ExecutionCapture(owner);
            return actor != null && actor.NativeTags.TryGetValue((GameTag)(32312+slot+(actor.Id==owner.Id?3:0)),out value);
        }
        private static IPlayable[] RawEntries(IEntity owner, int key = 0)
        {
            List<IPlayable> found = null;
            int ownerId = owner.Id;
            var entities = owner.Game.IdEntityDic;
            for (int id = 0, n = entities.Capacity; id < n; id++)
            {
                var e = entities.At(id);
                if (e == null) continue;
                // Payload tags exist only as entity overrides (no card or aura defines them): skip the full indexer for the rest.
                if (e is Entity entity && !entity._data.ContainsKey(Owner)) continue;
                if (e[Owner] == ownerId && (key == 0 || e[Key] == key)) (found ??= new List<IPlayable>()).Add(e);
            }
            if (found == null) return Array.Empty<IPlayable>();
            var result = found.ToArray();
            Array.Sort(result, (a, b) => a.Id.CompareTo(b.Id)); // ids are unique: same order as OrderBy
            return result;
        }

        public static IPlayable[] Entries(IEntity owner, int key = 0)
        {
            var capture=ExecutionCapture(owner);
            return RawEntries(capture != null && capture.Id != owner.Id ? capture : owner,key);
        }
        public static void GainDeathrattles(IPlayable recipient, IPlayable donor, string helper, bool printed = true)
        {
            if (printed && (!(donor is Minion m) || !m.IsSilenced) && donor.Power?.DeathrattleTask != null)
                CaptureEffect(recipient,donor,null,helper);
            foreach(var actor in donor.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>())
                if(actor.Power?.DeathrattleTask != null) CaptureEffect(recipient,donor,actor,helper);
        }
        private static void CaptureEffect(IPlayable recipient,IPlayable donor,Enchantment original,string helper,Card frozenDefinition = null,int deadDonor = 0)
        {
            // Client text "Copied Deathrattle from {0}" names the donor minion: a board minion (or one that died there), public to
            // both players. A death ledger is private bookkeeping, so its grants carry the minion that died instead.
            int reference = donor == null ? deadDonor
                : donor[Stored] != 0 && original != null && original[ReferenceEntity] != 0 ? original[ReferenceEntity] : donor.Id;
            var definition=Cards.FromId(helper).Clone();
            definition.Power=new SabberStoneCore.Enchants.Power { DeathrattleTask=(frozenDefinition?.Power??original?.Power??donor.Power).DeathrattleTask };
            var effectDefinition = frozenDefinition ?? original?.Card ?? donor.Card;
            foreach (var tag in new[] { DeathContextRules.CaptureDeath, DeathContextRules.PositionAnchor })
                if (effectDefinition[tag] != 0) definition.Tags[tag] = effectDefinition[tag];
            var actor=Enchantment.GetInstance(recipient.Controller,recipient,recipient,definition,0,0,reference);
            actor.CapturedCard=original?.CapturedCard;
            actor[CapturedEffect]=1;
            actor[GameTag.TAG_SCRIPT_DATA_NUM_1]=(frozenDefinition??donor.Card).AssetId;
            for(int i=0;i<Scripts.Length;i++)
            {
                actor[(GameTag)(32312+i)]=original != null && original[CapturedEffect]!=0 ? original[(GameTag)(32312+i)] : donor?[Scripts[i]] ?? 0;
                actor[(GameTag)(32315+i)]=original != null && original[CapturedEffect]!=0 ? original[(GameTag)(32315+i)] : original?[Scripts[i]] ?? 0;
            }
            if (donor != null) CopyState(original ?? donor,actor);
            recipient.HasDeathrattle=true;
        }
        internal static void CaptureDeaths(Game game)
        {
            if ((game[Tracking] & 1) == 0) return;
            foreach(var donor in game.DeadMinions.OrderBy(m=>m.OrderOfPlay).ToArray())
            {
                if (donor.Zone != donor.Controller.BoardZone) continue;
                int incarnation=checked((int)game.HistoryFacts.Board(donor.Id).Incarnation);
                if (!donor.HasDeathrattle)
                {
                    if (donor.Power?.DeathrattleTask != null) donor.Controller[(GameTag)(SuppressedDeathTagBase+incarnation)]=1;
                    continue;
                }
                // Immutable death facts already retain stateless printed recipes; do not allocate a duplicate body.
                bool simple=!donor.IsSilenced && donor.Power?.DeathrattleTask != null
                    && donor.AppliedEnchantments?.Any(a=>a.Power?.DeathrattleTask != null) != true
                    && Scripts.All(tag=>donor[tag]==0) && RawEntries(donor).Length==0;
                if(simple) continue;
                var ledger=StoreCard(donor.Controller,DeathLedgerKey,donor.Card,0);
                ledger[Value]=game.Turn; ledger[Link]=incarnation;
                GainDeathrattles(ledger,donor,"EDR_491e");
            }
        }
        public static void GainDeathsThisTurn(IPlayable recipient,string helper)
        {
            var c=recipient.Controller;
            var ledgers=RawEntries(c,DeathLedgerKey).Where(e=>e[Value]==recipient.Game.Turn).ToDictionary(e=>e[Link]);
            foreach(var death in recipient.Game.HistoryFacts.Query.Deaths().Where(d=>d.ControllerId==c.Id && d.Turn==recipient.Game.Turn))
                if(ledgers.TryGetValue(checked((int)death.Incarnation),out var ledger))
                    GainDeathrattles(recipient,ledger,helper,printed:false);
                else if(c[(GameTag)(SuppressedDeathTagBase+checked((int)death.Incarnation))]==0
                    && death.Definition.ReadDefinition().Power?.DeathrattleTask != null)
                    CaptureEffect(recipient,null,null,helper,death.Definition.Materialize(),death.EntityId);
        }

        public static IPlayable Store(IEntity owner, int key, IPlayable source, PayloadPolicy policy = PayloadPolicy.Copy | PayloadPolicy.Silence)
        {
            Validate(owner, key);
            if (source == null || source.Game != owner.Game) throw new ArgumentException("Payload source belongs to another game.");
            for (IEntity ancestor = owner; ancestor != null;)
            {
                if (ancestor.Id == source.Id) throw new ArgumentException("Cyclic payload capture.");
                ancestor = ancestor[Owner] != 0 && owner.Game.IdEntityDic.TryGetValue(ancestor[Owner], out var parent) ? parent : null;
            }
            var entry = Generic.Copy(source.Controller, owner, source, Zone.SETASIDE, preserveCapturedState: true);
            Attach(owner, key, entry, policy);
            return entry;
        }

        /// <summary>Retain an already detached original instead of capturing a copy.</summary>
        internal static void Retain(IEntity owner, int key, IPlayable entry)
        {
            Validate(owner, key);
            if (entry.Game != owner.Game || entry.Zone != null || entry.Controller != owner.Controller)
                throw new System.ArgumentException("Retained payload must be detached and belong to its host controller.");
            Attach(owner, key, entry, 0);
        }

        public static IPlayable StoreCard(IEntity owner, int key, Card card, PayloadPolicy policy = PayloadPolicy.Copy | PayloadPolicy.Silence)
        {
            Validate(owner, key);
            if (card == null || card.Type == CardType.ENCHANTMENT) throw new ArgumentException("Payload needs a playable definition.");
            var entry = Entity.FromCard(owner.Controller, card, new EntityData { [Stored] = 1 }, zone: owner.Controller.SetasideZone);
            Attach(owner, key, entry, policy);
            return entry;
        }
        /// <summary>
        /// Creates a detached stored entry before its host exists, so the host's first snapshots can carry the entry's id
        /// (<see cref="ReferenceEntity"/>); <see cref="Bind"/> then attaches it. Until bound the entry is private.
        /// </summary>
        public static IPlayable PrepareCard(Controller controller, Card card)
        {
            if (card == null || card.Type == CardType.ENCHANTMENT) throw new ArgumentException("Payload needs a playable definition.");
            return Entity.FromCard(controller, card, new EntityData { [Stored] = 1 }, zone: controller.SetasideZone);
        }
        /// <summary>Attaches an entry made by <see cref="PrepareCard"/> to its (now registered) host.</summary>
        public static void Bind(IEntity owner, int key, IPlayable entry, PayloadPolicy policy = PayloadPolicy.Copy | PayloadPolicy.Silence)
        {
            Validate(owner, key);
            if (entry[Stored] == 0 || entry[Owner] != 0) throw new ArgumentException("Only an unbound prepared entry can be bound.");
            Attach(owner, key, entry, policy);
        }
        public static IPlayable StoreValue(IEntity owner, int key, int value, PayloadPolicy policy = PayloadPolicy.Copy | PayloadPolicy.Silence)
        {
            var entry = StoreCard(owner, key, owner.Card, policy);
            entry[Value] = value;
            return entry;
        }
        private static void Validate(IEntity owner, int key)
        {
            if (owner == null || key <= 0 || (owner != owner.Game.Player1 && owner != owner.Game.Player2
                && (!owner.Game.IdEntityDic.TryGetValue(owner.Id, out var actual) || actual != owner)))
                throw new ArgumentException("Payload requires a registered host and positive key.");
        }
        private static void Attach(IEntity owner, int key, IPlayable entry, PayloadPolicy policy)
        {
            if ((policy & ~(PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.HandExit | PayloadPolicy.Resummon | PayloadPolicy.ShowOwner | PayloadPolicy.ShowAll)) != 0)
                throw new ArgumentOutOfRangeException(nameof(policy));
            entry[Owner] = owner.Id; entry[Key] = key; entry[Policy] = (int)policy;
            entry[Stored] = 1;
        }
        public static void Remove(IPlayable entry)
        {
            entry[Owner] = 0; entry[Key] = 0;
            // Keep the privacy marker after retirement; old creation packets must remain hidden.
            foreach (var nested in Entries(entry)) Remove(nested);
        }
        public static void Clear(IEntity owner, PayloadPolicy? only = null)
        {
            bool removed = false;
            foreach (var entry in RawEntries(owner))
                if (!only.HasValue || ((PayloadPolicy)entry[Policy] & only.Value) != 0)
                { Remove(entry); removed = true; }
            if (removed && owner is IPlayable playable) owner.Card.UpdateHistoryDisplays(playable);
        }
        internal static void CopyState(IPlayable source, IPlayable copy, bool handCopy = false, bool resummon = false)
        {
            if (source is Enchantment && source[CapturedEffect] != 0)
                for(int tag=32311;tag<=32317;tag++) copy[(GameTag)tag]=source[(GameTag)tag];
            var entries = RawEntries(source).Where(e => !resummon || ((PayloadPolicy)e[Policy] & PayloadPolicy.Resummon) != 0).ToArray();
            if (entries.Length != 0 && copy is Minion) copy.HasDeathrattle = source.HasDeathrattle;
            foreach (var entry in entries)
            {
                if (((PayloadPolicy)entry[Policy] & PayloadPolicy.Copy) == 0) continue;
                var duplicate = Generic.Copy(entry.Controller, copy, entry, Zone.SETASIDE, preserveCapturedState: true);
                Attach(copy, entry[Key], duplicate, (PayloadPolicy)entry[Policy]);
                foreach (var tag in new[] { Link, Generation, Value, Recipient }) duplicate[tag] = entry[tag];
            }
            if (Entries(copy).Length != 0 || copy.AppliedEnchantments?.Any(e => Entries(e).Length != 0) == true)
                copy.Card.UpdateHistoryDisplays(copy);
            // Retain present hand script state; existing mechanic-specific reset rules run afterward.
            if (source.Zone?.Type == Zone.HAND && handCopy)
                foreach (var tag in new[] { GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2, GameTag.TAG_SCRIPT_DATA_NUM_6 })
                    // A Lookout preview belongs to its controller; assignment would leak through TAG_CHANGE.
                    if (!(source.Card.Id == "EDR_950" && tag == GameTag.TAG_SCRIPT_DATA_NUM_6))
                    if (source.NativeTags.ContainsKey(tag) || source.Card[tag] != 0) copy[tag] = source[tag];
        }
        internal static void HandExit(IPlayable card)
        {
            Clear(card, PayloadPolicy.HandExit);
            if (card.AppliedEnchantments == null) return;
            foreach (var actor in card.AppliedEnchantments.ToArray())
                if (actor.Card.ExpiresOnHandExit) actor.Remove();
        }
    }
}
