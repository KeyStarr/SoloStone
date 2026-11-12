using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class ColossalDestinationRegressionProof
{
    internal static void Run(string only = null)
    {
        if (only == null || only == "TIME_730" || only == "BE_150")
        {
            foreach (string id in new[] { "TIME_730", "BE_150" }.Where(id => only == null || id == only))
                Core2026Proof.Check(OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools
                    .All(p => p.Destination == PoolDestination.Deck), id + " Discover is delivered only into deck");
            Console.WriteLine("PASS deck-only Discover destinations");
        }
        if (only == null || only == "deck-replacement")
        {
            var pools = OfflinePoolDeclarations.Cards.Single(c => c.CardId == "TIME_707").Pools;
            Core2026Proof.Check(pools.Single(p => p.Name == "choose-one").Destination == PoolDestination.Hand &&
                pools.Any(p => p.Name == "choose-one-deck" && p.Destination == PoolDestination.Deck),
                "Alternate Reality declares separate hand and deck replacement destinations");
            Console.WriteLine("PASS hand/deck replacement destinations");
        }
        if (only == null || only == "stored-summon")
        {
            Core2026Proof.Check(OfflinePoolDeclarations.Cards.Single(c => c.CardId == "DINO_430").Pools.Single()
                .Destination == PoolDestination.Summon, "Taka selects for eventual Deathrattle summon");
            Console.WriteLine("PASS stored Deathrattle summon destination");
        }
        if (only == null || only == "nespirah") Nespirah();
        if (only == null || only == "event") EventDestinations();
    }

    private static void EventDestinations()
    {
        var expected = new Dictionary<string, PoolDestination[]> {
            ["CATA_EVENT_000"] = new[] { PoolDestination.Hand },
            ["CATA_EVENT_400"] = new[] { PoolDestination.Summon },
            ["JAIL_EVENT_102"] = new[] { PoolDestination.Summon, PoolDestination.Transform },
            ["TIME_EVENT_997"] = new[] { PoolDestination.Summon },
            ["JAIL_EVENT_100"] = new[] { PoolDestination.Hand },
            ["TIME_EVENT_999"] = new[] { PoolDestination.Hand },
            ["TIME_EVENT_999t"] = Enumerable.Repeat(PoolDestination.Hand, 11).ToArray(),
            ["TLC_EVENT_400"] = new[] { PoolDestination.Hand, PoolDestination.Hand },
            ["TSC_660"] = new[] { PoolDestination.Hand }
        };
        var bodies = new[] { "TSC_026", "TSC_029", "TSC_030", "TSC_216", "TSC_937", "TSC_639",
            "TSC_950", "TSC_219", "TSC_962", "TSC_660", "TID_711", "TID_712", "NX2_037" };
        var declarations = OfflinePoolDeclarations.Cards.Where(c => c.CardId.Contains("_EVENT_") ||
            c.CardId == "EDR_950" || bodies.Contains(c.CardId)).ToArray();
        Core2026Proof.Check(declarations.Select(c => c.CardId).OrderBy(id => id).SequenceEqual(expected.Keys.OrderBy(id => id)),
            "EVENT and thirteen historical bodies have exactly the audited pool sources; Dredge and Gigafin use existing identities");
        foreach (var source in declarations)
            Core2026Proof.Check(source.Pools.Select(p => p.Destination).SequenceEqual(expected[source.CardId]),
                source.CardId + " declares its printed eventual pool destination");
        var manifest = OfflinePoolPolicy.Load().BuildManifest();
        var lord = declarations.Single(c => c.CardId == "CATA_EVENT_000");
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.WARRIOR, lord.Pools.Single().Filter,
            lord.DbfId, PoolDestination.Hand).Select(c => c.Id).ToHashSet().SetEquals(bodies),
            "installed Lord Hand contains exactly the thirteen Past Colossals");
        foreach (var destination in new[] { PoolDestination.Summon, PoolDestination.Transform })
            Core2026Proof.Check(OfflineCardPools.Candidates(manifest, CardClass.WARRIOR,
                OfflinePoolFilter.Random(type: CardType.MINION, scope: PoolScope.Wild), 0, destination)
                .All(c => c[(GameTag)2247] == 0), "installed " + destination + " excludes every Colossal");
        Console.WriteLine("PASS installed Colossal destinations: Lord Hand=13; Summon/Transform=0 Colossals");
        Console.WriteLine("PASS EVENT and historical Colossal pool destinations: hand rewards/components/crew, summons and transforms");
    }

    private static void Nespirah()
    {
        var game = LocationProofs.Game();
        var owner = game.Player1;
        var card = OfflineCardPools.Candidates(game, owner.HeroClass,
            OfflinePoolFilter.Random(CardType.MINION, race: Race.NAGA), 122845).First();
        var xml = OfflinePoolFixtures.Catalog();
        var members = xml.Root.Element("Members");
        var row = new XElement(members.Elements("Card").Single(r => (string)r.Attribute("cardId") == card.Id));
        members.RemoveNodes(); members.Add(row);
        row.Add(new XElement("Tag", new XAttribute("id", 2247), new XAttribute("value", 1)));
        var tag = (GameTag)2247;
        bool hadTag = card.Tags.TryGetValue(tag, out int original);
        try
        {
            card.Tags[tag] = 1;
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, OfflinePoolFixtures.Manifest(xml));
            Core2026Proof.Check(OfflineCardPools.Candidates(game, owner.HeroClass,
                OfflinePoolFilter.Random(CardType.MINION, race: Race.NAGA), 122845).Single() == card,
                "synthetic non-Herald Colossal Naga is eligible for ordinary hand generation");
            var freed = Core2026Proof.Summon(owner, "CATA_527t2");
            Core2026Proof.Play(game, owner, "CORE_BT_035");
            Core2026Proof.Check(!owner.HandZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == freed.Id),
                "Nespirah's explicit non-Colossal text excludes synthetic admitted Naga");
            card.Tags.Remove(tag);
            row.Elements("Tag").Single(t => (int)t.Attribute("id") == 2247).Remove();
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, OfflinePoolFixtures.Manifest(xml));
            Core2026Proof.Play(game, owner, "CORE_BT_035");
            Core2026Proof.Check(owner.HandZone.Single(c => c[GameTag.DISPLAYED_CREATOR] == freed.Id).Card == card,
                "Nespirah still generates ordinary Naga");
            Console.WriteLine("PASS Nespirah local non-Colossal restriction and ordinary Naga control");
        }
        finally
        {
            if (hadTag) card.Tags[tag] = original; else card.Tags.Remove(tag);
        }
    }
}
