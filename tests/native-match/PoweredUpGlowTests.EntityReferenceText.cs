using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Bound-card tooltips. Playtest 2026-11-04: Bashana Runetotem's Treants read "Cast another card." The pinned client
// builds that text with CardTextBuilderType 23 (Reference*Entity family), which resolves tag 2 through
// GameState.GetEntity(id).GetName(): the tag must be an ENTITY id the viewer's client can name, never a dbfId.
// tests/native-match/entity-text-builders.tsv lists every dbf card with such a builder (tools/dbf_text_builders.py).
internal static partial class PoweredUpGlowTests
{
    private const int HandZone = 3, PlayZone = 1, ZoneTag = 49, OverrideBuilderTag = 782;

    [GroupProof("entity-reference-text-native", ProofKind.Native)]
    private static void EntityReferenceTextNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            BashanaCarvedSpells(opponent);
            HighbornePupilTaughtSpell(opponent);
            HorribleEggCopiedDragon(opponent);
            EnchantmentHostsNameTheirEntity(opponent);
            MovingHostsKeepPrivacy(opponent);
        }
        MaskedEntityBuildersNeverSendTagTwo();
        RewindResetVisibility();
        EntityBuilderInventoryIsClassified();
        Console.WriteLine("PASS entity-reference card text: Treant, Pupil, Egg (both seats, copies, first-shown order, privacy) and builder inventory");
    }

    // What the client's text builder sees the moment the host is first shown in hand or play: the card name of the entity
    // that tag 2 resolves to (null when unknown or unnamed, which prints "another card"). It builds the text once, then.
    private static (int Reference, string Name) ReferenceWhenShown(Fixture f, int hostId, int onlyZone = 0)
    {
        int reference = 0; var names = new Dictionary<int, string>();
        foreach (var entry in f.Entries)
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
                : entry.HasChangeEntity ? entry.ChangeEntity : null;
            bool shown = false;
            if (snapshot != null)
            {
                if (!string.IsNullOrEmpty(snapshot.Name)) names[snapshot.Entity] = snapshot.Name;
                if (snapshot.Entity == hostId)
                    foreach (var tag in snapshot.Tags)
                    {
                        if (tag.Name == 2) reference = tag.Value;
                        if (tag.Name == ZoneTag && (onlyZone != 0 ? tag.Value == onlyZone : tag.Value == HandZone || tag.Value == PlayZone)) shown = true;
                    }
            }
            if (entry.HasTagChange && entry.TagChange.Entity == hostId)
            {
                if (entry.TagChange.Tag == 2) reference = entry.TagChange.Value;
                if (entry.TagChange.Tag == ZoneTag && (onlyZone != 0 ? entry.TagChange.Value == onlyZone : entry.TagChange.Value == HandZone || entry.TagChange.Value == PlayZone)) shown = true;
            }
            if (shown) return (reference, names.TryGetValue(reference, out string name) ? name : null);
        }
        throw new InvalidOperationException("TEST PoweredUp: host never shown in hand or play");
    }

    private static bool IsNamedToViewer(Fixture f, int id) =>
        f.Entries.Any(e => e.HasFullEntity && e.FullEntity.Entity == id && !string.IsNullOrEmpty(e.FullEntity.Name)
            || e.HasShowEntity && e.ShowEntity.Entity == id && !string.IsNullOrEmpty(e.ShowEntity.Name));

    private static void AssertReferencedBeforeShown(Fixture f, IPlayable host, IPlayable stored, string label, int onlyZone = 0)
    {
        var shown = ReferenceWhenShown(f, host.Id, onlyZone);
        Check(shown.Name == stored.Card.Id,
            label + ": first shown referencing an entity already named " + stored.Card.Id + " to the owner (got entity " + shown.Reference + " named " + (shown.Name ?? "nothing") + ")");
        var wire = ArchaedasWireTags(f, host.Id);
        Check(wire.TryGetValue(2, out int final) && final == stored.Id && final != stored.Card.AssetId && IsNamedToViewer(f, final),
            label + ": final native tag 2 is its own stored entity (named), never the dbfId or a source's entity (got " + (wire.TryGetValue(2, out int got) ? got : -1) + ")");
    }

    private static void AssertReferenceStaysPrivate(Fixture f, IPlayable host, IPlayable stored, string label)
    {
        Check(!IsNamedToViewer(f, stored.Id), label + ": opponent's stored spell is never named to the human");
        AssertArchaedasHidden(f, new[] { stored.Id });
        var wire = ArchaedasWireTags(f, host.Id);
        Check(!wire.TryGetValue(2, out int value) || value == 0, label + ": no reference or dbfId reaches the opponent's client");
        Check(!f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == host.Id && e.TagChange.Tag == OverrideBuilderTag),
            label + ": no text rebuild trigger for a card the human cannot read");
    }

    private static Fixture EntityReferenceFixture(bool opponent, out Controller owner)
    {
        var f = new Fixture(); var g = f.Game;
        if (opponent) Check(g.Process(EndTurnTask.Any(g.Player1)), "opponent turn");
        owner = opponent ? g.Player2 : g.Player1; owner.BaseMana = 10; owner.UsedMana = 0;
        foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
        f.Flush();
        return f;
    }

    private static void BashanaCarvedSpells(bool opponent)
    {
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var source = Generic.DrawCard(owner, Cards.FromId("MEND_046"));
        Check(g.Process(PlayCardTask.Any(owner, source)), "Bashana play"); f.Flush();
        var treants = owner.HandZone.Where(c => c.Card.Id == "MEND_046t").ToArray();
        Check(treants.Length == 3, "Bashana creates three Treants");
        foreach (var treant in treants)
        {
            var stored = PayloadRules.Entries(treant, 1).Single();
            Check(treant[GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Id, "engine Treant tag 2 is its carved entity id");
            if (opponent) AssertReferenceStaysPrivate(f, treant, stored, "opposing Treant");
            else AssertReferencedBeforeShown(f, treant, stored, "Treant");
        }
        // Copies (Faceless, Zola, ...) own an independent carved entity and reference it.
        var copy = Generic.Copy(owner, source, treants[0], SabberStoneCore.Enums.Zone.HAND); g.AuraUpdate(); f.Flush();
        var copyStored = PayloadRules.Entries(copy, 1).Single();
        Check(copyStored.Id != PayloadRules.Entries(treants[0], 1).Single().Id && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == copyStored.Id,
            "Treant copy references its own carved entity");
        if (opponent) AssertReferenceStaysPrivate(f, copy, copyStored, "opposing Treant copy");
        else AssertReferencedBeforeShown(f, copy, copyStored, "Treant copy");
        if (opponent)
        {
            // Once the opponent's Treant is public on the board, only the minion is: its reference is zeroed, the carved spell stays private.
            var board = Generic.Copy(owner, source, treants[1], SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
            var boardStored = PayloadRules.Entries(board, 1).Single();
            Check(IsNamedToViewer(f, board.Id), "opponent's board Treant is a public minion");
            Check(!IsNamedToViewer(f, boardStored.Id), "opponent's board Treant does not reveal its carved spell");
            Check(ArchaedasWireTags(f, board.Id).TryGetValue(2, out int leaked) ? leaked == 0 : true,
                "public opposing Treant leaks neither entity id nor dbfId");
        }
        else
        {
            // The owner's Treant on the board keeps naming its spell.
            var board = Generic.Copy(owner, source, treants[1], SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
            AssertReferencedBeforeShown(f, board, PayloadRules.Entries(board, 1).Single(), "board Treant");
        }
        Check(f.Entries.All(e => !e.HasTagChange || !PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),
            "payload bookkeeping tags never reach the client");
    }

    // The Pupil is created before the Discover and taught afterwards, so its reference arrives while it is already in
    // hand. The client builds text once and ignores tag 2 changes: the host adds the client's own rebuild trigger.
    private static void HighbornePupilTaughtSpell(bool opponent)
    {
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var mentor = Generic.DrawCard(owner, Cards.FromId("TIME_704"));
        Check(g.Process(PlayCardTask.Any(owner, mentor)), "Highborne Mentor play"); f.Flush();
        var offers = owner.Choice.Choices.ToArray();
        Check(offers.Length > 0, "Mentor offers a Past spell");
        var decision = g.GetPendingDecisions().Single();
        Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { offers[0] }), out _), "Mentor teaches the pick"); f.Flush();
        var pupil = owner.HandZone.Single(c => c.Card.Id == "TIME_704t");
        var stored = PayloadRules.Entries(pupil, 1).Single();
        Check(pupil[GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Id, "engine Pupil tag 2 is the taught spell's entity id");
        if (opponent) { AssertReferenceStaysPrivate(f, pupil, stored, "opposing Pupil"); return; }
        Check(IsNamedToViewer(f, stored.Id), "taught spell is named to its owner");
        var rows = f.Entries.ToList();
        int reference = rows.FindIndex(e => e.HasTagChange && e.TagChange.Entity == pupil.Id && e.TagChange.Tag == 2 && e.TagChange.Value == stored.Id);
        int named = rows.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == stored.Id && e.FullEntity.Name == stored.Card.Id);
        Check(named >= 0 && reference > named, "the Pupil's reference arrives after its target entity is named");
        Check(rows.Skip(reference + 1).Take(1).Any(e => e.HasTagChange && e.TagChange.Entity == pupil.Id
                && e.TagChange.Tag == OverrideBuilderTag && e.TagChange.Value == 23),
            "a Pupil already in hand rebuilds its text: OVERRIDECARDTEXTBUILDER follows the late reference");
    }

    // Retail names the copied Dragon to both players (public board information), so the Egg's stored copy is shared.
    private static void HorribleEggCopiedDragon(bool opponent)
    {
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var dragon = f.Summon("EX1_043", owner);
        var location = (BoardObject)Generic.DrawCard(owner, Cards.FromId("EDR_454"));
        Check(g.Process(PlayCardTask.Any(owner, location)), "Clutch of Corruption play");
        Check(g.Process(new ActivateLocationTask(owner, location, dragon)), "Clutch of Corruption activation"); f.Flush();
        var egg = owner.BoardZone.Single(m => m.Card.Id == "EDR_454t");
        var stored = PayloadRules.Entries(egg, 1).Single();
        Check(egg[GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Id && stored.Card.Id == "EX1_043", "engine Egg tag 2 is its copied Dragon's entity id");
        AssertReferencedBeforeShown(f, egg, stored, opponent ? "opposing Egg" : "Egg");
    }

    private static void AnswerPick(Fixture f, int pick)
    {
        var decision = f.Game.GetPendingDecisions().Single();
        Check(f.Game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { pick }), out _), "decision answered"); f.Flush();
    }

    // Enchantments, auras and Jars whose client text names a card: tag 2 is the referenced entity's id, the entity is named
    // to the owner only, and an opponent's reads 0 (or an id the human's client cannot name).
    private static void AssertHostReference(Fixture f, IEntity host, IPlayable referenced, bool opponent, string label, bool idMayShow = false)
    {
        var wire = ArchaedasWireTags(f, host.Id);
        if (opponent)
        {
            // A discarded card is revealed to both players by the discard itself; every other reference stays unnamed.
            if (!idMayShow) Check(!IsNamedToViewer(f, referenced.Id), label + ": opposing card is never named to the human");
            if (!idMayShow) Check(!wire.TryGetValue(2, out int leaked) || leaked == 0, label + ": opposing tag 2 stays 0");
            Check(!wire.TryGetValue(2, out int dbf) || dbf != referenced.Card.AssetId, label + ": no dbfId reaches the human");
            return;
        }
        Check(wire.TryGetValue(2, out int value) && value == referenced.Id && value != referenced.Card.AssetId,
            label + ": tag 2 is the referenced entity (got " + (wire.TryGetValue(2, out int got) ? got : -1) + ", expected " + referenced.Id + ")");
        Check(IsNamedToViewer(f, referenced.Id), label + ": referenced entity is named to its owner");
    }

    private static void EnchantmentHostsNameTheirEntity(bool opponent)
    {
        string who = opponent ? "opposing " : "";
        // Overcast (CATA_563e2, builder 23): the absorbed hand spell.
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var spell = Generic.DrawCard(owner, Cards.FromId("CS2_029"));
        var cloud = Generic.DrawCard(owner, Cards.FromId("CATA_563")); f.Flush(); // draws are sent before the next Process
        Check(g.Process(PlayCardTask.Any(owner, cloud)), "Cloudstrider play"); f.Flush();
        AnswerPick(f, spell.Id);
        var overcast = ((IPlayable)g.IdEntityDic[cloud.Id]).AppliedEnchantments.Single(e => e.Card.Id == "CATA_563e2");
        AssertHostReference(f, overcast, spell, opponent, who + "Overcast");
        // Hoarded Gemstone (CATA_897e3, builder 19): the discarded card (public once discarded; its id may show).
        f = EntityReferenceFixture(opponent, out owner); g = f.Game;
        var discard = Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        var hoarder = Generic.DrawCard(owner, Cards.FromId("CATA_897")); f.Flush();
        Check(g.Process(PlayCardTask.Any(owner, hoarder)), "Gemstone Hoarder play"); f.Flush();
        AnswerPick(f, discard.Id);
        var gem = ((IPlayable)g.IdEntityDic[hoarder.Id]).AppliedEnchantments.Single(e => e.Card.Id == "CATA_897e3");
        AssertHostReference(f, gem, discard, opponent, who + "Hoarded Gemstone", idMayShow: true);
        // Feed Me! (EDR_494e, builder 19): its engine tag 2 is the meal's Attack; the client must get the meal's entity.
        f = EntityReferenceFixture(opponent, out owner); g = f.Game;
        foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
        var meal = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182")); owner.DeckZone.Add(meal);
        var ancient = Generic.DrawCard(owner, Cards.FromId("EDR_494"));
        Check(g.Process(PlayCardTask.Any(owner, ancient)), "Hungering Ancient play"); f.Flush();
        Check(g.Process(EndTurnTask.Any(owner)), "Hungering Ancient turn end"); f.Flush();
        var feed = ((IPlayable)g.IdEntityDic[ancient.Id]).AppliedEnchantments.Single(e => e.Card.Id == "EDR_494e");
        Check(feed[GameTag.TAG_SCRIPT_DATA_NUM_1] == meal.AttackDamage, "engine keeps the meal's Attack in tag 2");
        AssertHostReference(f, feed, meal, opponent, who + "Feed Me!");
        // A copied Hungering Ancient keeps the reference (its engine tag 2 is still the meal's Attack: never a wrong entity).
        var ancientCopy = (Minion)Generic.Copy(owner, ancient, (Minion)g.IdEntityDic[ancient.Id], SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var copiedFeed = ancientCopy.AppliedEnchantments?.SingleOrDefault(e => e.Card.Id == "EDR_494e");
        Check(copiedFeed != null && copiedFeed[PayloadRules.ReferenceEntity] == meal.Id, who + "copied Feed Me! keeps its meal reference");
        AssertHostReference(f, copiedFeed, meal, opponent, who + "copied Feed Me!");
        // Ursol's Aura (EDR_259e1, builder 19): the captured spell.
        f = EntityReferenceFixture(opponent, out owner); g = f.Game;
        Generic.DrawCard(owner, Cards.FromId("CS2_029"));
        var ursol = Generic.DrawCard(owner, Cards.FromId("EDR_259")); f.Flush();
        Check(g.Process(PlayCardTask.Any(owner, ursol)), "Ursol play"); f.Flush();
        var aura = owner.SecretZone.Auras.Single(a => a.Card.Id == "EDR_259e1");
        var captured = PayloadRules.Entries(aura, 1).Single();
        Check(aura[GameTag.TAG_SCRIPT_DATA_NUM_1] == captured.Id, "engine aura tag 2 is the captured entity");
        AssertHostReference(f, aura, captured, opponent, who + "Ursol's Aura");
        // Specimen Jar's enchantment (TLC_841e, builder 19): the jarred minion.
        f = EntityReferenceFixture(opponent, out owner); g = f.Game;
        Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        var toru = Generic.DrawCard(owner, Cards.FromId("TLC_841"));
        Check(g.Process(PlayCardTask.Any(owner, toru)), "Toru play"); f.Flush();
        var jar = owner.HandZone.Single(c => c.Card.Id == "TLC_841t");
        var jarring = jar.AppliedEnchantments.Single(e => e.Card.Id == "TLC_841e");
        var jarred = PayloadRules.Entries(jarring, 1).Single();
        AssertHostReference(f, jarring, jarred, opponent, who + "Jarring");
    }

    // Hosts that change hands: a stolen opponent Treant must not name its spell, a bounced Treant keeps what it names,
    // an Egg's hidden copy and a Treant shuffled into the human's deck tell the human nothing before they are shown.
    private static void MovingHostsKeepPrivacy(bool opponent)
    {
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var source = Generic.DrawCard(owner, Cards.FromId("MEND_046"));
        Check(g.Process(PlayCardTask.Any(owner, source)), "Bashana play"); f.Flush();
        var treant = owner.HandZone.First(c => c.Card.Id == "MEND_046t");
        var board = (Minion)Generic.Copy(owner, source, treant, SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var boardStored = PayloadRules.Entries(board, 1).Single();
        var human = g.Player1; var npc = g.Player2;
        if (opponent)
        {
            // Stolen NPC Treant: its spell was never named; it reads "another card", never the spell.
            new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, human, human.Hero, board);
            g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(!IsNamedToViewer(f, boardStored.Id), "stolen opposing Treant never names its spell");
            Check(!ArchaedasWireTags(f, board.Id).TryGetValue(2, out int stolen) || stolen == 0, "stolen opposing Treant sends no reference");
            // A copy of the NPC's Treant shuffled into the human's deck names nothing, before or after it is drawn.
            // (The engine drops payloads on a plain Copy to DECK, so build the carrying card directly: stored by the NPC.)
            var npcSpell = Generic.DrawCard(npc, Cards.FromId("CS2_029"));
            var deckCopy = SabberStoneCore.Model.Entities.Entity.FromCard(human, Cards.FromId("MEND_046t"), zone: human.DeckZone);
            var deckStored = PayloadRules.Store(deckCopy, 1, npcSpell, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
            deckCopy[GameTag.TAG_SCRIPT_DATA_NUM_1] = deckStored.Id; f.Flush();
            Check(!IsNamedToViewer(f, deckStored.Id), "a Treant shuffled into the human's deck does not name its spell");
            Generic.Draw(human, deckCopy); g.AuraUpdate(); f.Flush();
            Check(!IsNamedToViewer(f, deckStored.Id), "nor once the human draws the opposing Treant copy");
        }
        else
        {
            // Positive side: the human's own Treant copy shuffled into the deck is anonymous there, and is named the moment it is
            // drawn, before the client first shows it (otherwise it reads "Cast another card").
            var deckCopy = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("MEND_046t"), zone: owner.DeckZone);
            var deckStored = PayloadRules.StoreCard(deckCopy, 1, Cards.FromId("CS2_029"), PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
            deckCopy[GameTag.TAG_SCRIPT_DATA_NUM_1] = deckStored.Id; f.Flush();
            Check(!IsNamedToViewer(f, deckStored.Id), "a Treant copy in the human's deck names nothing yet");
            Generic.Draw(owner, deckCopy); g.AuraUpdate(); f.Flush();
            AssertReferencedBeforeShown(f, deckCopy, deckStored, "drawn Treant copy");
            // Bounce resets the Treant (payload dropped, reference cleared).
            SabberStoneCore.Actions.Generic.ReturnToHandBlock.Invoke(owner, board); g.DeathProcessingAndAuraUpdate(); f.Flush();
            var wire = ArchaedasWireTags(f, board.Id);
            // Returning to hand resets the minion (the engine drops its payload): it references nothing, and a stale id never lingers.
            Check(PayloadRules.Entries(board, 1).Length == 0 && (!wire.TryGetValue(2, out int kept) || kept == 0),
                "a bounced Treant no longer references its dropped spell");
        }
        // An Egg token holding a copied Dragon in HAND (a hidden copy) names nothing until it is public on the board; then its
        // owner and opponent alike are told what it will hatch, before the Egg is shown.
        f = EntityReferenceFixture(opponent, out owner); g = f.Game;
        var dragon = f.Summon("EX1_043", owner);
        var heldEgg = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("EDR_454t"), zone: owner.HandZone);
        var heldStored = PayloadRules.Store(heldEgg, 1, dragon, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowAll);
        heldEgg[GameTag.TAG_SCRIPT_DATA_NUM_1] = heldStored.Id; heldEgg.HasDeathrattle = true; g.AuraUpdate(); f.Flush();
        Check(PayloadRules.Entries(heldEgg, 1).Length == 1, "the held Egg has its payload");
        Check(!IsNamedToViewer(f, heldStored.Id), "a hidden-hand Egg names its Dragon to nobody");
        Check(!PayloadRules.SharedWith(heldStored, 1), "ShowAll is not shared while the host is in a hand");
        owner.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(owner, heldEgg)), "the held Egg is played"); f.Flush();
        Check(heldEgg.Zone == owner.BoardZone && PayloadRules.SharedWith(heldStored, 1), "a played Egg is public: its entry is shared");
        AssertReferencedBeforeShown(f, heldEgg, heldStored, opponent ? "opposing played Egg" : "played Egg", PlayZone);
        var inDeck = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("EDR_454t"), zone: owner.DeckZone);
        var deckedStored = PayloadRules.Store(inDeck, 1, dragon, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowAll);
        f.Flush();
        Check(!PayloadRules.SharedWith(deckedStored, 1) && !IsNamedToViewer(f, deckedStored.Id), "an Egg in a deck names its Dragon to nobody");
    }

    // Cards whose client builder resolves entities but that send none yet carry a dbfId or a stat in tag 2: never sent.
    private static void MaskedEntityBuildersNeverSendTagTwo()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
            var host = f.Summon("CS2_182", owner);
            foreach (string id in PayloadRules.MaskedEntityBuilderCardIds.Where(id => Cards.FromId(id).Type == CardType.ENCHANTMENT && Cards.FromId(id).Power != null).OrderBy(id => id))
            {
                var attachment = Enchantment.GetInstance(owner, owner.Hero, host, Cards.FromId(id), 119755, 6);
                f.Flush();
                var wire = ArchaedasWireTags(f, attachment.Id);
                Check(!wire.TryGetValue(2, out int value) || value == 0, id + " never sends its dbfId/stat as an entity reference");
            }
        }
    }

    // Rewind rebuilds the human's view from snapshots: the stored entity rule must hold there too.
    private static void RewindResetVisibility()
    {
        var method = typeof(NativeMatch).GetMethod("IsHiddenFromHuman", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        foreach (bool opponent in new[] { false, true })
        {
            var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
            var source = Generic.DrawCard(owner, Cards.FromId("MEND_046"));
            Check(g.Process(PlayCardTask.Any(owner, source)), "Bashana play"); f.Flush();
            var treant = owner.HandZone.First(c => c.Card.Id == "MEND_046t");
            var stored = PayloadRules.Entries(treant, 1).Single();
            var snapshots = new Dictionary<int, SabberStoneCore.Kettle.PowerHistoryEntity>();
            foreach (var entity in new IPlayable[] { treant, stored })
                snapshots[entity.Id] = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true).Entity;
            bool hidden = (bool)method.Invoke(f.Match, new object[] { stored.Id, null, snapshots });
            Check(hidden == opponent, "Rewind reset " + (opponent ? "hides" : "keeps") + " the carved spell for the human");
        }
    }

    // Class inventory: every dbf card whose client builder resolves entities (tag 2/3 or CREATOR through
    // GameState.GetEntity) and that this engine defines must be either declared a reference host (and then sends
    // entity ids) or carry an explicit verdict below. A new such card cannot slip in unnoticed.
    // Entity-builder cards the engine defines that are neither declared reference hosts nor masked (PayloadRules.MaskedEntityBuilderCardIds).
    // Builder 18 reads the CREATOR tag (an entity id the transport already projects), not a script tag.
    private static readonly Dictionary<string, string> EntityBuilderVerdicts = new Dictionary<string, string>
    {
        ["DS1_175o"] = "creator: CREATOR entity; unchanged", ["DS1_178e"] = "creator: CREATOR entity; unchanged",
        ["EX1_162o"] = "creator: CREATOR entity; unchanged",
    };

    private static void EntityBuilderInventoryIsClassified()
    {
        var rows = File.ReadAllLines("tests/native-match/entity-text-builders.tsv").Skip(1)
            .Select(line => line.Split('\t')).Where(cells => cells.Length >= 4).ToDictionary(cells => cells[0], cells => int.Parse(cells[1]));
        Check(rows.Count > 80 && rows["MEND_046t"] == 23 && rows["TIME_704t"] == 23 && rows["BE_055t"] == 23 && rows["EDR_454t"] == 20,
            "inventory pins the Treant, Pupil, Hammer and Egg builders");
        var declared = PayloadRules.EntityReferenceCardIds.ToArray();
        foreach (string id in declared)
        {
            Check(rows.TryGetValue(id, out int builder) && builder == PayloadRules.ReferenceBuilderType(Cards.FromId(id)),
                id + " is declared with its pinned dbf builder type");
            Check(Cards.FromId(id).Implemented, id + " declared reference host is implemented");
        }
        var defined = new HashSet<string>(Cards.All.Select(c => c.Id), StringComparer.Ordinal);
        var masked = PayloadRules.MaskedEntityBuilderCardIds;
        var unclassified = rows.Where(r => defined.Contains(r.Key) && !declared.Contains(r.Key) && !masked.Contains(r.Key) && !EntityBuilderVerdicts.ContainsKey(r.Key))
            .Select(r => r.Key + "(" + r.Value + ")").ToArray();
        Check(unclassified.Length == 0, "entity-builder cards neither declared, masked nor classified: " + string.Join(" ", unclassified));
        foreach (string id in masked)
            Check(rows.TryGetValue(id, out int builder) && builder != 18 && defined.Contains(id) && !declared.Contains(id),
                id + " masked card is a defined, undeclared entity-builder card");
        foreach (string id in EntityBuilderVerdicts.Keys)
            Check(rows.TryGetValue(id, out int builder) && builder == 18 && !declared.Contains(id), id + " verdict is stale");
    }
}
