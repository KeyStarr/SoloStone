using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TimelessCausalityProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_061", (g, p, id) => {
        var before = p.DeckZone.GetAll(); var enemy = p.Opponent.DeckZone.GetAll();
        Effects.SetCost(1).ApplyTo(before[0]);
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Select(e => e.Id).SequenceEqual(before.Reverse().Select(e => e.Id)), "reverses exact entity order");
        Core2026Proof.Check(p.DeckZone.Last().Cost == 1 && p.Opponent.DeckZone.Select(e => e.Id).SequenceEqual(enemy.Select(e => e.Id)), "retains modifications; opponent deck unchanged");
        Core2026Proof.Check(p.DeckZone.Draw().Id == before[0].Id, "draw uses newly reversed top");
        var clone = g.CloneExact(history: true); var original = p.DeckZone.Select(e => e.Id).ToArray(); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player1.DeckZone.Select(e => e.Id).SequenceEqual(original.Reverse()) && p.DeckZone.Select(e => e.Id).SequenceEqual(original), "clone reversal isolated");
        foreach (var e in p.DeckZone.GetAll()) { p.DeckZone.Remove(e); p.SetasideZone.Add(e); }
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == 0, "empty deck supported");
        var one = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(one); Play(g, p, id); Core2026Proof.Check(p.DeckZone.TopCard == one, "single-card deck unchanged");
    });
}
