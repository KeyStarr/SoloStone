using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// FastFinish (plugin/Npc/FastFinish.cs): search-free turn completion once the live turn budget is exhausted.
public static class FastFinishTests
{
    private static BotAction[] Legal(Game g) => g.Player1.Options().Select(BotAction.From).ToArray();
    private static BotAction Pick(Game g, out string kind) => FastFinish.Choose(View(g), Legal(g), out kind);
    private static Minion Stats(Minion m, int attack, int health)
    {
        m[GameTag.ATK] = attack;
        m.Damage = Math.Max(0, m.BaseHealth - health);
        return m;
    }

    public static void TestFastFinishNoTauntGoesFace()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
        var croc = Board(g, true, "CS2_120", true);
        Board(g, false, "CS2_182");   // 4/5 Yeti: a 2/3 neither kills nor survives profitably
        var action = Pick(g, out string kind);
        Check(kind == FastFinish.Attack && action.Source == croc.Id && action.Target == g.Player2.Hero.Id, "no Taunt, no good trade: face, got " + action);
    }

    public static void TestFastFinishTauntMustBeHit()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
        var yeti = Board(g, true, "CS2_182", true);
        var taunt = Board(g, false, "CS2_179");   // Sen'jin 3/5 Taunt: Yeti survives the chip
        Board(g, false, "CS2_120");
        var action = Pick(g, out string kind);
        Check(kind == FastFinish.Attack && action.Source == yeti.Id && action.Target == taunt.Id, "Taunt must be hit, got " + action);
        // A Taunt the attacker can neither survive nor clear is not suicided into.
        var h = Fixture(); h.Turn = 9; h.Player1.UsedMana = 10;
        Stats(Board(h, true, "CS2_120", true), 2, 1);
        Board(h, false, "CS2_179");
        Check(Pick(h, out string none) == null && none == FastFinish.None, "hopeless Taunt swing must be skipped");
        // Ready attackers that together clear the Taunt both swing.
        var k = Fixture(); k.Turn = 9; k.Player1.UsedMana = 10;
        Stats(Board(k, true, "CS2_120", true), 3, 1); Stats(Board(k, true, "CS2_120", true), 3, 1);
        var wall = Board(k, false, "CS2_179");
        var first = Pick(k, out string clear);
        Check(clear == FastFinish.Attack && first.Target == wall.Id, "attackers that clear the Taunt together must hit it");
    }

    public static void TestFastFinishTakesFavourableTrade()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
        var yeti = Board(g, true, "CS2_182", true);
        var croc = Board(g, false, "CS2_120");    // 4/5 kills a 2/3 and survives
        var action = Pick(g, out string kind);
        Check(kind == FastFinish.Attack && action.Source == yeti.Id && action.Target == croc.Id, "favourable trade first, got " + action);
        // An even trade into a higher-value minion is taken too; into a cheaper one it is not (face instead).
        var h = Fixture(); h.Turn = 9; h.Player1.UsedMana = 10;
        var small = Stats(Board(h, true, "CS2_120", true), 2, 2);
        var big = Stats(Board(h, false, "CS2_182"), 4, 2);
        var even = Pick(h, out _);
        Check(even.Source == small.Id && even.Target == big.Id, "kill a higher-value minion, got " + even);
        var j = Fixture(); j.Turn = 9; j.Player1.UsedMana = 10;
        Stats(Board(j, true, "CS2_182", true), 2, 2);
        Stats(Board(j, false, "CS2_120"), 2, 2);
        Check(Pick(j, out _).Target == j.Player2.Hero.Id, "even trade into a cheaper minion: face");
    }

    public static void TestFastFinishNothingUsefulEndsTurn()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
        Board(g, true, "CS2_182");                // exhausted
        Board(g, false, "CS2_120");
        Check(Pick(g, out string kind) == null && kind == FastFinish.None, "nothing useful: END_TURN (null, none)");
        // A pending choice is never answered by fast finish.
        Check(FastFinish.Choose(View(g), new[] { BotAction.From(ChooseTask.Pick(g.Player1, g.Player1.Hero.Id)) }, out string choice) == null
            && choice == null, "pending choice is left to the caller");
    }

    public static void TestFastFinishSpendsObviousMana()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 5;   // 5 mana left
        var yeti = Entity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, Cards.FromId("CS2_120"), zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, Cards.FromId("CS2_029"), zone: g.Player1.HandZone);   // Fireball: spells are skipped
        var action = Pick(g, out string kind);
        Check(kind == FastFinish.Mana && action.Type == PlayerTaskType.PLAY_CARD && action.Source == yeti.Id, "highest-cost minion first, got " + action);
        // Mana left only for the hero power (Warrior Armor Up, untargeted): it is used.
        var h = Fixture(); h.Turn = 9; h.Player1.UsedMana = 8;
        var power = Pick(h, out string spent);
        Check(spent == FastFinish.Mana && power.Type == PlayerTaskType.HERO_POWER, "untargeted hero power, got " + power);
    }

    public static void TestFastFinishIsDeterministic()
    {
        string Line()
        {
            var g = Fixture(); g.Turn = 11; g.Player1.UsedMana = 4;
            Stats(Board(g, true, "CS2_120", true), 2, 2); Board(g, true, "CS2_182", true); Board(g, true, "CS2_120", true);
            Board(g, false, "CS2_120"); Stats(Board(g, false, "CS2_182"), 4, 2);
            Entity.FromCard(g.Player1, Cards.FromId("CS2_120"), zone: g.Player1.HandZone);
            return string.Join(";", Complete(g).Select(s => s.Kind + "=" + s.Action));
        }
        string first = Line(), second = Line();
        Check(first == second && first.Length > 0, "same position twice must give the same line: " + first + " vs " + second);
    }

    // Apply fast finish decisions on the authoritative game, a fresh fair view per decision (as live), until none.
    private static List<(string Kind, BotAction Action)> Complete(Game g)
    {
        var line = new List<(string, BotAction)>();
        for (int i = 0; i < 30; i++)
        {
            var action = Pick(g, out string kind);
            if (action == null) return line;
            line.Add((kind, action));
            Check(g.Process(action.Resolve(g.Player1.Options())), "fast finish action rejected: " + action);
        }
        throw new Exception("fast finish did not terminate");
    }

    // Live incident 2026-11-01 (Steam Deck, Mage NPC Biscuitzz, match 347aafe4, journal #144): turn 27, NPC at 12 HP with
    // three ready Malignant Horrors 2/2, 7 mana unspent (journal mana=USED/BASE 3/10), every line valued about -1M. After
    // three clock-interrupted searches the clock-cut fallback ended the turn with nodes=0. Board/hand from #143 `after:`.
    public static void TestFastFinishIncidentBiscuitTurn27()
    {
        var g = Fixture(CardClass.MAGE, CardClass.MAGE); g.Turn = 27;
        var npc = g.Player1; var human = g.Player2;
        npc.Hero.Damage = 18; npc.BaseMana = 10; npc.UsedMana = 3;
        var horrors = Enumerable.Range(0, 3).Select(_ => Stats(Board(g, true, "CORE_RLK_745", true), 2, 2)).ToArray();
        Stats(Board(g, true, "CATA_487"), 3, 4);
        Stats(Board(g, true, "CORE_BT_701"), 3, 1);
        foreach (var id in new[] { "TLC_334", "EDR_856", "CATA_484", "CATA_485", "SW_108t" })
            Entity.FromCard(npc, Cards.FromId(id), zone: npc.HandZone);
        var enemy = new[] { ("CS2_033", 3, 1), ("TIME_EVENT_998", 5, 3), ("END_031", 3, 3), ("CATA_304", 3, 8), ("EDR_890", 4, 2),
            ("TIME_036", 2, 4), ("TIME_861", 3, 3) }.Select(m => Stats(Board(g, false, m.Item1), m.Item2, m.Item3)).ToArray();
        for (int i = 0; i < 10; i++) Entity.FromCard(human, Cards.FromId("CS2_182"), zone: human.HandZone);
        Check(npc.RemainingMana == 7 && horrors.All(h => h.CanAttack) && npc.Hero.Health == 12, "incident fixture");

        var line = Complete(g);
        string text = string.Join(";", line.Select(s => s.Kind + "=" + s.Action));
        var attacked = new HashSet<int>(line.Where(s => s.Action.Type == PlayerTaskType.MINION_ATTACK).Select(s => s.Action.Source));
        Check(horrors.All(h => attacked.Contains(h.Id)), "all three ready 2/2s must attack before END_TURN: " + text);
        // The two even trades into higher-value minions (Nightmare Dragonkin 4/2, Water Elemental 3/1), the last 2/2 face.
        Check(line[0].Action.Target == enemy[4].Id && line[1].Action.Target == enemy[0].Id && line[2].Action.Target == human.Hero.Id,
            "incident attack order: " + text);
        // Then mana: every hand card is a spell or a Discover minion; the face Fireblast is the obvious spend.
        Check(line.Count == 4 && line[3].Kind == FastFinish.Mana && line[3].Action.Type == PlayerTaskType.HERO_POWER
            && line[3].Action.Target == human.Hero.Id && npc.RemainingMana < 7, "incident mana spend: " + text);
    }

    // Live 2026-11-01 (tater96 turn 17): a carved Treant ("Battlecry: Cast {0}", here Typhoon) is a minion that wipes the
    // board. Fast finish only attacks and makes positive-value plays: it never plays a carried-card minion, and a play
    // that removes or weakens a friendly minion is rejected even when the one-ply evaluation likes it.
    public static void TestFastFinishNeverPlaysBoardWipe()
    {
        var g = Fixture(CardClass.DRUID, CardClass.DEATHKNIGHT); g.Turn = 17; g.Player1.UsedMana = 8;
        for (int i = 0; i < 4; i++) Board(g, true, "CS2_182");          // exhausted: no attacks
        Board(g, false, "CS2_179");
        var typhoon = Cards.FromId("EDR_232");
        var treant = Entity.FromCard(g.Player1, Cards.FromId("MEND_046t"), zone: g.Player1.HandZone);
        // Exactly what Bashana's Battlecry does: the client reads tag 2 as the carved entity's id, never its dbfId.
        var carved = SabberStoneCore.Actions.PayloadRules.StoreCard(treant, 1, typhoon,
            SabberStoneCore.Actions.PayloadPolicy.Copy | SabberStoneCore.Actions.PayloadPolicy.Silence | SabberStoneCore.Actions.PayloadPolicy.ShowOwner);
        treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        g.AuraUpdate();
        Check(treant[GameTag.TAG_SCRIPT_DATA_NUM_1] == carved.Id && carved.Id != typhoon.AssetId, "fixture displays the carved entity id");
        var view = View(g);
        Check(view.Facts.Own.Hand.Any(c => c.EntityId == treant.Id && FastFinish.CarriesCard(c)), "carved Treant shows its carried card to its owner");
        var action = FastFinish.Choose(view, Legal(g), out string kind);
        Check(action == null || action.Source != treant.Id, "fast finish played the carved Typhoon Treant: " + action);
        // The board guard alone: a wiped board is never an obvious play, whatever the evaluation says.
        var wiped = Fixture(CardClass.DRUID, CardClass.DEATHKNIGHT);
        Check(!FastFinish.KeepsBoard(view.Facts, View(wiped).Facts), "a play that removes friendly minions must be rejected");
        Check(FastFinish.KeepsBoard(view.Facts, view.Facts), "an unchanged board is kept");
    }
}
