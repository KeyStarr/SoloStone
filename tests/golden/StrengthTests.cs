using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static ReplyTests;

public static class StrengthTests
{
    public static void TestFairSimulationCompletesHeldUpgrade()
    {
        foreach (var upgrade in new[] { new[] { "JAIL_805", "JAIL_805t" }, new[] { "EDR_843", "EDR_843t1" } })
        {
            var game = Fixture(CardClass.SHAMAN);
            var held = Entity.FromCard(game.Player1, Cards.FromId(upgrade[0]), zone: game.Player1.HandZone);
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            var coin = Entity.FromCard(game.Player1, Cards.FromId("GAME_005"), zone: game.Player1.HandZone);
            game.DeathProcessingAndAuraUpdate();
            var before = game.SaveSnapshot(); var state = View(game);
            var action = state.Actions.First(a => upgrade[0] == "JAIL_805" ? a.Source == coin.Id
                : a.Type == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN);
            var child = state.Apply(action);
            Check(child.Facts.Own.Hand.Any(c => c.EntityId == held.Id && c.CardId == upgrade[1]),
                "Fair simulation must complete the visible held-form upgrade");
            Check(before.SequenceEqual(game.SaveSnapshot()), "Held-form simulation changed authority");
        }
    }

    public static void TestRepeatedFairFactsReadsDoNotRebuildSnapshots()
    {
        var state = View(Fixture());
        int health = state.Facts.Own.Hero.Health, sum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) sum += state.Facts.Own.Hero.Health;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(sum == health * 100 && allocated < 1024,
            "Repeated immutable facts reads allocated " + allocated + " bytes");
        var child = state.Apply(state.Actions.Single(a => a.Type == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        Check(child.Facts.Turn == state.Facts.Turn + 1 && state.Facts.Own.Hero.Health == health,
            "Child state must have fresh facts without changing the parent snapshot");
    }

    public static void TestObservedEggbearerTargetsRetainZeeRefill()
    {
        var game = Fixture(CardClass.SHAMAN, CardClass.ROGUE); var bot = game.Player1;
        new ReplaceHeroPower(Cards.FromId("JAIL_800hp2")).Process(game, bot, bot.Hero, null);
        for (int i = 0; i < 4; i++)
        {
            var filler = SabberStoneCore.Actions.Generic.DrawCard(bot, Cards.FromId("CS2_231"));
            Check(game.Process(bot.Options().First(t => t.Source == filler)), "Zee progress play");
            filler.Zone.Remove(filler); bot.SetasideZone.Add(filler);
        }
        foreach (string id in new[] { "CATA_565", "CORE_GIL_531" })
        {
            var seen = SabberStoneCore.Actions.Generic.DrawCard(bot, Cards.FromId(id));
            bot.HandZone.Remove(seen); SabberStoneCore.Actions.Generic.ShuffleIntoDeck(bot, bot.Hero, seen);
        }
        Board(game, true, "CORE_UNG_809", true); Board(game, true, "CORE_UNG_205", true); Board(game, true, "UNG_809t1", true);
        bot.BaseMana = 3; bot.UsedMana = 0; game.Player2.Hero.Damage = 1;
        Entity.FromCard(bot, Cards.FromId("CATA_497"), zone: bot.HandZone);
        Entity.FromCard(bot, Cards.FromId("CATA_190h"), zone: bot.HandZone);
        var eggbearer = Entity.FromCard(bot, Cards.FromId("DINO_411"), zone: bot.HandZone);
        var goon = Entity.FromCard(bot, Cards.FromId("JAIL_802"), zone: bot.HandZone);
        var state = View(game);
        Check(state.Facts.OwnDeckKnowledge["CATA_565"] == 1 && state.Facts.OwnDeckKnowledge["CORE_GIL_531"] == 1,
            "Visible shuffled targets must remain known");
        var job = TurnPlanner.Job(state, NpcCatalog.All[2], DeckPlans.ZeeShaman, 71); Run(job);
        Check(job.Result.Line.Any(a => a.Source == eggbearer.Id) && !job.Result.Line.Any(a => a.Source == goon.Id),
            "Observed double refill should beat non-Battlecry Goon");
    }

    public static void TestLuckyRandomReplyDoesNotBecomeCertainLoss()
    {
        var game = Fixture(CardClass.WARRIOR, CardClass.MAGE); game.Player1.Hero.Damage = 29;
        game.Player2.BaseMana = 0;
        for (int i = 0; i < 7; i++) Board(game, true, "CS2_182");
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = ReplyModel.Job(View(game), NpcCatalog.All[0], 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
            job.StepNode(); job.StepNode();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(new[] { "EX1_277" }, 1) });
            Run(job, slice);
            Check(job.Value > -999000 && job.Value < View(game).Evaluate(new DeckPlanEvaluator(DeckPlans.DragonWarrior)),
                "lucky Missiles retain ordinary damage cost, never a terminal sentinel");
            Check(!job.Survival.HasValue || job.Survival > 0, "one lucky Missiles roll must not become a certain-loss veto");
            Check(job.Nodes <= ReplyModel.NodeAllowance(1, NpcCatalog.All[0].Budget.ReplyR), "verification exceeded reply allowance");
        }
    }
    public static void TestReplyFindsBurnLethalDespiteExtraArmorAndTradeTargets()
    {
        foreach (int armor in new[] { 0, 2 }) {
            var game = Fixture(CardClass.WARRIOR, CardClass.ROGUE); game.Turn = 25;
            game.Player1.Hero.Damage = 22; game.Player1.Hero.Armor = armor;
            Board(game, true, "END_035").AttackDamage = 8;
            Board(game, true, "CAP_107"); Board(game, true, "CS2_231");
            Board(game, false, "END_036").Damage = 3;
            Board(game, false, "TLC_630").Damage = 4;
            var weapon = (Weapon)Entity.FromCard(game.Player2, Cards.FromId("FIR_922"));
            game.Player2.Hero.AddWeapon(weapon); weapon.AttackDamage = 1; weapon.Durability = 1;
            for (int i = 0; i < 5; i++) Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.HandZone);
            var job = ReplyModel.Job(View(game), NpcCatalog.All[0], 24956, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
            job.StepNode(); job.StepNode();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(new[] {
                "CATA_785", "CORE_BT_701", "TIME_770", "JAIL_720", "CATA_785", "EDR_889" }, 1) });
            Run(job);
            Check(job.Outcomes.Count == 1 && job.Outcomes[0].Key > -999000,
                "two Rites keep ordinary utility separate from lethal risk through " + armor + " armor, not look safe behind trade branches");
            Check(job.Survival == 0, "deterministic burn must retain its verified-loss ranking");
            Check(job.Nodes <= ReplyModel.NodeAllowance(1, NpcCatalog.All[0].Budget.ReplyR), "reply exceeded existing allowance");
        }
    }
    public static void TestDiscountCreditsReachableNextTurnWithoutLosingIntrinsicValue()
    {
        var game = Fixture(CardClass.SHAMAN); game.Player1.BaseMana = 7;
        var deathwing = Entity.FromCard(game.Player1, Cards.FromId("CATA_190h"), zone: game.Player1.HandZone);
        var evaluator = new DeckPlanEvaluator(DeckPlans.ZeeShaman);
        double full = View(game).Evaluate(evaluator);
        deathwing.Cost = 9; double distant = View(game).Evaluate(evaluator);
        deathwing.Cost = 7; double reachable = View(game).Evaluate(evaluator);
        Check(reachable > full && reachable > distant && reachable - full <= 8, "reachable discount needs bounded positive value");
        Check(distant >= full, "discount must not destroy intrinsic hand value");
    }
    public static void TestTinyTauntCannotEraseOverwhelmingDanger()
    {
        var game = Fixture(); game.Player1.Hero.Damage = 28;
        for (int i = 0; i < 4; i++) Board(game, false, "CS2_200");
        var small = Board(game, true, "CS2_231");
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        double plain = View(game).Evaluate(evaluator); small.HasTaunt = true;
        Check(View(game).Evaluate(evaluator) - plain < 100, "tiny Taunt erased lethal danger cliff");
    }
    public static void TestFifthSetupNeedsAffordableUsefulBattlecry()
    {
        foreach (string id in new[] { "CATA_497", "JAIL_802", "JAIL_800" }) {
            var game = Fixture(CardClass.SHAMAN); game.Player1.BaseMana = 2;
            new ReplaceHeroPower(Cards.FromId("JAIL_800hp2")).Process(game, game.Player1, game.Player1.Hero, null);
            Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
            var evaluator = new DeckPlanEvaluator(DeckPlans.ZeeShaman);
            var power = game.Player1.Hero.HeroPower;
            power[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1; double ready = View(game).Evaluate(evaluator);
            power[GameTag.TAG_SCRIPT_DATA_NUM_1] = 2;
            Check(Math.Abs(ready - View(game).Evaluate(evaluator)) < .00001, "unreachable/non-Battlecry setup bonus: " + id);
        }
    }
}
