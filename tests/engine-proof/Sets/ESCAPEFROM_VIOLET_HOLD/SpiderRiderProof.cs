using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpiderRiderProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_872", (g, p, id) => {

        var source = (Minion)Play(g, p, id); Play(g, p, "CS2_082");
        int deck = p.DeckZone.Count; Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Single().Card.Id == "CS2_182", "friendly hero attack draws one");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.DeckZone.Count == deck - 1, "minion attack does not draw");
        var clone = g.Clone(); End(clone); End(clone); int originalHand = p.HandZone.Count;
        Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)), "clone hero attack accepted");
        Core2026Proof.Check(clone.Player1.DeckZone.Count == p.DeckZone.Count - 2 && p.HandZone.Count == originalHand, "trigger survives end turn and clone is isolated");
        End(g); Play(g, p.Opponent, "CS2_082"); deck = p.DeckZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && p.DeckZone.Count == deck, "opponent hero attack does not draw"); End(g);
        source.Silence(); deck = p.DeckZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.DeckZone.Count == deck, "silence removes attack trigger");
        Kill(g, source); var replacement = Summon(p, id); Kill(g, replacement); Play(g, p, "CS2_082"); End(g); End(g); deck = p.DeckZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.DeckZone.Count == deck, "dead source no longer draws");
        Summon(p, id); Play(g, p, "CS2_082"); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        End(g); End(g); deck = p.DeckZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full-hand attack burns one draw");
    });
}
