using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void DeckDiscoverHandActor()
    {
        var failures = new List<string>();
        foreach (string cardId in new[] { "EDR_856", "CORE_DS1_184", "JAIL_734", "TLC_515", "TIME_701" })
        foreach (bool opponent in new[] { false, true })
        foreach (bool fullHand in cardId == "EDR_856" ? new[] { false, true } : new[] { false })
        {
            string label = cardId + (opponent ? " opponent" : " owner") + (fullHand ? " full-hand" : "");
            void Expect(bool ok, string message) { if (!ok) failures.Add(label + ": " + message); }
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE),
                Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            if (opponent)
            {
                InvokeProcess(match, EndTurnTask.Any(match.Game.Player1));
                FlushFieldHistory(match);
            }
            var game = match.Game; var owner = game.CurrentPlayer;
            owner.BaseMana = 10; owner.UsedMana = 0;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            var deck = new[] { "EDR_852", "CS2_182", "CS2_200" }
                .Select(id => EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone)).ToArray();
            var source = Generic.DrawCard(owner, Cards.FromId(cardId));
            FlushFieldHistory(match);
            int start = capture.Packets.Count;
            if (!opponent)
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int play = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
                Check(play >= 0, label + " native play option");
                match.SendOption(options.Id, play, 0, -1, source is SabberStoneCore.Model.Entities.Minion ? 1 : 0);
            }
            else
            {
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
                FlushFieldHistory(match);
            }
            var offers = owner.Choice.Choices.ToArray();
            Check(offers.Length == 3, label + " three offers");
            // Sweet Dreams deliberately returns its pick to DECK; select an ordinary hand delivery.
            int selected = offers.First(id => cardId != "EDR_856"
                || game.IdEntityDic[game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
            var chosen = deck.Single(c => c.Card.Id == game.IdEntityDic[selected].Card.Id);
            int offeredGift = chosen[GameTag.DARK_GIFT_ENTITY];
            var before = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
            EntityChoices choice = null;
            if (!opponent)
            {
                choice = capture.Last<EntityChoices>(17);
                Check(choice.Entities.Count == offers.Length && choice.ChoiceType == 2 && choice.CountMin == 1 && choice.CountMax == 1,
                    label + " mandatory native GENERAL prompt");
                Expect(choice.Entities.Select((id, i) => before.TryGetValue(id, out var state) && state.Card == game.IdEntityDic[offers[i]].Card.Id).All(ok => ok),
                    "every choice actor must have its named identity before the choice packet");
            }
            if (fullHand)
            {
                while (owner.HandZone.Count < 10) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
                FlushFieldHistory(match);
            }
            if (!opponent && cardId == "EDR_856")
            {
                var pending = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
                Expect(offers.All(id => pending[game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card
                    == game.IdEntityDic[game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id),
                    "gift identities stay visible throughout the pending choice");
            }
            int answerStart = capture.Packets.Count;
            if (!opponent) match.SendChoices(choice.Id, new List<int> { choice.Entities[Array.IndexOf(offers, selected)] });
            else
            {
                // The NPC answers through the bot's own task path (NativeMatch.Process -> AnswerDecision).
                InvokeProcess(match, ChooseTask.Pick(owner, selected));
                FlushFieldHistory(match);
            }
            Check(fullHand ? chosen.Zone == owner.GraveyardZone && owner.HandZone.Count == 10
                : owner.HandZone.Contains(chosen) && owner.LastCardDrawn == chosen.Id, label + " draws/burns original deck entity");
            Check(deck.Where(c => c != chosen).All(c => c.Zone == owner.DeckZone), label + " unchosen originals remain in DECK");
            var answer = DeckDiscoverWireHistory(capture, answerStart);
            var all = DeckDiscoverWireHistory(capture, 0);
            var after = DeckDiscoverWireState(all);
            var destination = fullHand ? EngineZone.GRAVEYARD : EngineZone.HAND;
            int hand = answer.FindIndex(e => IsZone(e, chosen.Id, destination)
                || fullHand && e.HasShowEntity && e.ShowEntity.Entity == chosen.Id && SnapshotTag(e, GameTag.ZONE) == (int)destination);
            Check(hand >= 0, label + " native " + destination + " transition");
            if (!opponent || fullHand)
            {
                Expect(answer.Take(hand + 1).Any(e => (e.HasFullEntity || e.HasShowEntity) && SnapshotEntity(e) == chosen.Id
                    && SnapshotName(e) == chosen.Card.Id && (fullHand || SnapshotTag(e, GameTag.ZONE) == (int)destination)),
                    "chosen original needs named reveal before/as it enters " + destination);
                Expect(after[chosen.Id] == (chosen.Card.Id, (int)destination), "chosen entity must end named in " + destination);
            }
            else
            {
                // Retail shows the opponent's choice as card backs: ids of nameless SETASIDE actors only, never deck ids.
                var opposing = capture.Packets.Skip(start).Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).ToList();
                Expect(opposing.Count == 1 && opposing[0].PlayerId == owner.PlayerId && opposing[0].Entities.Count == offers.Length,
                    "opponent choice is shown once, as the chooser's backs");
                Expect(opposing.SelectMany(c => c.Entities).All(id => !deck.Any(c => c.Id == id) && after.TryGetValue(id, out var shown) && shown.Card == ""),
                    "opposing choice actors are nameless and never real deck entities");
                Expect(capture.Packets.Skip(start).Any(p => p.Id == 13 && ((EntitiesChosen)p.Message).PlayerId == owner.PlayerId
                    && ((EntitiesChosen)p.Message).ChooseEntities.Id == opposing[0].Id), "opponent choice is concealed by EntitiesChosen");
                Expect(all.Where(e => deck.Any(c => c.Id == SnapshotEntity(e))).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                    "opponent never sees original deck/hand identities");
                Expect(all.Where(e => offers.Contains(SnapshotEntity(e))).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                    "opponent never sees offered identities");
                Expect(after[chosen.Id] == ("", (int)EngineZone.HAND), "opponent sees chosen card hidden in HAND");
            }
            if (!opponent)
            {
                Expect(choice.Entities.All(id => before[id].Zone == (int)EngineZone.SETASIDE && !deck.Any(c => c.Id == id)),
                    "native choice uses separate SETASIDE actors, never real deck IDs");
                int chosenProxy = choice.Entities[Array.IndexOf(offers, selected)];
                Expect(choice.Entities.All(id => !owner.HandZone.Any(c => c.Id == id) && after[id].Zone == (id == chosenProxy
                    ? (int)EngineZone.REMOVEDFROMGAME : (int)EngineZone.SETASIDE)),
                    "offer proxies never enter HAND/PLAY; the chosen proxy is retired so its big card cannot linger");
                Expect(answer.Count(e => IsZone(e, chosenProxy, EngineZone.REMOVEDFROMGAME)) == 1
                    && answer.FindIndex(e => IsZone(e, chosenProxy, EngineZone.REMOVEDFROMGAME)) > hand,
                    "chosen proxy retires once, after the original's " + destination + " transition");
                var offeredHistory = DeckDiscoverWireHistory(capture, start).TakeWhile(e => !IsZone(e, chosen.Id, destination)).ToList();
                for (int i = 0; i < offers.Length; i++)
                {
                    int id = choice.Entities[i];
                    if (deck.Any(c => c.Id == offers[i]))
                        Expect(offeredHistory.Any(e => e.HasFullEntity && SnapshotEntity(e) == id
                            && SnapshotTag(e, GameTag.LINKED_ENTITY) == offers[i]
                            && SnapshotTag(e, GameTag.CREATOR) == choice.Source), "proxy links original and creator");
                    if (cardId == "EDR_856")
                        Expect(offeredHistory.Any(e => e.HasFullEntity && SnapshotEntity(e) == id
                            && SnapshotTag(e, GameTag.DARK_GIFT_ENTITY) == game.IdEntityDic[offers[i]][GameTag.DARK_GIFT_ENTITY]),
                            "proxy displays original's Dark Gift");
                }
                Expect(capture.Last<EntitiesChosen>(13).ChooseEntities.Entities.Single() == choice.Entities[Array.IndexOf(offers, selected)],
                    "native answer acknowledges proxy ID");
            }
            Expect(all.Where(e => deck.Any(c => c.Id == SnapshotEntity(e) && (!fullHand || c != chosen)) && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.DECK)
                .All(e => string.IsNullOrEmpty(SnapshotName(e))), "originals never named in DECK on either connection");
            foreach (var rejected in deck.Where(c => c != chosen))
                Expect(after[rejected.Id] == ("", (int)EngineZone.DECK),
                    "unchosen entity " + rejected.Id + " must end hidden in DECK");
            if (cardId == "EDR_856")
            {
                var gift = game.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>()
                    .Single(e => e.Target == chosen && e.Creator?.Id == offeredGift);
                Expect(answer.Any(e => (e.HasFullEntity || e.HasShowEntity) && SnapshotEntity(e) == gift.Id
                    && (opponent || fullHand ? SnapshotName(e) == "" : SnapshotName(e) == gift.Card.Id
                        && SnapshotTag(e, GameTag.ATTACHED) == chosen.Id)), "gift attachment identity/target obeys owner privacy");
            }
            DumpDrawHistory(label.Replace(' ', '-'), capture, start);
            DumpDeckDiscoverPackets(label.Replace(' ', '-'), capture, start);
            capture.SerializeAll();
            Console.WriteLine($"CHECK {label}: source={source.Id} offers={string.Join(",", offers)} chosen={chosen.Id}; hand reveal/privacy checked");
        }
        Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static List<PowerHistoryData> DeckDiscoverWireHistory(Capture capture, int cursor)
    {
        var result = new List<PowerHistoryData>();
        foreach (var packet in capture.Packets.Skip(cursor).Where(p => p.Id == 19))
        {
            using var stream = new MemoryStream(); packet.Message.Serialize(stream); stream.Position = 0;
            var wire = new PowerHistory(); wire.Deserialize(stream); result.AddRange(wire.List);
        }
        return result;
    }

    private static Dictionary<int, (string Card, int Zone)> DeckDiscoverWireState(IEnumerable<PowerHistoryData> rows)
    {
        var state = new Dictionary<int, (string Card, int Zone)>();
        foreach (var row in rows)
        {
            int id = SnapshotEntity(row);
            if (id != 0) state[id] = (SnapshotName(row), SnapshotTag(row, GameTag.ZONE));
            if (row.HasHideEntity) state[row.HideEntity.Entity] = ("", row.HideEntity.Zone);
            if (row.HasTagChange && row.TagChange.Tag == (int)GameTag.ZONE)
                state[row.TagChange.Entity] = (state.GetValueOrDefault(row.TagChange.Entity).Card ?? "", row.TagChange.Value);
        }
        return state;
    }

    private static void DumpDeckDiscoverPackets(string label, Capture capture, int cursor)
    {
        string root = Environment.GetEnvironmentVariable("HS_DRAW_HISTORY_OUTPUT");
        if (string.IsNullOrEmpty(root)) return;
        string directory = Path.Combine(root, label);
        Directory.CreateDirectory(directory);
        var rows = new List<string>();
        foreach (var (packet, i) in capture.Packets.Skip(cursor).Select((packet, i) => (packet, i)))
        {
            using (var stream = File.Create(Path.Combine(directory, $"packet-{i}-{packet.Id}.bin"))) packet.Message.Serialize(stream);
            rows.Add($"{i}: packet={packet.Id} " + (packet.Message is EntityChoices choices
                ? $"CHOICES id={choices.Id} source={choices.Source} hideChosen={choices.HideChosen} entities={string.Join(",", choices.Entities)}"
                : packet.Message is EntitiesChosen chosen ? $"CHOSEN id={chosen.ChooseEntities.Id} entities={string.Join(",", chosen.ChooseEntities.Entities)}"
                : packet.Message.GetType().Name));
        }
        File.WriteAllLines(Path.Combine(directory, "packets.txt"), rows);
    }
}
