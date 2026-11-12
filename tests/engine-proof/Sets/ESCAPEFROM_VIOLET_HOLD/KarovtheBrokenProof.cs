using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class KarovtheBrokenProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_448", (g, p, id) => {
        // Tier D: pinned export JAIL_448e/dbfId 131586 is exactly "Cost and stats set to 1."
        HandLimits(g, p, id, 3, death: true, ones: true);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.HandZone.Count == 0, "Taunt, generation only at death");
        var branch = g.CloneExact();
        var expected = Enumerable.Range(0, 3).Select(_ => {
            var picked = OfflineCardPools.Pick(branch, VioletHoldPoolProof.Filter(id), p.HeroClass, source.Card.AssetId);
            return picked.Id == "TLC_452"
                ? SabberStoneCore.CardSets.Client.Sets.TitanographerOsk.Variants[branch.Random.Next(31)] : picked.Id;
        }).ToArray();
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var results = HandResults(g, source, 3, ones: true);
        Core2026Proof.Check(results.Select(c => c.Card.Id).SequenceEqual(expected), "seeded independent Legendary picks");
        Resume(g, next => Core2026Proof.Check(results.All(c => next.IdEntityDic[c.Id].Cost == 1 && ((Minion)next.IdEntityDic[c.Id]).Health == 1), "1/1 and cost enchantment cloned"));
        var played = (Minion)results[0];
        // Summon the generated entity without its unrelated Battlecry.
        p.HandZone.Remove(played); Generic.SummonBlock(g, played, -1, source); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(played.AttackDamage == 1 && played.Health == 1, "generated stats persist in play");
        played.Silence();
        Core2026Proof.Check(played.AttackDamage == played.Card.ATK && played.BaseHealth == played.Card.Health && played.Cost == played.Card.Cost, "Silence removes setting enchantment");
        ClearBoard(g, p); EmptyHand(p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Karov Deathrattle");
        source = Summon(p, id); Generic.ReturnToHandBlock(p, source);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone.Single().Id == source.Id, "bounce does not trigger Deathrattle");
        EmptyHand(p);
        var enemy = Summon(p.Opponent, id); Kill(g, enemy);
        HandResults(g, enemy, 3, ones: true);
        Core2026Proof.Check(p.HandZone.Count == 0, "enemy Deathrattle generates only for its owner");
        LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        source = Summon(p, id); Kill(g, source); HandResults(g, source, 3, ones: true);
        Core2026Proof.Check(p.BoardZone.SlotCount == 6, "full board does not block Deathrattle hand generation");
    });
}
