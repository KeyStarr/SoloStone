using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ActivatedGolemProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_883", (g, p, id) => {

        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source[GameTag.REBORN] == 0, "no Reborn before first end turn");
        var clone = g.Clone(); End(clone); Core2026Proof.Check(clone.IdEntityDic[source.Id][GameTag.REBORN] == 1 && source[GameTag.REBORN] == 0, "end turn grants Reborn in clone only");
        End(g); Core2026Proof.Check(source[GameTag.REBORN] == 1, "own end turn grants Reborn");
        Kill(g, source); source = p.BoardZone.Single();
        Core2026Proof.Check(source.Card.Id == id && source.AttackDamage == 4 && source.Health == 1 && source[GameTag.REBORN] == 0 && source[GameTag.HAS_BEEN_REBORN] == 1, "death returns exact one-health Golem without Reborn");
        End(g); Core2026Proof.Check(source[GameTag.REBORN] == 1 && source[GameTag.HAS_BEEN_REBORN] == 0, "opponent end turn re-arms consumed Reborn");
        Kill(g, source); source = p.BoardZone.Single(); Core2026Proof.Check(source.Health == 1 && source.Card.Id == id, "re-armed Reborn works again");
        source.Silence(); End(g); End(g); Core2026Proof.Check(source[GameTag.REBORN] == 0, "silence removes future end-turn grants");
        Kill(g, source); End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "silenced death leaves no lingering trigger"); End(g);
        source = (Minion)Play(g, p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); End(g);
        Kill(g, source); source = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && source.Health == 1 && source.ZonePosition == 0, "Reborn returns to source slot on full board without overflow");
        ClearBoard(g, p); End(g);
        source = (Minion)Play(g, p, id); Kill(g, source); End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "death before end turn removes trigger");
    });
}
