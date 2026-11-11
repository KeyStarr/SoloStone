using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class MultipleMinionTypesProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "multiple types: " + message);
    private static readonly Race[] Types = { Race.DRAENEI, Race.UNDEAD, Race.MURLOC, Race.DEMON,
        Race.MECHANICAL, Race.ELEMENTAL, Race.BEAST, Race.TOTEM, Race.PIRATE, Race.DRAGON, Race.QUILBOAR, Race.NAGA };

    private static Card Definition(JsonElement row)
    {
        var xml = new XDocument(new XElement("CardDefs", new XElement("Entity",
            new XAttribute("CardID", row.GetProperty("cardId").GetString()),
            new XAttribute("ID", row.GetProperty("dbfId").GetInt32()),
            row.GetProperty("tags").EnumerateObject().Select(tag => new XElement("Tag",
                new XAttribute("enumID", tag.Name), new XAttribute("value", tag.Value.GetInt32()))))));
        return CardLoader.LoadDefinitions(xml, overlay: true).Single();
    }

    private static Card Typed(Race primary, params int[] flags) => new Card("race-proof", -100001,
        new[] { new Tag(GameTag.CARDTYPE, (int)CardType.MINION), new Tag(GameTag.CARDRACE, (int)primary),
            new Tag(GameTag.ATK, 1), new Tag(GameTag.HEALTH, 1) }
            .Concat(flags.Select(flag => new Tag((GameTag)flag, 1))).ToArray(),
        new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>());

    private static Game NewGame(bool history)
    {
        var game = new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, Logging = false, History = history });
        game.StartGame();
        return game;
    }

    internal static void Run()
    {
        var rows = ClientExport.TryCards();
        // With the export the card is rebuilt from the client's own row; without it, the engine's card must already say the same.
        var boar = rows != null ? Definition(rows["BE_730t"]) : Cards.FromId("BE_730t");
        Check(boar.IsRace(Race.BEAST) && boar.IsRace(Race.MECHANICAL), "Clockwork Boar must be both Beast and Mech");
        Check(boar.GetRawRace() == Race.BEAST && !boar.IsRace(Race.DRAGON), "primary preserved, unrelated type rejected");
        int checkedCards = 0;
        var supported = Types.Select(t => t.ToString()).Append("ALL").ToHashSet();
        foreach (var row in (rows?.Values ?? Enumerable.Empty<JsonElement>()).Where(r => r.GetProperty("cardType").GetString() == "MINION"))
        {
            var expected = row.GetProperty("races").EnumerateArray().Select(r => r.GetString()).ToHashSet();
            if (!expected.IsSubsetOf(supported)) continue; // Other game modes have non-gameplay races.
            var card = Definition(row);
            Check(expected.SetEquals(card.Races.Select(r => r.ToString())), "pinned races[] decoding " + card.Id);
            Check(card.GetRawRace() == (Race)(row.GetProperty("tags").TryGetProperty("200", out var raw) ? raw.GetInt32() : 0),
                "pinned primary race " + card.Id);
            checkedCards++;
        }
        if (rows != null)
        {
            Check(checkedCards > 1000, "broad pinned export coverage");
            Check(Definition(rows["CATA_476t"]).IsRace(Race.ELEMENTAL)
                && Definition(rows["TLC_888"]).IsRace(Race.DRAGON), "both primary orientations");
        }
        else ClientExport.Skip("Minion types decoded from client export rows");
        foreach (var card in Cards.All)
            foreach (var race in Types)
                Check(card.IsRaceForLegacyRandomPool(race) == (card.GetRawRace() == Race.ALL || card.GetRawRace() == race),
                    "legacy generation pool unchanged " + card.Id);
        Check(Cards.FromId("CORE_LOE_039").IsRace(Race.BEAST)
            && !Cards.FromId("CORE_LOE_039").IsRaceForLegacyRandomPool(Race.BEAST), "modern membership independent of frozen pools");
        Boundaries();
        CommonIntersection();
        foreach (bool history in new[] { false, true })
        {
            Gameplay(boar, history);
            Kindred(history);
            Persistence(history);
        }
        Console.WriteLine($"PASS multiple minion types: pinned={checkedCards}; membership, targeting, buffs, draw, pools, Kindred, intersection, silence/copy/transform, clone/snapshot; history=on/off");
    }

    private static void Boundaries()
    {
        var all = Typed(Race.ALL);
        var none = Typed(Race.INVALID);
        Check(Types.All(all.IsRace) && all.IsRace(Race.ALL), "All covers each gameplay type");
        foreach (var invalid in new[] { Race.INVALID, Race.BLANK, Race.EGG, Race.HUMAN, (Race)999 })
            Check(!all.IsRace(invalid) && !Typed(invalid).IsRace(invalid) && Typed(invalid).Races.Count == 0,
                "non-gameplay races do not match " + invalid);
        foreach (Race race in Types)
        {
            var single = Typed(race);
            Check(single.IsRace(race) && !single.IsRace(Race.ALL), "concrete is not All");
            Check(single.SharesRaceWith(all) && all.SharesRaceWith(single), "All overlap symmetric");
            var game = NewGame(false);
            var target = (Minion)Entity.FromCard(game.Player1, single);
            Check(TargetingPredicates.ReqTargetWithRace((int)race)(target), "target predicate for " + race);
        }
        Check(!none.SharesRaceWith(none) && !none.SharesRaceWith(all) && !all.SharesRaceWith(none)
            && !all.SharesRaceWith(Cards.FromId("CS2_029")) && !all.SharesRaceWith(null), "typeless/non-minions share nothing");
        var duplicate = Typed(Race.BEAST, 2542, 2539);
        Check(duplicate.Races.Count == 2 && duplicate.Races.Distinct().Count() == 2, "types deduplicated");
        bool rejected = false;
        try { ((IList<Race>)duplicate.Races)[0] = Race.DEMON; }
        catch (NotSupportedException) { rejected = true; }
        Check(rejected && duplicate.IsRace(Race.BEAST), "printed type collection immutable");
        Check(duplicate.Clone().Races.SequenceEqual(duplicate.Races), "definition clone keeps types");
    }

    private static void CommonIntersection()
    {
        var beastDragon = Typed(Race.BEAST, 2523);
        var dragonMech = Typed(Race.DRAGON, 2539);
        var beastMech = Typed(Race.BEAST, 2539);
        var all = Typed(Race.ALL);
        Check(beastDragon.SharesRaceWith(dragonMech) && dragonMech.SharesRaceWith(beastMech)
            && beastMech.SharesRaceWith(beastDragon), "pairwise overlaps exist");
        Check(!Card.MinionsShareRace(new[] { beastDragon, dragonMech, beastMech }), "pairwise overlap is not common intersection");
        Check(Card.MinionsShareRace(new[] { beastDragon, dragonMech, all, Cards.FromId("CS2_029") }), "Dragon intersection; All/spells unrestricted");
        Check(!Card.MinionsShareRace(new[] { all, Typed(Race.INVALID) }), "typeless breaks intersection");
        Check(Card.MinionsShareRace(Array.Empty<Card>()) && Card.MinionsShareRace(new[] { Cards.FromId("CS2_029") })
            && Card.MinionsShareRace(new[] { all, all }), "empty, spells-only and All-only satisfy intersection");
        var pool = new[] { beastDragon, dragonMech, beastMech, Typed(Race.ELEMENTAL, 2523), Typed(Race.DEMON) };
        var selected = pool.Where(c => c.IsRace(Race.ELEMENTAL) || c.IsRace(Race.DRAGON)).ToArray();
        Check(selected.Length == 3 && selected.Distinct().Count() == 3, "OR pool includes dual type only once");
    }

    private static void Gameplay(Card boar, bool history)
    {
        var game = NewGame(history);
        var player = game.Player1;
        var target = (Minion)Entity.FromCard(player, boar);
        Generic.SummonBlock(game, target, -1, null);
        Check(TargetingPredicates.ReqTargetWithRace((int)Race.BEAST)(target)
            && TargetingPredicates.ReqTargetWithRace((int)Race.MECHANICAL)(target)
            && !TargetingPredicates.ReqTargetWithRace((int)Race.DRAGON)(target), "dual targeting");
        Play(game, player, "DS1_070", target); // Houndmaster's Beast-targeted buff.
        Check(target.AttackDamage == 3 && target.BaseHealth == 3 && target.HasTaunt, "Beast buff accepts Boar");
        Play(game, player, "GVG_055", target); // Screwjank Clunker buffs a Mech.
        Check(target.AttackDamage == 5 && target.BaseHealth == 5, "Mech buff accepts Boar");
        target.Silence();
        Check(target.IsRace(Race.BEAST) && target.IsRace(Race.MECHANICAL), "silence keeps both printed types");
        var copy = Generic.Copy(player, player.Hero, target, Zone.HAND);
        Check(copy.Card.IsRace(Race.MECHANICAL) && copy.Card.IsRace(Race.BEAST), "ordinary copy keeps both types");
        var deckCard = Entity.FromCard(player, boar, zone: player.DeckZone);
        Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.DeckZone);
        new DrawMinionTask(Race.DRAGON, 1, false).Process(game, player, player.Hero, null);
        Check(deckCard.Zone == player.DeckZone, "unrelated draw excludes Boar");
        new DrawMinionTask(Race.MECHANICAL, 1, false).Process(game, player, player.Hero, null);
        Check(deckCard.Zone == player.HandZone, "secondary-type draw finds Boar");
        target = (Minion)Generic.ChangeEntityBlock(player, target, Cards.FromId("TLC_225"), true);
        Check(target.IsRace(Race.ELEMENTAL) && target.IsRace(Race.MURLOC)
            && !target.IsRace(Race.BEAST) && !target.IsRace(Race.MECHANICAL), "transform replaces printed set");
    }

    private static void Kindred(bool history)
    {
        var game = NewGame(history);
        var player = game.Player1;
        var dual = Typed(Race.BEAST, 2539);
        foreach (Race race in new[] { Race.BEAST, Race.MECHANICAL })
        {
            player.CardsPlayedThisTurn.Clear();
            player.CardsPlayedThisTurn.Add(Typed(race));
            player.SnapshotCardsPlayedThisTurn();
            Check(player.HasKindredMatch(dual), "Kindred matches either component");
            player.CardsPlayedThisTurn.Clear();
            player.CardsPlayedThisTurn.Add(dual);
            player.SnapshotCardsPlayedThisTurn();
            Check(player.HasKindredMatch(Typed(race)), "Kindred reverse overlap");
        }
        Check(!player.HasKindredMatch(Typed(Race.DRAGON)) && !player.HasKindredMatch(Typed(Race.INVALID))
            && !player.HasKindredMatch(Cards.FromId("CS2_029")), "Kindred negative cases");
        Check(player.HasKindredMatch(Typed(Race.ALL)), "Kindred All overlap");
        player.CardsPlayedThisTurn.Clear();
        player.CardsPlayedThisTurn.Add(Typed(Race.INVALID));
        player.SnapshotCardsPlayedThisTurn();
        Check(!player.HasKindredMatch(Typed(Race.ALL)), "Kindred All does not match typeless");
    }

    private static void Persistence(bool history)
    {
        var game = NewGame(history);
        var minion = Summon(game.Player1, "CORE_LOE_039"); // Existing registered Mech/Beast.
        game.Player1.CardsPlayedThisTurn.Add(minion.Card);
        game.Player1.SnapshotCardsPlayedThisTurn();
        var bytes = game.SaveSnapshot();
        var clone = game.CloneExact(history: history);
        var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "snapshot byte stability");
        foreach (var branch in new[] { game, clone, loaded })
        {
            var target = (Minion)branch.IdEntityDic[minion.Id];
            Check(target.IsRace(Race.MECHANICAL) && target.IsRace(Race.BEAST), "clone/snapshot retain secondary type");
            Check(branch.Player1.HasKindredMatch(Cards.FromId("CS2_171")), "clone/snapshot retain previous-turn type overlap");
            target.Silence();
            var copy = Generic.Copy(branch.Player1, branch.Player1.Hero, target, Zone.HAND);
            Check(copy.Card.IsRace(Race.BEAST), "resumed copy retains type");
            Generic.ChangeEntityBlock(branch.Player1, target, Cards.FromId("TLC_225"), true);
            branch.DeathProcessingAndAuraUpdate();
        }
        Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded), "resumed continuations agree");
    }
}
