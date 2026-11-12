using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FelrattlerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WC_701", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182"); var fragile = Summon(p.Opponent, "CS2_231"); var friendly = Summon(p, "CS2_182");
        Core2026Proof.Check(source.IsRush && source.IsRace(Race.BEAST), "printed Rush Beast");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == p.Opponent.Hero.Id), "printed Rush enables minion attack only on play turn");
        Kill(g, source);
        Core2026Proof.Check(enemy.Damage == 1 && fragile.Zone.Type == Zone.GRAVEYARD && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 0, "Deathrattle hits enemy minions only");
        source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(enemy.Damage == 1, "Silence removes Deathrattle");
        ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && friendly.Damage == 0, "empty enemy board does nothing");
    });
}
