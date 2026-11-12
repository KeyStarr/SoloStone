using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AnimatorofIronProof
{
    // Earlier blocker cleared: Character.GainArmor publishes OnArmorTrigger; TriggerType.ARMOR supports HERO.
    internal static void Run() => StandardSetsProof.Case("BE_600", (g, p, id) => {
        var held = Add(p, id); var source = (Minion)Play(g, p, id);
        int deck = p.DeckZone.Count;
        p.Hero.GainArmor(p.Hero, 0); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.DeckZone.Count == deck, "zero Armor does not draw");
        var vanguard = (Minion)Play(g, p, "BE_572");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 2 && held.Zone == p.HandZone,
            "one draw per gain event, not per Armor point; hand source inactive");
        Kill(g, vanguard);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2, "Deathrattle Armor also draws");
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, "BE_572");
        Core2026Proof.Check(clone.Player1.DeckZone.Count == deck - 3 && p.DeckZone.Count == deck - 2,
            "Armor subscription survives clone independently");
        End(g); deck = p.DeckZone.Count; Play(g, p.Opponent, "BE_572");
        Core2026Proof.Check(p.DeckZone.Count == deck, "opponent Armor does not trigger");
        End(g); deck = p.DeckZone.Count; Play(g, p, "BE_572");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "trigger persists across turns");
        source.Silence(); deck = p.DeckZone.Count; Play(g, p, "BE_572");
        Core2026Proof.Check(p.DeckZone.Count == deck && source.AttackDamage == 2 && source.BaseHealth == 5,
            "Silence removes Armor trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source);
        deck = p.DeckZone.Count; Play(g, p, "BE_572");
        Core2026Proof.Check(p.DeckZone.Count == deck, "departed source does not draw");
        source = (Minion)Play(g, p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        deck = p.DeckZone.Count; p.Hero.GainArmor(p.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand burns triggered draw");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        int fatigue = p.Hero.Fatigue; p.Hero.GainArmor(p.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Fatigue == fatigue + 1, "empty deck triggers fatigue");
    });
}
