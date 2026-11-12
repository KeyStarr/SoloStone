using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class DeathrattleValueTests
{
    private static readonly DeckPlanEvaluator Eval = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
    private static void Grant(Game g, Minion m, string id = "JAIL_876e") => Generic.AddEnchantmentBlock(g, Cards.FromId(id), m, m);
    private static double Pending(Game g, bool own = true) { var f = View(g).Facts; return own ? Eval.DeathValue(f.Own, f.Opponent) : Eval.DeathValue(f.Opponent, f.Own); }
    private static void Near(double a, double b, string why) => Check(Math.Abs(a - b) < 1e-9, why + ": " + a + " / " + b);

    public static void TestNativeGrantedStackSilenceAndPerspective()
    {
        var g = Fixture(); var m = Board(g, true, "EX1_556");
        var native = new FairCard(m).DeathValue;
        Check(native.Bodies == 1 && native.Attack == 2 && native.Health == 1, "native token payload");
        double original = Pending(g); Grant(g, m);
        double once = Pending(g); Grant(g, m); double twice = Pending(g);
        Check(once > original, "grant adds value to existing native Deathrattle");
        Near(twice - once, once - original, "two grants stack without keyword double count");
        var opponent = Fixture(); var enemy = Board(opponent, false, "EX1_556"); Grant(opponent, enemy); Grant(opponent, enemy);
        Near(twice, Pending(opponent, false), "same public effects have same value on both sides");
        var before = View(opponent).Evaluate(Eval); enemy.Silence();
        Check(View(opponent).Evaluate(Eval) > before, "silencing enemy payoff improves score");
        m.Silence(); Near(Pending(g), 0, "silence removes native and grants");
        Grant(g, m); Near(Pending(g), once - original, "grant after silence works without resurrecting native credit");
        var hidden = Variant(g); Near(Pending(hidden), Pending(g), "hidden deck/hand/RNG changes cannot affect payoff");
        var publicKey = GoldenPublicKey.For(View(opponent)); Grant(opponent, enemy);
        Check(GoldenPublicKey.For(View(opponent)) != publicKey, "opponent grant changes continuation key");
        var drawback = Fixture(); Board(drawback, true, "EX1_577");
        Check(Pending(drawback) < 0, "opponent summon is a drawback, not friendly board credit");
    }

    public static void TestRebornUsesPrintedBodyAndIsConsumed()
    {
        var g = Fixture(); var m = Board(g, true, "CS2_182");
        m.AttackDamage = 19; m.Health = 20; m[GameTag.REBORN] = 1;
        var facts = View(g).Facts;
        Check(facts.Own.Board[0].RebornValue.Attack == m.Card.ATK && facts.Own.Board[0].RebornValue.Health == 1,
            "Reborn is printed attack and one health, never current buffs");
        Check(Pending(g) > 0, "Reborn retains value");
        m[GameTag.HAS_BEEN_REBORN] = 1; Near(Pending(g), 0, "spent Reborn gets no second body");
        m[GameTag.HAS_BEEN_REBORN] = 0; m.Silence(); Near(Pending(g), 0, "silenced Reborn loses value");
        var resummon = Board(g, true, "CS2_200"); Grant(g, resummon, "CS2_038e");
        Check(new FairCard(resummon).DeathValue.Health == resummon.Card.Health, "Ancestral Spirit retains full printed health");
        var native = Fixture(); var hoarder = Board(native, true, "EX1_096"); hoarder[GameTag.REBORN] = 1; Grant(native, hoarder);
        var reborn = new FairCard(hoarder);
        Check(reborn.RebornDeathValue.Cards == 1 && reborn.RebornDeathValue.Bodies == 0,
            "Reborn retains printed draw, never the granted two summons");
    }

    public static void TestPressureSpaceDrawAndResolvedValue()
    {
        var g = Fixture(); var m = Board(g, true, "CS2_182"); Grant(g, m);
        double quiet = Pending(g); var attacker = Board(g, false, "CS2_200"); attacker.AttackDamage = 20;
        Check(Pending(g) > quiet, "public kill pressure brings payoff closer");
        attacker.CantAttack = true; Near(Pending(g), quiet, "cannot-attack body invents no pressure");
        attacker.CantAttack = false; m.HasDivineShield = true;
        Check(Pending(g) > quiet, "shield discounts but does not erase pressure");
        m.HasDivineShield = false; attacker.CantAttack = true;
        for (int i = 0; i < 6; i++) Board(g, true, "CS2_182");
        Near(Pending(g), quiet / 2, "full board leaves exactly one summon slot after death");
        m[GameTag.REBORN] = 1; Near(Pending(g), quiet / 2, "Deathrattle fills slot before Reborn");
        var draw = Fixture(); Board(draw, true, "EX1_096");
        Check(Pending(draw) > 0, "native draw retains a card");
        for (int i = 0; i < 10; i++) Entity.FromCard(draw.Player1, Cards.FromId("CS2_182"), zone: draw.Player1.HandZone);
        Near(Pending(draw), 0, "full hand burns expected draw");
        var resolved = Fixture(); var golem = Board(resolved, true, "EX1_556", true);
        var target = Board(resolved, false, "CS2_200");
        var view = View(resolved); var after = view.Apply(view.Actions.Single(a => a.Source == golem.Id && a.Target == target.Id));
        Check(after.Facts.Own.Board.Count == 1 && after.Facts.Own.Board[0].CardId == "skele21", "real death summons token");
        Near(Eval.DeathValue(after.Facts.Own, after.Facts.Opponent), 0, "resolved summon gets body credit only");
    }

    private static void DeckSize(Controller p, int count)
    {
        foreach (var card in p.DeckZone.Skip(count).ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
    }

    public static void TestOpponentDrawDrawbacksAndPublicCapacity()
    {
        foreach (bool own in new[] { true, false })
        foreach (string id in new[] { "FP1_029", "CAP_004" }) {
            var g = Fixture(); var host = Board(g, own, id);
            var recipient = own ? g.Player2 : g.Player1;
            double cards = id == "FP1_029" ? 1 : 2;
            Near(Pending(g, own), -.65 * 5.5 * cards, id + " opponent draw must be negative on either board");
            var enabled = View(g); host.HasDeathrattle = false; var disabled = View(g);
            Check(own ? enabled.Evaluate(Eval) < disabled.Evaluate(Eval) : enabled.Evaluate(Eval) > disabled.Evaluate(Eval),
                "symmetric board score signs the drawback for its owner");
            host.HasDeathrattle = true;
            Near(Pending(Variant(g), own), Pending(g, own), "hidden identities do not price opponent draws");
            DeckSize(recipient, 1);
            Near(Pending(g, own), -.65 * 5.5, "recipient deck capacity caps drawback");
            for (int i = 0; i < 9; i++) Entity.FromCard(recipient, Cards.FromId("CS2_182"), zone: recipient.HandZone);
            Near(Pending(g, own), -.65 * 5.5, "one hand slot retains one card");
            Entity.FromCard(recipient, Cards.FromId("CS2_182"), zone: recipient.HandZone);
            Near(Pending(g, own), 0, "full recipient hand gets no card credit");
            foreach (var card in recipient.HandZone.ToArray()) { recipient.HandZone.Remove(card); recipient.SetasideZone.Add(card); }
            DeckSize(recipient, 0);
            Near(Pending(g, own), 0, "empty recipient deck invents no cards");
        }
        var weblord = Fixture(); Board(weblord, true, "FP1_017");
        Near(Pending(weblord), 0, "Nerub'ar Weblord has no draw Deathrattle");
    }

    public static void TestDrawCapacitySharedAcrossHostsAndReborn()
    {
        foreach (bool own in new[] { true, false }) {
            var g = Fixture(); var owner = own ? g.Player1 : g.Player2; DeckSize(owner, 1);
            var first = Board(g, own, "EX1_096");
            double one = Pending(g, own); Near(one, .65 * 5.5, "one Hoarder, one deck card");
            var second = Board(g, own, "EX1_096");
            Near(Pending(g, own), one, "two Hoarders cannot both claim the last deck card");
            second.HasDeathrattle = false; first[GameTag.REBORN] = 1;
            double withDraw = Pending(g, own); first.HasDeathrattle = false;
            double continuationOnly = Pending(g, own);
            // The native payout pays sooner than the fresh copy; neither may reuse the other's card.
            Near(withDraw - continuationOnly, one * (1 - .65), "Reborn shares the native draw capacity");
            DeckSize(owner, 0);
            Near(withDraw - Pending(g, own), one, "Reborn body plus exactly one retained card");

            var hand = Fixture(); var handOwner = own ? hand.Player1 : hand.Player2;
            for (int i = 0; i < 9; i++) Entity.FromCard(handOwner, Cards.FromId("CS2_182"), zone: handOwner.HandZone);
            Board(hand, own, "EX1_096"); Board(hand, own, "EX1_096");
            Near(Pending(hand, own), one, "multiple hosts also share the last hand slot");

            var opponents = Fixture(); var recipient = own ? opponents.Player2 : opponents.Player1; DeckSize(recipient, 1);
            var harmful = Board(opponents, own, "CAP_004"); harmful[GameTag.REBORN] = 1;
            double before = Pending(opponents, own); Board(opponents, own, "FP1_029");
            Near(Pending(opponents, own), before, "opponent draws share capacity across hosts and Reborn");
        }
        foreach (bool own in new[] { true, false }) {
            var mixed = Fixture(); DeckSize(own ? mixed.Player1 : mixed.Player2, 1);
            var hoarder = Board(mixed, own, "EX1_096"); var swords = Board(mixed, !own, "FP1_029");
            hoarder.AttackDamage = swords.AttackDamage = 0; // Equal horizons isolate shared recipient capacity.
            double both = View(mixed).Evaluate(Eval); swords.HasDeathrattle = false;
            Near(View(mixed).Evaluate(Eval), both, "both boards cannot claim the same recipient's last card");
        }
        var resolved = Fixture(); DeckSize(resolved.Player1, 1);
        var a = Board(resolved, true, "EX1_096", true); var b = Board(resolved, true, "EX1_096", true);
        var target = Board(resolved, false, "CS2_200");
        resolved.Process(MinionAttackTask.Any(resolved.Player1, a, target));
        resolved.Process(MinionAttackTask.Any(resolved.Player1, b, target));
        Check(resolved.Player1.HandZone.Count == 1 && resolved.Player1.DeckZone.Count == 0
            && resolved.Player1.Hero.Damage == 1, "engine resolves one card and one fatigue damage");
    }

    public static void TestDeathValueHotPathAllocation()
    {
        var g = Fixture(); var m = Board(g, true, "EX1_556"); Grant(g, m); m[GameTag.REBORN] = 1;
        Board(g, false, "CS2_200"); var f = View(g).Facts;
        for (int i = 0; i < 1000; i++) Eval.DeathValue(f.Own, f.Opponent);
        long start = GC.GetAllocatedBytesForCurrentThread(); double total = 0;
        for (int i = 0; i < 10000; i++) total += Eval.DeathValue(f.Own, f.Opponent);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Check(total > 0 && bytes == 0, "death-value hot path allocated " + bytes + " bytes");
    }

    public static void TestForcedLossResistanceIgnoresBypassedPayoffs()
    {
        var g = Fixture(); g.Player1.Hero.Damage = 29;
        var own = Board(g, true, "CS2_182"); var enemy = Board(g, false, "CS2_182", true);
        var plain = View(g); double resistance = Eval.Resistance(plain.Facts);
        Grant(g, own); own[GameTag.REBORN] = 1;
        var retained = View(g);
        Check(retained.Evaluate(Eval) > plain.Evaluate(Eval), "normal score must retain future payoffs");
        Near(Eval.Resistance(retained.Facts), resistance, "untriggered friendly payoffs cannot resist face lethal");
        var replies = ReplyModel.Job(retained, NpcCatalog.All[0], 71, Eval); Run(replies);
        Check(replies.Survival == 0, "enemy must bypass the host and kill the hero");
        Grant(g, enemy); enemy[GameTag.REBORN] = 1;
        Near(Eval.Resistance(View(g).Facts), resistance, "untriggered enemy payoffs cannot change forced-loss resistance");
    }
}
