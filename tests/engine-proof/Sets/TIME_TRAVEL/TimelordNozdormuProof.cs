using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using System.IO.Compression;
using System.Text.Json;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TimelordNozdormuProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_063", (g, p, id) => {
        PinnedMetas();
        KnownMetaProofFixture.UseBlackEmpire(g);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Card.AssetId == 120981 && source.AttackDamage == 8 && source.BaseHealth == 8 && source.IsRush &&
            source.Card.IsRace(Race.DRAGON) && source.IsDormant && DormantRules.TurnsRemaining(source) == 5,
            "3-Mana 8/8 Dragon Rush starts asleep five turns; own play excluded");
        Core2026Proof.Check(!source.AttackableByRush && !g.RushMinions.Contains(source.Id),
            "Unpinned_DormantExclusions natural sleeper has no Rush readiness or Rush registration");
        var condition = source.AppliedEnchantments.Single(e => e.Card.Id == "TIME_063e1");
        var listener = source.AppliedEnchantments.Single(e => e.Card.Id == "TIME_063e2");
        Core2026Proof.Check(condition.Card.AssetId == 122531 && condition.Card[(GameTag)451] == 5 && condition[(GameTag)451] == 5 &&
            condition.Card.Text == "<b>Dormant</b>. Awaken in @ |4(turn, turns)." &&
            listener.Card.AssetId == 122532 && listener.Card[(GameTag)4390] == 1 && listener.Card[(GameTag)976] == 1 &&
            listener.Card.Text == "After you play a card from the newest expansion, awaken 1 turn sooner." &&
            source.AppliedEnchantments.All(e => e.Card.Type == CardType.ENCHANTMENT && e.Card.Cost == 0 &&
                e.Card.ATK == 0 && e.Card.Health == 0 && e[GameTag.ATTACHED] == source.Id),
            "Unpinned_DormantCounterProjection exact countdown and while-asleep helpers");
        source.Silence(); source.TakeDamage(p.Opponent.Hero, 10);
        Core2026Proof.Check(!source.IsSilenced && source.Damage == 0 && !p.BoardZone.GetAll().Contains(source), "Unpinned_DormantExclusions");
        Play(g, p, "GAME_005"); Play(g, p, "CS2_182"); Summon(p, "BE_861");
        Core2026Proof.Check(DormantRules.TurnsRemaining(source) == 5, "Unpinned_NozdormuNewest Coin/old set/summon do not accelerate");
        Play(g, p, "BE_861");
        Core2026Proof.Check(DormantRules.TurnsRemaining(source) == 4 && condition[(GameTag)451] == 4,
            "Unpinned_DormantWhile Unpinned_NozdormuNewest owner BE completed play accelerates once");
        End(g); Play(g, p.Opponent, "BE_861");
        Core2026Proof.Check(DormantRules.TurnsRemaining(source) == 4, "opponent BE play excluded");
        End(g);
        Core2026Proof.Check(DormantRules.TurnsRemaining(source) == 3, "Unpinned_DormantClock next owner start decrements once");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools) })
        {
            var sleeper = (Minion)next.IdEntityDic[source.Id];
            for (int i = 0; i < 3; i++) Play(next, next.Player1, "BE_861");
            Core2026Proof.Check(!sleeper.IsDormant && DormantRules.TurnsRemaining(sleeper) == 0 && sleeper.AppliedEnchantments.Count == 0 &&
                sleeper.IsRush && !sleeper.IsExhausted && sleeper.AttackDamage == 8 && sleeper.BaseHealth == 8,
                "Unpinned_DormantLifecycle cloned/restored listener wakes at zero with Rush; helpers retired");
            Core2026Proof.Check(!DormantRules.Accelerate(sleeper, 1) && DormantRules.TurnsRemaining(sleeper) == 0,
                "Unpinned_NozdormuNewest zero floor; no awake decrement");
        }
        Core2026Proof.Check(source.IsDormant && DormantRules.TurnsRemaining(source) == 3, "clone isolation");
        ParkBoard(p);
        var armor = p.Hero.Armor;
        Play(g, p, "BE_861");
        Core2026Proof.Check(p.Hero.Armor == armor && listener[GameTag.ATTACHED] == 0 && condition[GameTag.ATTACHED] == 0,
            "source exit retires helper listeners");
        source = Summon(p, id); DormantRules.Accelerate(source, 5); source.Silence();
        Core2026Proof.Check(!source.IsRush, "awake Silence removes Rush");
        DormantRules.Make(source, 2, "TIME_058e", p.Hero);
        Play(g, p, "BE_861");
        Core2026Proof.Check(source.IsDormant && DormantRules.TurnsRemaining(source) == 2 &&
            source.AppliedEnchantments.All(e => e.Card.Id != "TIME_063e2"), "Unpinned_DormantReplace Silence prevents while-asleep listener restoration");
        DormantRules.Awaken(source); ParkBoard(p);
        source = Summon(p, id);
        for (int i = 0; i < 5; i++) { End(g); End(g); Core2026Proof.Check(source.IsDormant == (i < 4), "five natural owner starts"); }
        Core2026Proof.Check(!source.IsDormant && source.IsRush, "natural clock awakens without BE plays");
        g.PowerHistory.Last.Clear();
        Generic.ReturnToHandBlock(p, source);
        Core2026Proof.Check(g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Any(e =>
            e.EntityId == source.Id && e.Tag == GameTag.DORMANT_AWAKENED_THIS_TURN && e.Value == 0),
            "Unpinned_DormantLifecycle bounce publishes cleared awaken cue before reset");
        PlayEntity(g, p, source);
        Core2026Proof.Check(source.IsDormant && DormantRules.TurnsRemaining(source) == 5 &&
            source[GameTag.DORMANT_AWAKENED_THIS_TURN] == 0 && !source.AttackableByRush,
            "replayed Nozdormu starts fresh sleep without stale awaken cue or Rush readiness");
        CounteredPlay(); NewestMetadata();
    });

    private static void PinnedMetas()
    {
        foreach (var meta in new[] { "violet-hold", "black-empire", "historical-v1" })
        {
            var game = HeraldProof.New(CardClass.MAGE);
            if (meta == "black-empire") KnownMetaProofFixture.UseBlackEmpire(game);
            if (meta == "historical-v1") PinnedPoolFixture.Use(game);
            string newest = meta == "violet-hold" ? "JAIL_007" : "BE_861";
            string older = meta == "violet-hold" ? "BE_861" : "JAIL_007";
            var source = (Minion)Play(game, game.Player1, "TIME_063");
            foreach (var next in new[] { game.CloneExact(history: true),
                Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools), game })
            {
                var player = next.Player1;
                var sleeper = (Minion)next.IdEntityDic[source.Id];
                Play(next, player, older);
                Play(next, player, "GAME_005");
                Core2026Proof.Check(DormantRules.TurnsRemaining(sleeper) == 5,
                    meta + " excludes other expansion and Coin after clone/restore");
                for (int i = 1; i <= 5; i++)
                {
                    Play(next, player, newest);
                    Core2026Proof.Check(DormantRules.TurnsRemaining(sleeper) == 5 - i && sleeper.IsDormant == (i < 5),
                        meta + " newest expansion accelerates and awakens after clone/restore");
                }
            }
        }
    }

    private static void CounteredPlay()
    {
        foreach (bool blackEmpire in new[] { false, true })
        {
            var g = HeraldProof.New(CardClass.MAGE);
            if (blackEmpire) KnownMetaProofFixture.UseBlackEmpire(g);
            var p = g.Player1;
            var source = Summon(p, "TIME_063");
            End(g); Play(g, p.Opponent, "EX1_287"); End(g);
            Core2026Proof.Check(DormantRules.TurnsRemaining(source) == 4, "one owner start before countered play");
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools), g })
            {
                var spell = (Spell)Play(next, next.Player1, blackEmpire ? "BE_201" : "JAIL_436",
                    blackEmpire ? next.Player2.Hero : null);
                var sleeper = (Minion)next.IdEntityDic[source.Id];
                Core2026Proof.Check(spell.IsCountered && DormantRules.TurnsRemaining(sleeper) == 3 && next.Player1.BoardZone.SlotCount == 1,
                    "Unpinned_NozdormuNewest countered newest spell accelerates after clone/restore: " + blackEmpire);
            }
        }
    }

    private static void NewestMetadata()
    {
        // Export-only definitions test admission; no Fabled payload or cosmetic Coin is registered.
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.MAGE);
        var condition = Cards.FromId("TIME_063e2").Power.Trigger.Condition;
        foreach (string id in new[] { "BE_COIN1", "BE_COIN2", "BE_COIN3", "MONK_COIN", "BE_013", "BE_017", "BE_784", "BE_009" })
        {
            var data = doc.RootElement.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("cardId").GetString() == id);
            var tags = data.GetProperty("tags").EnumerateObject().ToDictionary(t => (GameTag)int.Parse(t.Name), t => t.Value.GetInt32());
            tags[GameTag.CARD_SET] = data.GetProperty("cardSet").GetProperty("id").GetInt32();
            tags[GameTag.COLLECTIBLE] = data.GetProperty("collectible").GetBoolean() ? 1 : 0;
            var printed = new Card(id, data.GetProperty("dbfId").GetInt32(), tags.Select(t => new Tag(t.Key, t.Value))
                .Concat(new[] { new Tag(GameTag.CARDNAME, data.GetProperty("name").GetString()),
                    new Tag(GameTag.CARDTEXT, data.GetProperty("text").GetString()) }).ToArray(),
                new Dictionary<PlayReq, int>(), Array.Empty<string>(),
                data.GetProperty("referencedTags").EnumerateObject().Select(t => new Tag((GameTag)int.Parse(t.Name), t.Value.GetInt32())).ToArray(), preserveText: true);
            bool fabled = tags.GetValueOrDefault((GameTag)4754) == 1;
            Core2026Proof.Check(tags[GameTag.CARD_SET] == 1994 && (fabled || tags[(GameTag)2088] == 1) &&
                condition.Eval(Entity.FromCard(g.Player1, printed)) == fabled,
                "Unpinned_NozdormuNewest exact BE Fabled included, cosmetic Coin excluded: " + id);
        }
    }
}
