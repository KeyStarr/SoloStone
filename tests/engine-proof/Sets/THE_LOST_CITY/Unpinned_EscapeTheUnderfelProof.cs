using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class Unpinned_EscapeTheUnderfelProof
{
    [GroupProof("b15-underfel-cards", ProofKind.Cards)]
    internal static void Run()
    {
        Core2026Proof.Check(Cards.All.Any(c => c.Id == "TLC_446" && c.Implemented), "Escape the Underfel recipe missing");
        StandardSetsProof.Case("TLC_446", Unpinned_CommittedTemporaryPlays);
        Unpinned_ThrowIsNotDiscard();
        Unpinned_SeededBeastsAndCapacity();
        Unpinned_OncePerTurnCloneSnapshotRewind();
    }

    private static IPlayable TemporaryPlay(Game g, Controller p, string id)
    {
        p.UsedMana = 0;
        var card = Add(p, id); TemporaryRules.Make(card);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card, zonePosition: 0)), "Temporary play accepted");
        return card;
    }

    private static void Unpinned_CommittedTemporaryPlays(Game g, Controller p, string id)
    {
        TemporaryPlay(g, p, "GAME_005");
        var q = (Spell)Play(g, p, id);
        Play(g, p, "GAME_005");
        var expires = Add(p, "GAME_005"); TemporaryRules.Make(expires);
        var discarded = Add(p, "GAME_005"); TemporaryRules.Make(discarded); Generic.DiscardBlock(p, discarded);
        Summon(p, "CS2_231"); End(g);
        TemporaryPlay(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(q.QuestProgress == 0, "creation, discard, expiry, summon, prequest and enemy plays excluded");
        Play(g, p.Opponent, "EX1_287"); End(g);
        var countered = (Spell)TemporaryPlay(g, p, "GAME_005");
        Core2026Proof.Check(countered.IsCountered && q.QuestProgress == 1, "committed Temporary play counts even when countered");
        TemporaryPlay(g, p, "CS2_231"); TemporaryPlay(g, p, "CORE_REV_990");
        Core2026Proof.Check(q.QuestProgress == 3, "marker captured before hand exit across spell/minion/location");
        foreach (var branch in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = branch.Player1;
            for (int i = 0; i < 3; i++) TemporaryPlay(branch, owner, "GAME_005");
            Core2026Proof.Check(((Spell)branch.IdEntityDic[q.Id]).QuestProgress == 6 && owner.SecretZone.ModernQuests.Count == 0
                && owner.HandZone.Count(c => c.Card.Id == "TLC_446t") == 1, "six committed plays grant one reward after clone/load");
        }
        var full = QuestLifecycleProof.Fixture(); var fq = (Spell)Play(full, full.Player1, id);
        while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_231");
        QuestRules.Advance(fq, 6);
        Core2026Proof.Check(full.Player1.PendingQuestRewards.Count == 1, "full-hand reward waits");
        Generic.DiscardBlock(full.Player1, full.Player1.HandZone[0]);
        Core2026Proof.Check(full.Player1.HandZone.Count(c => c.Card.Id == "TLC_446t") == 1, "first free slot delivers Rift spell");
        foreach (string token in new[] { "TLC_446t", "TLC_446t1", "TLC_446t2", "TLC_446t3", "TLC_446t4" })
            CostSliceProof.Definition(token);
    }

    private static (Game Game, Minion Rift, IPlayable Hand) Fixture()
    {
        var g = QuestLifecycleProof.Fixture();
        Play(g, g.Player1, "TLC_446t");
        var rift = g.Player1.BoardZone.Single(m => m.Card.Id == "TLC_446t1");
        return (g, rift, Add(g.Player1, "CS2_231"));
    }

    private static PlayerTask Activation(Game g, int rift, int hand) => g.CurrentPlayer.Options()
        .Single(t => t is ActivateLocationTask && t.Source.Id == rift && t.ActionTarget?.Id == hand);

    private static void Unpinned_ThrowIsNotDiscard()
    {
        var (g, rift, hand) = Fixture(); var p = g.Player1;
        p.SetasideZone.Add(p.HandZone.Remove(hand));
        hand = Add(p, "CATA_499");
        int discards = 0; g.TriggerManager.DiscardTrigger += _ => discards++;
        int plays = p.PlayHistory.Count, deaths = g.NumMinionsKilledThisTurn, mana = p.UsedMana;
        TemporaryRules.Make(hand);
        Core2026Proof.Check(g.Process(Activation(g, rift.Id, hand.Id)), "Rift interaction accepted");
        Core2026Proof.Check(discards == 0 && hand[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME && !hand.ToBeDestroyed
            && p.PlayHistory.Count == plays && g.NumMinionsKilledThisTurn == deaths && p.UsedMana == mana,
            "throw removes privately without discard/destroy/play/payment");
        Core2026Proof.Check(p.BoardZone.Count(m => m[GameTag.DISPLAYED_CREATOR] == rift.Id) == 2,
            "only two Beasts summoned; discard listener adds nothing");
        Core2026Proof.Check(!TemporaryRules.IsTemporary(hand), "removed Temporary card cannot expire later");
        Generic.DiscardBlock(p, Add(p, "CATA_499"));
        Core2026Proof.Check(discards == 1 && p.BoardZone.SlotCount == 5, "same on-discard recipe summons for genuine discard");
    }

    private static void Unpinned_SeededBeastsAndCapacity()
    {
        foreach (int free in new[] { 0, 1, 2 })
        {
            var (g, rift, hand) = Fixture(); var p = g.Player1;
            while (p.BoardZone.FreeSpace > free) Summon(p, "CS2_182");
            g.Random = new Util.DeepCloneableRandom(81);
            var rng = g.Random.CaptureState();
            var options = p.Options().Where(t => t is ActivateLocationTask && t.Source.Id == rift.Id).ToArray();
            if (free == 0)
            {
                Core2026Proof.Check(options.Length == 0 && g.Random.CaptureState().Equals(rng) && hand.Zone == p.HandZone,
                    "full board unavailable without RNG/payment"); continue;
            }
            var branches = new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) };
            foreach (var b in branches)
            {
                Core2026Proof.Check(b.Process(Activation(b, rift.Id, hand.Id)), "seeded Rift activation");
                var outputs = b.Player1.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == rift.Id).Select(m => m.Card.Id).ToArray();
                Core2026Proof.Check(outputs.SequenceEqual(new[] { "TLC_446t4", "TLC_446t3" }.Take(free)),
                    "seed 81 ordered independent fixed-three picks: " + string.Join(",", outputs));
                Core2026Proof.Check(b.Random.DrawCount == free, "no pick for blocked summon slot");
            }
        }
        var (empty, er, eh) = Fixture(); empty.Player1.SetasideZone.Add(empty.Player1.HandZone.Remove(eh));
        Core2026Proof.Check(!empty.Player1.Options().Any(t => t.Source?.Id == er.Id), "empty hand unavailable");
        var (repeat, rr, rh) = Fixture(); repeat.Random = new Util.DeepCloneableRandom(5);
        Core2026Proof.Check(repeat.Process(Activation(repeat, rr.Id, rh.Id))
            && repeat.Player1.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == rr.Id)
                .Select(m => m.Card.Id).SequenceEqual(new[] { "TLC_446t2", "TLC_446t2" }),
            "seed 5 independent picks can repeat Felscreamer");
        Console.WriteLine("PASS Underfel fixed Fel Beast bucket=3; full/one/two slots, ordered seeded picks");
    }

    private static void Unpinned_OncePerTurnCloneSnapshotRewind()
    {
        var (g, rift, hand) = Fixture();
        var stale = Activation(g, rift.Id, hand.Id);
        Play(g, g.Player1, "GAME_005");
        var before = Canonical.State(g); var rng = g.Random.CaptureState();
        Core2026Proof.Check(!g.Process(stale) && Canonical.State(g) == before && g.Random.CaptureState().Equals(rng), "expired interaction changes nothing");
        var rewind = g.SaveSnapshot();
        Core2026Proof.Check(g.Process(Activation(g, rift.Id, hand.Id)), "first use succeeds");
        g.RestoreRewindBaseline(rewind);
        Core2026Proof.Check(g.Player1.HandZone.Any(c => c.Id == hand.Id) && Activation(g, rift.Id, hand.Id) != null,
            "Rewind restores thrown hand card and unused Rift");
        Core2026Proof.Check(g.Process(Activation(g, rift.Id, hand.Id)), "restored first use succeeds");
        foreach (var b in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var held = Add(b.Player1, "GAME_005");
            Core2026Proof.Check(!b.Player1.Options().Any(t => t.Source?.Id == rift.Id), "used Rift stays unavailable after clone/load");
            End(b);
            Core2026Proof.Check(!b.Player1.Options().Any(t => t.Source?.Id == rift.Id), "opponent cannot activate Rift");
            End(b);
            Core2026Proof.Check(b.Process(Activation(b, rift.Id, held.Id)), "owner turn start refreshes Rift");
            Core2026Proof.Check(b.Player1.Choice == null && !b.Resolution.HasSuspendedEffect, "interaction leaves no pending prompt");
        }
        var (silenced, sr, sh) = Fixture(); sr.Silence();
        Core2026Proof.Check(!silenced.Player1.Options().Any(t => t.Source?.Id == sr.Id), "Silence disables Rift interaction");
        var (left, lr, lh) = Fixture(); var old = Activation(left, lr.Id, lh.Id);
        left.Player1.SetasideZone.Add(left.Player1.BoardZone.Remove(lr));
        Core2026Proof.Check(!left.Process(old), "leave-play invalidates interaction");
        var (decision, dr, dh) = Fixture();
        var pending = decision.GetPendingDecisions().Single();
        var option = pending.Options.Single(o => o.EntityId == dr.Id && o.TargetId == dh.Id);
        Core2026Proof.Check(decision.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { option.OptionId }), out _)
            && decision.IdEntityDic[dh.Id][GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME,
            "pending-decision answer rebinds minion source and hand target");
    }
}
