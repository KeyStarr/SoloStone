using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Combined quest/reward proof: missing board facts, turn deduplication, or weapon cleanup must fail here.
internal static class RestoreTheWildProof
{
    private static void Fill(Controller p)
    {
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
    }

    private static void Refill(Game g, Controller p)
    {
        Kill(g, p.BoardZone.First());
        Fill(p);
    }

    private static void NextOwnerTurn(Game g) { End(g); End(g); }
    private static int Rewards(Controller p) => p.HandZone.Count(c => c.Card.Id == "TLC_239t")
        + p.GraveyardZone.Count(c => c.Card.Id == "TLC_239t");

    internal static void Run() => StandardSetsProof.Case("TLC_239", (g, p, id) => {
        var quest = (Spell)Play(g, p, id);
        Core2026Proof.Check(quest.QuestProgress == 0 && quest.QuestTotalProgress == 3, "Restore the Wild starts at zero of three");
        for (int i = 0; i < 6; i++) Summon(p, "CS2_231");
        Core2026Proof.Check(quest.QuestProgress == 0, "six minions is below threshold");
        Fill(p.Opponent);
        Core2026Proof.Check(quest.QuestProgress == 0, "opponent board ignored during owner turn");
        Play(g, p, "CS2_231");
        Core2026Proof.Check(quest.QuestProgress == 1 && Rewards(p) == 0, "played seventh minion counts once without early reward");
        Refill(g, p); Refill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 1, "multiple fills in one owner turn count once");
        End(g);
        Refill(g, p); Refill(g, p.Opponent);
        Core2026Proof.Check(quest.QuestProgress == 1, "both boards ignored on opponent turn");
        End(g);
        Core2026Proof.Check(quest.QuestProgress == 1, "already full at next turn start gives no credit");
        Refill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 2 && Rewards(p) == 0, "second distinct turn below completion threshold");
        NextOwnerTurn(g); Refill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 3 && quest.Zone == p.GraveyardZone && p.SecretZone.ModernQuests.Count == 0
            && p.HandZone.Count(c => c.Card.Id == "TLC_239t") == 1, "third distinct turn consumes quest and grants Everbloom");
        Refill(g, p); NextOwnerTurn(g); Refill(g, p);
        Core2026Proof.Check(Rewards(p) == 1 && !QuestRules.Advance(quest), "completed quest cannot grant again");
        Unpinned_AlreadyFullBoard();
        Unpinned_TransientFull();
        PinnedRule_RewardWaitsForHandSpace();
        foreach (bool history in new[] { false, true }) QuestResume(history);
        TheEverbloom();
    });

    private static void Unpinned_AlreadyFullBoard()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        Fill(p);
        var quest = (Spell)Play(g, p, "TLC_239");
        Core2026Proof.Check(quest.QuestProgress == 0, "Unpinned_AlreadyFullBoard: activation is not a board change");
        Fill(p.Opponent);
        Core2026Proof.Check(quest.QuestProgress == 0, "opponent changes cannot credit already-full friendly board");
        Play(g, p, "CS2_009", p.BoardZone.First());
        Core2026Proof.Check(quest.QuestProgress == 0, "stat buffs do not change board membership");
        NextOwnerTurn(g);
        Core2026Proof.Check(quest.QuestProgress == 0, "Unpinned_AlreadyFullBoard: turn start does not count");
        Play(g, p, "CS2_022", p.BoardZone.First());
        Core2026Proof.Check(p.BoardZone.Count == 7 && quest.QuestProgress == 1,
            "Unpinned_AlreadyFullBoard: later committed transform on full board counts");
        NextOwnerTurn(g); Refill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 2, "later membership refill counts on another turn");
    }

    private static void Unpinned_TransientFull()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var quest = (Spell)Play(g, p, "TLC_239");
        for (int i = 0; i < 6; i++) Summon(p, "CS2_231");
        var terror = Add(p, "EX1_304"); p.UsedMana = 0;
        // Two neighbours are required by the legacy Void Terror task recipe.
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, terror, zonePosition: 3)), "Void Terror play accepted");
        Core2026Proof.Check(p.BoardZone.Count == 5 && quest.QuestProgress == 1,
            "Unpinned_TransientFull: full before battlecry destruction still counts");
        NextOwnerTurn(g); Fill(p);
        Core2026Proof.Check(quest.QuestProgress == 2, "transient fill consumes only its owner turn");
    }

    private static void PinnedRule_RewardWaitsForHandSpace()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var quest = (Spell)Play(g, p, "TLC_239");
        Fill(p); NextOwnerTurn(g); Refill(g, p); NextOwnerTurn(g);
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        Refill(g, p);
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && p.SecretZone.ModernQuests.Count == 0 && p.HandZone.Count == 10
            && !p.HandZone.Any(c => c.Card.Id == "TLC_239t")
            && !g.IdEntityDic.Values.Any(c => c.Card.Id == "TLC_239t"), "full hand waits without creating or burning reward; quest consumed");
        End(g);
        Generic.DiscardBlock(p, p.HandZone[0]);
        Core2026Proof.Check(g.CurrentPlayer == p.Opponent && p.HandZone.Count(c => c.Card.Id == "TLC_239t") == 1
            && !p.GraveyardZone.Any(c => c.Card.Id == "TLC_239t"), "reward delivered on opponent turn without burning");
        End(g); Refill(g, p);
        Core2026Proof.Check(Rewards(p) == 1, "completed quest is not retried");
    }

    private static void QuestResume(bool history)
    {
        var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
        var quest = (Spell)Play(g, p, "TLC_239"); Fill(p);
        var clone = g.CloneExact(history: history);
        var simulation = g.CloneForSimulation(42, history: history);
        byte[] bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
        Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot()), "quest snapshot byte round trip");
        foreach (var copy in new[] { clone, simulation, restored })
        {
            var q = copy.Player1.SecretZone.ModernQuests.Single();
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(copy) && q.QuestProgress == 1
                && q.QuestState.LastCountedTurn == quest.QuestState.LastCountedTurn
                && !ReferenceEquals(q.QuestState, quest.QuestState), "mid-progress quest clone/snapshot equal and independent");
            Refill(copy, copy.Player1);
            Core2026Proof.Check(q.QuestProgress == 1, "resume retains same-turn latch");
            NextOwnerTurn(copy); Refill(copy, copy.Player1);
            Core2026Proof.Check(q.QuestProgress == 2 && quest.QuestProgress == 1, "copy progress cannot mutate source");
        }
        Refill(g, p); NextOwnerTurn(g); Refill(g, p);
        foreach (var game in new[] { g, clone, simulation, restored })
        {
            NextOwnerTurn(game); Refill(game, game.Player1);
            var q = (Spell)game.IdEntityDic[quest.Id];
            Core2026Proof.Check(q.QuestProgress == 3 && q.Zone == game.Player1.GraveyardZone && Rewards(game.Player1) == 1,
                "resumed third turn grants exactly once");
            NextOwnerTurn(game); Refill(game, game.Player1);
            Core2026Proof.Check(Rewards(game.Player1) == 1, "resumed completion remains consumed");
        }
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
            && Canonical.State(g) == Canonical.State(simulation),
            "resumed quest futures equal");
        if (history) Core2026Proof.Check(Canonical.History(g) == Canonical.History(restored), "quest snapshot future history equal");
    }

    private static void HeroAttack(Game g)
    {
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero)), "Everbloom hero attack accepted");
    }

    private static void Stats(Minion m, int attack, int health, int buffs, string reason)
    {
        Core2026Proof.Check(m.AttackDamage == attack && m.Health == health
            && (m.AppliedEnchantments?.Count(e => e.Card.Id == "TLC_239e") ?? 0) == buffs, reason);
    }

    private static void TheEverbloom()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "TLC_239t");
        Core2026Proof.Check(p.Hero.Weapon.Cost == 3 && p.Hero.Weapon.AttackDamage == 2 && p.Hero.Weapon.Durability == 5,
            "Everbloom printed 3 mana 2/5");
        Stats(friendly, 4, 5, 0, "equipping weapon does not buff");
        Attack(g, friendly, p.Opponent.Hero);
        Stats(friendly, 4, 5, 0, "friendly minion attack does not buff");
        HeroAttack(g);
        Stats(friendly, 6, 7, 1, "hero attack buffs friendly minion with TLC_239e");
        Stats(enemy, 4, 5, 0, "enemy minion excluded");
        End(g); Attack(g, enemy, p.Hero); End(g);
        Stats(friendly, 6, 7, 1, "opponent attack does not buff");
        var later = Summon(p, "CS2_231");
        Stats(later, 1, 1, 0, "later summon receives no passive buff");
        var clone = g.CloneExact(history: true);
        byte[] bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
        Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot()) && Canonical.State(g) == Canonical.State(clone)
            && Canonical.State(g) == Canonical.State(restored), "mid-durability weapon clone/snapshot equal");
        HeroAttack(clone);
        Stats((Minion)clone.IdEntityDic[friendly.Id], 8, 9, 2, "clone weapon trigger remains active");
        Stats(friendly, 6, 7, 1, "clone weapon trigger independent");
        HeroAttack(g); HeroAttack(restored);
        foreach (var game in new[] { g, clone, restored })
        {
            for (int i = 0; i < 3; i++) { NextOwnerTurn(game); HeroAttack(game); }
            Core2026Proof.Check(game.Player1.Hero.Weapon == null, "fifth attack breaks Everbloom");
            Stats((Minion)game.IdEntityDic[friendly.Id], 14, 15, 5, "last durability attack still buffs");
            Stats((Minion)game.IdEntityDic[later.Id], 9, 9, 4, "later minion receives later attacks only");
            Play(game, game.Player1, "CS2_106"); NextOwnerTurn(game); HeroAttack(game);
            Stats((Minion)game.IdEntityDic[friendly.Id], 14, 15, 5, "broken weapon trigger removed");
        }
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
            && Canonical.History(g) == Canonical.History(restored), "weapon resumed futures equal without duplicate buffs");
        Play(g, p, "TLC_239t"); Play(g, p, "CS2_106"); NextOwnerTurn(g); HeroAttack(g);
        Stats(friendly, 14, 15, 5, "replacement removes Everbloom trigger and preserves earned buffs");
    }
}
