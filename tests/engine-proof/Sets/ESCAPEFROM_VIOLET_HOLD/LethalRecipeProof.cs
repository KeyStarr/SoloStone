using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LethalRecipeProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_866", (g, p, id) => {

        var held = (Minion)Add(p, "CS2_182");
        int deck = p.DeckZone.Count; Play(g, p, id);
        var drawn = p.HandZone.OfType<Minion>().Where(m => m.Id != held.Id).ToArray();
        Core2026Proof.Check(drawn.Length == 2 && drawn.All(m => m.Card.Id == "CS2_182" && m.AttackDamage == 7 && m.Health == 8) && held.AttackDamage == 4 && held.Health == 5 && p.DeckZone.Count == deck - 2, "ten crystals despite three spent: only drawn minions +3/+3");
        PlayEntity(g, p, drawn[0]); drawn[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(drawn[0].AttackDamage == 4 && drawn[0].Health == 5, "drawn buff survives play and is silenced");
        var source = Add(p, id); p.BaseMana = 9; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "nine-mana recipe legal");
        Core2026Proof.Check(p.HandZone.OfType<Minion>().Count(m => m.AttackDamage == 4 && m.Health == 5) == 3, "below ten crystals no buff");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var only = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        int fatigue = p.Hero.Fatigue; Play(g, p, id);
        Core2026Proof.Check(only.Zone == p.HandZone && ((Minion)only).AttackDamage == 7 && p.DeckZone.Single().Card.Id == "CS2_029" && p.Hero.Fatigue == fatigue, "one eligible minion drawn; no spell or fatigue fallback");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var burned = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(burned.Zone == p.GraveyardZone && burned.AttackDamage == 4 && p.HandZone.Count == 10, "burned draw never buffed");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        ClearBoard(g, p); Summon(p, "BRM_031");
        var first = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var second = (Minion)Entity.FromCard(p, Cards.FromId("EX1_008"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        int draws = p.NumCardsDrawnThisTurn;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 4 && p.HandZone.Count(c => c.Card.Id == first.Card.Id) == 2
            && p.HandZone.Count(c => c.Card.Id == second.Card.Id) == 2 && p.NumCardsDrawnThisTurn == draws + 2,
            "Chromaggus copies both filtered draws through normal draw triggers");
        Core2026Proof.Check(first.AttackDamage == 7 && second.AttackDamage == 4
            && p.HandZone.OfType<Minion>().Where(m => m.Id != first.Id && m.Id != second.Id)
                .All(m => m.AttackDamage == m.Card.ATK), "only actual draws get recipe buff, not Chromaggus copies");
    });
}
