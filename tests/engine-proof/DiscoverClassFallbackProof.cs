using System.Xml.Linq;
using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

// Regression test. A Rogue's Illidari Studies offered nothing, because Rogue plus neutral has no Outcast cards. A normal
// Discover keeps its hero-class-plus-neutral pool; only when that pool is truly empty does it fall back to the printed
// class(es) of the card doing the Discovering. Random/OtherClass/Exact filters are unchanged. The fallback comes from
// community reports with no retail trace (an unpinned assumption).
internal static class DiscoverClassFallbackProof
{
    [GroupProof("discover-class-fallback")]
    internal static void Run()
    {
        Boundaries();
        GeneratedSource();
        (string[] Offers, RngState Before, RngState Open, RngState After) Studies(CardClass hero)
        {
            var game = OfflinePoolTestRecipes.NewGame(hero: hero);
            var owner = game.Player1;
            var before = game.Random.CaptureState();
            var source = Play(game, owner, "CORE_YOP_001");
            var open = game.Random.CaptureState();
            Core2026Proof.Check(open.DrawCount - before.DrawCount == 3, "Studies consumes exactly three sampling draws for " + hero);
            Core2026Proof.Check(owner.Choice != null, "Studies opens Outcast choice for " + hero);
            var offers = owner.Choice.Choices.Select(id => game.IdEntityDic[id].Card).ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Id).Distinct().Count() == 3
                && offers.All(c => c[GameTag.OUTCAST] > 0 && c.Id != source.Card.Id && game.OfflinePools.Standard
                    .Single(m => m.DbfId == c.AssetId).Classes.Contains("DEMONHUNTER")),
                "Studies offers distinct DH Outcast cards for " + hero);
            int chosen = owner.Choice.Choices[0];
            var choiceIds = owner.Choice.Choices.ToArray();
            var copies = new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) };
            int historyCount = game.PowerHistory.Full.Count;
            CoreCostProof.Answer(game, chosen);
            string continuation = Canonical.Value(game.PowerHistory.Full.Skip(historyCount).ToArray());
            Core2026Proof.Check(owner.Choice == null && game.IdEntityDic[chosen].Zone == owner.HandZone
                && source.Zone == owner.GraveyardZone && game.SpellCompletion == null,
                "Studies pick reaches hand and spell completes for " + hero);
            foreach (var copy in copies)
            {
                Core2026Proof.Check(copy.Player1.Choice.Choices.SequenceEqual(choiceIds)
                    && copy.Random.CaptureState().Equals(open), "open fallback clone/snapshot preserves offers and RNG");
                int copyHistoryCount = copy.PowerHistory.Full.Count;
                CoreCostProof.Answer(copy, chosen);
                Core2026Proof.Check(Canonical.State(copy) == Canonical.State(game)
                    && Canonical.Value(copy.PowerHistory.Full.Skip(copyHistoryCount).ToArray()) == continuation
                    && copy.Random.CaptureState().Equals(game.Random.CaptureState()),
                    "identical fallback answer preserves clone/snapshot continuation state/history/RNG");
            }
            Core2026Proof.Check(game.Random.CaptureState().Equals(open), "Studies continuation consumes no extra RNG");
            return (offers.Select(c => c.Id).ToArray(), before, open, game.Random.CaptureState());
        }
        var rogue = Studies(CardClass.ROGUE);
        var dh = Studies((CardClass)14);
        Core2026Proof.Check(rogue.Offers.SequenceEqual(dh.Offers) && rogue.Before.Equals(dh.Before)
            && rogue.Open.Equals(dh.Open) && rogue.After.Equals(dh.After),
            "empty Rogue pool and DH control have identical offers and RNG states/draw counts");

        var game = OfflinePoolTestRecipes.NewGame(hero: CardClass.ROGUE);
        var owner = game.Player1;
        Play(game, owner, "CORE_BAR_541", owner.Opponent.Hero);
        Core2026Proof.Check(owner.Choice != null && owner.Choice.Choices.All(id => game.OfflinePools.Standard
            .Single(m => m.DbfId == game.IdEntityDic[id].Card.AssetId).Classes.Contains("ROGUE")),
            "off-class Runed Orb keeps nonempty hero spell pool");
        CoreCostProof.Answer(game, owner.Choice.Choices[0]);
        Play(game, owner, "EDR_273");
        Core2026Proof.Check(owner.Choice != null && owner.Choice.Choices.All(id => {
            var card = game.IdEntityDic[id].Card;
            var member = game.OfflinePools.Standard.Single(m => m.DbfId == card.AssetId);
            return card[GameTag.CHOOSE_ONE] > 0 && !member.Classes.Contains("ROGUE") && !member.Classes.Contains("NEUTRAL");
        }), "explicit other-class Choose One Discover unchanged");
        CoreCostProof.Answer(game, owner.Choice.Choices[0]);

        var xml = OfflinePoolFixtures.Catalog();
        xml.Root.Element("Members").Elements("Card").Where(row => row.Elements("Tag")
            .Any(tag => (int)tag.Attribute("id") == (int)GameTag.OUTCAST)).Remove();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, OfflinePoolFixtures.Manifest(xml));
        long draws = game.Random.DrawCount;
        var empty = Play(game, owner, "CORE_YOP_001");
        Core2026Proof.Check(owner.Choice == null && game.SpellCompletion == null && empty.Zone == owner.GraveyardZone
            && game.Random.DrawCount == draws, "both Outcast pools empty: no choice, no RNG, spell completes");
        Console.WriteLine("PASS Unpinned_DiscoverClassFallback: generated source live/fair; Rogue Studies/DH RNG; clone/snapshot continuation; nonempty hero pool; OtherClass; both empty");
    }

    private static void GeneratedSource()
    {
        var game = OfflinePoolTestRecipes.NewGame(hero: CardClass.ROGUE);
        var owner = game.Player1;
        owner.Opponent.Hero[GameTag.CLASS] = 14;
        var xml = OfflinePoolFixtures.Catalog();
        // Isolate the generated body, retaining the real spell domain for its Battlecry.
        xml.Root.Element("Members").Elements("Card").Where(row => (string)row.Attribute("type") == "MINION"
            && (string)row.Attribute("cardId") != "FIR_952").Remove();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, OfflinePoolFixtures.Manifest(xml));
        var generated = Watfin.Offers(game, owner.Opponent, owner.Opponent.Hero, new()).Single(c => c.Id == "JAIL_EVENT_100t");
        Core2026Proof.Check(generated[GameTag.MODULAR_ENTITY_PART_1] == Scorchreaver.DbfId && (int)generated.Class == 14
            && Cards.FromId(generated.Id).Class == CardClass.NEUTRAL, "Watfin Scorchreaver retains DH definition under neutral token ID");
        var stolen = Generic.DrawCard(owner.Opponent, generated);
        Summon(owner, "JAIL_205");
        End(game);
        Core2026Proof.Check(stolen.Controller == owner && stolen.Zone == owner.HandZone, "Rat Burglar steals generated definition intact");
        End(game);
        var fair = game.CloneExact(history: true);
        Watfin.RedactForFairView(fair.IdEntityDic[stolen.Id]);
        Core2026Proof.Check(fair.IdEntityDic[stolen.Id].Card.Id == "FIR_952", "fair-search redaction restores original source identity");
        var before = game.Random.CaptureState();
        string[] expected = null;
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools), game, fair })
        {
            PlayEntity(next, next.Player1, next.IdEntityDic[stolen.Id]);
            Core2026Proof.Check(next.Player1.Choice != null, "generated Scorchreaver opens Rogue Fel fallback in live/fair search");
            var ids = next.Player1.Choice.Choices.ToArray();
            var offers = ids.Select(id => next.IdEntityDic[id].Card.Id).ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.All(id => Cards.FromId(id)[GameTag.SPELL_SCHOOL] == 7), "generated source offers three Fel spells");
            expected ??= offers;
            Core2026Proof.Check(expected.SequenceEqual(offers) && next.Random.DrawCount - before.DrawCount == 3,
                "live/fair generated source offers and sampling draw deltas agree");
            // Eye Beam's intrinsic Outcast cost would mask the one-mana discount.
            int selected = ids.First(id => next.IdEntityDic[id].Card.Id != "CORE_BT_801");
            CoreCostProof.Answer(next, selected);
            var chosen = next.IdEntityDic[selected];
            Core2026Proof.Check(next.Player1.Choice == null && chosen.Zone == next.Player1.HandZone
                && chosen.Cost == Math.Max(0, chosen.Card.Cost - 1),
                $"generated source continuation grants and discounts chosen spell: {chosen.Card.Id} zone={chosen.Zone?.Type} cost={chosen.Cost}/{chosen.Card.Cost} choice={next.Player1.Choice != null}");
        }
        Core2026Proof.Check(game.Random.CaptureState().Equals(fair.Random.CaptureState()), "live/fair generated source RNG states agree after continuation");
    }

    private static void Boundaries()
    {
        var definition = Cards.FromId("CS2_182").Clone();
        var discover = OfflinePoolFilter.Discover(type: CardType.SPELL);
        definition.Power = new Power { PowerTask = new DiscoverWithDarkGiftThenContinueTask(discover) };
        string encoded = SnapshotCodec.EncodeDefinition(definition, detached: true);
        Core2026Proof.Check(Convert.FromBase64String(encoded).Take(3).SequenceEqual(new byte[] { 255, 255, 7 }),
            "Discover fallback requires definition layout v7");
        var restored = SnapshotCodec.DecodeDefinition(encoded);
        Core2026Proof.Check((OfflinePoolFilter)typeof(DiscoverWithDarkGiftThenContinueTask)
            .GetField("_filter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(restored.Power.PowerTask) == discover
            && SnapshotCodec.EncodeDefinition(restored) == encoded, "Discover fallback survives detached definition round trip");
        var manifest = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture());
        int studies = Cards.FromId("CORE_YOP_001").AssetId;
        var filter = OfflinePoolFilter.Discover(type: CardType.SPELL).WithCost(4).WithMaxCost(4);
        var raw = new OfflinePoolFilter(classMode: PoolClassMode.ControllerAndNeutral, type: CardType.SPELL, minCost: 4, maxCost: 4);
        Core2026Proof.Check(filter != raw && filter.GetHashCode() != raw.GetHashCode(), "Discover intent participates in filter/cache identity");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, studies).Count == 1
            && OfflineCardPools.Candidates(manifest, CardClass.ROGUE, raw, studies).Count == 0,
            "fallback after cost/type filters; random controller/neutral query stays empty");
        int dhMask = OfflineCardPools.PrintedClassMask(Cards.FromAssetId(studies));
        int token = Cards.FromId("JAIL_EVENT_100t").AssetId;
        var generated = OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, token, sourceClassMask: dhMask);
        Core2026Proof.Check(generated.Count == 1
            && OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, token).Count == 0
            && OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, studies, sourceClassMask: 0).Count == 0,
            "same ID caches distinct runtime printed classes; explicit neutral overrides registry class");
        Core2026Proof.Check(ReferenceEquals(generated, OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, token,
            sourceClassMask: dhMask | int.MinValue | 1 << ((int)CardClass.NEUTRAL - 1))),
            "cache normalizes neutral and unsupported class bits");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, generated.Single().AssetId,
            sourceClassMask: dhMask).Count == 0, "runtime class mask does not replace source self-exclusion ID");
        Core2026Proof.Check(OfflineCardPools.Pick(manifest, new Util.DeepCloneableRandom(1), filter, CardClass.ROGUE, studies) == null,
            "random picker never falls back even with a Discover filter");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, Cards.FromId("CORE_KAR_062").AssetId).Count == 0
            && OfflineCardPools.Candidates(manifest, CardClass.ROGUE, filter, 0).Count == 0,
            "neutral or absent source does not choose an arbitrary fallback class");
        var exact = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE,
            type: CardType.SPELL, minCost: 4, maxCost: 4, discoverFallback: true);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.ROGUE, exact, studies).Count == 0,
            "explicit Exact cannot fall back");
        var other = new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.SPELL,
            minCost: 4, maxCost: 4, discoverFallback: true);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, (CardClass)14, other, studies).Count == 0,
            "explicit OtherClass cannot fall back");

        var xml = OfflinePoolFixtures.Fixture();
        var rows = xml.Root.Element("Members").Elements("Card").Where(r => (string)r.Attribute("scope") == "Standard")
            .OrderBy(r => (int)r.Attribute("dbfId")).ToArray();
        rows[1].Elements("Class").Remove();
        rows[1].Add(new XElement("Class", new XAttribute("name", "DEATHKNIGHT")));
        var dual = OfflinePoolFixtures.Manifest(xml);
        var dualResult = OfflineCardPools.Candidates(dual, CardClass.ROGUE, OfflinePoolFilter.Discover(), Cards.FromId("END_005").AssetId);
        // Neutral candidate makes the normal pool nonempty; source fallback must not top it up.
        Core2026Proof.Check(dualResult.Select(c => c.AssetId).SequenceEqual(new[] { (int)rows[0].Attribute("dbfId") }),
            "one normal neutral candidate wins over dual-class source");
        rows[0].Remove();
        dual = OfflinePoolFixtures.Manifest(xml);
        dualResult = OfflineCardPools.Candidates(dual, CardClass.ROGUE, OfflinePoolFilter.Discover(), Cards.FromId("END_005").AssetId);
        Core2026Proof.Check(dualResult.Select(c => c.AssetId).SequenceEqual(new[] { (int)rows[1].Attribute("dbfId"), (int)rows[3].Attribute("dbfId") }),
            "Unpinned dual-class source fallback unions both printed classes in manifest order");
        rows[1].Elements("Class").Remove();
        rows[1].Add(new XElement("Class", new XAttribute("name", "ROGUE")));
        var singleHero = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(singleHero, CardClass.ROGUE, OfflinePoolFilter.Discover(), Cards.FromId("END_005").AssetId)
            .Select(c => c.AssetId).SequenceEqual(new[] { (int)rows[1].Attribute("dbfId") }), "one hero candidate wins over source classes");
    }
}
