using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BlasphemyProof
{
    // STILL-UNPINNED: tie-break-cards/w02-blasphemy-{hand-sample,tie,wave-recheck}.yaml;
    // tier D text and owner assumption: committed Cost, ties/empty qualify, one pre-wave check, deferred deaths.
    internal static void Run() => StandardSetsProof.Case("BE_086", Unpinned_Blasphemy);

    private static void Unpinned_Blasphemy(Game fixture, Controller owner, string id)
    {
        foreach (int otherCost in new[] { -1, 3, 4, 5 })
        foreach (int cost in new[] { 0, 1, 4, 6 })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var friendly = Summon(p, "CS2_200");
            var enemy = Summon(p.Opponent, "CS2_200"); enemy[GameTag.HEALTH] = 40;
            if (otherCost >= 0) Add(p, "CS2_182").Cost = otherCost;
            var spell = Add(p, id); spell.Cost = cost;
            PlayEntity(g, p, spell);
            int damage = otherCost <= cost ? 4 : 2;
            Core2026Proof.Check(enemy.Damage == damage && p.Opponent.Hero.Damage == damage
                && friendly.Damage == 0 && p.Hero.Damage == 0,
                $"committed Cost {cost}, remaining {otherCost}: enemies take {damage}; friendlies untouched");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var golem = Summon(p.Opponent, "EX1_556");
            golem.Damage = 1;
            var source = Play(g, p, id);
            Core2026Proof.Check(golem.Zone == p.Opponent.GraveyardZone && p.Opponent.BoardZone.Single().Card.Id == "skele21"
                && p.Opponent.BoardZone.Single().Damage == 0, "Deathrattle replacement waits until both waves finish");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            Summon(p, "EX1_012"); var enemy = Summon(p.Opponent, "CS2_200");
            var held = Add(p, id);
            CostRules.AddNextCardRule(p, cardType: CardType.SPELL, setCost: 1);
            Add(p, "CS2_182").Cost = 2;
            // Play helper expects current hand Cost, which includes the consumed next-card rule.
            PlayEntity(g, p, held);
            Core2026Proof.Check(enemy.Damage == 3, "consumed discount snapshot prevents repeat; Spell Damage applied once");
        }
    }
}
