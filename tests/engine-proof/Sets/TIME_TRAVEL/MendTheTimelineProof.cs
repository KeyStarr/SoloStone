using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class MendTheTimelineProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_018", (g, p, id) => {
        EmptyPool(g, p, id);
        WithCards(g, new[] { "CORE_EX1_619" }, () => {
            EmptyHand(p); p.Hero.Damage = 20;
            var source = Resolve(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card.Id == "CORE_EX1_619" && c.Cost == 2)
                && p.Hero.Damage == 16, "two Holy spells with replacement, heal sum of current costs");
            Core2026Proof.Check(p.HandZone.All(c => c[GameTag.DISPLAYED_CREATOR] == source.Id), "generated creator");
            EmptyHand(p);
            Summon(p, "EX1_608"); p.Hero.Damage = 20;
            Resolve(g, p, id);
            Core2026Proof.Check(p.HandZone.All(c => c.Cost == 1) && p.Hero.Damage == 18, "heal uses aura-modified current costs");
            ParkBoard(p);
            Unpinned_FullHandCosts(g, p, id);
        });
        EmptyHand(p); p.Hero.Damage = 29;
        var played = Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        TimeTravelPoolProof.RandomGenerated(g, played, p.HandZone, 2);
        Core2026Proof.Check(p.HandZone.All(c => c.Card[GameTag.SPELL_SCHOOL] == 5)
            && p.Hero.Damage == 29 - p.HandZone.Sum(c => c.Cost), "installed unrestricted Holy pool and cost-based heal");
    });

    private static void Unpinned_FullHandCosts(Game g, Controller p, string id)
    {
        EmptyHand(p); FillHand(p, 9); p.Hero.Damage = 20;
        Resolve(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Hero.Damage == 16, "Unpinned_FullHandCosts: burned generated spell also contributes");
        p.Hero.Damage = 1; Resolve(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Hero.Damage == 0, "full hand still heals, capped at max Health");
    }
}
