using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class PlayLockRulesProof
{
    internal static Game Fixture(bool history = true, int seed = 31)
    {
        var game = LocationProofs.Game(seed, history);
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            player.BaseMana = 10;
        }
        return game;
    }

    [GroupProof("b15-play-locks")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var game = Fixture(history);
            var p = game.Player1;
            foreach (string id in new[] { "CS2_182", "CS2_024", "CS2_106", "CORE_EX1_323", "CORE_REV_990" })
            {
                var card = Add(p, id);
                Core2026Proof.Check(card.IsPlayableByPlayer, id + " unlocked fixture");
                PlayLockRules.LockHand(card, p.Hero, "CATA_215e");
                Core2026Proof.Check(!card.IsPlayableByPlayer && card[GameTag.CANT_PLAY] == 1
                    && !p.Options().Any(t => t is PlayCardTask && t.Source == card), id + " all-card predicate/options");
                string before = Canonical.State(game);
                Core2026Proof.Check(!game.Process(PlayCardTask.Any(p, card, skipPrePhase: true)) && Canonical.State(game) == before,
                    id + " forced invalid hand play has no mutation");
            }
            foreach (var next in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                Core2026Proof.Check(next.Player1.HandZone.All(PlayLockRules.BlocksPlay), "clone/snapshot retains all locks");
                End(next);
                Core2026Proof.Check(next.Player1.HandZone.All(c => c[GameTag.CANT_PLAY] == 0), "one anchored owner-end expiry");
            }
            game = Fixture(history); p = game.Player1;
            var target = Add(p, "CS2_182");
            PlayLockRules.LockHand(target, p.Hero, "EDR_234e2", 2);
            PlayLockRules.LockHand(target, p.Hero, "CATA_215e");
            p[GameTag.EXTRA_END_TURN_EFFECT] = 1;
            End(game); Core2026Proof.Check(target[GameTag.CANT_PLAY] == 1, "overlap retains longer lock");
            End(game); Core2026Proof.Check(target[GameTag.CANT_PLAY] == 1, "opponent boundary does not age owner lock");
            End(game); Core2026Proof.Check(target[GameTag.CANT_PLAY] == 0, "second owner end clears longer lock");
            game = Fixture(history); p = game.Player1; target = Add(p, "CS2_182");
            PlayLockRules.LockHand(target, p.Hero, "CATA_215e");
            p.DeckZone.Add(p.HandZone.Remove(target));
            p.HandZone.Add(p.DeckZone.Remove(target));
            Core2026Proof.Check(target[GameTag.CANT_PLAY] == 0 && target.AppliedEnchantments.Count == 0, "hand exit/reentry cancels residence");
            target[GameTag.CANT_PLAY] = 1;
            Core2026Proof.Check(target.IsPlayableByPlayer, "legacy unscoped minion CANT_PLAY behavior unchanged");
            target[GameTag.CANT_PLAY] = 0;
            PrepareRules.Grant(target);
            Core2026Proof.Check(game.Process(PrepareCardTask.Any(p, target)), "Prepare remains available");
            PlayLockRules.LockHand(target, p.Hero, "EDR_234e2", 2);
            End(game);
            Core2026Proof.Check(target[GameTag.PREPARING] == 0 && target[GameTag.CANT_PLAY] == 1, "Prepare expiry cannot clear independent lock");
            game = Fixture(history); p = game.Player1; target = Add(p, "CS2_182");
            PlayLockRules.LockHand(target, p.Hero, "EDR_234e2", 2);
            var copied = Generic.Copy(p.Opponent, p.Hero, target, Zone.HAND);
            Core2026Proof.Check(copied[GameTag.CANT_PLAY] == 1, "hand copy carries independent restriction");
            End(game); End(game); End(game);
            Core2026Proof.Check(target[GameTag.CANT_PLAY] == 0 && copied[GameTag.CANT_PLAY] == 1, "copied lock ages on receiving controller ends");
            End(game);
            Core2026Proof.Check(copied[GameTag.CANT_PLAY] == 0, "copied lock has live expiry trigger");
        }
        Console.WriteLine("PASS play locks: five card types, force-play guard, overlap, owner expiry, hand exit, Prepare, clone/snapshot");
    }
}
