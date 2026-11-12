using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class IsisetHeldFormProof
{
    internal static void Unpinned_Isiset(Game g, Controller p, string id)
    {
        Play(g, p, "CS2_029", p.Opponent.Hero);
        var body = (Minion)Play(g, p, id);
        var constructs = p.BoardZone.Where(m => m.Card.Id == "BE_254").ToArray();
        Core2026Proof.Check(constructs.Length == 2 && constructs.All(m => m.IsFrozen && m[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15),
            "Unpinned_IsisetSpellManaCounter: separate fresh 15-Mana Constructs");
        body.Silence(); Kill(g, body);
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var a = (Minion)next.IdEntityDic[constructs[0].Id]; var b = (Minion)next.IdEntityDic[constructs[1].Id];
            End(next); End(next); End(next);
            Core2026Proof.Check(a.IsFrozen && b.IsFrozen, "Unpinned_IsisetFreezeGate: normal thaw suppressed");
            Play(next, next.Player2, "GAME_005"); End(next);
            Core2026Proof.Check(a[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15, "opponent payments excluded");
            a.Silence(); Core2026Proof.Check(!a.IsFrozen, "Silence bypasses intrinsic restriction");
            Play(next, next.Player1, "CS2_026");
            Core2026Proof.Check(b[GameTag.TAG_SCRIPT_DATA_NUM_1] == 12 && a[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15,
                "own actual Mana only, silenced source stops counting");
            for (int i = 0; i < 4; i++) Play(next, next.Player1, "CS2_026");
            Core2026Proof.Check(!b.IsFrozen && b[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "threshold thaws immediately");
        }
        PaymentsAndBypass();
    }

    private static void PaymentsAndBypass()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var first = Summon(p, "BE_254");
        Play(g, p, "GAME_005");
        var automatic = (Spell)Entity.FromCard(p, Cards.FromId("CS2_026"));
        Generic.CastSpell(p, g, automatic, null, 0);
        Core2026Proof.Check(first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15, "free and automatic casts spend no Mana");
        var reduced = (Playable)Add(p, "CS2_026");
        SabberStoneCore.Enchants.Effects.ReduceCost(2).ApplyTo(reduced); PlayEntity(g, p, reduced);
        Core2026Proof.Check(first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 14, "discounted spell counts actual payment, not printed Cost");
        var health = (Playable)Add(p, "CS2_026");
        CostRules.AddPaymentOverride(health, CostPaymentKind.Health);
        Core2026Proof.Check(g.Process(PlayCardTask.Spell(p, health)) && first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 14,
            "Health payment excluded");
        var second = Summon(p, "BE_254");
        End(g); Play(g, g.Player2, "EX1_287"); End(g);
        var countered = (Spell)Play(g, p, "CS2_026");
        Core2026Proof.Check(countered.IsCountered && first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 11 && second[GameTag.TAG_SCRIPT_DATA_NUM_1] == 12,
            "paid countered spell counts; separate cohorts retain distinct progress");
        first[GameTag.CANT_BE_FROZEN] = 1; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!first.IsFrozen, "cannot-be-Frozen aura suppresses gate");
        first[GameTag.CANT_BE_FROZEN] = 0; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(first.IsFrozen, "leaving bypass reapplies live gate");
        Generic.ReturnToHandBlock(p, first); PlayEntity(g, p, first);
        Core2026Proof.Check(first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15 && first.IsFrozen, "backward reset creates fresh intrinsic requirement");
    }
}
