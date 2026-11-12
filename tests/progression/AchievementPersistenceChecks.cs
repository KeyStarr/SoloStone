using System.Xml.Serialization;

internal static class AchievementPersistenceChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
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
        string fresh = Path.Combine(root, "achievement-fresh.xml");
        string missingSnapshot = Path.Combine(root, "achievement-missing-snapshot.xml");
        Reject(() => typeof(LocalProfile).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(bool) }, null)!.Invoke(new object[] { missingSnapshot, true }), "missing snapshot");
        Check(!File.Exists(missingSnapshot), "missing snapshot created profile bytes");
        using (var profile = new LocalProfile(fresh))
        {
            profile.CommitChange(next => true);
            var state = profile.Achievements;
            Check(Read(fresh).Version == 15 && state.CatalogVersion == 1 && state.Unlocks.Count == 0 && state.Grants.Count == 0
                && state.Career.EligibleWins == 0 && state.Career.ClassWins.Count == 11
                && state.Career.ClassWins.All(row => row.Wins == 0) && profile.PendingNonMatchAchievementNoticeId == null,
                "fresh V15 achievement defaults");
            state.Career.ClassWins[0].Wins = 9;
            Check(profile.Achievements.Career.ClassWins[0].Wins == 0, "achievement state escaped by reference");
        }

        string old = Path.Combine(root, "achievement-v12.xml");
        var v12 = Read(fresh); v12.Version = 12; Write(old, v12); byte[] oldBytes = File.ReadAllBytes(old);
        Reject(() => { using var ignored = new LocalProfile(old); }, "old debug profile");
        Check(File.ReadAllBytes(old).SequenceEqual(oldBytes) && !File.Exists(old + ".v12.bak"), "old profile rejection changed bytes");
        string future = Path.Combine(root, "achievement-v16.xml");
        var v16 = Read(fresh); v16.Version = 16; Write(future, v16); byte[] futureBytes = File.ReadAllBytes(future);
        Reject(() => { using var ignored = new LocalProfile(future); }, "future profile");
        Check(File.ReadAllBytes(future).SequenceEqual(futureBytes), "future profile rejection changed bytes");

        string rewards = Path.Combine(root, "achievement-v4.xml");
        using (var profile = new LocalProfile(rewards))
        {
            var pending = profile.Begin("mage", "priest", 77);
            var expected = LocalEconomy.RewardV6("Won", 8, false, LocalEconomy.RewardRollSeed(pending.Id, pending.Seed));
            var settled = profile.Settle(pending.Id, "Won", 8, false,
                new LocalRewardFacts { DistinctCardsPlayed = 99, EnemyMinionsDefeated = 99, MinimumHealth = 1 });
            Check(settled.RewardVersion == LocalEconomy.RollRewardVersion && settled.Gold == profile.GrindTier.Gold(expected.Gold) && settled.DustReward == profile.GrindTier.Dust(expected.Dust)
                && settled.BonusGold == 0 && !settled.PackReward && settled.AchievementContext == null
                && settled.AchievementGrants.Count == 0 && profile.Achievements.Unlocks.Count == 0,
                "ineligible V4 settlement stays ordinary");
            profile.AcknowledgeRewardPresentation(settled.Id);
            var loss = profile.Begin("mage", "priest", 78);
            var lossRoll = LocalEconomy.RewardV6("Lost", 8, false, LocalEconomy.RewardRollSeed(loss.Id, loss.Seed));
            var lost = profile.Settle(loss.Id, "Lost", 8, false);
            Check(lost.Gold == profile.GrindTier.Gold(lossRoll.Gold) && lost.DustReward == profile.GrindTier.Dust(lossRoll.Dust), "V5 loss roll");
            profile.AcknowledgeRewardPresentation(loss.Id);
        }

        string atomic = Path.Combine(root, "achievement-atomic.xml");
        using (var profile = new LocalProfile(atomic))
        {
            var pending = profile.Begin("mage", "priest", 79);
            byte[] before = File.ReadAllBytes(atomic);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("achievement fault"); };
            try { profile.Settle(pending.Id, "Won", 8, false); throw new Exception("Expected save failure"); }
            catch (IOException) { }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(File.ReadAllBytes(atomic).SequenceEqual(before) && profile.Pending?.Id == pending.Id
                && profile.Gold == 0 && profile.Achievements.Unlocks.Count == 0, "failed V4 settlement mutated state");
            var settled = profile.Settle(pending.Id, "Won", 8, false);
            int gold = profile.Gold;
            Check(profile.Settle(pending.Id, "Won", 8, false).Id == settled.Id && profile.Gold == gold,
                "V4 retry duplicated reward");
        }

#if ENGINE_SOURCE
        CollectionUnlock(root);
#endif

        string corrupt = Path.Combine(root, "achievement-corrupt.xml");
        using (var profile = new LocalProfile(corrupt)) profile.SelectCardBack(0);
        var bad = Read(corrupt);
        bad.Achievements.Unlocks.Add(new LocalAchievementUnlock { Id = "calculated", CatalogVersion = 1,
            UnlockedUtc = DateTime.UtcNow.ToString("o"), Provenance = "Match", MatchId = Guid.NewGuid().ToString(),
            Witness = new LocalAchievementWitnessRecord { Kind = "Terminal", Sequence = 1 } });
        Write(corrupt, bad); byte[] corruptBytes = File.ReadAllBytes(corrupt);
        Reject(() => { using var ignored = new LocalProfile(corrupt); }, "unpaired achievement unlock");
        Check(File.ReadAllBytes(corrupt).SequenceEqual(corruptBytes), "corrupt achievement rejection rewrote save");
        Console.WriteLine("PASS achievement persistence: fresh V14, V4, atomic retry, craft/collection notice, strict rejection");
    }

#if ENGINE_SOURCE
    private static void CollectionUnlock(string root)
    {
        VirtualOwnershipDoesNotQualify(root);
        string path = Path.Combine(root, "achievement-collection.xml");
        string packType = FairPackRules.PackTypes
            .Where(type => type != FairPackRules.Standard && type != FairPackRules.Legacy)
            .OrderBy(type => FairPackRules.Eligible(type, FairPackRules.RuleVersion).Count)
            .First();
        var sets = FairPackRules.Eligible(packType, FairPackRules.RuleVersion).Select(card => card.Set).ToHashSet();
        var roster = LocalShop.StandardCollectionCards.Where(card => !LocalCardCatalog.IsCore(card.Id) && sets.Contains(card.Set))
            .GroupBy(card => LocalCardCatalog.CopyGroup(card.Id), StringComparer.Ordinal)
            .Select(group => group.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
        var last = roster.First(card => LocalCrafting.GetIneligibility(card.Id) == CraftIneligibility.None);

        using (var profile = new LocalProfile(path))
        {
            profile.CommitChange(next => {
                var byId = next.Collection.ToDictionary(card => card.Id, StringComparer.Ordinal);
                foreach (var card in roster)
                {
                    int needed = LocalEconomy.PlayableCap(card);
                    if (card.Id == last.Id) needed--;
                    if (!byId.TryGetValue(card.Id, out var row)) {
                        row = new LocalCardCount { Id = card.Id }; next.Collection.Add(row); byId.Add(card.Id, row);
                    }
                    row.Count = Math.Max(row.Count, needed); row.Acquired = Math.Max(row.Acquired, needed);
                }
                next.DustBalance = 10000;
                return true;
            });
            var plan = new LocalCraftingLedger(profile).CommitBatch(last.Id, CraftDirection.Craft, 1, profile.OwnedCount(last.Id));
            Check(plan.Ok && profile.Achievements.Unlocks.Any(row => row.Id == "the-complete-works")
                && profile.Achievements.Grants.Single(row => row.AchievementId == "the-complete-works").CardBackId == 189
                && profile.OwnedCardBacks.Contains(189) && profile.PendingNonMatchAchievementNoticeId == "the-complete-works",
                "collection craft did not atomically grant #55 and notice");

            profile.CommitChange(next => { next.GoldBalance = checked(next.GoldBalance + 100); return true; });
            profile.BuyPack();
            var pendingPack = profile.BeginNativePack(FairPackRules.Standard);
            Reject(() => profile.AcknowledgeNonMatchAchievementNotice("the-complete-works"), "notice during pack reveal");
            Check(profile.PendingNonMatchAchievementNoticeId == "the-complete-works"
                && profile.CompleteNativePackReveal(profile.PackSequence, pendingPack.Cards.Select(card => card.Key)),
                "pack reveal did not preserve the collection notice");

            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("notice fault"); };
            try { profile.AcknowledgeNonMatchAchievementNotice("the-complete-works"); throw new Exception("Expected notice save failure"); }
            catch (IOException) { }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(profile.PendingNonMatchAchievementNoticeId == "the-complete-works", "failed notice acknowledgement mutated state");
            Check(profile.AcknowledgeNonMatchAchievementNotice("the-complete-works")
                && !profile.AcknowledgeNonMatchAchievementNotice("the-complete-works"), "notice acknowledgement was not idempotent");
        }
        using (var restarted = new LocalProfile(path))
            Check(restarted.PendingNonMatchAchievementNoticeId == null
                && restarted.Achievements.Unlocks.Count(row => row.Id == "the-complete-works") == 1,
                "collection grant/acknowledgement did not survive restart");
        foreach (var corruption in new Action<LocalProfileData>[] {
            save => save.Achievements.Grants.Single(row => row.AchievementId == "the-complete-works").CardBackId = 0,
            save => save.OwnedCardBacks.Remove(189),
            save => save.PendingNonMatchAchievementNoticeId = "calculated"
        })
        {
            var invalid = Read(path); corruption(invalid);
            string corrupt = Path.Combine(root, "achievement-collection-corrupt-" + Guid.NewGuid() + ".xml");
            Write(corrupt, invalid); byte[] before = File.ReadAllBytes(corrupt);
            Reject(() => { using var ignored = new LocalProfile(corrupt); }, "collection receipt/ownership corruption");
            Check(File.ReadAllBytes(corrupt).SequenceEqual(before), "collection corruption rejection changed bytes");
        }
        ExclusiveCosmetics(root, path);
    }

    private static void ExclusiveCosmetics(string root, string collectionGrantPath)
    {
        string basePath = Path.Combine(root, "achievement-exclusive-base.xml");
        LocalAchievementPendingContext context;
        using (var profile = new LocalProfile(basePath))
        {
            context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-exclusive", 2500, "Diamond");
            var pending = profile.Begin("mage", "priest", 812, "npc-exclusive", "priest", 2500);
            profile.Settle(pending.Id, "Lost", 8, false, null, 2500, false);
            profile.AcknowledgeRewardPresentation(pending.Id);
            foreach (var row in AchievementExclusiveCosmetics.All)
            {
                if (row.Kind == "CardBack") Reject(() => profile.SelectCardBack(row.CardBackId), "ungranted back selection " + row.Id);
                else Reject(() => profile.SetFavoriteHeroSkin(row.Class, row.HeroSkinId), "ungranted skin selection " + row.Id);
            }
        }
        var eligible = Read(basePath);
        var eligibleMatch = eligible.Matches.Single();
        context.MatchId = eligibleMatch.Id;
        eligibleMatch.AchievementContext = context;
        eligibleMatch.AchievementEvidenceFingerprint = new string('A', 64);
        Write(basePath, eligible);
        using (var validated = new LocalProfile(basePath)) Check(validated.Achievements.Grants.Count == 0, "valid exclusive test fixture");
        HeroSkinGrants(root, basePath);

        foreach (var cosmetic in AchievementExclusiveCosmetics.All)
        {
            string path = Path.Combine(root, "achievement-exclusive-" + cosmetic.Id + ".xml");
            if (cosmetic.CardBackId == 189) File.Copy(collectionGrantPath, path);
            else
            {
                var data = Read(basePath);
                var definition = LocalAchievementCatalog.All.Single(row =>
                    row.Reward.CardBackId == cosmetic.CardBackId && cosmetic.CardBackId > 0
                    || row.Reward.HeroSkinId == cosmetic.HeroSkinId && cosmetic.HeroSkinId != null);
                var receipt = LocalAchievementGrantReceipt.FromCatalog(definition.Id);
                var match = data.Matches.Single();
                data.Achievements.Grants.Add(receipt);
                match.AchievementGrants.Add(LocalProfile.Clone(receipt));
                data.PendingAchievementToastMatchIds.Add(match.Id);
                data.Achievements.Unlocks.Add(new LocalAchievementUnlock { Id = definition.Id, CatalogVersion = LocalAchievementCatalog.Version,
                    UnlockedUtc = DateTime.UtcNow.ToString("o"), MatchId = match.Id, Provenance = "Match", Witness = ValidWitness(definition.Id) });
                if (cosmetic.Kind == "CardBack") data.OwnedCardBacks.Add(cosmetic.CardBackId);
                else data.OwnedHeroSkins.Add(cosmetic.HeroSkinId);
                Write(path, data);
            }
            using (var profile = new LocalProfile(path))
            {
                if (cosmetic.Kind == "CardBack") profile.SelectCardBack(cosmetic.CardBackId);
                else Check(profile.FavoriteHero(cosmetic.Class) == cosmetic.HeroSkinId,
                    "load adopts the only achievement skin " + cosmetic.Id);
                if (cosmetic.CardBackId != 189)
                    Check(profile.PendingAchievementToast?.AchievementGrants.Count == 1,
                        "cosmetic-only receipt retains a pending toast: " + cosmetic.Id);
            }
            byte[] adopted = File.ReadAllBytes(path);
            using (var reloaded = new LocalProfile(path))
                Check(cosmetic.Kind == "CardBack" ? reloaded.OwnsCardBack(cosmetic.CardBackId) && reloaded.FavoriteCardBacks.Single() == cosmetic.CardBackId
                    : reloaded.OwnsHeroSkin(cosmetic.HeroSkinId) && reloaded.FavoriteHeroSkins[cosmetic.Class] == cosmetic.HeroSkinId,
                    "achievement cosmetic selection/reload " + cosmetic.Id);
            Check(adopted.SequenceEqual(File.ReadAllBytes(path)), "achievement skin adoption is idempotent " + cosmetic.Id);
        }
    }

    private static void HeroSkinGrants(string root, string basePath)
    {
        var grant = typeof(LocalProfile).GetMethod("ApplyAchievementGrant",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var cosmetic in AchievementExclusiveCosmetics.All.Where(row => row.Kind == "HeroSkin"))
        foreach (string choice in new[] { (string)null, cosmetic.Class, LocalClassRewards.Portraits.Single(row => row.Hero == cosmetic.Class).Honored })
        {
            string path = Path.Combine(root, "achievement-equip-" + cosmetic.HeroSkinId + "-" + (choice ?? "unset") + ".xml");
            File.Copy(basePath, path);
            var definition = LocalAchievementCatalog.All.Single(row => row.Reward.HeroSkinId == cosmetic.HeroSkinId);
            var options = new ProfileOptions { AllCosmetics = true };
            using (var profile = new LocalProfile(path, options))
            {
                if (choice != null) profile.SetFavoriteHeroSkin(cosmetic.Class, choice);
                bool Apply(LocalProfileData next) => (bool)grant.Invoke(null, new object[] {
                    next, definition.Id, "Match", next.Matches.Single(), ValidWitness(definition.Id) })!;
                byte[] before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("skin grant fault"); };
                try { profile.CommitChange(Apply); throw new Exception("Expected skin grant failure"); }
                catch (IOException) { }
                finally { LocalProfile.CommitFaultHook = null; }
                Check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.Achievements.Grants.Count == 0
                    && profile.FavoriteHero(cosmetic.Class) == (choice ?? cosmetic.Class), "failed skin grant is atomic");
                Check(profile.CommitChange(Apply), "first skin grant succeeds");
                Check(profile.OwnedHeroSkins.Contains(cosmetic.HeroSkinId)
                    && profile.FavoriteHero(cosmetic.Class) == (choice ?? cosmetic.HeroSkinId),
                    "grant equips unset favourite and keeps explicit choice " + cosmetic.Id + "/" + choice);
                Check(!profile.CommitChange(Apply) && profile.Achievements.Grants.Count == 1, "skin grant retry is idempotent");
            }
            using (var profile = new LocalProfile(path, options))
                Check(profile.FavoriteHero(cosmetic.Class) == (choice ?? cosmetic.HeroSkinId), "skin favourite survives load");
        }
        Console.WriteLine("PASS achievement skin grants: auto-equip, explicit base/skin, atomic failure, retry, reload");
    }

    private static LocalAchievementWitnessRecord ValidWitness(string id)
    {
        string kind = (string)typeof(LocalProfile).GetMethod("ExpectedWitnessKind",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { id })!;
        var row = new LocalAchievementWitnessRecord { Kind = kind };
        switch (kind)
        {
            case "Terminal": row.Sequence = 1; break;
            case "Resolution": row.Sequence = 1; row.EntityIds.Add(1); break;
            case "Attack": row.Sequence = 1; row.AttackId = 1; row.EntityId = 1; break;
            case "Turn": row.Sequence = 1; row.Turn = 1; break;
            case "Entity": row.Sequence = 1; row.EntityId = 1; break;
            case "Deck": row.BaseCardIds.Add("CS2_029"); break;
            case "Career": row.Value = id == "the-board-is-a-workplace-hazard" ? 1000 : 1; break;
            case "Threshold": row.Sequence = 1; row.Value = 1; break;
            default: throw new Exception("Unknown achievement witness kind: " + kind);
        }
        return row;
    }

    private static void VirtualOwnershipDoesNotQualify(string root)
    {
        // Fabled primaries and sideboard owners: ordinary collectible cards since 2026-10-30 (owner), never owned virtually.
        var virtualIds = LocalDecks.KnownBundleAndSideboardCards.Where(LocalCardCatalog.InActiveMeta)
            .Select(card => LocalCardCatalog.CopyGroup(card.Id))
            .Where(KnownCraftIdentity)
            .ToHashSet(StringComparer.Ordinal);
        var rosters = FairPackRules.PackTypes.Where(type => type != FairPackRules.Standard && type != FairPackRules.Legacy)
            .Select(type => new {
                Type = type,
                Cards = LocalShop.StandardCollectionCards.Where(card => !LocalCardCatalog.IsCore(card.Id)
                    && FairPackRules.Eligible(type, FairPackRules.RuleVersion).Select(pool => pool.Set).Contains(card.Set))
                    .GroupBy(card => LocalCardCatalog.CopyGroup(card.Id), StringComparer.Ordinal).Select(group => group.First()).ToArray()
            }).ToArray();
        var roster = rosters.First(row => row.Cards.Any(card => virtualIds.Contains(LocalCardCatalog.CopyGroup(card.Id))));
        string virtualId = roster.Cards.Select(card => LocalCardCatalog.CopyGroup(card.Id)).First(virtualIds.Contains);
        string path = Path.Combine(root, "achievement-virtual-ownership.xml");
        using (var profile = new LocalProfile(path))
        {
            profile.CommitChange(next => {
                foreach (var card in roster.Cards.Where(card => LocalCardCatalog.CopyGroup(card.Id) != virtualId))
                    next.Collection.Add(new LocalCardCount { Id = card.Id, Count = LocalEconomy.PlayableCap(card), Acquired = LocalEconomy.PlayableCap(card) });
                LocalProfile.ReconcileCollectionAchievement(next);
                return true;
            });
            Check(!profile.Achievements.Unlocks.Any(row => row.Id == "the-complete-works"),
                "Fabled/sideboard expansion card completed #55 without real ownership");
            Check(!profile.Collection.ContainsKey(virtualId), "Fabled/sideboard card owned without a ledger copy: " + virtualId);
            // The Complete Works stays completable: a real ledger copy (pack, craft or deck purchase) finishes the roster.
            var missing = roster.Cards.First(card => LocalCardCatalog.CopyGroup(card.Id) == virtualId);
            profile.CommitChange(next => {
                next.Collection.Add(new LocalCardCount { Id = missing.Id, Count = LocalEconomy.PlayableCap(missing), Acquired = LocalEconomy.PlayableCap(missing) });
                LocalProfile.ReconcileCollectionAchievement(next);
                return true;
            });
            Check(profile.Achievements.Unlocks.Any(row => row.Id == "the-complete-works"),
                "#55 is not completable with a real Fabled/sideboard copy: " + virtualId);
        }

        var realPrints = LocalShop.StandardCollectionCards.GroupBy(card => LocalCardCatalog.CopyGroup(card.Id), StringComparer.Ordinal)
            .Where(group => KnownCraftIdentity(group.Key))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var decks = LocalDecks.All.Concat(LocalDecks.TestLoaners).Concat(LocalDecks.PlayerStarterDecks).ToArray();
        var craftedDeck = decks.First(deck => {
            var groups = deck.CardIds.Select(LocalCardCatalog.CopyGroup).Distinct(StringComparer.Ordinal).ToArray();
            return groups.Any(virtualIds.Contains) && groups.Count(id => realPrints.ContainsKey(id) && !virtualIds.Contains(id)) >= 9;
        });
        var deckGroups = craftedDeck.CardIds.Select(LocalCardCatalog.CopyGroup).Distinct(StringComparer.Ordinal).ToArray();
        string craftedVirtual = deckGroups.First(virtualIds.Contains);
        var crafted = deckGroups.Where(id => realPrints.ContainsKey(id) && id != craftedVirtual).Take(9)
            .Concat(new[] { craftedVirtual }).ToList();
        string craftPath = Path.Combine(root, "achievement-crafted-virtual.xml");
        using (var profile = new LocalProfile(craftPath))
        {
            profile.CommitChange(next => {
                next.Achievements.Career.CraftedBaseCardIds = crafted.OrderBy(id => id, StringComparer.Ordinal).ToList();
                foreach (string id in crafted.Where(id => id != craftedVirtual))
                    if (!next.Collection.Any(row => LocalCardCatalog.CopyGroup(row.Id) == id))
                        next.Collection.Add(new LocalCardCount { Id = realPrints[id].Id, Count = 1, Acquired = 1 });
                return true;
            });
            var context = profile.AchievementContextFor(craftedDeck, true, "npc-crafted", 2500, "Diamond");
            Check(context.CraftedAndOwnedBaseCardIds.Count == 9 && !context.CraftedAndOwnedBaseCardIds.Contains(craftedVirtual),
                "disenchanted virtual identity could qualify #57");
        }
    }

    private static bool KnownCraftIdentity(string id)
    {
        try { return SabberStoneCore.Model.Cards.FromId(id)?.Collectible == true; }
        catch (KeyNotFoundException) { return false; }
    }
#endif
}
