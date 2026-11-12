using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HoundsOfFuryProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_443", Unpinned_HoundsOfFury);
    private static void Unpinned_HoundsOfFury(Game g, Controller p, string id)
    {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.AttackDamage == 3 && m.Health == 2 && m.IsRace(Race.DEMON))
            && !g.HistoryFacts.Query.AttacksStarted().Any(), "pinned 3/2 Demons, minion deck prevents attacks");
        ForcedCardProof.Clean(g); PoolPilotProof.ClassDeck(p);
        var target = Summon(p.Opponent, "EX1_556"); target.Damage = 2;
        var spell = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id]));
        var attacks = g.HistoryFacts.Query.AttacksStarted().ToArray();
        Core2026Proof.Check(attacks.Length == 2 && attacks[0].TargetId == target.Id && attacks[1].TargetId != target.Id
            && p.Opponent.BoardZone.IsEmpty, "Unpinned_HoundsAttackReselection: second attack sees first target's Deathrattle replacement");
        Core2026Proof.Check(p.BoardZone.All(m => m.NumAttacksThisTurn == 0), "forced Hounds retain normal allowance");
        ForcedCardProof.Clean(g); p.Opponent.Hero.Damage = 999; p.Opponent.Hero.Armor = 20;
        var healthy = Summon(p.Opponent, "CS2_182"); healthy.BaseHealth = 20;
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 14 && healthy.Damage == 0,
            "Pinned_LowestHealthHeroPool: hero eligible, compare Health ignoring Armor");
        p.Opponent.Hero.Damage = 0;
        ForcedCardProof.Clean(g);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        target = Summon(p.Opponent, "CS2_200"); target.AttackDamage = 20; target.BaseHealth = 1;
        int cursor = g.HistoryFacts.Count; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 6 && g.HistoryFacts.Query.AttacksStarted().Count(a => a.Sequence > cursor) == 1,
            "Unpinned_HoundsAttackReselection: only one initial slot; dead first Demon does not permit a second summon");
        bool heroPicked = false, minionPicked = false;
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
        {
            ForcedCardProof.Clean(g); p.Opponent.Hero.Damage = 986; p.Opponent.Hero.Armor = 20;
            var a = Summon(p.Opponent, "CS2_182"); a.BaseHealth = 14;
            var b = Summon(p.Opponent, "CS2_182"); b.BaseHealth = 14;
            g.SetRandomSeed(seed); var oracle = g.CloneExact(history: true);
            var candidates = new[] { a.Id, b.Id, p.Opponent.Hero.Id };
            int expected = candidates[oracle.Random.Next(3)];
            int expectedSecond = expected == p.Opponent.Hero.Id ? candidates[oracle.Random.Next(3)] : expected;
            heroPicked |= expected == p.Opponent.Hero.Id; minionPicked |= expected != p.Opponent.Hero.Id;
            cursor = g.HistoryFacts.Count; Play(g, p, id);
            var tied = g.HistoryFacts.Query.AttacksStarted().Where(f => f.Sequence > cursor).ToArray();
            Core2026Proof.Check(tied.Length == 2 && tied[0].TargetId == expected && tied[1].TargetId == expectedSecond,
                "Unpinned_LowestHealthTies: uniform seeded three-way choice, no hero/age preference; fresh reselection");
        }
        Core2026Proof.Check(heroPicked && minionPicked, "ties exercise both hero and minion choices");
        p.Opponent.Hero.Damage = 0;
    }
}
