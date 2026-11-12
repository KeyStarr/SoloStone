using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ZerOntuProof
{
    internal static void Run() => StandardSetsProof.Case("BE_790", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (string cardId in new[] { "CS2_051", "CS2_101t", "CS2_120", "CS2_029", "CS2_182" }) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(cardId)));
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle recruit play");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS2_182", id, "CS2_051", "CS2_101t", "CS2_120", "CS2_182" }), "0,1,2 Attack recruits in order right of source");
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 9 && source.HasTaunt && p.DeckZone.Count == 2, "gains recruited current stats, spell and unmatched stay deck");
        Play(g, p, "BE_228", source); Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3 && !source.HasTaunt, "silence removes gained stats");
        ClearBoard(g, p); var empty = (Minion)Play(g, p, id); Core2026Proof.Check(empty.AttackDamage == 3 && empty.Health == 3, "missing attack buckets add nothing");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_051"))); var full = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && full.Health == 3 && p.DeckZone.Any(c => c.Card.Id == "CS2_051"), "full board neither recruits nor gains stats");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var partial = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && partial.AttackDamage == 3 && partial.Health == 5 && !p.DeckZone.Any(c => c.Card.Id == "CS2_051"), "one free slot recruits zero-Attack minion and gains only its stats");
        ClearBoard(g, p);
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_101t"))); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS1_042")));
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); Kill(g, source);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        var chosen = p.BoardZone.Last();
        Core2026Proof.Check(p.DeckZone.Count == 1 && chosen.AttackDamage == 1 && (chosen.Card.Id == "CS2_101t" || chosen.Card.Id == "CS1_042") && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, chosen.Id }), "random eligible deck minion, removed source appends");
        g.DeathProcessingAndAuraUpdate();
    });
}
