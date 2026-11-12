using System;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class InfiniteStatsTests
{
    public static void TestInfiniteAttackReplySearch()
    {
        var g = Fixture(); Giant(g, true, false); Board(g, true, "CS2_120", true);
        var view = View(g);
        var beam = new TurnBeam(view, s => s.Evaluate(new DeckPlanEvaluator(DeckPlans.DragonWarrior)), 70000, 25, true);
        while (!beam.IsDone) beam.StepNode();
    }
    internal static NpcProfile Elite()
    {
        var p = NpcCatalog.All[0];
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
            p.Personality, new Budget(280000, 70000, 4000, 14, 9, 160, 25, false, 45, 55), p.PlanId);
    }
    private static Minion Giant(Game g, bool own, bool buff)
    {
        var m = Board(g, own, "CORE_NEW1_018", true);
        m.AttackDamage = int.MaxValue;
        if (buff) new Effect(GameTag.ATK, EffectOperator.ADD, 1).ApplyTo(m);
        Check(m.AttackDamage == int.MaxValue, "engine +1 attack must saturate");
        return m;
    }
    public static void TestInfiniteAttackTeacherSearch()
    {
        foreach (int seat in new[] { 1, 2 }) foreach (bool own in new[] { false, true })
        foreach (bool buff in new[] { false, true }) {
            Minion giant;
            var g = Position(seat, own, buff, out giant);
            var view = FairView(g, seat); var profile = Elite();
            var planner = new TurnPlannerJob(view, profile, DeckPlans.DragonWarrior, 71, profile.Budget.PlanNodes, null);
            int nodes = 0;
            while (!planner.IsDone) { planner.StepNode(); Check(++nodes <= 70001, "elite planner node guard"); }
            Check(planner.Result.Line.Count > 0 && view.Actions.Contains(planner.Result.Line[0]), "elite line legal");
            var replay = view;
            foreach (var action in planner.Result.Line) replay = replay.Apply(action);
            Check(own ? replay.Facts.Own.PlayState == PlayState.WON
                : replay.Facts.Opponent.Board.All(c => c.EntityId != giant.Id), "take lethal or remove opposing giant");
        }
    }
    private static FairBotState FairView(Game g, int seat) => FairBotState.Create(g, seat, 71, DeckStyle.Tempo,
        new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
    private static Game Position(int seat, bool own, bool buff, out Minion giant)
    {
        var g = Fixture();
        if (seat == 2) Check(g.Process(EndTurnTask.Any(g.Player1)), "second-seat turn");
        var player = seat == 1 ? g.Player1 : g.Player2;
        player.BaseMana = 10; player.UsedMana = 0;
        foreach (var c in player.HandZone.ToArray()) { player.HandZone.Remove(c); player.SetasideZone.Add(c); }
        giant = Giant(g, own == (seat == 1), buff);
        Board(g, own == (seat == 1), "CS2_120", true);
        if (!own) {
            giant.Damage = 1;
            Entity.FromCard(player, Cards.FromId("CS2_108"), zone: player.HandZone);
        }
        return g;
    }
    public static void TestInfiniteAttackLiveTurnGuard()
    {
        bool sawClockCut = false;
        foreach (int seat in new[] { 1, 2 }) foreach (bool own in new[] { false, true })
        foreach (bool buff in new[] { false, true }) foreach (bool expired in new[] { false, true }) {
            Minion giant;
            var g = Position(seat, own, buff, out giant);
            var player = seat == 1 ? g.Player1 : g.Player2;
            if (expired) player.Hero.HeroPower.IsExhausted = true;
            var profile = Elite(); var pilot = new GoldenPilot(profile);
            double now = 0; int decisions = 0; bool done = false;
            var runner = new NpcTurnRunner(profile, "infinite-stats", () => now += expired ? 1000 : .001,
                () => !done, () => new GoldenContext(g.Turn, ++decisions, 71, profile),
                context => pilot.Job(FairView(g, seat), context),
                reason => { throw new Exception("overflow must not cause fallback: " + reason); },
                answer => {
                    Check(++decisions < 20, "bounded live turn");
                    Check(FairView(g, seat).Actions.Contains(answer.Action), "live guard returned illegal decision");
                    Check(g.Process(answer.Action.Resolve(player.Options())), "live action applied");
                    done = answer.Action.Type == PlayerTaskType.END_TURN || player.PlayState != PlayState.PLAYING;
                }, () => true);
            runner.Start(); runner.RunSynchronously(); sawClockCut |= runner.ClockCut;
            Check(own ? player.PlayState == PlayState.WON : !player.Opponent.BoardZone.Any(c => c.Id == giant.Id),
                "live turn must take lethal or remove giant: seat=" + seat + " own=" + own + " expired=" + expired);
        }
        Check(sawClockCut, "expired-clock fixtures must exercise live cutoff");
    }
    public static void TestExtremeStatsEvaluationAndThreats()
    {
        var g = Fixture(); var own = Giant(g, true, true);
        own.Health = int.MaxValue; own.HasDivineShield = true;
        own[GameTag.REBORN] = 1;
        var blocker = Board(g, true, "EX1_110"); blocker.HasTaunt = true; blocker.Health = int.MaxValue;
        blocker.HasDivineShield = true;
        for (int i = 0; i < 2; i++) Giant(g, false, true)[GameTag.MEGA_WINDFURY] = 1;
        foreach (var p in new[] { g.Player1, g.Player2 }) { p.Hero.Health = int.MaxValue; p.Hero.Armor = int.MaxValue; }
        var f = View(g).Facts; var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        // Eight giant hits, one shield and one giant-health blocker consume exactly two.
        Check(DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId) == 6L * int.MaxValue,
            "Windfury/Taunt threat must retain damage above int.MaxValue");
        Check(!DeckPlanEvaluator.PublicBoardSurvives(f), "two-max life must not hide six-max threat");
        foreach (double value in new[] { evaluator.Evaluate(f), evaluator.DeathValue(f.Own, f.Opponent),
            evaluator.Resistance(f), View(g).Evaluate() }) Check(!double.IsNaN(value) && !double.IsInfinity(value), "finite extreme valuation");
        var features = LearnedValue.Features(f.Own, f.Opponent, f.Turn, f.Own.Hand);
        Check(features.All(v => !double.IsNaN(v) && !double.IsInfinity(v)) && features[9] == 2L * int.MaxValue / 5.0,
            "learned attack totals must widen before Sum");
        var banana = Entity.FromCard(g.Player1, Cards.FromId("EX1_014t"), zone: g.Player1.HandZone);
        // The ordinary +1/+1 spell traverses the same real task path as a live buff.
        Check(g.Process(g.Player1.Options().Single(t => t.Source?.Id == banana.Id && t.Target?.Id == own.Id)), "play giant buff");
        Check(own.AttackDamage == int.MaxValue && own.Health == int.MaxValue, "engine spell buff must saturate both attributes");
        Entity.FromCard(g.Player1, Cards.FromId("EX1_619"), zone: g.Player1.HandZone);
        Check(g.Player1.Options().All(t => !double.IsNaN(LocalBot.Score(t))), "fallback ordering handles giant health totals");
        var publicBoard = new[] { new NpcPublicMinion(1, int.MaxValue, int.MaxValue, true), new NpcPublicMinion(2, int.MaxValue, int.MaxValue, true) };
        Check(new NpcPublicPosition(publicBoard, new NpcPublicMinion[0], 30, 30, 10, 0).Value == 4L * int.MaxValue,
            "public presentation totals must not throw during NPC turn");
    }
    public static void TestInfiniteDeathrattleAndWeaponValues()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        double? expected = null;
        foreach (int size in new[] { 10, int.MaxValue }) {
            var g = Fixture(); var host = Board(g, true, "EX1_110");
            host.Health = size; host.HasDivineShield = true; host[GameTag.REBORN] = 1;
            Board(g, false, "CS2_120").AttackDamage = size;
            var f = View(g).Facts; double value = evaluator.DeathValue(f.Own, f.Opponent);
            if (expected.HasValue) Check(Math.Abs(expected.Value - value) < 1e-12, "shield pressure ratio wrapped in Deathrattle/Reborn discount");
            else expected = value;
        }
        var game = Fixture();
        var infinity = Entity.FromCard(game.Player1, Cards.FromId("END_012"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == infinity.Id)), "equip Hand of Infinity");
        var raider = Entity.FromCard(game.Player1, Cards.FromId("CORE_NEW1_018"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == raider.Id)), "Raider copies Infinity weapon");
        Check(((Minion)raider).AttackDamage == int.MaxValue, "Raider battlecry must saturate printed attack plus Infinity");
        var weapon = game.Player1.Hero.Weapon;
        weapon.Durability = int.MaxValue;
        var facts = View(game).Facts;
        Check(LearnedValue.Features(facts.Own, facts.Opponent, facts.Turn, facts.Own.Hand)[23]
            == (long)int.MaxValue * int.MaxValue / 5.0, "weapon attack-times-durability must widen first");
        Check(LethalBound.UpperBound(facts) >= int.MaxValue, "lethal time-pool bound wrapped Infinity damage");
        var announcer = Entity.FromCard(game.Player1, Cards.FromId("TIME_034"), zone: game.Player1.HandZone);
        game.Player1.UsedMana = 0;
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == announcer.Id)), "Stadium Rewind offer");
        weapon = game.Player1.Hero.Weapon;
        weapon.AttackDamage = int.MaxValue; weapon.Durability = int.MaxValue;
        Check(new GoldenPilot(NpcCatalog.All[0]).KeepRewind(new RewindView(game, game.Player1)), "keep huge visible weapon rather than overflow its value");
    }
}
