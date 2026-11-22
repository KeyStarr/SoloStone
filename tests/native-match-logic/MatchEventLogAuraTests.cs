using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// An aura's stats reach the client through the aura's attachment, not as a stat tag change, so the event log meets them only at
// its batch-end check. These checks pin what it may call an aura's stat change (STATS by=aura) and what stays drift (RESYNC).
internal static partial class Program
{
    private sealed class AuraLogBench
    {
        internal readonly Game Game;
        internal readonly MatchEventLog Log;
        internal readonly List<string> Lines = new List<string>();
        private int mark;

        internal AuraLogBench()
        {
            Game = new Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true,
                Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            Game.StartGame();
            foreach (var player in new[] { Game.Player1, Game.Player2 })
                foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            Game.Player1.BaseMana = 10;
            Log = new MatchEventLog("auralog0", Lines.Add);
            Observe();
            Lines.Clear();
        }

        internal void Observe()
        {
            var entries = Game.PowerHistory.Full.Skip(mark).ToList();
            mark = Game.PowerHistory.Full.Count;
            if (entries.Count > 0) Log.Observe(Game, entries);
        }

        internal IPlayable Play(string cardId, IPlayable target = null, bool observe = true)
        {
            var player = Game.Player1;
            player.UsedMana = 0;
            var card = Generic.DrawCard(player, Cards.FromId(cardId));
            Check(Game.Process(PlayCardTask.Any(player, card, (ICharacter)target)), "play " + cardId);
            if (observe) Observe();
            return card;
        }

        internal IPlayable OnBoard(string cardId)
        {
            var minion = Entity.FromCard(Game.Player1, Cards.FromId(cardId), zone: Game.Player1.BoardZone);
            Observe();
            return minion;
        }

        // A batch of history entries the engine never wrote, observed as if it had.
        internal void Synthetic(params IPowerHistoryEntry[] entries) => Log.Observe(Game, entries.ToList());

        internal string Dump() => "\n  " + string.Join("\n  ", Lines);
        internal string[] About(int id, string kind) => Lines.Where(l => l.Contains(" " + kind + " ") && l.Contains("#" + id)).ToArray();
    }

    private static void MatchEventLogAuraChecks()
    {
        AuraGainAndLossAreStats();
        EnchantedDriftStaysResync();
        PrivateCorrectionNotApplied();
        RecoverForgetsAuraStats();
        DriftUnderLiveAuraStaysResync();
        PartlyExplainedAuraChangeStaysResync();
        StaleAuraPartDoesNotSurviveABounce();
        EnchantedMinionKeepsItsAuraPart();
        LostHealthAuraSplitIsTaken();
        UnrevealedCorrectionNotApplied();
        Console.WriteLine("PASS match event log auras: aura gain and loss are STATS by=aura, other drift stays RESYNC (also under a live aura"
            + " and when partly explained), no stale aura part, private corrections are skipped, a fault forgets aura state");
    }

    // A Yeti beside Flametongue Totem: the +2 Attack arrives without a stat tag change, and so does its end. Both are an aura's
    // stat change.
    private static void AuraGainAndLossAreStats()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        var leader = bench.Play("EX1_565");
        bench.Play("CS2_189", yeti);   // Elven Archer: a hit, so the batch checks the Yeti's stats
        var gained = bench.About(yeti.Id, "STATS");
        Check(gained.Any(l => l.Contains("stats=4/4->6/4") && l.Contains("by=aura")), "Flametongue's +2 Attack is STATS by=aura:" + bench.Dump());
        bench.Play("CS2_029", leader);   // Fireball on the Totem: the aura ends; the Yeti is not in that batch
        bench.Play("CS2_189", yeti);
        var lost = bench.About(yeti.Id, "STATS");
        Check(lost.Any(l => l.Contains("stats=6/3->4/3") && l.Contains("by=aura")), "the aura's end is STATS by=aura:" + bench.Dump());
        Check(!bench.Lines.Any(l => l.Contains(" RESYNC ")), "an aura's stats are no drift:" + bench.Dump());
    }

    // An enchantment's stats reach the client as tag changes, so an enchanted minion has no excuse: a difference is drift.
    private static void EnchantedDriftStaysResync()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        bench.Play("CS2_092", yeti);   // Blessing of Kings
        Check(((Minion)yeti).AppliedEnchantments?.Count > 0, "the Yeti is enchanted");
        bench.Lines.Clear();
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ATK, 99));
        Check(bench.About(yeti.Id, "RESYNC").Length == 1, "a drift on an enchanted minion is RESYNC:" + bench.Dump());
        Check(!bench.Lines.Any(l => l.Contains("by=aura") || l.Contains("by=enchantment")), "no aura or enchantment excuse:" + bench.Dump());
    }

    // A correction that needs a public entity reaches the client only for one in play or in the human's hand; the log follows
    // exactly what the client is sent, so an opponent's hand card gets no line from one.
    private static void PrivateCorrectionNotApplied()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        var hidden = Generic.DrawCard(bench.Game.Player2, Cards.FromId("CS2_182"));
        bench.Observe();
        bench.Lines.Clear();
        // Made-up values (the engine keeps 4 Attack), so a STATS line can only come from the correction.
        bench.Log.Corrections = (entries, index) => index != entries.Count ? Array.Empty<MatchEventLog.TransportCorrection>() : new[] {
            new MatchEventLog.TransportCorrection(hidden.Id, new Dictionary<GameTag, int> { [GameTag.ARMOR] = 7 }, true),   // ARMOR is logged in any zone
            new MatchEventLog.TransportCorrection(yeti.Id, new Dictionary<GameTag, int> { [GameTag.ATK] = 6 }, true) };
        bench.Synthetic(PowerHistoryBuilder.TagChange(bench.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_ACTION));
        bench.Log.Corrections = null;
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("stats=4/5->6/5")), "a public correction is followed:" + bench.Dump());
        Check(!bench.Lines.Any(l => l.Contains("#" + hidden.Id)), "an opponent's hand card gets no line:" + bench.Dump());
    }

    // A fault forgets the tracked state; it must forget that a minion's stats held an aura too, or later drift reads as one.
    private static void RecoverForgetsAuraStats()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        var leader = bench.Play("EX1_565");
        bench.Play("CS2_189", yeti);
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("by=aura")), "the Yeti's stats held the aura:" + bench.Dump());
        bench.Log.TestFault = _ => { bench.Log.TestFault = null; throw new InvalidOperationException("injected"); };
        bench.Play("CS2_029", leader);
        Check(bench.Lines.Any(l => l.Contains("EVENT_LOG_ERROR")) && !bench.Log.Disabled, "the fault was taken and the log stays on:" + bench.Dump());
        bench.Lines.Clear();
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ATK, 99));
        Check(bench.About(yeti.Id, "RESYNC").Length == 1 && !bench.Lines.Any(l => l.Contains("by=aura")),
            "after the fault, a drift on the Yeti is RESYNC, not an aura:" + bench.Dump());
    }

    // A live aura excuses only its own change: a made-up Attack on a minion under Flametongue is drift.
    private static void DriftUnderLiveAuraStaysResync()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        bench.Play("EX1_565");
        bench.Play("CS2_189", yeti);
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("stats=4/4->6/4") && l.Contains("by=aura")), "the aura is tracked first:" + bench.Dump());
        bench.Lines.Clear();
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ATK, 99));
        Check(bench.About(yeti.Id, "RESYNC").Length == 1 && !bench.Lines.Any(l => l.Contains("by=aura")),
            "a drift under a live aura is RESYNC:" + bench.Dump());
    }

    // The aura's +2 Attack arrives together with tracker drift (the tracker is 1 Attack off the engine's base): the aura explains
    // only part of the difference, so it is RESYNC.
    private static void PartlyExplainedAuraChangeStaysResync()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        bench.Play("EX1_565", observe: false);   // the aura is in the engine, not yet in the log
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ATK, 5));   // base 4 in the engine
        Check(bench.About(yeti.Id, "RESYNC").Length == 1 && !bench.Lines.Any(l => l.Contains("RESYNC") && l.Contains("by=aura")),
            "an aura change plus drift is RESYNC:" + bench.Dump());
    }

    // A bounce to hand and back must not carry the aura part along: the log's own stats come from the printed card when the batch
    // changes them again, and an aura's part is not in those.
    private static void StaleAuraPartDoesNotSurviveABounce()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        bench.Play("EX1_565");
        bench.Play("CS2_189", yeti);
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("by=aura")), "the Yeti's stats held the aura:" + bench.Dump());
        bench.Lines.Clear();
        // Out of play and back in the same breath (the engine's Yeti never moved): stats come from the printed card each time.
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ZONE, (int)Zone.HAND), PowerHistoryBuilder.TagChange(yeti.Id, GameTag.DAMAGE, 1));
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ZONE, (int)Zone.PLAY), PowerHistoryBuilder.TagChange(yeti.Id, GameTag.DAMAGE, 1));
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("stats=4/4->6/4") && l.Contains("by=aura")) && !bench.Lines.Any(l => l.Contains(" RESYNC ")),
            "the live aura is explained from the printed stats:" + bench.Dump());
        bench.Lines.Clear();
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.ATK, 99));
        Check(bench.About(yeti.Id, "RESYNC").Length == 1, "a drift after the bounce is RESYNC:" + bench.Dump());
    }

    // Blessing of Kings writes the Yeti's Attack as the engine's entity[ATK], which includes Flametongue's +2: the log keeps the
    // aura part, so the aura's end in the same batch is still the aura's change, not drift.
    private static void EnchantedMinionKeepsItsAuraPart()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        var leader = bench.Play("EX1_565");
        bench.Play("CS2_189", yeti);
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("by=aura")), "the Yeti's stats held the aura:" + bench.Dump());
        bench.Lines.Clear();
        bench.Play("CS2_092", yeti, observe: false);   // Blessing of Kings
        bench.Play("CS2_029", leader, observe: false);   // Fireball on the Totem: the aura ends in the same batch
        bench.Observe();
        Check(!bench.Lines.Any(l => l.Contains(" RESYNC ")), "an aura's end after an enchantment is no drift:" + bench.Dump());
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("by=aura")), "the aura's end is STATS by=aura:" + bench.Dump());
    }

    // Attack and current Health match the engine, but the max Health / damage split does not (a Health aura lost with its clamp,
    // here made up with tags): the log takes the live split, so the next aura change is explained from the right max Health.
    private static void LostHealthAuraSplitIsTaken()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        bench.Play("CS2_008", yeti);   // Moonfire: 1 damage, 4/4 of 5
        bench.Synthetic(PowerHistoryBuilder.TagChange(yeti.Id, GameTag.HEALTH, 6), PowerHistoryBuilder.TagChange(yeti.Id, GameTag.DAMAGE, 2));
        bench.Lines.Clear();
        bench.Play("EX1_565");
        bench.Play("CS2_008", yeti);
        Check(!bench.Lines.Any(l => l.Contains(" RESYNC ")) && bench.About(yeti.Id, "STATS").Any(l => l.Contains("by=aura")),
            "a stale max Health split must not turn the next aura change into drift:" + bench.Dump());
    }

    // The log follows a correction only for an entity the transport has revealed (NativeMatch hands it that through IsRevealed).
    private static void UnrevealedCorrectionNotApplied()
    {
        foreach (bool revealed in new[] { false, true })
        {
            var bench = new AuraLogBench();
            var yeti = bench.OnBoard("CS2_182");
            bench.Lines.Clear();
            bench.Log.IsRevealed = _ => revealed;
            bench.Log.Corrections = (entries, index) => index != entries.Count ? Array.Empty<MatchEventLog.TransportCorrection>() : new[] {
                new MatchEventLog.TransportCorrection(yeti.Id, new Dictionary<GameTag, int> { [GameTag.ARMOR] = 3 }, true) };
            bench.Synthetic(PowerHistoryBuilder.TagChange(bench.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_ACTION));
            Check(bench.About(yeti.Id, "ARMOR").Length == (revealed ? 1 : 0), "a correction follows only a revealed entity (revealed=" + revealed + "):" + bench.Dump());
        }
    }
}
