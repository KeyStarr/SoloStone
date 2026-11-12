using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class IcebeltMagiProof
{
    internal static void Run() => StandardSetsProof.Case("BE_270", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Magi play");
        var water = p.BoardZone.Single(m => m.Card.Id == "CS2_033");
        Core2026Proof.Check(p.HandZone.Count == 2 && water.AttackDamage == 3 && water.Health == 6 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, water.Id, right.Id }), "one nonminion draws one exact Water Elemental beside source");
        ClearBoard(g, p); int health = p.Hero.Health; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Fatigue == 2 && p.Hero.Health == health - 3 && p.BoardZone.Count == 1, "two empty-deck draws deal 1+2 fatigue and summon nothing");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_033") == 1, "two nonminions respect board limit");
        ClearBoard(g, p);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1;
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        var waters = p.BoardZone.Where(m => m.Card.Id == "CS2_033").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(waters.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, waters[0].Id, waters[1].Id, right.Id }), "two Elementals in creation order, Dormant neighbor counts");
        left[GameTag.DORMANT] = 0; ClearBoard(g, p);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); Kill(g, source);
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        waters = p.BoardZone.Where(m => m.Card.Id == "CS2_033").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 0 && waters.Length == 0 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id }), "burned spells summon no Elementals");
        var removed = p.HandZone[0]; p.HandZone.Remove(removed); p.SetasideZone.Add(removed);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        waters = p.BoardZone.Where(m => m.Card.Id == "CS2_033").ToArray();
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 0 && waters.Length == 1 && p.BoardZone.Last() == waters[0], "one hand slot counts only successful spell draw, removed source appends");
        g.DeathProcessingAndAuraUpdate();
    });
}
