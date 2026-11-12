using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Enchantments whose client text names a card ("another card" when tag 2 is not an entity the viewer's client knows):
// Ascended CATA_567e ("Summon {0}": the minion Ascendance transformed), Devoured Soul EDR_491e and Pack Mentality BE_731e
// ("Copied Deathrattle from {0}": the donor minion), Assembled BE_044e ("Assembled with {0}": the half that lent a keyword).
// Their engine tag 2 is a dbfId or was never written; each now carries an entity reference (PayloadRules.ReferenceEntity).
// Per card: positive (the entitled player's client can name it before the text is built), privacy (nobody else can),
// copies keeping the reference, a late reveal, and the Rewind projection from snapshots.
internal static partial class PoweredUpGlowTests
{
    [GroupProof("entity-reference-grants-native", ProofKind.Native)]
    private static void EntityReferenceGrantsNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            AscendedNamesItsOriginal(opponent);
            AssembledNamesItsHalf(opponent);
            PackMentalityNamesItsDonor(opponent);
            DevouredSoulNamesTheDeadMinion(opponent);
        }
        HiddenDonorIsNeverNamed();
        ResetProjectionFollowsSnapshots();
        StoredClassIsCovered();
        Console.WriteLine("PASS entity-reference grants: Ascended, Assembled, Devoured Soul, Pack Mentality (both seats, copies, late reveal, privacy, Rewind) and class inventory");
    }

    // Row index of the first entry that gives `id` a name (FULL/SHOW with a card id), or -1.
    private static int NamedAt(Fixture f, int id)
    {
        int index = 0;
        foreach (var e in f.Entries)
        {
            var snapshot = e.HasFullEntity ? e.FullEntity : e.HasShowEntity ? e.ShowEntity : e.HasChangeEntity ? e.ChangeEntity : null;
            if (snapshot != null && snapshot.Entity == id && !string.IsNullOrEmpty(snapshot.Name)) return index;
            index++;
        }
        return -1;
    }

    // Row index of the first entry that sends a non-zero tag 2 for `id` (snapshot tag or delta), or -1: when the client builds the text.
    private static int ReferenceSentAt(Fixture f, int id)
    {
        int index = 0;
        foreach (var e in f.Entries)
        {
            var snapshot = e.HasFullEntity ? e.FullEntity : e.HasShowEntity ? e.ShowEntity : e.HasChangeEntity ? e.ChangeEntity : null;
            if (snapshot != null && snapshot.Entity == id && snapshot.Tags.Any(t => t.Name == 2 && t.Value != 0)) return index;
            if (e.HasTagChange && e.TagChange.Entity == id && e.TagChange.Tag == 2 && e.TagChange.Value != 0) return index;
            index++;
        }
        return -1;
    }

    // The host's tag 2 names `expectedCard` to the viewer, and the entity it points at was named before the first row that sends it.
    private static void AssertNamesCard(Fixture f, IEntity host, string expectedCard, string label, int expectedId = 0)
    {
        var wire = ArchaedasWireTags(f, host.Id);
        Check(wire.TryGetValue(2, out int reference) && reference != 0 && (expectedId == 0 || reference == expectedId),
            label + ": tag 2 is an entity id (got " + (wire.TryGetValue(2, out int got) ? got : -1) + ", expected " + expectedId + ")");
        var names = new Dictionary<int, string>();
        foreach (var e in f.Entries)
        {
            var snapshot = e.HasFullEntity ? e.FullEntity : e.HasShowEntity ? e.ShowEntity : e.HasChangeEntity ? e.ChangeEntity : null;
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.Name)) names[snapshot.Entity] = snapshot.Name;
        }
        Check(names.TryGetValue(reference, out string name) && name == expectedCard,
            label + ": the client can name tag 2's entity " + expectedCard + " (got " + (name ?? "nothing") + ")");
        int named = NamedAt(f, reference), sent = ReferenceSentAt(f, host.Id);
        Check(named >= 0 && sent >= 0 && named < sent, label + ": the entity is named before the host's reference reaches the client (named row " + named + ", reference row " + sent + ")");
    }

    private static void AssertNamesNothing(Fixture f, IEntity host, IEnumerable<int> forbidden, string label)
    {
        var wire = ArchaedasWireTags(f, host.Id);
        Check(!wire.TryGetValue(2, out int value) || value == 0, label + ": no reference reaches the human (got " + value + ")");
        foreach (int id in forbidden)
            Check(!IsNamedToViewer(f, id), label + ": entity " + id + " is never named to the human");
    }

    // Ascendance transforms every friendly minion; each Ascended attachment names the original through a stored copy.
    private static void AscendedNamesItsOriginal(bool opponent)
    {
        string who = opponent ? "opposing " : "";
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var yeti = f.Summon("CS2_182", owner); f.Flush();
        var ascendance = Generic.DrawCard(owner, Cards.FromId("CATA_567")); f.Flush();
        Check(g.Process(PlayCardTask.Any(owner, ascendance)), "Ascendance play"); f.Flush();
        var body = owner.BoardZone.Single(m => m.Id == yeti.Id);
        Check(body.Card.Id != "CS2_182", "Ascendance transformed the minion");
        var attachment = body.AppliedEnchantments.Single(e => e.Card.Id == "CATA_567e");
        var stored = PayloadRules.Entries(attachment, 1).Single();
        Check(stored.Card.Id == "CS2_182" && attachment[PayloadRules.ReferenceEntity] == stored.Id
            && attachment[GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Card.AssetId && attachment.CapturedCard.Id == "CS2_182",
            who + "Ascended stores its original and keeps its engine dbfId and captured card");
        // The original stood face up on the board, so both players may read it while the Ascended minion is on the board.
        Check(PayloadRules.SharedWith(stored, 1) && PayloadRules.SharedWith(stored, 2), who + "the original is public information while its Ascended minion is on the board");
        AssertNamesCard(f, attachment, "CS2_182", who + "Ascended", stored.Id);
        Check(ArchaedasWireTags(f, attachment.Id)[2] != stored.Card.AssetId, who + "Ascended never sends the dbfId");

        // A copy keeps the reference: it names the same card and owns a stored copy of its own.
        var copy = (Minion)Generic.Copy(owner, ascendance, body, SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var copied = copy.AppliedEnchantments.Single(e => e.Card.Id == "CATA_567e");
        var duplicate = PayloadRules.Entries(copied, 1).Single();
        Check(copied[PayloadRules.ReferenceEntity] == stored.Id && duplicate.Id != stored.Id && duplicate.Card.Id == "CS2_182",
            who + "a copied Ascended keeps the reference and owns its own stored copy");
        AssertNamesCard(f, copied, "CS2_182", who + "copied Ascended", stored.Id);
        Check(IsNamedToViewer(f, duplicate.Id), who + "the copy's own stored copy is named once its minion is on the board");

        // Late reveal: a stored copy whose host sits in a hand names nothing, and is named the moment the host reaches the board.
        var heldMinion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
        var prepared = PayloadRules.PrepareCard(owner, Cards.FromId("CS2_231"));
        var heldAscended = Enchantment.GetInstance(owner, heldMinion, heldMinion, Cards.FromId("CATA_567e"), 0, 0, prepared.Id);
        PayloadRules.Bind(heldAscended, 1, prepared, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowAll);
        g.AuraUpdate(); f.Flush();
        Check(!PayloadRules.SharedWith(prepared, 1) && !IsNamedToViewer(f, prepared.Id), who + "a stored copy hosted by a card in a hand is never named");
        owner.BaseMana = 10; owner.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(owner, heldMinion)), "held minion played"); f.Flush();
        Check(heldMinion.Zone == owner.BoardZone && PayloadRules.SharedWith(prepared, 1) && IsNamedToViewer(f, prepared.Id),
            who + "the stored copy is named late, once its host is on the board");

        // Rewind: the reset rebuilds the stored entity's visibility from the restored snapshots, never from the live game.
        var method = typeof(NativeMatch).GetMethod("IsHiddenFromHuman", BindingFlags.Instance | BindingFlags.NonPublic);
        var snapshots = new Dictionary<int, SabberStoneCore.Kettle.PowerHistoryEntity>();
        foreach (var entity in new IPlayable[] { attachment, body, stored })
            snapshots[entity.Id] = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true).Entity;
        Check(!(bool)method.Invoke(f.Match, new object[] { stored.Id, null, snapshots }), who + "Rewind reset keeps the original named while the Ascended minion is on the board");
        snapshots[body.Id].Tags[GameTag.ZONE] = (int)SabberStoneCore.Enums.Zone.HAND;
        Check((bool)method.Invoke(f.Match, new object[] { stored.Id, null, snapshots }), who + "Rewind reset hides it when the restored minion is in a hand");
    }

    private static IPlayable AssembledHero(Fixture f, Controller owner, out Enchantment grant, out SabberStoneCore.Model.Card half)
    {
        var rajh = f.Summon("BE_004", owner);
        var definition = SabberStoneCore.Model.Card.CreateAssembledCard(Cards.FromId("TIME_100"), Cards.FromId("TIME_100"), Cards.FromId("CATA_999"));
        var power = BasicHeroPowerRules.InstallPassive(owner, rajh, definition);
        half = Cards.FromAssetId(power.Card[AssembleRules.FirstHalf]);
        Check(owner.Hero.HasDivineShield, "Rajh's passive grants the hero a keyword");
        grant = owner.Hero.AppliedEnchantments.Single(e => e.Card.Id == "BE_044e");
        return rajh;
    }

    // Rajh's passive: the hero's BE_044e names the half that lent the keyword, to its owner only.
    private static void AssembledNamesItsHalf(bool opponent)
    {
        string who = opponent ? "opposing " : "";
        var f = EntityReferenceFixture(opponent, out var owner);
        AssembledHero(f, owner, out var grant, out var half); f.Flush();
        var stored = PayloadRules.Entries(grant, 1).Single();
        Check(stored.Card.Id == half.Id && grant[PayloadRules.ReferenceEntity] == stored.Id, who + "Assembled stores the half that lent the keyword");
        if (opponent)
        {
            AssertNamesNothing(f, grant, new[] { stored.Id }, who + "Assembled");
            Check(!PayloadRules.SharedWith(stored, 1), who + "the opposing hero's half is private");
        }
        else
        {
            AssertNamesCard(f, grant, half.Id, "Assembled", stored.Id);
            Check(PayloadRules.SharedWith(stored, 1) && !PayloadRules.SharedWith(stored, 2), "Assembled's half is shared with its owner only");
        }
        // A reset builds the same decision from the snapshots.
        var method = typeof(NativeMatch).GetMethod("IsHiddenFromHuman", BindingFlags.Instance | BindingFlags.NonPublic);
        var snapshots = new Dictionary<int, SabberStoneCore.Kettle.PowerHistoryEntity>();
        foreach (var entity in new IPlayable[] { grant, stored })
            snapshots[entity.Id] = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true).Entity;
        Check((bool)method.Invoke(f.Match, new object[] { stored.Id, null, snapshots }) == opponent, who + "Rewind reset " + (opponent ? "hides" : "keeps") + " the Assembled half for the human");
    }

    // Pack Mentality: the donor is an adjacent board minion; the grant names it to both players, and a copy keeps the reference.
    private static void PackMentalityNamesItsDonor(bool opponent)
    {
        string who = opponent ? "opposing " : "";
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        var donor = f.Summon("EX1_029", owner); f.Flush();
        Check(IsNamedToViewer(f, donor.Id), who + "the donor stands named on the board");
        var rockfang = Generic.DrawCard(owner, Cards.FromId("BE_731")); f.Flush();
        Check(g.Process(PlayCardTask.Any(owner, rockfang)), "Primal Rockfang play"); f.Flush();
        var grant = rockfang.AppliedEnchantments.Single(e => e.Card.Id == "BE_731e");
        Check(grant[PayloadRules.ReferenceEntity] == donor.Id && PayloadRules.Entries(grant).Length == 0 && grant[GameTag.TAG_SCRIPT_DATA_NUM_1] == donor.Card.AssetId,
            who + "Pack Mentality references the donor minion itself (no new entity) and keeps its engine dbfId");
        AssertNamesCard(f, grant, "EX1_029", who + "Pack Mentality", donor.Id);
        // The donor dies: the grant, already built, is unchanged; a copy of the recipient keeps naming the donor.
        donor.Destroy(); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var copy = (Minion)Generic.Copy(owner, rockfang, rockfang, SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var copied = copy.AppliedEnchantments.Single(e => e.Card.Id == "BE_731e");
        Check(copied[PayloadRules.ReferenceEntity] == donor.Id, who + "a copied Pack Mentality keeps the donor reference");
        AssertNamesCard(f, copied, "EX1_029", who + "copied Pack Mentality", donor.Id);
    }

    // Archdruid of Thorns: the donors are friendly minions that died this turn. A plain printed Deathrattle and a scripted one
    // (kept in a private death ledger) both name the minion that died, never the ledger entity.
    private static void DevouredSoulNamesTheDeadMinion(bool opponent)
    {
        string who = opponent ? "opposing " : "";
        var f = EntityReferenceFixture(opponent, out var owner); var g = f.Game;
        g[PayloadRules.Tracking] = 1;
        var plain = f.Summon("EX1_029", owner);
        var scripted = f.Summon("CATA_210", owner); scripted[GameTag.TAG_SCRIPT_DATA_NUM_1] = 4; f.Flush();
        plain.Destroy(); scripted.Destroy(); g.DeathProcessingAndAuraUpdate(); f.Flush();
        var ledgers = PayloadRules.Entries(owner, 16002);
        Check(ledgers.Length == 1, who + "the scripted Deathrattle is kept in a private death ledger (plain printed ones are not)");
        var archdruid = Generic.DrawCard(owner, Cards.FromId("EDR_491")); f.Flush();
        Check(g.Process(PlayCardTask.Any(owner, archdruid)), "Archdruid of Thorns play"); f.Flush();
        var grants = archdruid.AppliedEnchantments.Where(e => e.Card.Id == "EDR_491e").ToArray();
        Check(grants.Length == 2, who + "Archdruid gains both Deathrattles");
        Check(grants.Select(e => e[PayloadRules.ReferenceEntity]).OrderBy(id => id).SequenceEqual(new[] { plain.Id, scripted.Id }.OrderBy(id => id)),
            who + "each Devoured Soul references the minion that died (plain: its death fact, scripted: through the ledger)");
        foreach (var grant in grants)
        {
            int expected = grant[PayloadRules.ReferenceEntity];
            string card = g.IdEntityDic[expected].Card.Id;
            AssertNamesCard(f, grant, card, who + "Devoured Soul (" + card + ")", expected);
            Check(ledgers.All(l => l.Id != ArchaedasWireTags(f, grant.Id)[2]) && ledgers.All(l => !IsNamedToViewer(f, l.Id)),
                who + "the private death ledger is never referenced or named");
        }
    }

    // The reference is never a way to read a hidden card: a donor the human cannot see is not named.
    private static void HiddenDonorIsNeverNamed()
    {
        var f = EntityReferenceFixture(false, out var human); var g = f.Game;
        var npc = g.Player2;
        var hiddenDonor = Generic.DrawCard(npc, Cards.FromId("EX1_029")); f.Flush();
        var recipient = f.Summon("CS2_182", human); f.Flush();
        PayloadRules.GainDeathrattles(recipient, hiddenDonor, "BE_731e"); f.Flush();
        var grant = recipient.AppliedEnchantments.Single(e => e.Card.Id == "BE_731e");
        Check(grant[PayloadRules.ReferenceEntity] == hiddenDonor.Id, "the grant references the NPC's card in hand");
        AssertNamesNothing(f, grant, new[] { hiddenDonor.Id }, "donor in the opponent's hand");
    }

    // Rewind builds reference text from the restored snapshots (ProjectResetReference): the donor must be public in them.
    private static void ResetProjectionFollowsSnapshots()
    {
        var f = EntityReferenceFixture(false, out var human); var g = f.Game;
        var project = typeof(NativeMatch).GetMethod("ProjectResetReference", BindingFlags.Instance | BindingFlags.NonPublic);
        var donor = f.Summon("EX1_029", human); var other = f.Summon("CS2_182", human);
        var npcCard = Generic.DrawCard(g.Player2, Cards.FromId("EX1_029")); f.Flush();
        var snapshots = new Dictionary<int, SabberStoneCore.Kettle.PowerHistoryEntity>();
        foreach (var entity in new IPlayable[] { donor, other, npcCard })
            snapshots[entity.Id] = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true).Entity;
        foreach (string id in new[] { "BE_731e", "EDR_491e" })
        {
            int Project(int reference) => (int)project.Invoke(f.Match, new object[] { Cards.FromId(id), reference, snapshots });
            Check(Project(donor.Id) == donor.Id, id + " Rewind reset keeps a donor that is public in the restored snapshots");
            Check(Project(npcCard.Id) == 0, id + " Rewind reset sends 0 for a donor that is hidden in the restored snapshots");
            Check(Project(123456) == 0, id + " Rewind reset sends 0 for an entity the restored game does not have");
        }
    }

    // Class inventory (scenario coverage): a card that names a stored or referenced card must have a scenario above or in the
    // earlier entity-reference tests, and no card still sending "another card" may be reachable in a shipped meta.
    private static void StoredClassIsCovered()
    {
        var covered = new HashSet<string>(StringComparer.Ordinal) {
            "MEND_046t", "TIME_704t", "BE_055t", "EDR_454t", "CATA_563e2", "CATA_897e3", "EDR_494e", "EDR_259e1", "TLC_841e", // EntityReferenceText
            "CATA_567e", "EDR_491e", "BE_731e", "BE_044e" };                                                                   // this file
        var declared = PayloadRules.EntityReferenceCardIds.ToArray();
        var missing = declared.Where(id => !covered.Contains(id)).ToArray();
        Check(missing.Length == 0, "declared reference hosts without a native scenario: " + string.Join(" ", missing));
        foreach (string id in declared.Where(id => PayloadRules.UsesReferenceTag(Cards.FromId(id))))
            Check(!PayloadRules.MasksReference(Cards.FromId(id)), id + " sends a reference tag, so it is not masked");
        // Masked cards keep sending "another card": that is only acceptable for Wild-only legacy cards outside every shipped pool.
        string catalog = File.ReadAllText("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml");
        var reachable = PayloadRules.MaskedEntityBuilderCardIds.Where(id => Regex.IsMatch(catalog, "cardId=\"" + Regex.Escape(id) + "\"")).ToArray();
        Check(reachable.Length == 0, "masked entity-builder cards reachable in an offline pool (they would read 'another card'; give them an entity reference): " + string.Join(" ", reachable));
    }
}
