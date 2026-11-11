using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class StandardSetsProof
{
    private static readonly List<string> Failures = new();
    private static readonly HashSet<string> ProvenIds = new(StringComparer.Ordinal);
    private static Dictionary<string, JsonElement> Definitions;
    internal static void Case(string id, Action<Game, Controller, string> scenario, [System.Runtime.CompilerServices.CallerFilePath] string proofFile = "", bool legacyPoolMember = false)
    {
        try
        {
            var card = Cards.FromId(id); var data = Definitions[id];
            // Existing CORE members opt in while retaining their legacy pool-exclusion flag.
            Core2026Proof.Check(card != null && card.Implemented && card.ExcludeFromLegacyRandomPools != legacyPoolMember && EffectModules.ByDbfId.GetValueOrDefault(card.AssetId) == id, "registered Standard module " + id);
            Core2026Proof.Check(data.GetProperty("isStandardLegal").GetBoolean() && Cards.IsFormatLegal(card, FormatType.FT_WILD), "client Standard identity and implemented deck legality");
            Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Cost == data.GetProperty("cost").GetInt32()
                && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32()
                && card.Text == data.GetProperty("text").GetString(), "exact pinned identity/stats/text");
            foreach (var tag in data.GetProperty("tags").EnumerateObject())
                Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "pinned tag " + tag.Name);
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_STANDARD });
                game.StartGame();
                foreach (var owner in new[] { game.Player1, game.Player2 })
                {
                    foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
                    owner.BaseMana = 10;
                }
                scenario(game, game.Player1, id);
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after scenario");
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after scenario");
            }
            Core2026Proof.Check(ProvenIds.Add(id), "one proof per registered card");
            PoolProofEvidence.Passed(id, Path.GetFileNameWithoutExtension(proofFile) + ".Run");
            Console.WriteLine("PASS Standard sets " + id);
        }
        catch (Exception e) { Failures.Add(id + ": " + e); }
    }

    internal static void ClearBoard(Game game, Controller player)
    {
        foreach (var minion in player.BoardZone.ToArray()) Kill(game, minion);
    }

    /// <summary>Moves every board entity, locations included, to SETASIDE without deaths.</summary>
    internal static void ParkBoard(Controller player)
    {
        foreach (var minion in player.BoardZone.ToArray()) player.SetasideZone.Add(player.BoardZone.Remove(minion));
        foreach (var location in player.BoardZone.GetLocations().ToArray()) player.SetasideZone.Add(player.BoardZone.Remove(location));
        player.Game.DeathProcessingAndAuraUpdate();
    }

    /// <summary>Deathrattle position fixture: [location, left, source, right] on an otherwise empty board, so the dying
    /// source is neither rightmost nor at its minion-only index. Retail puts its summons in the source's last slot.</summary>
    internal static (BoardObject Location, Minion Left, Minion Right) DeathrattleSlotBoard(Controller player, Func<Minion> placeSource)
    {
        ParkBoard(player);
        var location = LocationProofs.Location(player); var left = Summon(player, "CS2_182");
        var source = placeSource();
        var right = Summon(player, "CS2_182");
        Core2026Proof.Check(player.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, right.Id }),
            "Deathrattle slot fixture is [location, left, source, right]");
        return (location, left, right);
    }

    /// <summary>Plays a minion from hand into an explicit physical board slot (Core2026Proof.Play takes slot 0).</summary>
    internal static Minion PlayAt(Game game, Controller player, string id, int position)
    {
        var source = (Minion)Add(player, id);
        player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(player, source, zonePosition: position)) && source.ZonePosition == position,
            "positioned play accepted: " + id);
        return source;
    }

    internal static void CheckDeathrattleSlot(Controller player, (BoardObject Location, Minion Left, Minion Right) board,
        IEnumerable<IPlayable> outputs, string message)
    {
        var expected = new[] { board.Location.Id, board.Left.Id }.Concat(outputs.Select(o => o.Id)).Append(board.Right.Id);
        Core2026Proof.Check(player.BoardZone.Slots.Select(e => e.Id).SequenceEqual(expected),
            message + ": " + string.Join(",", player.BoardZone.Slots.Select(e => e.Card.Id)));
    }

    internal static void TargetRules(Game game, Controller player, string id, Minion enemy, Minion friendly, bool enemyOnly)
    {
        var source = Add(player, id); player.BaseMana = 10; player.UsedMana = 0;
        var targets = player.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(targets.Length > 0 && targets.All(t => t.Target is Minion), "available minion target required; heroes excluded");
        Core2026Proof.Check(targets.Any(t => t.Target?.Id == enemy.Id), "enemy target allowed");
        Core2026Proof.Check(targets.Any(t => t.Target?.Id == friendly.Id) == !enemyOnly, "friendly target restriction");
    }

    internal static void Elusive(Game game, Controller owner, Minion minion)
    {
        Core2026Proof.Check(minion.CantBeTargetedBySpells && minion.CantBeTargetedByHeroPowers, "Elusive maps to both protections");
        End(game); var enemy = owner.Opponent;
        enemy.BaseMana = 10; enemy.UsedMana = 0;
        var spell = Add(enemy, "CS2_029");
        var ordinary = Summon(owner, "CS2_182");
        Core2026Proof.Check(enemy.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == ordinary.Id), "spell has ordinary control target");
        Core2026Proof.Check(enemy.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == ordinary.Id), "Mage power has ordinary control target");
        Core2026Proof.Check(!enemy.Options().Any(t => (t.Source?.Id == spell.Id || t.PlayerTaskType == PlayerTaskType.HERO_POWER) && t.Target?.Id == minion.Id), "enemy spell and hero power cannot target Elusive");
        var attacker = Summon(enemy, "CS2_182"); Attack(game, attacker, minion);
        Core2026Proof.Check(minion.Damage > 0 || minion.Zone.Type == Zone.GRAVEYARD, "minion attack can hit Elusive");
        var battlecryTarget = minion.Zone.Type == Zone.PLAY ? minion : Summon(owner, minion.Card.Id);
        Play(game, enemy, "BE_228", battlecryTarget);
        Core2026Proof.Check(battlecryTarget.IsSilenced && !battlecryTarget.CantBeTargetedBySpells, "minion battlecry targets Elusive and silence removes it");
        End(game);
    }

    internal static void Run(bool poolPilotsOnly = false, bool outcastOnly = false, bool selectorsOnly = false, bool rewindOnly = false, bool timeTravelS2Only = false, bool rewindSliceOnly = false, bool rewindSmokeOnly = false, bool lifestealUnblockedOnly = false, Action[] selectedProofs = null)
    {
        Failures.Clear();
        ProvenIds.Clear();
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        Definitions = doc.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        if (lifestealUnblockedOnly)
        {
            StormfuryProof.Run(); WyvernsSlumberProof.Run(); GrimHarvestProof.Run(); DreadsoulCorrupterProof.Run();
            HallsofOriginationProof.Run(); GraftedKnightProof.Run(); AldrachiWarbladesProof.Run(); VoidShardProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("lifesteal-unblocked: cards=8/8; seeds=3; PASS");
            return;
        }
        if (selectedProofs != null)
        {
            foreach (var proof in selectedProofs) proof();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            return;
        }
        if (rewindSmokeOnly) { RewindCardsProof.Run(); return; }
        if (rewindOnly)
        {
            ChronoDaggersProof.Run(); MisterClocksworthProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("rewind-cards: cards=2/2; seeds=3; PASS");
            return;
        }
        if (timeTravelS2Only)
        {
            TimeTravelS2Proof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("time-travel-s2: cards=9/9; seeds=3; PASS");
            return;
        }
        if (rewindSliceOnly)
        {
            AeonWizardProof.Run(); PortalVanguardProof.Run(); ConfluxCrasherProof.Run();
            TimeMachineProof.Run(); MorchieProof.Run(); RaptorHeraldProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("rewind-s1: cards=7/7; seeds=3; PASS");
            return;
        }
        if (outcastOnly)
        {
            CrimsonSigilRunnerProof.Run(); SpectralSightProof.Run(); CosmicManifestationsProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("outcast-cards: cards=3/3; seeds=3; PASS");
            return;
        }
        if (selectorsOnly)
        {
            BarbedThornProof.Run(); OminousNightmaresProof.Run(); LightmenderProof.Run();
            SleepParalysisProof.Run(); TwilightTimereaverProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("selectors: cards=5/5; seeds=3; PASS");
            return;
        }
        if (poolPilotsOnly)
        {
            WinterspringWhelpProof.Run(); DrakeadonMongrelProof.Run(); AetherialKindlingProof.Run();
            ColdSnapProof.Run(); EntropicRiftProof.Run(); DaydreamingPixieProof.Run();
            FadingMemoryProof.Run(); ScarletBruiserProof.Run(); PhotosynthesisProof.Run();
            Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
            Console.WriteLine("pool-pilots: cards=9/9; seeds=3; PASS");
            return;
        }
        GroupProofs.Run(ProofKind.Facility);
        GroupProofs.Run(ProofKind.Cards);
        BashanaRunetotemProof.Run();
        ClutchOfCorruptionProof.Run();
        TimewayWardenProof.Run(); IsorathProof.Run(); TrickySatyrProof.Run(); KeeperOfFlameProof.Run();
        TolvirCarverProof.Run(); IridaSinseekerProof.Run(); EntomologistToruProof.Run();
        DefileProof.Run();
        BEProofs.Run();
        CataclysmProofs.Run();
        EmeraldDreamProofs.Run();
        EscapeFromVioletHoldProofs.Run();
        TheLostCityProofs.Run();
        TimeTravelProofs.Run();
        CoreNeutralAProofs.Run();
        CoreNeutralBProofs.Run();
        CorePriestPaladinProofs.Run();
        CoreMageWarriorShamanProofs.Run();
        CoreDemonHunterProofs.Run();
        CoreHunterRogueProofs.Run();
        CoreDruidWarlockProofs.Run();
        CoreDeathKnightProofs.Run();
        LocationsProofs.Run();

        Core2026Proof.Check(Failures.Count == 0, string.Join("\n", Failures));
        Core2026Proof.Check(ProvenIds.SetEquals(StandardSets.CollectibleIds), "every registered collectible has a passing proof");
        var decks = Simulation.DeckIds("standard-batch-1").Select(Simulation.Deck).ToArray();
        Core2026Proof.Check(decks.All(deck => deck.Cards.Length == 30 && deck.Cards.SequenceEqual(Simulation.Deck(deck.Id).Cards))
            && decks.Select(deck => deck.Hero).Distinct().Count() >= 2, "deterministic 30-card stress decks with opposing classes");
        var deckCards = Simulation.DeckIds("standard-batch-1").SelectMany(id => Simulation.Deck(id).Cards).ToHashSet();
        Core2026Proof.Check(deckCards.SetEquals(StandardSets.CollectibleIds), "every registered collectible in stress decks");
        Console.WriteLine($"standard-sets: cards={StandardSets.CollectibleIds.Count}/{StandardSets.CollectibleIds.Count}; seeds=3; PASS");
    }
}
