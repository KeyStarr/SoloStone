using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TheUnseenAtlasProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_514", (g, p, id) => {

        var held = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 9 && held[GameTag.COST] == 9, "source counts itself in visible discount");
        Add(p.Opponent, "CS2_182"); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 9, "opponent hand irrelevant");
        var other = Add(p, "CS2_182"); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held[GameTag.COST] == 8, "own hand growth updates COST");
        var clone = g.Clone(); clone.Player1.SetasideZone.Add(clone.Player1.HandZone.Remove(clone.IdEntityDic[other.Id])); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.COST] == 9 && held[GameTag.COST] == 8, "clone hand cost isolated");
        int deck = p.DeckZone.Count; PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == 4 && p.DeckZone.Count == deck - 3 && held.Zone == p.GraveyardZone && held.OngoingEffect == null, "draw three; cost aura detaches on play");
        held = Add(p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 0 && held[GameTag.COST] == 0, "ten held cards floor cost at zero");
        deck = p.DeckZone.Count; PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 3, "full hand draws one and burns two");
        End(g); End(g);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.First()));
        int fatigue = p.Hero.Fatigue; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Fatigue == fatigue + 3, "empty deck attempts all three fatigue draws");
    });
}
