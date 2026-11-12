using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EscapeArtistProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_030", (g, p, id) => {
        var source = (Minion)Play(g, p, id); int before = p.HandZone.Count;
        var clone = g.Clone(); Attack(clone, (Minion)clone.IdEntityDic[source.Id], clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[source.Id].Zone.Type == Zone.SETASIDE && clone.Player1.HandZone.Count == before + 1 && source.Zone.Type == Zone.PLAY && p.HandZone.Count == before, "surviving attack draws and escapes, clone isolated");
        source.Silence(); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(source.Zone.Type == Zone.PLAY && p.HandZone.Count == before, "Silence removes escape trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182"); before = p.HandZone.Count;
        Attack(g, source, enemy); Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == before, "lethal attack neither draws nor escapes");
        source = Summon(p, id); End(g); before = p.HandZone.Count;
        var attacker = Summon(p.Opponent, "CS2_231"); Attack(g, attacker, source);
        Core2026Proof.Check(source.Zone.Type == Zone.PLAY && p.HandZone.Count == before, "defending does not trigger");
        End(g); Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(source.Zone.Type == Zone.SETASIDE && p.HandZone.Count == before + 2, "attack on later turn escapes without death");
        before = p.HandZone.Count; End(g); End(g); Core2026Proof.Check(p.HandZone.Count == before + 1, "escaped source remains inactive");
    });
}
