using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("b15-draw-replacement-native", ProofKind.Native)]
    private static void B15DrawReplacementNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = DrawFixture(); var owner = opponent ? game.Player2 : game.Player1;
            var recipient = owner.Opponent;
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush()
            {
                typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(match, new object[] { game.PowerHistory.Last });
                game.PowerHistory.Last.Clear();
            }
            Flush();
            EngineEntity.FromCard(owner, Cards.FromId("CORE_SCH_717"), zone: owner.BoardZone);
            var card = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            card.Cost = 2;
            Generic.DrawFrom(recipient, owner, card);
            Flush();
            Check(card.Controller == recipient && card.Zone == recipient.HandZone && card.Cost == 2,
                "native cross-controller draw preserves original and cost");
            var copied = owner.HandZone.Single();
            Check(copied.Card.Id == "CS2_029" && copied.Cost == 1, "native draw listener creates one-Cost copy");
            foreach (var hidden in new[] { card, copied }.Where(p => p.Controller == game.Player2))
                AssertHiddenDraw(capture, hidden.Id, "cross-controller hand/copy");
            while (!recipient.HandZone.IsFull) Generic.DrawCard(recipient, Cards.FromId("CS2_182"));
            var burn = EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.DeckZone);
            Generic.DrawFrom(recipient, owner, burn);
            Flush();
            Check(burn.Zone == recipient.GraveyardZone && owner.HandZone.Last().Card.Id == "CS2_200",
                "native burn remains draw event");

            // Retention and later delivery must never SHOW an enemy's hidden original from SETASIDE.
            var effect = SabberStoneCore.Model.Entities.Enchantment.GetInstance(recipient, recipient.Hero, recipient, Cards.FromId("JAIL_509e"));
            var backlog = EngineEntity.FromCard(owner, Cards.FromId("CS2_024"), zone: owner.DeckZone);
            Generic.DrawFrom(recipient, owner, backlog); Flush();
            Check(backlog.Zone == recipient.SetasideZone, "native backlog retains original");
            recipient.SetasideZone.Add(recipient.HandZone.Remove(recipient.HandZone[0])); Flush();
            Check(backlog.Zone == recipient.HandZone && backlog.Cost == 1, "native backlog delivered at reduced cost");
            if (recipient == game.Player2) AssertHiddenDraw(capture, backlog.Id, "backlog hand delivery");
            var snapshots = DrawRows(capture).SelectMany(r => new[] { r.FullEntity, r.ShowEntity, r.ChangeEntity })
                .Where(e => e != null).ToArray();
            Check(snapshots.All(e => e.Tags.All(t => !PayloadRules.IsInternalTag((GameTag)t.Name)))
                && DrawRows(capture).All(r => !r.HasTagChange || !PayloadRules.IsInternalTag((GameTag)r.TagChange.Tag)),
                "backlog payload state never reaches native wire");
            var imp = EngineEntity.FromCard(recipient, Cards.FromId("JAIL_398"));
            // Make room without redrawing; ordinary creation then discard must reveal the retired actor.
            recipient.SetasideZone.Add(recipient.HandZone.Remove(recipient.HandZone[0]));
            Generic.AddHandPhase(recipient, imp); Flush();
            int damage = recipient.Hero.Damage;
            Generic.DiscardBlock(recipient, imp); Flush();
            Check(recipient.Hero.Damage == damage + 3 && DrawRows(capture).Any(r =>
                r.ShowEntity?.Entity == imp.Id && r.ShowEntity.Name == "JAIL_398"),
                "offboard Deathrattle source is revealed after leaving hidden hand");
            capture.SerializeAll();
        }
        foreach (bool opponent in new[] { false, true })
        {
            var game = DrawFixture();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.Player1)), "Discs opponent turn");
            var owner = game.CurrentPlayer;
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            var source = Generic.DrawCard(owner, Cards.FromId("BE_250"));
            owner.BaseMana = 10; owner.UsedMana = 0;
            Check(game.Process(PlayCardTask.Any(owner, source)), "native Discs play");
            game.MainDraw();
            var offers = owner.Choice.Choices.ToArray();
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { game.PowerHistory.Last });
            if (!opponent)
            {
                typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var packet = capture.Last<EntityChoices>(17);
                Check(packet.ChoiceType == 2 && packet.CountMin == 1 && packet.CountMax == 1
                    && packet.Entities.SequenceEqual(offers), "retail-shaped top/bottom GENERAL choice");
            }
            else
            {
                foreach (int offer in offers) AssertHiddenDraw(capture, offer, "enemy direction prompt");
                Check(OpposingPromptsHidden(capture, owner.PlayerId), "enemy prompt reaches the human only as card backs");
            }
            var decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { offers[1] }), out var error),
                "native direction answer: " + error);
            Check(owner.Choice == null && game.GetPendingDecisions().All(d => d.Kind == DecisionKind.Options) && owner.Options().Count > 0,
                "direction completion returns actionable options without stalled decision");
            var ninja = EngineEntity.FromCard(owner, Cards.FromId("TLC_513t2"), zone: owner.BoardZone);
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { game.PowerHistory.Last });
            Check(match.SnapshotVisibleTransportTags(ninja).TryGetValue(3128, out int summonWhenDrawn) && summonWhenDrawn == 1,
                "exported Summoned When Drawn tag projected for Ninja");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS draw replacement native projection: both sides, hidden draws/copies/backlog/options, retail choice, Ninja tag");
    }

    private static SabberStoneCore.Model.Game DrawFixture()
    {
        var game = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 42, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            player.BaseMana = 10;
        }
        return game;
    }

    private static IEnumerable<PowerHistoryData> DrawRows(Capture capture) =>
        capture.Packets.Select(p => p.Message).OfType<PowerHistory>().SelectMany(p => p.List);

    private static void AssertHiddenDraw(Capture capture, int id, string cause)
    {
        bool Basic(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
            || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;
        var rows = DrawRows(capture).ToArray();
        var snapshots = rows.SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity })
            .Where(entity => entity?.Entity == id).ToArray();
        Check(snapshots.Length > 0 && snapshots.All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => Basic(t.Name)))
            && rows.Where(r => r.HasTagChange && r.TagChange.Entity == id).All(r => Basic(r.TagChange.Tag)),
            "opponent never receives hidden identity/definition tags: " + cause);
    }
}
