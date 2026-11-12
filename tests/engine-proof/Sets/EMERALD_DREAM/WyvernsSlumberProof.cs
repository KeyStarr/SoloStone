using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

using static ChooseOneProof;
internal static class WyvernsSlumberProof
{
    // misc-cards/dreadseed-token-recipes.yaml PINNED-RULE-ONLY (A/C/D exact recipes);
    // dreadseed-roll-replacement.yaml STILL-UNPINNED: owner assumes independent uniform rolls with replacement.
    internal static void Run() => StandardSetsProof.Case("EDR_820", Unpinned_WyvernsSlumber);
    private static void Unpinned_WyvernsSlumber(Game g, Controller p, string id) {
        bool duplicate = false;
        for (int seed = 1; seed <= 12; seed++)
        {
            ParkBoard(p); EmptyHand(p); g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            var expected = new[] { DreadseedProof.Ids[oracle.Random.Next(3)], DreadseedProof.Ids[oracle.Random.Next(3)] };
            var spell = PlayOption(g, p, id, 1);
            Core2026Proof.Check(BEPoolProof.Children(p, spell).Select(m => m.Card.Id).SequenceEqual(expected),
                "Unpinned_WyvernsSlumber: exact independent seeded token rolls in summon order");
            duplicate |= expected[0] == expected[1];
        }
        Core2026Proof.Check(duplicate, "Unpinned_WyvernsSlumber: real plays include a duplicate pair, not a no-replacement bag");
        ParkBoard(p); EmptyHand(p);

        DreadseedProof.Closure(); EmeraldDreamCardStateProof.Token("EDR_820a", 115200); EmeraldDreamCardStateProof.Token("EDR_820b", 115201);
        OptionsSurvive(g, p, id, "EDR_820a", "EDR_820b");
        DreadseedProof.Outcomes(g, p, () => BEPoolProof.Children(p, PlayOption(g, p, id, 1)), 2);
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Summon(p, "CS2_052"); PlayOption(g, p, id, 2);
        Core2026Proof.Check(friendly.Damage == 3 && enemy.Damage == 3 && p.Opponent.Hero.Damage == 0, "damage branch hits both boards with Spell Damage, excludes heroes");
        ParkBoard(p); ParkBoard(p.Opponent);
        var fandral = Both(p); enemy = Summon(p.Opponent, "CS2_182");
        var source = PlayOption(g, p, id, -1); var children = BEPoolProof.Children(p, source);
        Core2026Proof.Check(children.Length == 2 && children.All(m => m.IsDormant && m.Damage == 0)
            && fandral.Damage == 2 && enemy.Damage == 2, DreadseedProof.Leans + ": Choose Both summons two then AoE; new sleepers excluded");
        ParkBoard(p); ParkBoard(p.Opponent);
        BEPoolProof.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        source = PlayOption(g, p, id, 1);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && BEPoolProof.Children(p, source).Length == 1, "one physical slot produces only one Dreadseed");
    }
}
