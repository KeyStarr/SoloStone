using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15HealthCorpseReplacementProof
{
    [GroupProof("b15-health-corpse-replacement")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { Unpinned_FalricReplacement, Unpinned_OnyxiaHealthLoss, Unpinned_LifeTapAndCombat, HistoryDisabled })
            try { test(); } catch (Exception e) { failures.Add(test.Method.Name + ": " + e.ToString()); }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
        Console.WriteLine("PASS b15 Health/Corpse replacement facility");
    }
    private static void Unpinned_FalricReplacement()
    {
        var g = DeathKnightC1Proof.NewGame(1); var p = g.Player1;
        var falric = Summon(p, "CORE_EDR_003");
        CorpseRules.Gain(p, 3);
        Core2026Proof.Check(CorpseRules.Available(p) == 6, "Falric doubles explicit gains");
        var other = Summon(p, "CORE_EDR_003");
        CorpseRules.Gain(p, 1); Core2026Proof.Check(CorpseRules.Available(p) == 8, "Falric doubling does not stack");
        falric.Silence(); CorpseRules.Gain(p, 1); Core2026Proof.Check(CorpseRules.Available(p) == 10, "surviving second source remains");
        other.Silence(); CorpseRules.Gain(p, 1); Core2026Proof.Check(CorpseRules.Available(p) == 11, "Silence removes replacement immediately");
        CorpseRules.Gain(p.Opponent, 1); Core2026Proof.Check(CorpseRules.Available(p.Opponent) == 1, "opponent never doubled");
        foreach (bool oldest in new[] { false, true })
        foreach (bool survivor in new[] { false, true })
        {
            g = DeathKnightC1Proof.NewGame(1); p = g.Player1;
            var first = Summon(p, oldest ? "CORE_EDR_003" : "CS2_182");
            var second = Summon(p, "CS2_182");
            var last = Summon(p, oldest ? "CS2_182" : "CORE_EDR_003");
            if (survivor) Summon(p, "CORE_EDR_003");
            first.Destroy(); second.Destroy(); last.Destroy();
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(CorpseRules.Available(p) == (survivor ? 6 : 3), "Unpinned_FalricDeathBatch independent of play order");
        }
        g = DeathKnightC1Proof.NewGame(1); p = g.Player1;
        falric = Summon(p, "CORE_EDR_003");
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; CorpseRules.Gain(owner, 2);
            Core2026Proof.Check(CorpseRules.Available(owner) == 4, "clone/snapshot retain live Corpse replacement");
            var body = (Minion)next.IdEntityDic[falric.Id]; owner.SetasideZone.Add(owner.BoardZone.Remove(body));
            CorpseRules.Gain(owner, 1); Core2026Proof.Check(CorpseRules.Available(owner) == 5, "departure removes replacement");
        });
    }
    private static void Unpinned_OnyxiaHealthLoss()
    {
        var g = DeathKnightC1Proof.NewGame(1); var p = g.Player1;
        var body = Summon(p, "CATA_155");
        p.Hero.Damage = 20; p.Hero.Armor = 3;
        p.Hero.TakeDamage(p.Opponent.Hero, 5);
        Core2026Proof.Check(p.Hero.BaseHealth == 32 && p.Hero.Health == 12 && p.Hero.Armor == 0, "Onyxia replaces only Health portion after Armor");
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; int health = owner.Hero.Health, max = owner.Hero.BaseHealth;
            owner.Hero.TakeDamage(owner.Hero, 2);
            Core2026Proof.Check(owner.Hero.Health == health + 2 && owner.Hero.BaseHealth == max + 2, "fatigue replacement survives clone/snapshot");
            ((Minion)next.IdEntityDic[body.Id]).Silence();
            owner.Hero.TakeDamage(owner.Opponent.Hero, 1);
            Core2026Proof.Check(owner.Hero.Health == health + 1 && owner.Hero.BaseHealth == max + 2, "Silence removes Onyxia replacement");
        });
        g = DeathKnightC1Proof.NewGame(1); p = g.Player1; Summon(p, "CATA_155");
        p.Hero.IsImmune = true; p.Hero.TakeDamage(p.Opponent.Hero, 3);
        Core2026Proof.Check(p.Hero.BaseHealth == 30 && p.Hero.Health == 30, "prevention precedes replacement");
        p.Hero.IsImmune = false; End(g); p.Hero.TakeDamage(p.Opponent.Hero, 2);
        Core2026Proof.Check(p.Hero.BaseHealth == 30 && p.Hero.Health == 28, "opponent-turn damage unchanged");
    }
    private static void Unpinned_LifeTapAndCombat()
    {
        var g = HeraldProof.New(CardClass.WARLOCK); var p = g.Player1;
        Summon(p, "CATA_155"); PoolPilotProof.EmptyHand(p);
        int deck = p.DeckZone.Count;
        Core2026Proof.Check(Generic.HeroPower(p) && p.Hero.Health == 32 && p.Hero.BaseHealth == 32
            && p.DeckZone.Count == deck - 1, "Life Tap replaces Health loss and still draws");
        g = DeathKnightC1Proof.NewGame(1); p = g.Player1; Summon(p, "CATA_155");
        var defender = Summon(p.Opponent, "CS2_182"); p.Hero.AttackDamage = 3; p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, defender)) && p.Hero.Health == 34
            && p.Hero.BaseHealth == 34 && defender.Damage == 3, "own-turn combat retaliation replaces Health loss");
        p.Hero.Armor = 3; p.Hero.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(p.Hero.Armor == 1 && p.Hero.BaseHealth == 34, "Armor-only hit never increases Health");
    }

    private static void HistoryDisabled()
    {
        var g = new Game(new GameConfig { EnableCorpses = true, History = false, RandomSeed = 1,
            StartPlayer = 1, Shuffle = false, SkipMulligan = true, Player1HeroClass = CardClass.DEATHKNIGHT,
            Player2HeroClass = CardClass.MAGE, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); var p = g.Player1; PoolPilotProof.EmptyHand(p); p.BaseMana = 10;
        Summon(p, "CORE_EDR_003"); Summon(p, "CATA_155");
        CorpseRules.Gain(p, 2); p.Hero.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(CorpseRules.Available(p) == 4 && p.Hero.Health == 32, "replacements independent of presentation history");
        PoolPilotProof.EmptyHand(p);
        var agam = Add(p, "EDR_489"); Core2026Proof.Check(g.Process(PlayCardTask.Any(p, agam)), "history-off Agamaggan");
        var next = Add(p, "CS2_182");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, next)) && p.Opponent.Hero.Health == 26
            && p.AppliedEnchantments.All(e => e.Card.Id != "EDR_489e1"), "history-off grant persists and consumes");
    }

}
