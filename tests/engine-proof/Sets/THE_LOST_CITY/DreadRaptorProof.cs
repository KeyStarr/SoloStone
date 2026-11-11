using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class DreadRaptorProof
{
    internal static void Run() => DeathKnightC1Proof.Case("TLC_432", (g, p, id) => {
        ClassDeck(p);
        var expensive = Entity.FromCard(p, Cards.FromId("CORE_EX1_110"), zone: p.DeckZone);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var vanilla = Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        var eligible = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(eligible.Zone == p.HandZone && eligible.Cost == 2 && p.DeckZone.Count == 3, "only cheap Deathrattle minion drawn; no Kindred discount");
        StandardSetsProof.ClearBoard(g, p);
        var random = g.Random.CaptureState(); Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == 3 && g.Random.CaptureState().Equals(random), "no eligible minion: no draw, fatigue or RNG");
        Core2026Proof.Check(expensive.Zone == p.DeckZone && spell.Zone == p.DeckZone && vanilla.Zone == p.DeckZone, "all nonmatches remain in deck");
        StandardSetsProof.ClearBoard(g, p);
        var first = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        var second = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(new[] { first, second }.Count(m => m.Zone == p.HandZone && m.Cost == 2) == 1
            && new[] { first, second }.Count(m => m.Zone == p.DeckZone) == 1, "random tutor selects one entity; clone/load preserve choice and RNG");
        var remaining = new[] { first, second }.Single(m => m.Zone == p.DeckZone);
        p.SetasideZone.Add(p.DeckZone.Remove(remaining));
        StandardSetsProof.ClearBoard(g, p); Play(g, p, "CS2_171"); End(g); End(g); EmptyHand(p);
        eligible = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        Effects.ReduceCost(1).ApplyAuraTo(eligible);
        Core2026Proof.Check(eligible.Cost == 1, "eligible tutor starts with a cached cost aura");
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(eligible.Zone == p.HandZone && eligible.Cost == 0 && Cards.FromId("CORE_EX1_012").Cost == 2,
            "previous-turn Beast activates permanent entity-only zero cost");
        int drawnId = eligible.Id;
        Resume(g, next => { End(next); End(next); });
        Core2026Proof.Check(g.IdEntityDic[drawnId].Cost == 0, "discount persists across turns and snapshot");
        End(g); End(g);
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
        Play(g, p, "CS2_171");
        eligible = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(eligible.Cost == 2, "same-turn Beast is not Kindred");
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p); Play(g, p, "CS2_171"); End(g); End(g); EmptyHand(p);
        eligible = Entity.FromCard(p, Cards.FromId("CORE_EX1_012"), zone: p.DeckZone);
        var source = Entity.FromCard(p, Cards.FromId(id)); FillHand(p);
        Resolve(g, source);
        Core2026Proof.Check(eligible.Zone == p.GraveyardZone && eligible.Cost == 2 && p.HandZone.Count == 10, "burned tutor does not discount or replace hand card");
        EmptyHand(p); DeathKnightC1Proof.FullBoard(p); source = Add(p, id);
        Core2026Proof.Check(!DeathKnightC1Proof.HasPlay(p, source), "full mixed board prevents play");
    });
}
