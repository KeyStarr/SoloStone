using HsSim;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AlAkirLordOfStormsProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_153", (g, p, id) => {
        for (int tier = 0; tier < 3; tier++)
        {
            ParkBoard(p); p[HeraldRules.Progress] = tier * 2;
            foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
            var body = (Minion)Play(g, p, id);
            int factor = 1 << tier, attack = 2 + 2 * factor;
            TheBlackBloodProof.Lean_Appendages(body, new[] { "CATA_153t", "CATA_153t1" }, factor, 2 * factor, factor);
            Core2026Proof.Check(body.IsRush && body.HasWindfury && !p.Options().Any(t => t.Source?.Id == body.Id && t.PlayerTaskType == PlayerTaskType.MINION_ATTACK) && body.AttackDamage == attack, $"Lean_C2 both hand auras buff body before Battlecry: rush={body.IsRush}, windfury={body.HasWindfury}, canAttack={body.CanAttack}, attack={body.AttackDamage}/{attack}");
            Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(m => m.Card.Type == CardType.MINION && m.Card.Cost == attack && m.Cost == 1 && m[GameTag.COST] == 1
                && m[GameTag.DISPLAYED_CREATOR] == body.Id), "two independent current-Attack pool draws set visible cost 1");
            var enemy = Summon(p.Opponent, "CS2_182");
            Core2026Proof.Check(body.CanAttack && p.Opponent.BoardZone.All(m => m.AttackDamage == 4), "Rush attack available; opposing minions not aura-buffed");
            var hands = ColossalRules.AppendagesOf(body).ToArray();
            var clone = g.CloneExact(history: true); ((Minion)clone.IdEntityDic[hands[0].Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)clone.IdEntityDic[body.Id]).AttackDamage == 2 + factor && body.AttackDamage == attack, "clone Silence removes only own hand aura");
            hands[0].Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(body.AttackDamage == 2 + factor, "Silence removes hand aura");
            Kill(g, hands[1]);
            Core2026Proof.Check(body.AttackDamage == 2, "hand death removes remaining aura");
            Kill(g, enemy); End(g); End(g);
            Core2026Proof.Check(p.HandZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == body.Id).All(m => m.Cost == 1), "generated discount persists across turns");
        }
        foreach (int count in new[] { 0, 1 })
        {
            ParkBoard(p); foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
            p[HeraldRules.Progress] = 0;
            var installed = g.OfflinePools;
            var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
            var ids = OfflineCardPools.Candidates(g, p.HeroClass,
                OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4), Cards.FromId(id).AssetId).Select(c => c.Id).ToHashSet();
            rows.ReplaceNodes(count == 0 ? rows.Elements("Card").Where(r => (string)r.Attribute("type") == "WEAPON").Take(1).ToArray()
                : rows.Elements("Card").Where(r => ids.Contains((string)r.Attribute("cardId"))).Take(1).ToArray());
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
            try
            {
                var rng = g.Random.CaptureState(); var source = (Minion)Play(g, p, id);
                Core2026Proof.Check(p.HandZone.Count == count * 2 && (count != 0 || g.Random.CaptureState().Equals(rng)), "empty dynamic pool no-op, singleton draws twice");
                if (count == 1) Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1
                    && p.HandZone.Select(c => c.Id).Distinct().Count() == 2 && p.HandZone.All(c => c.Card.Cost == 4 && c.Cost == 1), "independent replacement draws have separate discounted entities");
            }
            finally { typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, installed); }
        }
        ParkBoard(p); p[HeraldRules.Progress] = 0;
        var handBeforeSummon = p.HandZone.Select(c => (c.Id, c.Card.Id, c.Cost)).ToArray();
        var rngBeforeSummon = g.Random.CaptureState();
        var summoned = Summon(p, id);
        Core2026Proof.Check(summoned.AttackDamage == 4, "effect summon receives adjacent auras");
        Core2026Proof.Check(p.HandZone.Select(c => (c.Id, c.Card.Id, c.Cost)).SequenceEqual(handBeforeSummon)
            && g.Random.CaptureState().Equals(rngBeforeSummon), "effect summon skips Battlecry: unchanged hand entities, identities, costs and RNG");
        var left = ColossalRules.AppendagesOf(summoned).First();
        var location = LocationProofs.Location(p, position: 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(summoned.AttackDamage == 3, "physical location interrupts hand adjacency");
        Kill(g, summoned);
        Core2026Proof.Check(p.BoardZone.Count == 2, "Lean_C4 body death leaves hands");
        TheBlackBloodProof.Lean_FullBoard(g, p, id);
        ParkBoard(p); foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var held = p.HandZone[0]; p.SetasideZone.Add(p.HandZone.Remove(held));
        var full = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(m => m.Cost == 1) == 1, "only one generated minion fits hand capacity");
    });
}
