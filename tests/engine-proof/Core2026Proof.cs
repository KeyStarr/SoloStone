using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.CardSets.Client;

internal static class Core2026Proof
{
    private static readonly List<string> Failures = new();
    // The client export's rows, or null without it (ClientExport). Read at each use, never kept from an earlier proof.
    private static IReadOnlyDictionary<string, JsonElement> Definitions => ClientExport.TryCards();
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    internal static IPlayable Add(Controller player, string id) => Generic.DrawCard(player, Cards.FromId(id));
    internal static Minion Summon(Controller player, string id)
    {
        var minion = (Minion)Entity.FromCard(player, Cards.FromId(id));
        Generic.SummonBlock(player.Game, minion, -1, null);
        player.Game.DeathProcessingAndAuraUpdate();
        return minion;
    }
    internal static void Kill(Game game, Minion minion)
    {
        minion.ToBeDestroyed = true;
        game.DeathProcessingAndAuraUpdate();
    }
    internal static void End(Game game) => Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "end turn accepted");
    internal static void Attack(Game game, Minion minion, ICharacter target)
    {
        minion.IsExhausted = false;
        var task = minion.Controller.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id && t.Target?.Id == target.Id);
        Check(task != null && game.Process(task), "attack accepted");
    }
    internal static IPlayable Play(Game game, Controller player, string id, ICharacter target = null)
    {
        var source = Add(player, id);
        PlayEntity(game, player, source, target);
        return source;
    }
    internal static void PlayEntity(Game game, Controller player, IPlayable source, ICharacter target = null)
    {
        player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
        int cost = source.Cost;
        var task = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && t.Target?.Id == target?.Id);
        Check(task != null, "legal play missing: " + source.Card.Id);
        Check(game.Process(task), "legal play rejected: " + source.Card.Id);
        Check(player.UsedMana == cost, source.Card.Id + " current cost paid");
    }
    internal static void Case(string id, Action<Game, Controller, string> scenario, [System.Runtime.CompilerServices.CallerFilePath] string proofFile = "")
    {
        try
        {
            var card = Cards.FromId(id);
            Check(card != null && card.Implemented && RebalancedCore2026.ByDbfId.ContainsKey(card.AssetId), "registered legal module");
            Check(Cards.IsFormatLegal(card, FormatType.FT_STANDARD) && Cards.IsFormatLegal(card, FormatType.FT_WILD),
                "deck legality includes current printing");
            if (Definitions != null)
            {
                var data = Definitions[id];
                Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Cost == data.GetProperty("cost").GetInt32()
                    && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32()
                    && card.Text == data.GetProperty("text").GetString(), "exact pinned client identity/stats/text");
            }
            else ClientExport.Skip("Core 2026 card identity against the client export");
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.ROGUE, History = true, Logging = false,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
                game.StartGame();
                foreach (var owner in new[] { game.Player1, game.Player2 })
                {
                    foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
                    owner.BaseMana = 10;
                }
                scenario(game, game.Player1, id);
            }
            PoolProofEvidence.Passed(id, Path.GetFileNameWithoutExtension(proofFile) + ".Run");
            Console.WriteLine("PASS current Core2026 " + id);
        }
        catch (Exception e) { Failures.Add(id + ": " + e.ToString()); }
    }
    private static void FormatLegalityBoundaries()
    {
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            foreach (string id in new[] { "CORE_EX1_002", "CORE_EX1_005" })
            {
                var card = Cards.FromId(id);
                Check(Cards.IsFormatLegal(card, format), "new Tradeable printing is deck legal");
                Check(!Cards.FormatTypeCards(format).Contains(card)
                    && Cards.FormatTypeClassCards(format).Values.All(pool => !pool.Contains(card)),
                    "new printing preserves historical global and class generation pools");
            }
            Check(Cards.IsFormatLegal(Cards.FromId("CORE_EX1_323"), format), "Jaraxxus is a legal deck Hero");
            Check(!Cards.IsFormatLegal(null, format) && !Cards.IsFormatLegal(Cards.FromId("EX1_tk33"), format)
                && !Cards.IsFormatLegal(Cards.FromId("HERO_01"), format), "null, Hero Powers and cosmetic Heroes are not deck cards");
            Check(Cards.FormatTypeCards(format).All(card => card.Type != CardType.HERO)
                && Cards.FormatTypeClassCards(format).Values.All(pool => pool.All(card => card.Type != CardType.HERO)),
                "global and per-class random-generation pools exclude all Heroes");
        }
        Check(!Cards.IsFormatLegal(Cards.FromId("CORE_EX1_323"), FormatType.FT_UNKNOWN), "unknown format is not deck legal");
        var wildOnly = Cards.FromId("ICC_831");
        Check(Cards.IsFormatLegal(wildOnly, FormatType.FT_WILD) && !Cards.IsFormatLegal(wildOnly, FormatType.FT_STANDARD),
            "implemented Wild Hero respects configured Standard sets");
        var unimplemented = Cards.All.First(card => card.Collectible && !card.Implemented && Cards.StandardSets.Contains(card.Set));
        Check(!Cards.IsFormatLegal(unimplemented, FormatType.FT_STANDARD) && !Cards.IsFormatLegal(unimplemented, FormatType.FT_WILD),
            "unimplemented collectible card is not deck legal");
        Console.WriteLine("PASS deck legality independent of random-generation pools");
    }

    private static void GeneratedSecretRegression()
    {
        // Another-class generation can reach Bear Trap; global AFTER_ATTACK must survive clone and snapshot.
        var game = new Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, SkipMulligan = true, Logging = false, History = true });
        game.StartGame();
        var owner = game.Player1;
        Play(game, owner, "AT_060");
        End(game);
        var attacker = Summon(game.Player2, "CS2_171");
        var clone = game.CloneExact(history: true);
        var loaded = Game.LoadSnapshot(game.SaveSnapshot());
        EventOrdinalProof.Same(game.TriggerManager.Events, clone.TriggerManager.Events, new[] { "AfterAttackTrigger" });
        EventOrdinalProof.Same(game.TriggerManager.Events, loaded.TriggerManager.Events, new[] { "AfterAttackTrigger" });
        Attack(game, attacker, owner.Hero);
        Attack(clone, (Minion)clone.IdEntityDic[attacker.Id], clone.Player1.Hero);
        Attack(loaded, (Minion)loaded.IdEntityDic[attacker.Id], loaded.Player1.Hero);
        Check(owner.SecretZone.Count == 0 && owner.BoardZone.Single().Card.Id == "CS2_125", "generated Bear Trap summons after attack");
        Check(Canonical.State(game) == Canonical.State(clone), "global AFTER_ATTACK subscriptions survive exact clone");
        Check(Canonical.State(game) == Canonical.State(loaded), "global AFTER_ATTACK subscriptions survive snapshot");
    }

    private static void Goldens()
    {
        using var document = JsonDocument.Parse(File.ReadAllText("tests/goldens/core-2026.json"));
        Check(document.RootElement.GetProperty("schema").GetInt32() == 3 && document.RootElement.GetProperty("suite").GetString() == "core-2026", "current Core golden schema/suite");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        int count = 0;
        foreach (var row in document.RootElement.GetProperty("games").EnumerateArray())
        {
            var expected = row.Deserialize<GameRecord>(options);
            Check(expected.ReplaySchema == 3, "current Core golden replay schema");
            var actual = new GameRecord();
            Simulation.Finish(actual, Simulation.Run(expected, replay: true));
            Check(expected.DecisionsSha != null && expected.DecisionsSha == actual.DecisionsSha && expected.StateSha == actual.StateSha
                && expected.Turns == actual.Turns && expected.Winner == actual.Winner, "current Core golden " + expected.Id);
            count++;
        }
        Check(count == 18, "18 current Core goldens (three decks, two seeds)");
        Console.WriteLine("PASS core-2026 goldens 18/18");
    }

    private static void GeneratedDarknessRegression()
    {
        var game = new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, SkipMulligan = true, Logging = false, History = true });
        game.StartGame();
        Play(game, game.Player1, "LOOT_526");
        var darkness = game.Player1.BoardZone.Single();
        Check(darkness.Card.Id == "LOOT_526d", "generated Darkness starts dormant");
        End(game);
        // The otherwise empty opponent deck draws all three shuffled Candles at turn start.
        Check(darkness.Card.Id == "LOOT_526", "third Candle awakens Darkness");
        Check(game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryChangeEntity>()
            .Any(change => change.Entity.Id == darkness.Id && change.CardId == "LOOT_526"),
            "Darkness awakening publishes changed card identity");
    }

    internal static void Run()
    {
        CardProofs();
        Goldens();
    }

    internal static void CardProofs()
    {
        Check(RebalancedCore2026.ByDbfId.Count == 68, "68 current Core modules");
        BallofSpidersProof.Run();
        AstromancerProof.Run();
        FinjatheFlyingStarProof.Run();
        GreaterHealingPotionProof.Run();
        GrimestreetOutfitterProof.Run();
        ShakutheCollectorProof.Run();
        DirtyRatProof.Run();
        HolyNovaProof.Run();
        HolySmiteProof.Run();
        MarkoftheWildProof.Run();
        FireElementalProof.Run();
        HellfireProof.Run();
        AssassinateProof.Run();
        ConsecrationProof.Run();
        HammerofWrathProof.Run();
        RaidLeaderProof.Run();
        WagglePickProof.Run();
        TrackingProof.Run();
        KingMuklaProof.Run();
        CairneBloodhoofProof.Run();
        FanofKnivesProof.Run();
        DefiasRingleaderProof.Run();
        SI7AgentProof.Run();
        BrightwingProof.Run();
        HexProof.Run();
        LightningStormProof.Run();
        MortalCoilProof.Run();
        SiphonSoulProof.Run();
        DoomguardProof.Run();
        FlameImpProof.Run();
        LordJaraxxusProof.Run();
        ArgentProtectorProof.Run();
        TirionFordringProof.Run();
        ArchmageAntonidasProof.Run();
        EqualityProof.Run();
        WitchsApprenticeProof.Run();
        BlazingInvocationProof.Run();
        CoghammerProof.Run();
        MusterforBattleProof.Run();
        SneedsOldShredderProof.Run();
        DrainSoulProof.Run();
        ShadowAscendantProof.Run();
        ObsidianStatueProof.Run();
        GnomeferatuProof.Run();
        IvoryKnightProof.Run();
        TheCuratorProof.Run();
        NetherspiteHistorianProof.Run();
        SwashburglarProof.Run();
        SilvermoonPortalProof.Run();
        GorillabotA3Proof.Run();
        VulgarHomunculusProof.Run();
        BladedGauntletProof.Run();
        SleepyDragonProof.Run();
        OakenSummonsProof.Run();
        VoidlordProof.Run();
        WildPyromancerProof.Run();
        DoomsayerProof.Run();
        HammerofTwilightProof.Run();
        FandralStaghelmProof.Run();
        CalloftheWildProof.Run();
        HeadhuntersHatchetProof.Run();
        HalazzitheLynxProof.Run();
        FirePlumePhoenixProof.Run();
        PrimordialDrakeProof.Run();
        JeweledMacawProof.Run();
        SpikeridgedSteedProof.Run();
        TheBlackKnightProof.Run();
        BigGameHunterProof.Run();
        Check(Failures.Count == 0, string.Join("\n", Failures));
        var deckCards = Simulation.DeckIds("core-2026").SelectMany(deck => Simulation.Deck(deck).Cards)
            .Where(card => card.StartsWith("CORE_", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        Check(deckCards.SetEquals(RebalancedCore2026.ByDbfId.Keys.Select(key => Cards.FromAssetId(key).Id)), "all 68 current cards in sim/fuzz/strict transport decks");
        // CORE_EX1_312 was blocked until locations existed; the location pilot now scripts it.
        Check(Cards.All.Any(card => card.Id == "CORE_EX1_312" && card.Implemented), "CORE_EX1_312 unblocked by the location pilot");
        FormatLegalityBoundaries();
        GeneratedSecretRegression();
        GeneratedDarknessRegression();
        Console.WriteLine("core-2026: current-text=68/68; seeds=3; blocked=0");
    }
}
