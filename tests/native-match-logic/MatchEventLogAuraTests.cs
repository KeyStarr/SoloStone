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

        internal IPlayable Play(string cardId, IPlayable target = null)
        {
            var player = Game.Player1;
            player.UsedMana = 0;
            var card = Generic.DrawCard(player, Cards.FromId(cardId));
            Check(Game.Process(PlayCardTask.Any(player, card, (ICharacter)target)), "play " + cardId);
            Observe();
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
        Console.WriteLine("PASS match event log auras: aura gain and loss are STATS by=aura, other drift stays RESYNC, private corrections"
            + " are skipped, a fault forgets aura state");
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
    // exactly what the client is sent, so it never learns a value from the opponent's hand.
    private static void PrivateCorrectionNotApplied()
    {
        var bench = new AuraLogBench();
        var yeti = bench.OnBoard("CS2_182");
        var hidden = Generic.DrawCard(bench.Game.Player2, Cards.FromId("CS2_182"));
        bench.Observe();
        bench.Lines.Clear();
        // Made-up values (the engine keeps 4 Attack), so a STATS line can only come from the correction.
        bench.Log.Corrections = (entries, index) => index != entries.Count ? Array.Empty<MatchEventLog.TransportCorrection>() : new[] {
            new MatchEventLog.TransportCorrection(hidden.Id, new Dictionary<GameTag, int> { [GameTag.ATK] = 7 }, true),
            new MatchEventLog.TransportCorrection(yeti.Id, new Dictionary<GameTag, int> { [GameTag.ATK] = 6 }, true) };
        bench.Synthetic(PowerHistoryBuilder.TagChange(bench.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_ACTION));
        bench.Log.Corrections = null;
        Check(bench.About(yeti.Id, "STATS").Any(l => l.Contains("stats=4/5->6/5")), "a public correction is followed:" + bench.Dump());
        Check(!bench.Lines.Any(l => l.Contains("#" + hidden.Id)), "an opponent's hand card learns nothing:" + bench.Dump());
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
}
