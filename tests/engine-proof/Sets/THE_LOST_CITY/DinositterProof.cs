using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DinositterProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_822", (g, p, id) => {
        var sitter = (Minion)Play(g, p, id);
        Core2026Proof.Check(sitter.AttackDamage == 2 && sitter.Health == 3, "Dinositter printed stats");
        var beast = Add(p, "CS2_172"); var ordinary = Add(p, "CS2_182");
        var enemy = Add(p.Opponent, "CS2_172");
        End(g); Core2026Proof.Check(beast.Cost == 1 && beast[GameTag.COST] == 1 && ordinary.Cost == 4 && enemy.Cost == 2,
            "owner end discounts only own hand Beast with visible COST");
        var copied = Generic.Copy(p, sitter, beast, Zone.HAND);
        Core2026Proof.Check(copied.Cost == 1, "ordinary hand copy retains discount");
        End(g); Core2026Proof.Check(beast.Cost == 1 && copied.Cost == 1, "opponent end does not trigger");
        PlayEntity(g, p, beast);
        Generic.ReturnToHandBlock(p, (Minion)beast);
        Core2026Proof.Check(p.HandZone.Single(m => m.Id == beast.Id).Cost == 2, "played then bounced Beast resets hand reduction");
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[sitter.Id]).Silence();
        End(clone); Core2026Proof.Check(((IPlayable)clone.IdEntityDic[copied.Id]).Cost == 1, "silence removes own end trigger in clone");
        int total = beast.Cost + copied.Cost; End(g);
        Core2026Proof.Check(beast.Cost + copied.Cost == total - 1 && ordinary.Cost == 4, "game RNG chooses one eligible Beast");
        End(g); Kill(g, sitter); total = beast.Cost + copied.Cost;
        End(g); Core2026Proof.Check(beast.Cost + copied.Cost == total, "leaving board removes trigger");
        End(g);
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        Play(g, p, id); End(g); End(g);
        Core2026Proof.Check(p.HandZone.All(m => !m.Card.IsRace(Race.BEAST)), "empty eligible set harmless");
    });
}
