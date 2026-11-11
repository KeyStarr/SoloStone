using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B15EmeraldPlayLocksProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_234", Unpinned_EmeraldBountyDuration);
        StandardSetsProof.Case("EDR_526", Unpinned_RenferalCountAndDuration);
        Unpinned_RenferalSeededOrder();
        BountyRewind();
    }

    private static void Unpinned_EmeraldBountyDuration(Game game, Controller p, string id)
    {
        var first = p.DeckZone.TopCard;
        var second = p.DeckZone.Reverse().Skip(1).First();
        Play(game, p, id);
        Core2026Proof.Check(new[] { first, second }.All(c => c.Zone == p.HandZone && c[GameTag.CANT_PLAY] == 1),
            "Unpinned_Bounty locks exactly two successful draws");
        Play(game, p, "CS2_182");
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var held = next.IdEntityDic[first.Id];
            End(next); End(next);
            Core2026Proof.Check(!held.IsPlayableByPlayer && held[GameTag.CANT_PLAY] == 1, "Unpinned_Bounty denies next owner turn");
            End(next); End(next);
            Core2026Proof.Check(held.IsPlayableByPlayer && held[GameTag.CANT_PLAY] == 0, "Unpinned_Bounty allows following owner turn");
        }
        var full = PlayLockRulesProof.Fixture();
        while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_182");
        var source = Entity.FromCard(full.Player1, Cards.FromId(id), zone: full.Player1.SetasideZone);
        int before = full.Player1.DeckZone.Count;
        source.Power.PowerTask.Process(full, full.Player1, source, null);
        Core2026Proof.Check(full.Player1.DeckZone.Count == before - 2 && full.Player1.HandZone.All(c => c[GameTag.CANT_PLAY] == 0)
            && full.Player1.GraveyardZone.Where(c => c.Card.Id == "CS2_231").All(c => c[GameTag.CANT_PLAY] == 0),
            "Bounty burned draws do not lock existing hand or burned cards");
    }

    private static IPlayable[] EnemyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        return new[] { "CS2_182", "CS2_024", "CS2_106", "CORE_REV_990", "CS2_172" }.Select(id => Add(p, id)).ToArray();
    }

    private static void Unpinned_RenferalCountAndDuration(Game game, Controller p, string id)
    {
        var hand = EnemyHand(p.Opponent);
        game.SetRandomSeed(31);
        var source = Play(game, p, id);
        Core2026Proof.Check(hand.Count(c => c[GameTag.CANT_PLAY] == 1) == 1, "Unpinned_Renferal first play traps one");
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var actor = next.IdEntityDic[source.Id];
            actor.Power.PowerTask.Process(next, next.Player1, actor, null);
            Core2026Proof.Check(next.Player2.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 2,
                "Unpinned_Renferal repeated Battlecry uses same one-card count, excludes trapped");
            var fresh = Add(next.Player1, id);
            next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(PowerHistoryBuilder.FullEntity(fresh).Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                "Unpinned_Renferal fresh copy displays shared next play count");
            PlayEntity(next, next.Player1, fresh);
            Core2026Proof.Check(next.Player2.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 4,
                "Unpinned_Renferal fresh second play traps two distinct unlocked cards");
            Play(next, next.Player1, id);
            Core2026Proof.Check(next.Player2.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 5, "Unpinned_Renferal count caps at eligible hand");
            long cursor = next.Random.DrawCount;
            actor.Power.PowerTask.Process(next, next.Player1, actor, null);
            Core2026Proof.Check(next.Random.DrawCount == cursor, "all trapped means no RNG or decision");
            End(next);
            Core2026Proof.Check(hand.Select(c => next.IdEntityDic[c.Id]).All(c => !c.IsPlayableByPlayer), "Unpinned_Renferal blocks whole victim turn");
            End(next);
            Core2026Proof.Check(next.Player2.HandZone.All(c => c[GameTag.CANT_PLAY] == 0), "Unpinned_Renferal victim end removes prohibition");
        }
        Core2026Proof.Check(Generic.ReturnToHandBlock(p, (Minion)source), "Renferal bounce fixture");
        PlayEntity(game, p, source);
        Core2026Proof.Check(p.Opponent.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 4,
            "Unpinned_Renferal bounced fourth play uses same controller count");
        for (int i = 0; i < 4; i++) Add(p, "CS2_182");
        End(game);
        Play(game, p.Opponent, id);
        Core2026Proof.Check(p.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 1,
            "Unpinned_Renferal opponent has independent first-play count");
    }

    private static void Unpinned_RenferalSeededOrder()
    {
        var positions = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++)
        {
            string forward = null;
            int position = -1;
            foreach (bool reverse in new[] { false, true })
            {
                var game = PlayLockRulesProof.Fixture(seed: seed);
                var hand = EnemyHand(game.Player2);
                if (reverse)
                {
                    foreach (var card in hand) game.Player2.SetasideZone.Add(game.Player2.HandZone.Remove(card));
                    foreach (var card in hand.Reverse()) game.Player2.HandZone.Add(game.Player2.SetasideZone.Remove(card));
                    hand = hand.Reverse().ToArray();
                }
                game.SetRandomSeed(seed);
                Play(game, game.Player1, "EDR_526");
                int selected = Array.FindIndex(hand, c => c[GameTag.CANT_PLAY] == 1);
                if (!reverse) { positions.Add(selected); position = selected; forward = hand[selected].Card.Id; }
                else Core2026Proof.Check(selected == position && (position == 2 || hand[selected].Card.Id != forward),
                    "Unpinned_Renferal seeded sample respects hand order");
            }
        }
        Core2026Proof.Check(positions.Count >= 3, "Unpinned_Renferal seeded picks are not always-first or fixed-position");
        Console.WriteLine("PASS Unpinned_Renferal seeded order: 12 seeds, selected positions=" + string.Join(",", positions.OrderBy(x => x)));
    }

    private static void BountyRewind()
    {
        var game = PlayLockRulesProof.Fixture();
        var source = Add(game.Player1, "EDR_234"); source[GameTag.REWIND] = 1;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(game.Player1, source)) && game.Rewind?.Stage == RewindStage.Offered,
            "Bounty opens real Rewind decision");
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var decision = next.GetPendingDecisions().Single();
            Core2026Proof.Check(next.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId,
                new[] { next.Rewind.RewindEntityId }), out var error), "Bounty Rewind answer: " + error);
            Core2026Proof.Check(next.Player1.HandZone.Count(c => c[GameTag.CANT_PLAY] == 1) == 2,
                "Rewind restores baseline then replays exactly two independent locks");
            End(next); End(next); End(next); End(next);
            Core2026Proof.Check(next.Player1.HandZone.All(c => c[GameTag.CANT_PLAY] == 0), "rewound locks expire normally");
        }
    }
}
