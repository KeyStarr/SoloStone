using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class RavenousFelfisherProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_529", (g, p, id) => {
        var held = Add(p, id);
        Core2026Proof.Check(held.Cost == 6 && held[GameTag.COST] == 6, "no Fel casts keeps base visible cost");
        End(g); Play(g, p.Opponent, "EX1_287"); End(g);
        var countered = (Spell)Play(g, p, "CATA_528");
        Core2026Proof.Check(countered.IsCountered && held.Cost == 6, "countered Fel play is not a completed cast");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Cost == 6, "non-Fel cast excluded");
        End(g); Play(g, p.Opponent, "CATA_528"); End(g);
        Core2026Proof.Check(held.Cost == 6, "opponent Fel cast excluded");
        Add(p, "CATA_528"); End(g);
        Play(g, p.Opponent, "DAL_538"); End(g);
        Core2026Proof.Check(held.Cost == 6 && g.HistoryFacts.Query.Casts().Any(f => f.ControllerId == p.Id
            && f.CasterKind == HistoryCastKind.Effect && f.Outcome == HistoryCastOutcome.Completed),
            "actual Saboteur automatic Fel cast excluded from player cast credit");
        Play(g, p, "CATA_528");
        var late = Add(p, id);
        Core2026Proof.Check(held.Cost == 5 && held[GameTag.COST] == 5 && late.Cost == 5,
            "one completed player Fel cast counts even before creation");
        End(g); End(g);
        Core2026Proof.Check(late.Cost == 5, "Fel cast credit lasts across turns");
        var clone = g.Clone();
        Play(clone, clone.Player1, "BOT_411"); Play(clone, clone.Player1, "CATA_528");
        Core2026Proof.Check(clone.IdEntityDic[late.Id].Cost == 3 && late.Cost == 5,
            "actual Electra player plus Repeat casts count separately only in clone");
        for (int i = 0; i < 7; i++) Play(g, p, "CATA_528");
        Core2026Proof.Check(held.Cost == 0 && held[GameTag.COST] == 0, "Fel discount floors at zero");
        PlayEntity(g, p, held);
        var minion = (Minion)held;
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 5 && minion.IsRace(Race.NAGA),
            "discounted play preserves pinned Naga stats");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
    });
}
