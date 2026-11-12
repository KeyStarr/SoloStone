using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

internal static class PrepareProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Prepare " + message);

    internal static void Run()
    {
        ZeePrepare();
        var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        session.Flush();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            player.BaseMana = 3;
            player.UsedMana = 0;
            var source = player.HandZone.First();
            source.Cost = 7;
            PrepareRules.Grant(source);
            session.Flush();
            int hand = player.HandZone.Count, position = source.ZonePosition;
            session.PublishDecision(player);
            session.Process(PrepareCardTask.Any(player, source));
            Check(player.HandZone.Count == hand && source.ZonePosition == position, "hand stable");
            var visible = session.Client.Entities[source.Id];
            if (player == game.Player1)
            {
                Check(visible.CardId == source.Card.Id && visible.Get(GameTag.COST) == 3
                    && visible.Get(GameTag.PREPARED) == 1 && visible.Get(GameTag.PREPARING) == 1
                    && visible.Get(GameTag.CANT_PLAY) == 1 && visible.Get(GameTag.IS_USING_PREPARE_OPTION) == 0,
                    "own cost/state and action flag clear");
                foreach (string enchantId in new[] { "JAIL_907e", "JAIL_907e02" })
                {
                    var enchant = source.AppliedEnchantments.Single(e => e.Card.Id == enchantId);
                    Check(session.Client.Entities[enchant.Id].CardId == enchantId
                        && session.Client.Entities[enchant.Id].Get(GameTag.ATTACHED) == source.Id, "own enchant identity and attachment");
                }
            }
            else
            {
                Check(visible.CardId == "" && !visible.Tags.ContainsKey((int)GameTag.COST)
                    && !visible.Tags.ContainsKey((int)GameTag.PREPARED), $"opponent card identity and discount hidden: card={visible.CardId}, tags={string.Join(",",visible.Tags)}");
                foreach (var enchant in source.AppliedEnchantments)
                    Check(session.Client.Entities[enchant.Id].CardId == ""
                        && !session.Client.Entities[enchant.Id].Tags.ContainsKey((int)GameTag.ATTACHED)
                        && !session.Client.Entities[enchant.Id].Tags.ContainsKey((int)GameTag.TAG_SCRIPT_DATA_NUM_1),
                        "opponent enchant identity/details hidden");
            }
            Check(session.Client.Entities[player.Id].Get(GameTag.RESOURCES_USED) == player.UsedMana, "public mana expenditure");
            var lockId = source.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_907e02").Id;
            session.Process(EndTurnTask.Any(player));
            if (player == game.Player1)
                Check(visible.Get(GameTag.PREPARING) == 0 && visible.Get(GameTag.CANT_PLAY) == 0
                    && visible.Get(GameTag.COST) == 3 && visible.Get(GameTag.PREPARED) == 1
                    && session.Client.Entities[lockId].Get(GameTag.ZONE) == (int)Zone.REMOVEDFROMGAME,
                    "explicit unlock and enchant removal preserve discount");
        }
        Console.WriteLine("PASS Prepare transport: distinct keyword option; own cost/lock/enchantments; explicit expiry; hidden opponent identity/details; public mana");
    }
    private static void ZeePrepare()
    {
        var deck = LocalDecks.Get("npc-zee-shaman");
        Check(deck.CardIds.Where(id => Cards.FromId(id)[GameTag.PREPARE] == 1).Distinct()
            .SequenceEqual(new[] { "JAIL_457" }), "Zee Prepare source is Hijacked Securitybot");
        var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 2, SkipMulligan = true,
            History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = deck.CardIds.Select(Cards.FromId).ToList() });
        game.StartGame();
        NativeSession session = null;
        var replay = new ClientModel();
        var snapshots = new List<PegasusGame.PowerHistoryEntity>();
        var match = new NativeMatch((id, packet) => {
            session.Receive(id, packet);
            if (packet is PegasusGame.PowerHistory history)
            {
                int deckAction = history.List.FindIndex(e => e.HasPowerStart
                    && (int)e.PowerStart.Type == (int)BlockType.DECK_ACTION);
                if (deckAction >= 0)
                {
                    var marker = history.List[deckAction + 1];
                    Check(marker.HasTagChange && marker.TagChange.Tag == (int)GameTag.PREPARE_REVEALED
                        && marker.TagChange.Value == 1 && marker.TagChange.Entity == history.List[deckAction].PowerStart.Source,
                        "Prepare history marker must be inside DECK_ACTION before its effects");
                }
                foreach (var entry in history.List)
                {
                    if (entry.HasShowEntity) snapshots.Add(entry.ShowEntity);
                    var single = new PegasusGame.PowerHistory(); single.List.Add(entry);
                    replay.Apply(single);
                    Check(replay.Entities.Values.All(e => e.Get(GameTag.ZONE) != (int)Zone.PLAY
                        || !string.IsNullOrEmpty(e.CardId) || e.Get(GameTag.CARDTYPE) == (int)CardType.GAME
                        || e.Get(GameTag.CARDTYPE) == (int)CardType.PLAYER),
                        "anonymous actor reached PLAY during Zee Prepare replay");
                }
            }
        });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush();
        var bot = game.Player2;
        var source = Generic.DrawCard(bot, Cards.FromId("JAIL_457"));
        bot.BaseMana = 3; bot.UsedMana = 0;
        session.Flush();
        session.Process(PrepareCardTask.Any(bot, source));
        var discount = source.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_907e");
        foreach (var enchant in source.AppliedEnchantments)
            Check(replay.Entities[enchant.Id].CardId == ""
                && replay.Entities[enchant.Id].Get(GameTag.ATTACHED) == 0,
                "private Prepare attachment leaked identity or target");
        Check(replay.Entities[source.Id].CardId == "", "Prepare revealed opponent hand source");
        // HistoryInfo.CanDuplicateEntity(false) otherwise waits forever on the hidden DECK_ACTION source.
        Check(replay.Entities[source.Id].Get(GameTag.PREPARE_REVEALED) == 1,
            "hidden Prepare source cannot complete native history without PREPARE_REVEALED");
        session.Process(EndTurnTask.Any(bot));
        session.PublishDecision(game.Player1);
        Check(replay.Entities[game.Player1.Id].Get(GameTag.CURRENT_PLAYER) == 1
            && replay.Entities[game.Id].Get(GameTag.STEP) == (int)Step.MAIN_ACTION
            && session.Packets.Last().Id == 14, "client projection did not advance to human turn/options");
        session.Process(EndTurnTask.Any(game.Player1));
        bot.BaseMana = 10; bot.UsedMana = 0;
        session.Process(PlayCardTask.Minion(bot, (SabberStoneCore.Model.Entities.Minion)source));
        Check(replay.Entities[source.Id].CardId == "JAIL_457"
            && snapshots.Any(e => e.Entity == discount.Id && e.Name == "JAIL_907e"
                && e.Tags.Any(t => t.Name == (int)GameTag.CARDTYPE && t.Value == (int)CardType.ENCHANTMENT)
                && e.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == source.Id)
                && e.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)Zone.PLAY)),
            "public play failed to reveal prepared source and attachment tags before consuming discount");
        Console.WriteLine("PASS Zee Prepare replay: no anonymous PLAY actors, private attachments, human turn/options, later public reveal");
    }

}
