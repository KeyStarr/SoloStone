using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using System.Xml.Linq;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RitualoftheNewMoonProof
{
    // Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection Unpinned_RitualPools.
    internal static void Run() => StandardSetsProof.Case("EDR_461", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("EDR_461t", 114493);
        EmeraldDreamCardStateProof.HiddenUpgrade(id, "EDR_461t");
        var location = LocationProofs.Location(p);
        var source = Play(g, p, id);
        Generated(g, p, source, 3, 2, 1);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == location, "spell summons append after physical location");
        ParkBoard(p);
        var held = EmeraldDreamCardStateProof.UpgradeBySpells(g, p, id, "EDR_461t");
        ParkBoard(p);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[held.Id]));
        Generated(g, p, held, 6, 2, 0);
        ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.SlotCount < 6) Summon(p, "TIME_046");
        source = Play(g, p, id);
        Generated(g, p, source, 3, 1, 6);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "Unpinned_RitualPools one free physical slot gives one summon");
        var before = p.BoardZone.Slots.Select(e => e.Id).ToArray();
        Play(g, p, "EDR_461t");
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(before), "full board cast legal; no overflow");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        var baseSpell = Add(p, id); baseSpell[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        PlayEntity(g, p, baseSpell);
        Generated(g, p, baseSpell, 3, 2, 0);
        Core2026Proof.Check(baseSpell.Card.Id == id, "Unpinned_HandCastScope base spell never upgrades itself");
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.DEMONHUNTER, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE,
            CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR })
            foreach (int cost in new[] { 3, 6 })
                Core2026Proof.Check(OfflineCardPools.Candidates(g, cls, OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost), 114300).Count >= 3,
                    "Unpinned_RitualPools admitted Any-class printed-cost candidates " + cls);
        foreach (int cost in new[] { 3, 6 })
        {
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
            string form = cost == 3 ? id : "EDR_461t";
            WithPool(g, cost, 0, () => {
                var random = g.Random.CaptureState(); Play(g, p, form);
                Core2026Proof.Check(p.BoardZone.SlotCount == 0 && random.Equals(g.Random.CaptureState()), "empty pool gives no summons/RNG");
            });
            WithPool(g, cost, 1, () => {
                Play(g, p, form);
                Core2026Proof.Check(p.BoardZone.SlotCount == 2 && p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1,
                    "independent picks permit duplicates for " + form);
            });
        }
    });

    private static void Generated(Game g, Controller p, IPlayable source, int cost, int count, int offset)
    {
        var outputs = p.BoardZone.Slots.Skip(offset).ToArray();
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost), 114300);
        Core2026Proof.Check(outputs.Length == count && outputs.All(m => candidates.Any(c => c.Id == m.Card.Id && c.AssetId == m.Card.AssetId)
            && m.Card.Cost == cost && m[GameTag.DISPLAYED_CREATOR] == source.Id && m.Controller == p), "Unpinned_RitualPools independent admitted summons at printed cost " + cost);
        foreach (var m in outputs)
        {
            var member = g.OfflinePools.Standard.Single(c => c.CardId == m.Card.Id);
            Core2026Proof.Check(m.Card.ATK == member.Attack && m.Card.Health == member.Health, "generated identity and pinned stats");
        }
    }

    private static void WithPool(Game g, int cost, int count, Action action)
    {
        var installed = g.OfflinePools;
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass,
            OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost), 114300);
        var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").First(r => candidates.Any(c => c.Id == (string)r.Attribute("cardId")));
        if (count == 0) selected.SetAttributeValue("cost", 999);
        rows.ReplaceNodes(selected);
        var fixture = OfflinePoolFixtures.Manifest(xml);
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, fixture); action(); }
        finally { property.SetValue(g, installed); }
    }
}
