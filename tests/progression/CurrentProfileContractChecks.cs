using System.Xml.Serialization;

internal static class CurrentProfileContractChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static LocalProfileData Read(string path)
    {
        using var stream = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream)!;
    }
    private static void Write(string path, LocalProfileData data)
    {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
    }

    internal static void Run(string root)
    {
        Settlement(root);
        NativePack(root);
        DeckLibrary(root);
        LegacyAzalinaDraft(root);
        Console.WriteLine("PASS current profile contracts: settlement, native pack, deck CRUD/security/restart");
    }

    private static void LegacyAzalinaDraft(string root)
    {
        string path = Path.Combine(root, "legacy-azalina.xml");
        long id;
        using (var profile = new LocalProfile(path)) id = profile.CreateDraft("Old Azalina", "HERO_09").Id;
        var data = Read(path);
        var twenty = LocalDecks.Get("test-standard-azalina-priest").CardIds;
        var extra = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Select(SabberStoneCore.Model.Cards.FromId)
            .Where(card => LocalCardCatalog.InActiveMeta(card) && LocalDecks.IsAllowedInDeck(card, SabberStoneCore.Enums.CardClass.PRIEST)
                && !twenty.Contains(card.Id) && !SabberStoneCore.Actions.FabledRules.IsFabled(card.Id)
                && !SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(card.Id) && card[(SabberStoneCore.Enums.GameTag)997] == 0)
            .Take(5).Select(card => card.Id).ToArray();
        var oversized = twenty.Concat(extra).ToList();
        data.CustomDecks.Single().Cards = oversized;
        Write(path, data);
        byte[] before = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            var saved = profile.GetDraft(id);
            Check(saved.Cards.SequenceEqual(oversized) && !profile.IsDeckComplete(id), "old 25-card Azalina draft must load incomplete");
            Reject(() => LocalProfile.PlayableDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards), "old draft must not play");
            Check(profile.VisibleDeckSummaries().Single().DeckSize == 20, "legacy draft diagnostic retains twenty limit");
            // Native bootstrap projects every saved deck through the incomplete ledger path.
            Check(LocalProfile.LedgerDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards, false).CardIds.Count == 25,
                "native bootstrap projects the oversized Azalina draft");
        }
        Check(File.ReadAllBytes(path).SequenceEqual(before), "legacy draft reload must preserve bytes");
        data.CustomDecks.Single().Cards = oversized.Concat(extra).Append(extra[0]).ToList();
        Write(path, data);
        Reject(() => { using var ignored = new LocalProfile(path); }, "stored drafts still reject above thirty");
        Console.WriteLine("PASS legacy Azalina drafts preserve 25 cards, project, stay incomplete and do not play");
    }

    private static void Settlement(string root)
    {
        string path = Path.Combine(root, "current-settlement.xml");
        string id;
        using (var profile = new LocalProfile(path))
        {
            var pending = profile.Begin("mage", "priest", 401); id = pending.Id;
            byte[] before = File.ReadAllBytes(path);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("settlement fault"); };
            try { Reject(() => profile.Settle(id, "Won", 8, false), "settlement save fault"); }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.Pending?.Id == id && profile.History.Count == 0,
                "settlement failure changed bytes or memory");
            var settled = profile.Settle(id, "Won", 8, false);
            int gold = profile.Gold; long dust = profile.DustBalance;
            Check(profile.Settle(id, "Won", 8, false).Id == id && profile.Gold == gold && profile.DustBalance == dust,
                "settlement retry duplicated payout");
            Reject(() => profile.Settle(id, "Lost", 8, false), "conflicting settlement retry");
        }
        using (var profile = new LocalProfile(path))
            Check(profile.PendingRewardPresentation?.Id == id && profile.AcknowledgeRewardPresentation(id)
                && !profile.AcknowledgeRewardPresentation(id), "settlement receipt/ack restart contract");
        var duplicate = Read(path); duplicate.Matches.Add(duplicate.Matches[0]);
        string forged = Path.Combine(root, "current-settlement-forged.xml"); Write(forged, duplicate); byte[] forgedBytes = File.ReadAllBytes(forged);
        Reject(() => { using var ignored = new LocalProfile(forged); }, "duplicate settled receipt");
        Check(File.ReadAllBytes(forged).SequenceEqual(forgedBytes), "forged settlement rejection rewrote bytes");
    }

    private static void NativePack(string root)
    {
        string path = Path.Combine(root, "current-pack.xml");
        int sequence; string[] cards;
        using (var profile = new LocalProfile(path))
        {
            profile.CommitChange(next => { next.GoldBalance = 100; return true; });
            profile.BuyPack();
            byte[] before = File.ReadAllBytes(path);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("pack fault"); };
            try { Reject(() => profile.BeginNativePack(FairPackRules.Standard), "native pack save fault"); }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.PackSequence == 0 && profile.UnopenedPacks == 1,
                "native pack failure changed inventory/receipt");
            var receipt = profile.BeginNativePack(FairPackRules.Standard); sequence = profile.PackSequence;
            cards = receipt.Cards.Select(card => card.Key).ToArray();
            Check(profile.BeginNativePack(FairPackRules.Standard).Cards.Select(card => card.Key).SequenceEqual(cards),
                "pending native pack replay changed cards");
            Reject(() => profile.OpenPack(FairPackRules.Standard), "pending native pack allowed another draw");
        }
        using (var profile = new LocalProfile(path))
        {
            Check(sequence == profile.PackSequence && profile.PackRevealPending && profile.LastPack.Cards.Select(card => card.Key).SequenceEqual(cards),
                "native pack pending receipt lost on restart");
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("pack ack fault"); };
            try { Reject(() => profile.CompleteNativePackReveal(sequence, cards), "native pack acknowledgement fault"); }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(profile.PackRevealPending && profile.CompleteNativePackReveal(sequence, cards)
                && !profile.CompleteNativePackReveal(sequence, cards), "native pack acknowledgement retry/idempotency");
        }
        var invalid = Read(path); invalid.PackRevealPending = true; invalid.LastPack.PackRuleVersion++;
        string forged = Path.Combine(root, "current-pack-forged.xml"); Write(forged, invalid); byte[] forgedBytes = File.ReadAllBytes(forged);
        Reject(() => { using var ignored = new LocalProfile(forged); }, "forged native pack receipt");
        Check(File.ReadAllBytes(forged).SequenceEqual(forgedBytes), "forged pack rejection rewrote bytes");
    }

    private static void DeckLibrary(string root)
    {
        string path = Path.Combine(root, "current-decks.xml");
        long first, second;
        using (var profile = new LocalProfile(path))
        {
            first = profile.CreateDraft("First", "HERO_08").Id;
            second = profile.CreateDraft("Second", "HERO_08").Id;
            profile.SaveDraft(first, "First saved", "HERO_08", Array.Empty<string>(), 4);
            var escaped = profile.GetDraft(first); escaped.Name = "mutated"; escaped.Cards.Add("BAD");
            Check(profile.GetDraft(first).Name == "First saved" && profile.GetDraft(first).Cards.Count == 0,
                "draft getter escaped by reference");
            profile.ReorderDrafts(new[] { second, first });
            Reject(() => profile.ReorderDrafts(new[] { first, first }), "invalid reorder permutation");
            byte[] before = File.ReadAllBytes(path);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("draft fault"); };
            try { Reject(() => profile.DeleteDraft(first), "draft delete save fault"); }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.GetDraft(first) != null,
                "failed draft delete changed bytes or memory");
            profile.DeleteDraft(first);
            Check(profile.VisibleDeckSummaries().Single().Id == second && profile.MatchCount == profile.History.Count,
                "non-copying profile status differs from durable library");
            var ui = new LocalCollectionUI(profile);
            ui.SelectCustom();
            Check(ui.SelectedDeck == "custom:" + second, "UI did not select the explicit saved deck");
            // A Fabled deck (Timethief Rafaam's bundled past Rafaams) once made DraftCardIds throw on every status write.
            var rafaam = LocalShop.Default.Recipe("rafaam-warlock").Cards.SelectMany(row => Enumerable.Repeat(row.Id, row.Count)).ToArray();
            profile.SaveDraft(second, "Second", "HERO_07", rafaam, 4);
            var fabled = new LocalCollectionUI(profile).DraftCardIds.Split(',');
            Check(fabled.Length == 40 && fabled.Contains("TIME_005t1") && fabled.Contains("CATA_190h"), "Fabled deck draft ids did not resolve");
        }
        using (var profile = new LocalProfile(path))
            Check(profile.GetDraft(first) == null && profile.GetDraft(second)?.Name == "Second",
                "deck CRUD/reorder state changed on restart");
    }
}
