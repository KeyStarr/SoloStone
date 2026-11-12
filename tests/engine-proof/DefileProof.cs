using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DefileProof
{
    // Rule-only: wiki Defile Notes (C), independently tested by RFMrSun/ZLYZAJO (E):
    // https://hearthstone.wiki.gg/wiki/Defile
    // https://www.hearthpwn.com/forums/hearthstone-general/card-discussion/199710-defile-limit
    // 14 total waves, including the initial cast; settle the last Deathrattle, then stop.
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            foreach (string form in new[] { "TLC_234", "TLC_234t" })
            foreach (bool friendly in new[] { false, true }) Loop(seed, form, friendly);
            Boundary(seed);
            Ordinary(seed);
            SpellDamage(seed, survives: false);
            SpellDamage(seed, survives: true);
        }
        Console.WriteLine("PASS Defile: 14 total waves; both forms/sides; cap boundary; early stop; Spell Damage; effect/repeated casts; clone/snapshot");
    }

    private static void Loop(int seed, string form, bool friendly)
    {
        var game = PlayerDecisionProof.Fixture(seed); var p = game.Player1;
        Summon(friendly ? p : p.Opponent, form);
        var witness = Summon(p.Opponent, "CS2_182"); witness[GameTag.HEALTH] = 100;
        PoolPilotProof.Resume(game, branch => {
            int deaths = 0, limit = 14;
            TriggerManager.TriggerHandler guard = entity => {
                if (entity.Card.Id == "TLC_234" || entity.Card.Id == "TLC_234t")
                    Core2026Proof.Check(++deaths <= limit, "Defile must stop after 14 total waves per cast");
            };
            branch.TriggerManager.DeathTrigger += guard;
            try
            {
                var spell = (Spell)Play(branch, branch.Player1, "ICC_041");
                AssertLoop(14);
                // A separate effect invocation of the same entity must receive a fresh budget.
                limit = 28;
                Generic.CastSpell(branch.Player1, branch, spell, null, 0);
                branch.DeathProcessingAndAuraUpdate();
                AssertLoop(28);
                limit = 42;
                var next = (Spell)Entity.FromCard(branch.Player1, Cards.FromId("ICC_041"));
                Generic.CastSpell(branch.Player1, branch, next, null, 0);
                branch.DeathProcessingAndAuraUpdate();
                AssertLoop(42);
            }
            finally { branch.TriggerManager.DeathTrigger -= guard; }

            void AssertLoop(int expected)
            {
                var owner = friendly ? branch.Player1 : branch.Player2;
                Core2026Proof.Check(deaths == expected && branch.IdEntityDic[witness.Id][GameTag.DAMAGE] == expected
                    && owner.BoardZone.Count(m => m.Card.Id == form) == 1 && branch.DeadMinions.Count == 0,
                    $"Defile cap settles final reciprocal Deathrattle: deaths={deaths}/{expected}, damage={branch.IdEntityDic[witness.Id][GameTag.DAMAGE]}, pending={branch.DeadMinions.Count}");
            }
        });
    }

    private static void Boundary(int seed)
    {
        var game = PlayerDecisionProof.Fixture(seed); var p = game.Player1;
        // A 1..14 Health ladder needs every allowed wave to finish clearing both full boards.
        for (int health = 1; health <= 14; health++)
            Summon(health <= 7 ? p : p.Opponent, "CS2_182")[GameTag.HEALTH] = health;
        Play(game, p, "ICC_041");
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0 && game.DeadMinions.Count == 0,
            "Defile clears a ladder ending exactly on wave 14");
    }

    private static void Ordinary(int seed)
    {
        var game = PlayerDecisionProof.Fixture(seed); var p = game.Player1;
        for (int health = 1; health <= 3; health++)
            Summon(p.Opponent, "CS2_182")[GameTag.HEALTH] = health;
        var witness = Summon(p, "CS2_182"); witness[GameTag.HEALTH] = 8;
        Play(game, p, "ICC_041");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && witness.Damage == 4 && game.DeadMinions.Count == 0,
            "ordinary three-death ladder keeps its final nonlethal fourth wave");
        Play(game, p, "ICC_041");
        Core2026Proof.Check(witness.Damage == 5, "Defile with no deaths deals damage only once");
    }

    private static void SpellDamage(int seed, bool survives)
    {
        var game = PlayerDecisionProof.Fixture(seed); var p = game.Player1;
        Summon(p.Opponent, "TLC_234");
        var totem = Summon(p, "CS2_052");
        if (survives) totem[GameTag.HEALTH] = 100;
        var witness = Summon(p.Opponent, "CS2_182"); witness[GameTag.HEALTH] = 100;
        int deaths = 0;
        TriggerManager.TriggerHandler guard = entity => {
            if (entity.Card.Id == "TLC_234" || entity.Card.Id == "TLC_234t")
                Core2026Proof.Check(++deaths <= 14, "Spell Damage does not change Defile's wave cap");
        };
        game.TriggerManager.DeathTrigger += guard;
        try { Play(game, p, "ICC_041"); }
        finally { game.TriggerManager.DeathTrigger -= guard; }
        Core2026Proof.Check(deaths == 14 && witness.Damage == (survives ? 28 : 15) && game.DeadMinions.Count == 0,
            "Spell Damage applies on every wave and is recomputed after its source dies");
    }
}
