using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class AsphyxiateProof
{
    // data/rules-evidence/tie-break/attack-extreme-ties.yaml (C; owner-approved uniform current-Attack ties).
    internal static void Run() => StandardSetsProof.Case("CORE_RLK_087", Unpinned_Asphyxiate);
    private static void Unpinned_Asphyxiate(SabberStoneCore.Model.Game g, Controller p, string id)
    {
        Play(g, p, id); // Empty enemy board fizzles.
        var friendly = Summon(p, "CS2_200");
        var dormant = Summon(p.Opponent, "CORE_BT_156"); dormant.AttackDamage = 99;
        var low = Summon(p.Opponent, "CS2_182");
        var high = Summon(p.Opponent, "CS2_171"); high.AttackDamage = 8; high.HasStealth = true;
        var tied = Summon(p.Opponent, "CS2_171"); tied.AttackDamage = 8;
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check((high.Zone.Type == Zone.GRAVEYARD) != (tied.Zone.Type == Zone.GRAVEYARD), "one highest-current-Attack enemy dies, including Stealth");
        Core2026Proof.Check(low.Zone == p.Opponent.BoardZone && friendly.Zone == p.BoardZone, "lower Attack and friendly excluded");
        Play(g, p, id);
        Core2026Proof.Check(high.Zone.Type == Zone.GRAVEYARD && tied.Zone.Type == Zone.GRAVEYARD, "remaining highest dies");
        low.AttackDamage = 0; Play(g, p, id);
        Core2026Proof.Check(low.Zone.Type == Zone.GRAVEYARD && friendly.Zone == p.BoardZone, "zero Attack remains eligible");
        Core2026Proof.Check(dormant.Zone == p.Opponent.BoardZone && dormant.Untouchable, "Dormant excluded despite higher Attack");
        foreach (bool reverse in new[] { false, true })
        {
            var ties = DeathKnightC1Proof.NewGame(g.Seed); var enemy = ties.Player2;
            var order = new[] { "CS2_171", "CS2_182", "CS2_200" };
            if (reverse) Array.Reverse(order);
            var victims = order.Select(cardId => Summon(enemy, cardId)).ToArray();
            foreach (var victim in victims) victim.AttackDamage = 8;
            // RNG v1's first Next(3) at seeds 1, 2, 3 selects slots 0, 1, 2.
            ties.Random.SetSeed(g.Seed);
            PoolPilotProof.Resume(ties, next => Play(next, next.Player1, id));
            Core2026Proof.Check(victims.Where(m => m.Zone.Type == Zone.GRAVEYARD).Select(m => m.Id)
                .SequenceEqual(new[] { victims[(int)g.Seed - 1].Id }),
                $"seed {g.Seed}, reversed={reverse}: exact tied victim, independent of card identity/order");
        }
    }
}
