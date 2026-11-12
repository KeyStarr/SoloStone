using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using static Core2026Proof;

internal static class CthunRulesProof
{
    private static readonly SuspendableTask Recipe = CthunRules.ExternalAbility("proof/cthun/base/v1");

    internal static Game Fixture(bool initialCthun = false)
    {
        var game = new Game(new GameConfig { RandomSeed = 3, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"),29).Append(Cards.FromId(initialCthun ? "BE_013" : "CS2_182")).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            p.BaseMana = 10;
        }
        return game;
    }

    internal static void Pick(Game game, string id)
    {
        int chosen = game.Player1.Choice.Choices.Single(e => game.IdEntityDic[e].Card.Id == id);
        PoolPilotProof.Resume(game, g => CoreCostProof.Answer(g,chosen));
    }

    internal static void PickTarget(Game game, int id) => PoolPilotProof.Resume(game,g => CoreCostProof.Answer(g,id));

    private static void Open(Game game, IPlayable source) => Recipe.Process(game,game.Player1,source,null,new TaskStack());

    [GroupProof("cthun")]
    internal static void Run()
    {
        foreach(string id in new[] { "BE_013t1","BE_013t2","BE_013t3","BE_013t4","BE_013t5","BE_013t2e1" }) CostSliceProof.Definition(id);
        Unpinned_CatalogAndLedger();
        Unpinned_GlareTargetsAndFizzle();
        Unpinned_DomainLifecycleAndCapacity();
        Unpinned_PerAttackDecisions();
        Unpinned_CancelTargetPrompt();
        Console.WriteLine("PASS C'Thun shared external facility: catalog, ledger, clone/snapshot, targets, Silence/removal, attacks");
    }

    private static void Unpinned_CatalogAndLedger()
    {
        var game = Fixture(); var p = game.Player1;
        var source = Summon(p,"CS2_182");
        Summon(p,"EX1_012");
        Open(game,source);
        Core2026Proof.Check(p.Choice?.Choices.Count == 4,"C'Thun external facility must offer four abilities without C'Thun in deck");
        var decision = game.GetPendingDecisions().Single(); var before = game.SaveSnapshot();
        Core2026Proof.Check(!game.TryAnswer(new DecisionAnswer(decision.Id,p.Id,new[] { -1 }),out _) && before.SequenceEqual(game.SaveSnapshot()),"invalid choice leaves snapshot/RNG untouched");
        Pick(game,"BE_013t1");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 7 && CthunRules.Progress(p) == 1,"Wrath is seven base hits despite Spell Damage, ledger records Wrath");
        Open(game,source); Pick(game,"BE_013t2");
        var enemy = Summon(p.Opponent,"CS2_200"); enemy[GameTag.HEALTH] = 20;
        p.Hero.Damage = 10;
        Open(game,source); Pick(game,"BE_013t3"); PickTarget(game,enemy.Id);
        Core2026Proof.Check(enemy.Damage == 5 && p.Hero.Damage == 5,"Glare uses five damage and its own Lifesteal source");
        Open(game,source); Pick(game,"BE_013t4");
        Core2026Proof.Check(CthunRules.Progress(p) == 15 && CthunRules.Progress(p.Opponent) == 0,"all four distinct abilities tracked per player");
        StandardSetsProof.ParkBoard(p.Opponent);
        Open(game,source); Pick(game,"BE_013t1");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 14 && CthunRules.Progress(p) == 15,"completed ledger does not triple external abilities or count duplicates twice");
        End(game); End(game);
        Core2026Proof.Check(CthunRules.Progress(p) == 15,"ledger has no turn expiry");
    }

    private static void Unpinned_GlareTargetsAndFizzle()
    {
        var game = Fixture(); var p = game.Player1; var source = Summon(p,"CS2_182");
        var legal = Summon(p.Opponent,"CS2_200");
        var hidden = Summon(p.Opponent,"EX1_028");
        var elusive = Summon(p.Opponent,"EDR_598");
        var immune = Summon(p.Opponent,"CS2_182"); immune.IsImmune = true;
        Open(game,source); Pick(game,"BE_013t3");
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { legal.Id, elusive.Id }),"minion-triggered Glare allows Elusive, excludes heroes, friendlies, Stealth and Immune");
        PickTarget(game,legal.Id);
        var spellSource = Add(p,"BE_778");
        Open(game,spellSource); Pick(game,"BE_013t3");
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { legal.Id }),"spell-triggered Glare excludes Elusive using original spell source");
        PickTarget(game,legal.Id);
        StandardSetsProof.ParkBoard(p.Opponent);
        Open(game,source); Pick(game,"BE_013t3");
        Core2026Proof.Check(p.Choice == null && CthunRules.Progress(p) == 4,"targetless Glare fizzles and records triggered ability");
    }

    private static void Unpinned_DomainLifecycleAndCapacity()
    {
        var game = Fixture(); var p = game.Player1; var source = Summon(p,"CS2_182");
        var a = Summon(p,"CS2_182"); var b = Summon(p,"CS2_182");
        Open(game,source); Pick(game,"BE_013t2");
        Core2026Proof.Check(source.AttackDamage == 4 && a.AttackDamage == 6 && a.Health == 8 && b.AttackDamage == 6 && b.Health == 8,"Domain buffs two distinct OTHER minions +2/+3");
        a.Silence();
        Core2026Proof.Check(a.AttackDamage == 4 && a.Health == 5,"Silence removes named Domain buff");
        End(game); End(game);
        Core2026Proof.Check(b.AttackDamage == 6 && b.Health == 8,"Domain does not expire at turn end");
        new ReturnHandTask(EntityType.TARGET).Process(game,p,b,b);
        Core2026Proof.Check(b.AttackDamage == 4 && b.Health == 5,"leave-play removes Domain enchantment");
        while(p.BoardZone.Count < 6) Summon(p,"CS2_182");
        Open(game,source); Pick(game,"BE_013t4");
        var fanatic = p.BoardZone.Single(m => m.Card.Id == "BE_013t5");
        Core2026Proof.Check(p.BoardZone.Count == 7 && fanatic.AttackDamage == 2 && fanatic.Health == 3 && fanatic.HasTaunt,"one free slot admits exactly one printed Fanatic");
        Open(game,source); Pick(game,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Count == 7,"full board loses both excess Fanatics");
    }

    private static readonly SuspendableTask Attacks = StateTaskList.Suspendable("proof/cthun/attacks/v1",
        new AttackTask(EntityType.HERO,EntityType.OP_MINIONS),new AttackTask(EntityType.HERO,EntityType.OP_MINIONS));

    private static void Unpinned_PerAttackDecisions()
    {
        foreach(bool clear in new[] { false,true })
        {
            var game = Fixture(); var p = game.Player1; Play(game,p,"BE_771");
            var target = Summon(p.Opponent,"CS2_200"); target[GameTag.ATK] = 0; target[GameTag.HEALTH] = clear ? 5 : 20;
            var source = Summon(p,"CS2_182");
            Attacks.Process(game,p,source,null,new TaskStack());
            Core2026Proof.Check(p.Hero.NumAttacksThisTurn == 0 && p.Hero.Weapon.Durability == 1 && p.Choice != null,"first forced attack pauses before second attack without spending normal attack");
            Pick(game,clear ? "BE_013t3" : "BE_013t4");
            if(clear)
            {
                PickTarget(game,target.Id);
                Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.Hero.NumAttacksThisTurn == 0 && p.Choice == null,"ability clearing final target stops remaining forced attack");
            }
            else
            {
                Core2026Proof.Check(p.Hero.NumAttacksThisTurn == 0 && p.Hero.Weapon == null && p.Choice != null,"second forced attack consumes last durability and opens its own choice without spending normal attack");
                Pick(game,"BE_013t4");
                Core2026Proof.Check(p.Choice == null && p.BoardZone.Count(m => m.Card.Id == "BE_013t5") == 4,"both per-attack decisions resolve once");
            }
        }
    }
    private static void Unpinned_CancelTargetPrompt()
    {
        var game = Fixture(); var p = game.Player1; var source = Summon(p,"CS2_182");
        var victim = Summon(p.Opponent,"CS2_200");
        Open(game,source); Pick(game,"BE_013t3");
        PoolPilotProof.Resume(game,g => {
            Core2026Proof.Check(g.Process(ConcedeTask.Any(g.Player1)),"concede during Glare target choice accepted");
            Core2026Proof.Check(g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect && g.IdEntityDic[victim.Id][GameTag.DAMAGE] == 0,
                "cancellation consumes no ability effect");
            Core2026Proof.Check(!g.IdEntityDic.Values.Any(e => e.Card.Id == "BE_013t3" && e[GameTag.ZONE] == (int)Zone.PLAY),
                "cancellation retires the selected ability actor");
        });
    }

}
