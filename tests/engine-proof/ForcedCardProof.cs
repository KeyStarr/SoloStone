using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ForcedCardProof
{
    internal static void Clean(Game g)
    {
        StandardSetsProof.ParkBoard(g.Player1); StandardSetsProof.ParkBoard(g.Player2);
        if (g.CurrentPlayer != g.Player1) End(g);
        g.Player1.Hero.BaseHealth = 1000; g.Player2.Hero.BaseHealth = 1000;
    }
    internal static void ForceAll(Game g, Controller p, string id, bool endTurn)
    {
        var first = Summon(p.Opponent, "CS2_182"); first.AttackDamage = 0; first.BaseHealth = 40;
        var second = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("JAIL_942"));
        Generic.SummonBlock(g, second, 0, null); g.DeathProcessingAndAuraUpdate(); second.IsFrozen = true; second.NumAttacksThisTurn = 1;
        second.BaseHealth = 40;
        // Newer summon occupies the leftmost slot, so board order disagrees with age.
        var dead = Summon(p.Opponent, "EX1_556"); // Harvest Golem replacement must not join snapshot.
        var dormant = Summon(p.Opponent, "TIME_046");
        int cursor = g.HistoryFacts.Count;
        if (endTurn)
        {
            var source = Summon(p, id); p.Hero.Damage = 20;
            PoolPilotProof.Resume(g, next => End(next));
            Core2026Proof.Check(p.Hero.Damage == 8, "Terror Lifesteal heals from three retaliations");
        }
        else PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        var attacks = g.HistoryFacts.Query.AttacksStarted().Where(f => f.Sequence > cursor).ToArray();
        Core2026Proof.Check(attacks.Select(a => a.AttackerId).SequenceEqual(new[] { first.Id, second.Id, dead.Id }),
            "Unpinned_ForceAllOrder: initial oldest-first identities, not slots; zero Attack/Cant attack/Frozen included");
        Core2026Proof.Check(first.Damage == 4 && second.Damage == 4 && first.NumAttacksThisTurn == 0 && second.NumAttacksThisTurn == (endTurn ? 0 : 1),
            "PinnedRule_ForcedBudget: damage and retaliation without spending ordinary attacks");
        Core2026Proof.Check(dormant.Damage == 0 && p.Opponent.BoardZone.Any(m => m.Card.Id == "skele21" && m.Damage == 0),
            "Unpinned_ForceAllOrder: Dormant skipped and fresh Deathrattle summon never added");
        Clean(g);
        var poison = Summon(p.Opponent, "EX1_170"); poison.Poisonous = true;
        var later = Summon(p.Opponent, "CS2_182");
        cursor = g.HistoryFacts.Count;
        if (endTurn) { Summon(p, id); End(g); }
        else Play(g, p, id);
        attacks = g.HistoryFacts.Query.AttacksStarted().Where(f => f.Sequence > cursor).ToArray();
        Core2026Proof.Check(attacks.Length == 1 && attacks[0].AttackerId == poison.Id && later.Damage == 0,
            "Unpinned_ForceAllSourceLeaves: first poisonous attacker kills defender; remaining attacks stop");
        Clean(g);
        if (endTurn)
        {
            var source = Summon(p, id); source.Silence(); var enemy = Summon(p.Opponent, "CS2_182"); End(g);
            Core2026Proof.Check(enemy.Damage == 0 && source.Damage == 0, "Silence removes end-turn trigger");
            Clean(g);
        }
    }
}
