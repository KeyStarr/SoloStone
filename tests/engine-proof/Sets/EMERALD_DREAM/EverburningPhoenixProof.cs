using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EverburningPhoenixProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_919", (g, p, id) => {
        var held = Add(p, id); EmeraldDreamBatch3Proof.Cost(held, 4);
        Core2026Proof.Check(held.Card.IsRace(Race.ELEMENTAL) && held.Card.IsRace(Race.BEAST), "both printed minion types");
        Play(g, p, "GAME_005"); EmeraldDreamBatch3Proof.Cost(held, 3);
        Play(g, p, "CS2_182"); EmeraldDreamBatch3Proof.Cost(held, 2);
        var clone = g.Clone(); Play(clone, clone.Player1, "GAME_005");
        EmeraldDreamBatch3Proof.Cost(clone.IdEntityDic[held.Id], 1); EmeraldDreamBatch3Proof.Cost(held, 2);
        for (int i = 0; i < 4; i++) Play(g, p, "GAME_005");
        EmeraldDreamBatch3Proof.Cost(held, 0);
        End(g); EmeraldDreamBatch3Proof.Cost(held, 4);
        Play(g, p.Opponent, "GAME_005"); EmeraldDreamBatch3Proof.Cost(held, 4); End(g);
        PlayEntity(g, p, held);
        var source = (Minion)held;
        Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_889e"), source, source);
        Kill(g, source); Core2026Proof.Check(p.HandZone.All(x => x.Card.Id != id), "Deathrattle waits for end of turn");
        clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.HandZone.Count(x => x.Card.Id == id) == 1 && p.HandZone.All(x => x.Card.Id != id), "pending return clone isolated");
        var restored = Game.LoadSnapshot(g.SaveSnapshot()); End(restored);
        Core2026Proof.Check(restored.Player1.HandZone.Count(x => x.Card.Id == id) == 1, "pending return survives snapshot");
        End(g); var next = (Minion)p.HandZone.Single(x => x.Card.Id == id);
        Core2026Proof.Check(next.Id != source.Id && next.AttackDamage == 3 && next.Health == 2 && next.IsRace(Race.ELEMENTAL) && next.IsRace(Race.BEAST), "new exact Phoenix, not buffed copy");
        End(g); PlayEntity(g, p, next); next.Silence(); Kill(g, next); End(g); End(g);
        Core2026Proof.Check(p.HandZone.All(x => x.Card.Id != id), "Silence removes Deathrattle; prior timer never repeats");
        source = Summon(p, id); End(g); Kill(g, source); End(g);
        Core2026Proof.Check(p.HandZone.Count(x => x.Card.Id == id) == 1 && p.Opponent.HandZone.All(x => x.Card.Id != id), "opponent-turn death returns at that turn end to correct owner");
        next = (Minion)p.HandZone.Single(x => x.Card.Id == id); PlayEntity(g, p, next); Kill(g, next);
        while (!p.HandZone.IsFull) Add(p, "CS2_182"); End(g); End(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(x => x.Card.Id != id), "full hand loses return");
        var removed = p.HandZone.First(); p.HandZone.Remove(removed); p.SetasideZone.Add(removed); End(g); End(g);
        Core2026Proof.Check(p.HandZone.All(x => x.Card.Id != id), "full-hand one-shot consumed");
    });
}
