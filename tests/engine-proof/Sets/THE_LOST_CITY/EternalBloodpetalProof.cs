using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EternalBloodpetalProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_234", (g, p, id) => {
        GodfreyLoop((int)g.Seed);
        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182");
        for (int i = 0; i < 4; i++)
        {
            Kill(g, source); string next = i % 2 == 0 ? "TLC_234t" : id;
            source = p.BoardZone.Single(m => m.Card.Id == next);
            Core2026Proof.Check(source.Card.Implemented && source.Health == 1 && source.AttackDamage == (i % 2 == 0 ? 0 : 5), "reciprocal pinned 0/1 Seedling and 5/1 Bloodpetal");
            Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { source.Id, left.Id, right.Id }), "dead leftmost source's slot takes replacement");
        }
        Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == id || m.Card.Id == "TLC_234t"), "silence stops chain");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[0].Card.Id == "TLC_234t", "full board uses freed slot");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.BoardZone.Count == 7, "full board blocks extra Seedling");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        left[GameTag.DORMANT] = 1;
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        var generated = p.BoardZone.Where(m => new[] { "TLC_234t" }.Contains(m.Card.Id)).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 1 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(generated.Select(m => m.Id)).Append(right.Id)), "live source summons right in creation order with Dormant left neighbour");
        left[GameTag.DORMANT] = 0;
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => new[] { "TLC_234t" }.Contains(m.Card.Id)) == 1, "full board prevents additional summons");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        var seedling = p.BoardZone.Single(m => m.Card.Id == "TLC_234t");
        CheckDeathrattleSlot(p, board, new[] { seedling }, "Seedling fills the dying Bloodpetal's physical slot");
        Kill(g, seedling);
        CheckDeathrattleSlot(p, board, p.BoardZone.Where(m => m.Card.Id == id), "Bloodpetal regrows in the dying Seedling's physical slot");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ParkBoard(p);
        End(g);
    });

    // PINNED-RULE-ONLY: https://hearthstone.wiki.gg/wiki/Lord_Godfrey (Notes: 14 rounds; patch list only adds Undead)
    // Independently corroborated: https://www.reddit.com/r/hearthstone/comments/dmroxf (2019-10-25: 14 including first).
    // Checked 2026-10-21; exact retail wire order remains unpinned.
    private static void GodfreyLoop(int seed)
    {
        foreach (string form in new[] { "TLC_234", "TLC_234t" })
        foreach (bool friendly in new[] { false, true })
        foreach (bool doubled in new[] { false, true })
        {
            var game = PlayerDecisionProof.Fixture(seed); var player = game.Player1;
            Summon(friendly ? player : player.Opponent, form);
            var witness = Summon(player.Opponent, "CS2_182"); witness[GameTag.HEALTH] = 100;
            if (doubled) { var brann = Summon(player, "LOE_077"); brann[GameTag.HEALTH] = 100; }
            PoolPilotProof.Resume(game, branch => {
                int deaths = 0, expected = doubled ? 28 : 14;
                TriggerManager.TriggerHandler guard = entity => {
                    if (entity.Card.Id == "TLC_234" || entity.Card.Id == "TLC_234t")
                        Core2026Proof.Check(++deaths <= expected, "Godfrey must terminate after 14 waves per Battlecry");
                };
                branch.TriggerManager.DeathTrigger += guard;
                try { Play(branch, branch.Player1, "GIL_825"); }
                finally { branch.TriggerManager.DeathTrigger -= guard; }
                var owner = friendly ? branch.Player1 : branch.Player2;
                Core2026Proof.Check(deaths == expected && branch.IdEntityDic[witness.Id][GameTag.DAMAGE] == expected * 2
                    && owner.BoardZone.Count(m => m.Card.Id == form) == 1 && branch.DeadMinions.Count == 0,
                    "bounded Godfrey keeps reciprocal Deathrattles, settles final wave and grants Brann a fresh limit");
            });
        }
        var finite = PlayerDecisionProof.Fixture(seed); var p = finite.Player1;
        Summon(p.Opponent, "TLC_234t").Silence();
        var survivor = Summon(p.Opponent, "CS2_182"); survivor[GameTag.HEALTH] = 7;
        Play(finite, p, "GIL_825");
        Core2026Proof.Check(survivor.Damage == 4 && survivor.Health == 3 && finite.DeadMinions.Count == 0,
            "Godfrey stops after first wave without a death instead of always firing 14 times");
        Console.WriteLine("PASS Godfrey/Bloodpetal: 14 waves; both forms/sides; Brann; early stop; clone/snapshot");
    }
}
