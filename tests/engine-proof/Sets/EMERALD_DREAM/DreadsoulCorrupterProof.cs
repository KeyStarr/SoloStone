using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class DreadsoulCorrupterProof
{
    // misc-cards/dreadseed-token-recipes.yaml PINNED-RULE-ONLY (A/C/D exact recipes);
    // dreadseed-roll-replacement.yaml STILL-UNPINNED: owner assumes independent uniform rolls with replacement.
    internal static void Run() => StandardSetsProof.Case("EDR_841", Unpinned_DreadsoulCorrupter);
    private static void Unpinned_DreadsoulCorrupter(Game g, Controller p, string id) {
        for (int rngSeed = 1; rngSeed <= 3; rngSeed++)
        {
            ParkBoard(p); EmptyHand(p); g.SetRandomSeed(rngSeed);
            var oracle = g.CloneExact(history: true);
            string battlecry = DreadseedProof.Ids[oracle.Random.Next(3)];
            string expectedDeathrattle = DreadseedProof.Ids[oracle.Random.Next(3)];
            var source = (Minion)Play(g, p, id);
            Core2026Proof.Check(BEPoolProof.Children(p, source).Single().Card.Id == battlecry,
                "Unpinned_DreadsoulCorrupter: exact seeded Battlecry token");
            var first = BEPoolProof.Children(p, source).Single(); Kill(g, source);
            Core2026Proof.Check(BEPoolProof.Children(p, source).Single(m => m.Id != first.Id).Card.Id == expectedDeathrattle,
                "Unpinned_DreadsoulCorrupter: next independent seeded Deathrattle token");
        }
        ParkBoard(p); EmptyHand(p);

        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var positioned = PlayAt(g, p, id, 2);
        var beside = BEPoolProof.Children(p, positioned).Single();
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, positioned.Id, beside.Id, right.Id }),
            "Battlecry Dreadseed is immediately right of living source, counting location slot");
        ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        var full = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && BEPoolProof.Children(p, full).Length == 0,
            "source fills last physical slot; Battlecry cannot summon");
        Kill(g, full);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && BEPoolProof.Children(p, full).Length == 1,
            "Deathrattle reuses exactly the freed source slot");
        ParkBoard(p); EmptyHand(p);
        DreadseedProof.Closure();
        DreadseedProof.Outcomes(g, p, () => {
            var source = Play(g, p, id);
            Core2026Proof.Check(((Minion)source).AttackDamage == 4 && ((Minion)source).Health == 4, "printed 4/4 body");
            return BEPoolProof.Children(p, source);
        }, 1);
        var board = DeathrattleSlotBoard(p, () => Summon(p, id));
        var corrupter = p.BoardZone.Single(m => m.Card.Id == id);
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[corrupter.Id]));
        var deathrattle = BEPoolProof.Children(p, corrupter).Single();
        DreadseedProof.Sleeping(deathrattle);
        CheckDeathrattleSlot(p, board, new[] { deathrattle }, "Dreadsoul Deathrattle uses source's physical slot");
        ParkBoard(p);
        var silenced = (Minion)Play(g, p, id); silenced.Silence(); var seed = BEPoolProof.Children(p, silenced).Single();
        Kill(g, silenced);
        Core2026Proof.Check(BEPoolProof.Children(p, silenced).Single().Id == seed.Id, "Silence removes Deathrattle");
    }
}
