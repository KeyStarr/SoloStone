using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InterrogationProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_518", (g, p, id) => {
        int deckCount = p.DeckZone.Count; Play(g, p, id);
        var ninjas = p.DeckZone.Where(c => c.Card.Id == "TLC_513t2").ToArray();
        Core2026Proof.Check(ninjas.Length == 3 && p.DeckZone.Count == deckCount + 3 && ninjas.All(c => c.Card.Implemented && ((Minion)c).AttackDamage == 3 && ((Minion)c).Health == 3 && ((Minion)c).HasStealth), "shuffle exact three 3/3 Stealth Ninjas");
        foreach (var ninja in ninjas) p.DeckZone.Remove(ninja);
        foreach (var ninja in ninjas) p.DeckZone.Add(ninja, 0);
        var clone = g.Clone(); Generic.Draw(clone.Player1, clone.IdEntityDic[ninjas[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 0 && clone.Player1.BoardZone.Count == 1, "draw summon clone isolated");
        Generic.Draw(p, ninjas[0]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ninjas[0].Zone == p.BoardZone && p.BoardZone.Last().Id == ninjas[0].Id && !p.SetasideZone.Contains(ninjas[0]) && p.HandZone.Count >= 1, "draw summons original Ninja and draws replacement");
        ClearBoard(g, p); for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
        Generic.Draw(p, ninjas[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && ninjas[1].Zone == p.GraveyardZone, "full board loses drawn Ninja without exceeding seven");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); Generic.Draw(p, ninjas[2]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ninjas[2].Zone == p.GraveyardZone && p.HandZone.Count == 10 && p.BoardZone.Count == 7, "full hand burns Ninja without triggering");
    });
}
