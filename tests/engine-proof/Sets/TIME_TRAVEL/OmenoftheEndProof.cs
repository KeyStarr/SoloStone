using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OmenoftheEndProof
{
    internal static void Run() => StandardSetsProof.Case("END_035", (g, p, id) => {
        var top = p.Opponent.DeckZone.Reverse().Take(6).ToArray(); int count = p.Opponent.DeckZone.Count;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.DeckZone.Count == count, "nonempty own deck does nothing");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == count - 5 && top.Take(5).All(c => c.Zone.Type == Zone.GRAVEYARD) && p.Opponent.DeckZone.TopCard.Id == top[5].Id, "exact top five destroyed in draw order");
        while (p.Opponent.DeckZone.Count > 2) { var card = p.Opponent.DeckZone[0]; p.Opponent.DeckZone.Remove(card); p.Opponent.SetasideZone.Add(card); }
        int damage = p.Opponent.Hero.Damage; Play(g, p, id); Core2026Proof.Check(p.Opponent.DeckZone.Count == 0 && p.Opponent.Hero.Damage == damage, "short deck destroys remaining cards without fatigue");
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "empty enemy deck causes no fatigue");
    });
}
