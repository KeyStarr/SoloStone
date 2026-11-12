using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SetasideDeathProof
{
    [GroupProof("setaside-death")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            BeetleStart(history, doomFirst: true);
            BeetleStart(history, doomFirst: false);
            PendingRemoval(history);
            EscapeAndDormant(history);
        }
        Console.WriteLine("PASS SETASIDE deaths: Beetle trigger order, clone/snapshot, pending transform/removal, late hits, dormant, real deaths");
    }

    private static Game Fixture(bool history)
    {
        var g = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false, EnableCorpses = true,
            FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        return g;
    }

    private static void BeetleStart(bool history, bool doomFirst)
    {
        var g = Fixture(history); var p = g.Player1;
        if (doomFirst) Summon(p, "NEW1_021");
        var beetles = new[] { Summon(p, "EDR_818t"), Summon(p, "EDR_818t") };
        // A consumed body must not create a Reborn or Deathrattle either.
        beetles[1][GameTag.REBORN] = 1;
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_158e"), p.Hero, beetles[1]);
        if (!doomFirst) Summon(p, "NEW1_021");
        foreach (var branch in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            End(branch); End(branch);
            var owner = branch.Player1;
            var deaths = branch.HistoryFacts.Query.Deaths().ToArray();
            Core2026Proof.Check(beetles.All(b => branch.IdEntityDic[b.Id].Zone == owner.SetasideZone
                && deaths.All(d => d.EntityId != b.Id)), "merged Beetles leave play without dying");
            Core2026Proof.Check(deaths.Length == (doomFirst ? 1 : 2)
                && owner.NumFriendlyMinionsThatDiedThisTurn == deaths.Length
                && CorpseRules.Available(owner) == deaths.Length,
                $"only real deaths give history, counters and corpses: doomFirst={doomFirst} history={history} deaths={deaths.Length} counter={owner.NumFriendlyMinionsThatDiedThisTurn} corpses={CorpseRules.Available(owner)} board={string.Join(',', owner.BoardZone.Select(m => m.Card.Id))}");
            Core2026Proof.Check(doomFirst
                ? owner.BoardZone.Count == 1 && owner.BoardZone[0].Card.Id == "EDR_818"
                    && owner.BoardZone[0].AttackDamage == 2 && owner.BoardZone[0].Health == 2
                : owner.BoardZone.Count == 2 && owner.BoardZone.All(m => m.Card.Id == "EDR_818t"),
                "trigger order preserves new Nythendra or its actual Deathrattle, never consumed Reborn/Soul of the Forest");
            Core2026Proof.Check(branch.DeadMinions.Count == 0, "death queue drains");
            Game.LoadSnapshot(branch.SaveSnapshot());
            End(branch); // The next native EndTurn path remains usable.
        }
    }

    private static void PendingRemoval(bool history)
    {
        foreach (bool transform in new[] { false, true })
        foreach (bool damage in new[] { false, true })
        {
            var g = Fixture(history); var p = g.Player1;
            var victim = Summon(p, "EX1_556"); // Harvest Golem would summon a Damaged Golem on a real death.
            victim[DeathContextRules.CaptureDeath] = 1;
            var realDeath = Summon(p, "CS2_231");
            if (damage) victim.TakeDamage(p.Hero, victim.Health);
            else victim.Destroy();
            realDeath.Destroy();
            Core2026Proof.Check(g.DeadMinions.Contains(victim), "victim was pending before removal");
            if (transform) Generic.TransformBlock(p, Cards.FromId("CS2_tk1"), victim);
            else
            {
                var recycle = Entity.FromCard(p.Opponent, Cards.FromId("GVG_031"));
                recycle.Power.PowerTask.Process(g, p.Opponent, recycle, victim);
            }
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(victim.Zone == p.SetasideZone && g.DeadMinions.Count == 0
                && victim[DeathContextRules.Captured] == 0
                && g.HistoryFacts.Query.Deaths().Single().EntityId == realDeath.Id
                && CorpseRules.Available(p) == 1 && p.NumFriendlyMinionsThatDiedThisTurn == 1,
                "off-board pending victim excluded before death side effects; on-board victim still dies");
            Core2026Proof.Check(p.BoardZone.Count == (transform ? 1 : 0)
                && (!transform || p.BoardZone.Single().Card.Id == "CS2_tk1"), "no removed Harvest Golem Deathrattle");
        }
    }

    private static void EscapeAndDormant(bool history)
    {
        var g = Fixture(history); var p = g.Player1;
        var artist = Summon(p, "JAIL_030");
        Attack(g, artist, p.Opponent.Hero);
        Core2026Proof.Check(artist.Zone == p.SetasideZone, "real Escape Artist attack removes it");
        Core2026Proof.Check(artist.TakeDamage(p.Hero, 100) == 0, "late damage ignores escaped minion");
        artist.Destroy();
        var dormant = Summon(p, "TIME_046");
        Core2026Proof.Check(dormant.IsDormant && dormant.Zone == p.BoardZone, "dormant stays on board, not SETASIDE");
        dormant.Destroy();
        Core2026Proof.Check(dormant.TakeDamage(p.Hero, 100) == 0, "dormant ignores damage");
        var stealth = Summon(p, "EX1_010");
        stealth.Destroy();
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(artist.Zone == p.SetasideZone && dormant.Zone == p.BoardZone
            && g.HistoryFacts.Query.Deaths().Single().EntityId == stealth.Id && CorpseRules.Available(p) == 1,
            "escaped/dormant bodies never die; Stealth does not prevent destruction");
    }
}
