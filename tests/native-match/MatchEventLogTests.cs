using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

// Match event log (owner rule 2026-10-31): after a Deck report (match ead250ca, turn 22) nobody could tell which spell each
// Bashana Treant cast, at what target, what it drew or burned. The log is decoded from the engine's history stream.
internal static partial class Program
{
    private static NativeMatch StartLoggedMatch(Capture capture, List<string> lines, int seed = 1337)
    {
        var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.DRUID),
            Enumerable.Repeat(Cards.FromId("CS2_182"), 30), seed);
        match.MatchId = "ead250ca-0000-4000-8000-000000000001";
        match.EventTrace = lines.Add;
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        return match;
    }

    private static string[] EventLines(List<string> lines, string kind, params string[] contains) =>
        lines.Where(l => Regex.IsMatch(l, @" s=\S+ " + kind + @"( |$)") && contains.All(l.Contains)).ToArray();

    private static void EventCheck(List<string> lines, string kind, string description, params string[] contains)
    {
        if (EventLines(lines, kind, contains).Length == 0)
            throw new InvalidOperationException("TEST: no " + kind + " line (" + description + ") containing [" + string.Join("] [", contains)
                + "] in:\n" + string.Join("\n", lines.TakeLast(60)));
    }

    private static void ClearHand(Controller player)
    {
        foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
    }

    private static void MatchEventLogNative()
    {
        BashanaTreantsNamedInLog();
        ScriptedMarkDrawBurnPosition();
        EffectKindSweep();
        EnchantedCopyCreatedWithItsStats();
        EventLogIsPassiveAndBounded();
        Console.WriteLine("PASS match event log: Bashana carves, each Treant's spell + target + draws + burn, effect-kind sweep, passive and bounded");
    }

    // The owner's case: a real Bashana, then each carved Treant played. Every carved spell is named when carved and when cast.
    private static void BashanaTreantsNamedInLog()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines);
        var game = match.Game; var player = game.Player1;
        ClearHand(player);
        FlushHistory(match);
        player.BaseMana = 10; player.UsedMana = 0;
        EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        var bashana = Generic.DrawCard(player, Cards.FromId("MEND_046"));
        lines.Clear();
        Check(game.Process(PlayCardTask.Any(player, bashana)), "Bashana play");
        FlushHistory(match);
        var treants = player.HandZone.Where(c => c.Card.Id == "MEND_046t").ToArray();
        Check(treants.Length == 3, "three Treants carved");
        foreach (var treant in treants)
        {
            var carved = PayloadRules.Entries(treant, 1).Single();
            EventCheck(lines, "PAYLOAD", "carve of " + treant.Id, "owner=MEND_046t#" + treant.Id, "card=" + carved.Card.Id + "#" + carved.Id);
            EventCheck(lines, "CREATE", "Treant creation", "card=MEND_046t#" + treant.Id, "creator=MEND_046#" + bashana.Id);
        }
        Check(lines.Any(l => (l.Contains(" PAYLOAD ") || l.Contains(" CREATE ")) && l.Contains("rng=")), "the random carve reports the draws that picked it:\n" + string.Join("\n", lines));
        foreach (var treant in treants)
        {
            string carvedId = PayloadRules.Entries(treant, 1).Single().Card.Id;
            lines.Clear();
            player.UsedMana = 0;
            Check(game.Process(PlayCardTask.Any(player, treant)), "Treant play");
            FlushHistory(match);
            EventCheck(lines, "PLAY", "Treant played", "card=MEND_046t#" + treant.Id);
            EventCheck(lines, "CAST", "carved spell cast by the Treant", "card=" + carvedId + "#", "kind=effect", "by=MEND_046t#" + treant.Id);
        }
    }

    // Deck match ead250ca turn 22, scripted: one Treant carries Mark of the Wild next to an enemy Taunt, one carries Arcane Intellect
    // with a nearly full hand. The log must name each spell, its target, the stat change, both draws and the burn.
    private static void ScriptedMarkDrawBurnPosition()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines);
        var game = match.Game; var player = game.Player1;
        ClearHand(player);
        player.BaseMana = 10; player.UsedMana = 0;
        var taunt = EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        var drawTreant = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carvedDraw = PayloadRules.StoreCard(drawTreant, 1, Cards.FromId("CS2_023"));
        drawTreant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carvedDraw.Id;
        var markTreant = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carvedMark = PayloadRules.StoreCard(markTreant, 1, Cards.FromId("CS2_009"));
        markTreant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carvedMark.Id;
        for (int i = 0; i < 8; i++) EngineEntity.FromCard(player, Cards.FromId("CS2_122"), zone: player.HandZone);
        Check(player.HandZone.Count == 10, "full hand");
        FlushHistory(match);
        lines.Clear();
        int deckBefore = player.DeckZone.Count;

        Check(game.Process(PlayCardTask.Any(player, drawTreant)), "draw Treant play");
        FlushHistory(match);
        EventCheck(lines, "CAST", "Arcane Intellect named", "card=CS2_023#", "by=MEND_046t#" + drawTreant.Id, "kind=effect");
        var drawLine = EventLines(lines, "DRAW", "hand=10", "by=CS2_023#").Single();
        var burnLine = EventLines(lines, "BURN", "reason=hand-full-draw", "from=DECK", "by=CS2_023#").Single();
        int drawnId = int.Parse(Regex.Match(drawLine, @"card=CS2_182#(\d+)").Groups[1].Value);
        int burnedId = int.Parse(Regex.Match(burnLine, @"card=CS2_182#(\d+)").Groups[1].Value);
        Check(player.HandZone.Any(c => c.Id == drawnId) && player.GraveyardZone.Any(c => c.Id == burnedId) && drawnId != burnedId,
            "the logged draw is in the hand and the logged burn in the graveyard");
        Check(player.DeckZone.Count == deckBefore - 2, "both draws left the deck");

        lines.Clear(); player.UsedMana = 0;
        Check(game.Process(PlayCardTask.Any(player, markTreant)), "mark Treant play");
        FlushHistory(match);
        var cast = EventLines(lines, "CAST", "card=CS2_009#", "by=MEND_046t#" + markTreant.Id, "pick=auto").Single();
        var target = Regex.Match(cast, @"target=\S+?#(\d+)\(").Groups[1].Value;
        Check(target == taunt.Id.ToString() || target == markTreant.Id.ToString(), "Mark of the Wild picked one of the two legal minions: " + cast);
        EventCheck(lines, "ENCHANT", "Mark of the Wild buff", "target=", "#" + target + "(", "ench=CS2_009e#");
        EventCheck(lines, "STATS", "stat change result", "#" + target + "(", "->", "by=CS2_009#");
        Check(cast.Contains("random=yes") && cast.Contains("rng="), "the automatic cast reports that its target was random: " + cast);
    }

    // One position, many effect kinds: each must leave a line naming the source, the card and the result.
    // Match 229c46b1: Specter Specialist copied a buffed Egg of Khelos. The copy's enchantments change its stats with no tag change.
    private static void EnchantedCopyCreatedWithItsStats()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines, 4243);
        var game = match.Game; var player = game.Player1;
        ClearHand(player);
        player.BaseMana = 10; player.UsedMana = 0;
        var yeti = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        yeti[GameTag.REBORN] = 1;
        var shield = EngineEntity.FromCard(player, Cards.FromId("CS2_004"), zone: player.HandZone);
        var specter = EngineEntity.FromCard(player, Cards.FromId("CAP_804"), zone: player.HandZone);
        FlushHistory(match);
        Check(game.Process(PlayCardTask.Any(player, shield, yeti)), "Power Word: Shield play");
        FlushHistory(match);
        lines.Clear();
        Check(game.Process(PlayCardTask.Any(player, specter, yeti)), "Specter Specialist play");
        FlushHistory(match);
        var copy = (Minion)player.BoardZone.Single(minion => minion != yeti && minion.Card.Id == "CS2_182");
        copy.PeekStats(out int atk, out int maxHealth, out int damage);
        Check(atk == 4 && maxHealth - damage == 7, "the copy carries the buff in the engine");
        EventCheck(lines, "CREATE", "copy created with its real stats", "CS2_182#" + copy.Id, "stats=4/7");
        Check(EventLines(lines, "RESYNC").Length == 0, "no RESYNC for an enchanted copy:\n" + string.Join("\n", lines));
    }

    private static void EffectKindSweep()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines, 4242);
        var game = match.Game; var player = game.Player1; var enemy = game.Player2;
        ClearHand(player);
        player.BaseMana = 10; player.UsedMana = 0;
        var senjin = EngineEntity.FromCard(enemy, Cards.FromId("CS2_179"), zone: enemy.BoardZone);
        var yeti = EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        var fireball = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        var polymorph = EngineEntity.FromCard(player, Cards.FromId("CS2_022"), zone: player.HandZone);
        var control = EngineEntity.FromCard(player, Cards.FromId("CS1_113"), zone: player.HandZone);
        var succubus = EngineEntity.FromCard(player, Cards.FromId("EX1_306"), zone: player.HandZone);
        var filler = EngineEntity.FromCard(player, Cards.FromId("CS2_122"), zone: player.HandZone);
        FlushHistory(match);
        lines.Clear();
        void Play(IPlayable card, IPlayable target = null)
        {
            player.UsedMana = 0;
            Check(game.Process(target == null ? PlayCardTask.Any(player, card) : PlayCardTask.Any(player, card, (ICharacter)target)), "play " + card.Card.Id);
            FlushHistory(match);
        }

        Play(fireball, senjin);
        EventCheck(lines, "CAST", "hand cast with a chosen target", "kind=player", "card=CS2_029#" + fireball.Id, "target=CS2_179#" + senjin.Id, "pick=chosen");
        EventCheck(lines, "DAMAGE", "spell damage result", "target=CS2_179#" + senjin.Id, "amount=6", "by=CS2_029#" + fireball.Id);
        EventCheck(lines, "DEATH", "minion death", "card=CS2_179#" + senjin.Id);

        Play(polymorph, yeti);
        EventCheck(lines, "TRANSFORM", "polymorph", "from=CS2_182#" + yeti.Id, "by=CS2_022#" + polymorph.Id);

        Play(control, yeti);
        EventCheck(lines, "CONTROL", "mind control", "to=1", "by=CS1_113#" + control.Id);

        Play(succubus);
        EventCheck(lines, "DISCARD", "Succubus discards", "card=CS2_122#" + filler.Id);

        // Shuffle into deck, mill, Herald soldier, Colossal: direct engine effects, still decoded from history.
        var shuffled = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: null);
        Generic.ShuffleIntoDeck(player, player.Hero, shuffled);
        FlushHistory(match);
        EventCheck(lines, "SHUFFLE_INTO_DECK", "shuffle", "card=CS2_029#" + shuffled.Id);
        var milled = player.DeckZone.First(c => c.Id != shuffled.Id);
        Generic.RemoveFromZone(player, milled); player.GraveyardZone.Add(milled);
        FlushHistory(match);
        EventCheck(lines, "MILL", "mill", "card=" + milled.Card.Id + "#" + milled.Id);

        var herald = EngineEntity.FromCard(player, Cards.FromId("CS2_122"), zone: player.HandZone);
        player[HeraldRules.Profile] = (int)CardClass.WARRIOR;
        HeraldRules.Resolve(player, herald);
        FlushHistory(match);
        EventCheck(lines, "CREATE", "herald soldier", "card=CATA_580t#", "herald=soldier", "count=", "tier=");
        var body = EngineEntity.FromCard(player, Cards.FromId("TSC_950"), zone: player.HandZone);
        player.BaseMana = 10; player.UsedMana = 0;
        Play(body);
        EventCheck(lines, "COLOSSAL", "colossal body", "body=TSC_950#" + body.Id, "appendages=[TSC_950t, TSC_950t2]");
        EventCheck(lines, "CREATE", "colossal appendage", "card=TSC_950t#", "by=TSC_950#" + body.Id);

        // Every line carries match id, turn, sequence and seat; the sequence only grows.
        string shape = @"^m=ead250ca t=\d+ n=\d+ s=[-12] [A-Z_]+( |$)";
        Check(lines.All(l => Regex.IsMatch(l, shape)), "every line has match, turn, sequence, seat: " + lines.FirstOrDefault(l => !Regex.IsMatch(l, shape)));
        var numbers = lines.Select(l => int.Parse(Regex.Match(l, @" n=(\d+) ").Groups[1].Value)).ToArray();
        Check(numbers.Zip(numbers.Skip(1), (a, b) => b > a).All(x => x), "sequence strictly increases");
    }

    // The log reads, never writes: same seed, same moves, with and without a sink => identical RNG, entity ids and fingerprint.
    // It is also bounded per turn with an explicit marker.
    private static void EventLogIsPassiveAndBounded()
    {
        string Run(bool logged)
        {
            var capture = new Capture(); var lines = new List<string>();
            var match = StartLoggedMatch(capture, lines, 99);
            if (!logged) match.EventTrace = null;
            var game = match.Game; var player = game.Player1;
            ClearHand(player); player.BaseMana = 10; player.UsedMana = 0;
            var bashana = Generic.DrawCard(player, Cards.FromId("MEND_046"));
            Check(game.Process(PlayCardTask.Any(player, bashana)), "Bashana play (passive run)");
            FlushHistory(match);
            foreach (var treant in player.HandZone.Where(c => c.Card.Id == "MEND_046t").ToArray())
            {
                player.UsedMana = 0; game.Process(PlayCardTask.Any(player, treant)); FlushHistory(match);
            }
            return MatchRecorder.RandomState(game) + "|" + MatchRecorder.Fingerprint(game) + "|" + game.IdEntityDic.Count;
        }
        Check(Run(true) == Run(false), "event log changes no RNG state, entity id or game fingerprint");

        // Per-turn cap: past it lines are dropped with one explicit marker, then counted when the next turn starts.
        var capture2 = new Capture(); var ignored = new List<string>();
        var match2 = StartLoggedMatch(capture2, ignored, 7);
        var game2 = match2.Game; var player2 = game2.Player1;
        ClearHand(player2); player2.BaseMana = 10; player2.UsedMana = 0;
        var capped = new List<string>();
        var cappedLog = new MatchEventLog("cap00000-0000", capped.Add, maxLinesPerTurn: 6);
        for (int i = 0; i < 10; i++) EngineEntity.FromCard(player2, Cards.FromId("CS2_122"), zone: player2.HandZone);
        cappedLog.Observe(game2, game2.PowerHistory.Full);
        Check(capped.Count(l => l.Contains(" EVENTS_TRUNCATED ")) >= 1 && capped.First(l => l.Contains(" EVENTS_TRUNCATED ")).Contains("cap=6"),
            "overflow is marked with the cap: " + string.Join(" | ", capped.Take(10)));
        Check(capped.Count <= 8, "overflowing lines are dropped, not emitted: " + capped.Count);
    }
}
