using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WarmaulChallengerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_120", Unpinned_WarmaulChallenger);
    private static void Unpinned_WarmaulChallenger(Game g, Controller p, string id)
    {
        var target = Summon(p.Opponent, "CS2_182"); target.AttackDamage = 2; target.BaseHealth = 3;
        var source = (Minion)Play(g, p, id, target);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && source.Damage == 6 && source.NumAttacksThisTurn == 0,
            "Unpinned_WarmaulCombatOrder: Challenger initiates three combats, retaliation each time");
        ForcedCardProof.Clean(g);
        target = Summon(p.Opponent, "CS2_182"); target.AttackDamage = 0;
        int cursor = g.HistoryFacts.Count;
        source = (Minion)Add(p, id); source.AttackDamage = 0; PlayEntity(g, p, source, target);
        var attacks = g.HistoryFacts.Query.AttacksStarted().Where(a => a.Sequence > cursor).ToArray();
        Core2026Proof.Check(attacks.Length == 30 && attacks.All(a => a.AttackerId == source.Id && a.TargetId == target.Id)
            && source.Damage == 0 && target.Damage == 0 && source.NumAttacksThisTurn == 0,
            "Unpinned_WarmaulBattleCap: exactly 30 same-initiator no-progress attacks per invocation");
        ForcedCardProof.Clean(g); Summon(p, "LOE_077");
        target = Summon(p.Opponent, "CS2_182"); target.AttackDamage = 0;
        source = (Minion)Add(p, id); source.AttackDamage = 0; cursor = g.HistoryFacts.Count;
        PlayEntity(g, p, source, target);
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count(a => a.Sequence > cursor) == 60,
            "Unpinned_WarmaulBattleCap: doubled Battlecry owns two independent 30-combat budgets");
        ForcedCardProof.Clean(g);
        target = Summon(p.Opponent, "CS2_182"); target.AttackDamage = 20; target.BaseHealth = 30;
        source = (Minion)Play(g, p, id, target);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && target.Damage == 1, "Unpinned_WarmaulCombatOrder: source death stops sequence");
    }
}
