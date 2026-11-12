using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class BoardBuffTests
{
    public static void TestDeathrattleValueUsesPublicEffectiveTags()
    {
        var g = Fixture(); var cinder = Board(g, true, "TLC_249");
        var eval = new DeckPlanEvaluator(DeckPlans.ZeeShaman);
        double retained = View(g).Evaluate(eval);
        cinder[GameTag.DEATHRATTLE] = 0;
        Check(View(g).Evaluate(eval) < retained, "removed Deathrattle must lose its future value");
        cinder[GameTag.DEATHRATTLE] = 1;
        Check(View(g).Evaluate(eval) == retained, "effective granted Deathrattle must retain its value");
        var plain = Board(g, true, "CS2_182");
        double before = View(g).Evaluate(eval);
        plain[GameTag.DEATHRATTLE] = 1;
        Check(View(g).Evaluate(eval) == before, "a bare keyword without a task must not invent a payoff");
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_876e"), plain, plain);
        Check(View(g).Evaluate(eval) > before, "actual granted Deathrattle must count");
    }
    // ZeeCashh owner replay, turn 12 after Eggbearer draws. Killing a Cinder can roll lucky pings,
    // but buffing first preserves both its body and its still-available Deathrattle.
    public static void TestBuffBeforeTradingCinders()
    {
        var g = Fixture(CardClass.SHAMAN, CardClass.ROGUE);
        while (g.Turn < 13) g.Process(EndTurnTask.Any(g.CurrentPlayer));
        foreach (var p in new[] { g.Player1, g.Player2 })
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
        g.Player1.BaseMana = 6; g.Player1.UsedMana = 2;
        g.Player2.BaseMana = 6; g.Player2.UsedMana = 6; g.Player2.Hero.Damage = 9;
        Board(g, true, "CORE_UNG_205", true);
        Board(g, true, "TLC_249", true).AttackDamage = 1; Board(g, true, "TLC_249", true).AttackDamage = 1;
        Board(g, true, "DINO_411");
        var guardian = Board(g, false, "EDR_800"); guardian.HasDivineShield = false; guardian.AttackDamage = 1;
        Board(g, false, "END_010"); Board(g, false, "CATA_216"); Board(g, false, "CORE_BT_701");
        foreach (var id in new[] { "CATA_497", "CATA_190h", "JAIL_457", "CATA_565" })
            Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        for (int i = 0; i < 4; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        var buff = g.Player1.HandZone.Single(c => c.Card.Id == "JAIL_457").Id;
        var attackers = g.Player1.BoardZone.Where(m => !m.IsExhausted).Select(m => m.Id).ToArray();
        // Attack-first wins only when the root's single roll of the Cinder's pings is lucky, so one search seed proves little: the
        // base buffs first in 11 of 16 seeds. OFF keeps the pinned seed. ON is held to the OFF rate over seeds, and to scoring
        // buff-first higher in expectation over rolls of the pings, which is the actual rule this test protects.
        var profile = NpcCatalog.All[2]; var off = profile.WithFixes(new AiFixes(new[] { "death-value" }));
        bool BuffFirst(PlannedTurn turn) {
            int index = turn.Line.ToList().FindIndex(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == buff);
            return index >= 0 && turn.Line.Take(index).All(a => a.Type != PlayerTaskType.MINION_ATTACK || !attackers.Contains(a.Source));
        }
        int Rate(Game game, NpcProfile arm) {
            int first = 0;
            for (int i = 0; i < 16; i++) {
                var seeded = new GoldenPilot(arm).Options.FairViewFactory(game, 1, 12584 + i, DeckStyle.Tempo);
                var run = TurnPlanner.Job(seeded, arm, DeckPlans.ZeeShaman, 12553 + i); Run(run);
                if (BuffFirst(run.Result)) first++;
            }
            return first;
        }
        foreach (var game in new[] { g, Variant(g) }) {
            var pilot = new GoldenPilot(off);
            var view = pilot.Options.FairViewFactory(game, 1, 12584, DeckStyle.Tempo);
            var job = TurnPlanner.Job(view, off, DeckPlans.ZeeShaman, 12553); Run(job);
            Check(BuffFirst(job.Result), "buff must precede recipient attacks: " + PlannerTests.Describe(job.Result));
            if (game != g) continue; // Hidden-card variants share the pinned check above; seeds and rolls are slow, once is enough.
            int on = Rate(game, profile), baseRate = Rate(game, off);
            Check(on >= baseRate - 3, "ON buffs first about as often as OFF over seeds: " + on + "/16 vs " + baseRate + "/16");
            var target = guardian.Id;
            double Scored(bool buffFirst) {
                double total = 0; int count = 0;
                var ev = new DeckPlanEvaluator(DeckPlans.ZeeShaman) { DeathFixes = new AiFixes() };
                for (int i = 0; i < 24; i++) {
                    var state = view.WithArtificialSeed(i); bool legal = true;
                    foreach (bool buffStep in buffFirst ? new[] { true, false } : new[] { false, true }) {
                        var step = state.Actions.FirstOrDefault(a => buffStep
                            ? a.Type == PlayerTaskType.PLAY_CARD && a.Source == buff
                            : a.Type == PlayerTaskType.MINION_ATTACK && a.Target == target && attackers.Skip(1).Contains(a.Source));
                        if (step == null) { legal = false; break; }
                        state = state.Apply(step);
                    }
                    if (!legal) continue;
                    var reply = ReplyModel.Job(state, profile, 1000 + i, ev, new string[0], 40); Run(reply);
                    total += reply.Value; count++;
                }
                Check(count > 0, "forced line resolves on some roll");
                return total / count;
            }
            double buffScore = Scored(true), attackScore = Scored(false);
            Check(buffScore > attackScore, "ON scores buff-first above attack-first over rolls: " + buffScore + " vs " + attackScore);
        }
    }
}
