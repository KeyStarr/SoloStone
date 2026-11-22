using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// Owner rulings of 2026-11-18:
//  - Twig of the World Tree (LOOT_392) follows current retail: "Refresh your Mana Crystals." It refills spent crystals and gains none.
//    The Refresh semantics are pinned (wiki Mana: a refresh refills the controller's non-Overloaded crystals).
//  - #179 Tricksy Improviser never picks a Secret already active for its player, nor one it already picked. Unpinned: a sensible default.
//  - #181 a Zombeast built with a Dormant part (Cyborg Patriarch) is Dormant, on the Build-A-Beast and the Storm the Gates paths. Unpinned.
internal static class Unpinned_TwigSecretsDormantProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, message);
    [GroupProof("twig-secrets-dormant")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { TwigRefreshesAndGainsNone, TwigKeepsOverload, TwigText, TricksySkipsActiveAndPicked, TricksyDoesNotDisturbRealPool, TricksySecondSeat, TricksyFullSecretZone, ZombeastThroughStormTheGates,
            ZombeastDormantBothPaths, ZombeastWithoutDormantPartIsAwake })
            try { test(); } catch (Exception e) { failures.Add(test.Method.Name + ": " + e.Message); }
        Verify(failures.Count == 0, string.Join("; ", failures));
        Console.WriteLine("PASS Twig refresh; Tricksy Improviser skips active and picked Secrets; Zombeast with a Dormant part is Dormant; Unpinned_TricksyNoDuplicates Unpinned_ZombeastDormant");
    }

    private static void TwigText()
    {
        Verify(Cards.FromId("LOOT_392").Text == "<b>Deathrattle:</b> Refresh your Mana Crystals.", "Twig text is the current retail text, got " + Cards.FromId("LOOT_392").Text);
    }

    private static (Game, Controller, Weapon) EquipTwig(int baseMana, int used)
    {
        var g = DeathKnightC1Proof.NewGame(5); var p = g.Player1;
        var twig = (Weapon)Add(p, "LOOT_392");
        PlayEntity(g, p, twig);
        Verify(p.Hero.Weapon == twig, "Twig equipped");
        p.BaseMana = baseMana; p.UsedMana = used; p.TemporaryMana = 0;
        return (g, p, twig);
    }

    private static void TwigRefreshesAndGainsNone()
    {
        foreach (int max in new[] { 6, 10 })
        {
            var (g, p, twig) = EquipTwig(max, max - 1);
            twig.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Verify(p.BaseMana == max && p.UsedMana == 0 && p.RemainingMana == max, $"Twig at {max} crystals refreshes and gains none, got {p.RemainingMana}/{p.BaseMana}");
        }
    }

    private static void TwigKeepsOverload()
    {
        var (g, p, twig) = EquipTwig(6, 6);
        p.OverloadLocked = 2;
        twig.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Verify(p.BaseMana == 6 && p.OverloadLocked == 2 && p.RemainingMana == 4, $"Twig refresh leaves Overloaded crystals locked, got {p.RemainingMana}/{p.BaseMana} locked {p.OverloadLocked}");
    }

    // Three real Mage Secrets from the catalog's Tricksy pool.
    internal static string[] PoolOf(Game g, string id, int count = 3) => OfflineCardPools.Candidates(g, g.Player1.HeroClass, LostCityPoolProof.Filter(id), Cards.FromId(id).AssetId)
        .Select(c => c.Id).Take(count).ToArray();

    // The same Standard fixture StandardSetsProof.Case builds (that helper allows one proof per card; Tricksy already has one).
    private static Game StandardGame(int seed)
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
        return game;
    }

    private static void TricksySkipsActiveAndPicked() { var g = StandardGame(1); const string id = "JAIL_321";
        var Pool = PoolOf(g, id); Verify(Pool.Length == 3, "three catalog Secrets");
        foreach (int seed in Enumerable.Range(1, 12))
        {
            var game = g.CloneExact(history: true); game.SetRandomSeed(seed); var player = game.Player1;
            AutomaticCastProof.Cast(game, (Spell)Entity.FromCard(player, Cards.FromId("GAME_005")));
            LostCityPoolProof.WithPool(game, id, Pool, () => {
                var active = (Spell)Entity.FromCard(player, Cards.FromId(Pool[0])); AutomaticCastProof.Cast(game, active);
                Verify(player.SecretZone.Count == 1, "one active Secret to start with");
                PlayEntity(game, player, Add(player, id));
                var ids = player.SecretZone.Select(s => s.Card.Id).OrderBy(x => x).ToArray();
                Verify(ids.SequenceEqual(Pool.OrderBy(x => x)), $"seed {seed}: Tricksy adds the two other pool Secrets, none twice, got {string.Join(",", ids)}");
                Verify(game.Player1.GraveyardZone.All(c => c.Card.Id != Pool[0] && c.Card.Id != Pool[1] && c.Card.Id != Pool[2]), $"seed {seed}: no duplicate attempt fizzled");
            });
        }
    }

    private static void TricksyDoesNotDisturbRealPool() { var g = StandardGame(1); const string id = "JAIL_321";
        // Real Mage Secret pool, no stand-in: two casts always end as two different Secrets.
        foreach (int seed in Enumerable.Range(1, 30))
        {
            var game = g.CloneExact(history: true); game.SetRandomSeed(seed); var player = game.Player1;
            AutomaticCastProof.Cast(game, (Spell)Entity.FromCard(player, Cards.FromId("GAME_005")));
            PlayEntity(game, player, Add(player, id));
            var ids = player.SecretZone.Select(s => s.Card.Id).ToArray();
            Verify(ids.Length == 2 && ids.Distinct().Count() == 2, $"seed {seed}: two different Secrets from the real pool, got {string.Join(",", ids)}");
        }
    }

    private static void TricksySecondSeat()
    {
        const string id = "JAIL_321"; var pool = PoolOf(StandardGame(1), id);
        foreach (int seed in Enumerable.Range(1, 12))
        {
            var g = StandardGame(seed); End(g);
            var player = g.Player2; Verify(g.CurrentPlayer == player, "second seat's turn");
            AutomaticCastProof.Cast(g, (Spell)Entity.FromCard(player, Cards.FromId("GAME_005")));
            LostCityPoolProof.WithPool(g, id, pool, () => {
                var active = (Spell)Entity.FromCard(player, Cards.FromId(pool[1])); AutomaticCastProof.Cast(g, active);
                PlayEntity(g, player, Add(player, id));
                Verify(player.SecretZone.Select(x => x.Card.Id).OrderBy(x => x).SequenceEqual(pool.OrderBy(x => x)), $"seed {seed}: second seat: Tricksy adds the two Secrets not active, none twice");
                Verify(g.Player1.SecretZone.Count == 0, "the other seat is untouched");
            });
        }
    }

    private static void TricksyFullSecretZone()
    {
        var g = StandardGame(1); const string id = "JAIL_321"; var five = PoolOf(g, id, 5); var p = g.Player1;
        Verify(five.Length == 5, "five catalog Secrets");
        AutomaticCastProof.Cast(g, (Spell)Entity.FromCard(p, Cards.FromId("GAME_005")));
        foreach (string secret in five) AutomaticCastProof.Cast(g, (Spell)Entity.FromCard(p, Cards.FromId(secret)));
        Verify(p.SecretZone.Count == 5, "Secret zone full");
        LostCityPoolProof.WithPool(g, id, five.Take(3).ToArray(), () => {
            PlayEntity(g, p, Add(p, id));
            Verify(p.SecretZone.Count == 5 && p.BoardZone.Any(m => m.Card.Id == id), "full Secret zone: Tricksy plays and casts nothing, no error");
            Verify(p.GraveyardZone.All(c => !five.Contains(c.Card.Id)), "full zone: no Secret cast attempt fizzled");
        });
    }

    // The real Storm the Gates chain: Sidequest completes, two crafting offers, the second one answered with Cyborg Patriarch.
    private static void ZombeastThroughStormTheGates()
    {
        bool found = false;
        for (int seed = 1; seed <= 150 && !found; seed++)
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
            var quest = (Spell)Play(g, p, "TLC_EVENT_400");
            QuestRules.Advance(quest, 3);
            if (p.Choice == null) continue;
            EvHooksCardsProof.Pick(g);
            var patriarch = p.Choice?.Choices.FirstOrDefault(i => g.IdEntityDic[i].Card.Id == "TIME_046") ?? 0;
            if (patriarch == 0) continue;
            found = true;
            EvHooksCardsProof.Pick(g, patriarch);
            var made = p.HandZone.OfType<Minion>().Single(m => m.Card.Id == "ICC_828t");
            Verify(made.Card.Dormant != null && made.Card.Dormant.Turns == 3, "Storm Zombeast built with Cyborg Patriarch carries the Dormant definition");
            PlayEntity(g, p, made);
            var onBoard = p.BoardZone.OfType<Minion>().Single(m => m.Card.Id == "ICC_828t");
            Verify(onBoard.IsDormant && DormantRules.TurnsRemaining(onBoard) == 3, "Storm Zombeast with Cyborg Patriarch sleeps for 3 turns");
        }
        Verify(found, "some seed offers Cyborg Patriarch in the second Storm choice");
    }

    private static void ZombeastDormantBothPaths()
    {
        var patriarch = Cards.FromId("TIME_046"); var raptor = Cards.FromId("CS2_172");
        foreach (bool storm in new[] { false, true })
        foreach (bool patriarchFirst in new[] { false, true })
        {
            var made = Card.CreateZombeastCard(patriarchFirst ? patriarch : raptor, patriarchFirst ? raptor : patriarch, true, storm);
            Verify(made.Dormant != null && made.Dormant.Turns == 3, $"storm={storm} patriarchFirst={patriarchFirst}: Zombeast carries the Dormant definition");
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var minion = (Minion)Entity.FromCard(p, made, zone: p.HandZone);
            PlayEntity(g, p, minion);
            var onBoard = p.BoardZone.OfType<Minion>().Single();
            Verify(onBoard.IsDormant && DormantRules.TurnsRemaining(onBoard) == 3, $"storm={storm} patriarchFirst={patriarchFirst}: played Zombeast is Dormant for 3 turns");
        }
    }

    private static void ZombeastWithoutDormantPartIsAwake()
    {
        foreach (bool storm in new[] { false, true })
        {
            var made = Card.CreateZombeastCard(Cards.FromId("CS2_172"), Cards.FromId("CS2_120"), true, storm);
            Verify(made.Dormant == null, $"storm={storm}: two plain Beasts make an awake Zombeast");
        }
    }
}
