using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MimicryProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_522", (g, p, id) => {
        var enemy = p.Opponent;
        foreach (var card in enemy.DeckZone.ToArray()) enemy.SetasideZone.Add(enemy.DeckZone.Remove(card));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_029")));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_182")));
        var drawn = enemy.DeckZone.ToArray();
        Play(g, p, id);
        Core2026Proof.Check(drawn.All(x => x.Zone == enemy.HandZone) && p.HandZone.Select(x => x.Card.Id).Order().SequenceEqual(new[] { "CS2_029", "CS2_182" }.Order()), "copies both actual draws");
        Core2026Proof.Check(p.HandZone.All(x => drawn.All(y => x.Id != y.Id)), "copies use new entities");
        foreach (var card in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(card));
        while (!enemy.HandZone.IsFull) Add(enemy, "CS2_231");
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_029")));
        int before = p.HandZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == before + 1 && p.HandZone.Count(x => x.Card.Id == "CS2_029") == 2, "burned opponent draw still copied; fatigue gives no copy");
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_182")));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_029")));
        var spell = p.HandZone.First(x => x.Card.Id == "CS2_231"); p.SetasideZone.Add(p.HandZone.Remove(spell));
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsFull && enemy.DeckZone.IsEmpty, "own full hand burns excess copy but both draws resolve");
        foreach (var owner in new[] { p, enemy })
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_029")));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("CS2_182")));
        enemy.DeckZone.Add(Entity.FromCard(enemy, Cards.FromId("JAIL_386t")));
        int armor = enemy.Hero.Armor;
        Play(g, p, id);
        Core2026Proof.Check(enemy.Hero.Armor == armor + 2 && enemy.HandZone.Select(x => x.Card.Id).Order().SequenceEqual(new[] { "CS2_029", "CS2_182" }.Order()), "automatic drawn Gear resolves armor and replacement draw");
        Core2026Proof.Check(p.HandZone.Select(x => x.Card.Id).Order().SequenceEqual(new[] { "JAIL_386t", "CS2_029" }.Order()) && p.HandZone.Single(x => x.Card.Id == "JAIL_386t").Cost == 1, "copies outer Gear and second draw; replacement draw is not copied");
    });
}
