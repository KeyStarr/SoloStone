using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SI7SupplierProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_003", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasStealth && p.HandZone.Count == 0, "Stealth; no Battlecry draw");
        var other = Summon(p, "EX1_008"); Attack(g, other, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "other friendly attack does not draw");
        var clone = g.Clone(); Attack(clone, (Minion)clone.IdEntityDic[source.Id], clone.Player2.Hero);
        Core2026Proof.Check(clone.Player1.HandZone.Count == 1 && p.HandZone.Count == 0, "clone draw independent");
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 1 && !source.HasStealth, "self attack draws exactly one");
        End(g); var enemy = Summon(p.Opponent, "EX1_008"); Attack(g, enemy, p.Hero);
        Core2026Proof.Check(p.HandZone.Count == 1, "enemy attack does not draw"); End(g);
        int before = p.HandZone.Count; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == before + 1, "trigger persists next turn without Stealth");
        End(g); End(g); source.Silence(); before = p.HandZone.Count; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == before, "silence removes draw"); Kill(g, source);
        var lethal = (Minion)Play(g, p, id); var defender = Summon(p.Opponent, "CS2_182");
        Attack(g, lethal, defender); Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == before + 1, "lethal combat still gives after-attack draw");
        var full = (Minion)Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        int deck = p.DeckZone.Count; Attack(g, full, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand burns drawn card");
        Kill(g, full); End(g); End(g); before = p.HandZone.Count; Attack(g, other, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == before, "dead sources no longer draw");
    });
}
