using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;

internal static partial class Program
{
    private static void TerminalDiscoverSpellLifecycle()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.DRUID),
            Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game; var player = game.Player1;
        player.BaseMana = 10; player.Hero.Damage = 29;
        foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        var spell = Generic.DrawCard(player, Cards.FromId("LOOT_054"));
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(match, new object[] { game.PowerHistory.Last });
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        int start = capture.Packets.Count;
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.MainOption?.Id == spell.Id);
        Check(index >= 0, "terminal Branching Paths option");
        match.SendOption(options.Id, index, 0, -1, 0);
        var choice = capture.Last<EntityChoices>(17);
        int draw = choice.Entities.Single(id => game.IdEntityDic[id].Card.Text.Contains("Draw"));
        int answerStart = capture.Packets.Count;
        match.SendChoices(choice.Id, new List<int> { draw });
        Check(game.State == State.COMPLETE && player.Choice == null && spell.Zone == player.GraveyardZone,
            "lethal first choice cancels second choice and removes source from PLAY");
        Check(!capture.Packets.Skip(answerStart).Any(packet => packet.Message is EntityChoices || packet.Message is AllOptions),
            "terminal answer emits no further choices or options");
        var blocks = new Stack<HistoryBlock.Type>();
        int graveyardMoves = 0;
        foreach (var packet in capture.Packets.Skip(start))
        {
            if (packet.Message is not PowerHistory original) continue;
            using var stream = new MemoryStream(); original.Serialize(stream); stream.Position = 0;
            var wire = new PowerHistory(); wire.Deserialize(stream);
            foreach (var entry in wire.List)
            {
                if (entry.HasPowerStart) blocks.Push(entry.PowerStart.Type);
                if (entry.HasPowerEnd) { Check(blocks.Count > 0, "terminal history has no unmatched end"); blocks.Pop(); }
                if (IsZone(entry, spell.Id, SabberStoneCore.Enums.Zone.GRAVEYARD))
                {
                    Check(blocks.Count == 0, "terminal graveyard follows PLAY closure");
                    graveyardMoves++;
                }
            }
        }
        Check(blocks.Count == 0 && graveyardMoves == 1, "terminal history closes original blocks and exits once");
        capture.SerializeAll();
        Console.WriteLine("PASS terminal Discover lifecycle: fatigue lethal closes wire PLAY/POWER");
    }

    private static void DiscoverSpellLifecycle(string filter = null, bool recast = false, bool scheduledRepeat = false)
    {
        foreach (var (id, heroClass, combo) in new[] {
            ("EDR_528", CardClass.ROGUE, false), ("UNG_029", CardClass.PRIEST, false),
            ("CORE_WON_350", CardClass.WARRIOR, false), ("EDR_528", CardClass.ROGUE, true),
            ("TIME_448", CardClass.DEMONHUNTER, false)
        }.Where(test => filter == null || test.Item1 == filter))
        {
            string label = id + (combo ? " Combo/Dark Gift" : "") + (recast ? " Electra recast" : "");
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, Cards.HeroCard(heroClass),
                Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
            match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var player = game.Player1;
            player.BaseMana = 10;
            Check(game.CurrentPlayer == player && game.Turn == 1 && !player.IsComboActive, label + " first human turn");
            player.IsComboActive = combo;
            var pyro = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("NEW1_020"), zone: player.BoardZone);
            var sherazin = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("UNG_065t"), zone: player.BoardZone);
            if (recast) Generic.AddEnchantmentBlock(game, Cards.FromId("BOT_411e"), player.Hero, player);
            var spell = Generic.DrawCard(player, Cards.FromId(id));
            if (scheduledRepeat) RepeatRules.Mark(spell,player.Hero,"JAIL_123e");
            if (id == "UNG_029")
                foreach (string card in new[] { "CS1_130", "CS2_004", "EX1_622" })
                    SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(card), zone: player.DeckZone);
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { game.PowerHistory.Last });
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            int start = capture.Packets.Count;
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(option => option.MainOption?.Id == spell.Id && option.MainOption.PowerKeyword == 0);
            Check(index >= 0, label + " spell option");
            match.SendOption(options.Id, index, 0, -1, 0);
            int answers = 0, selected = 0;
            var blocks = new Stack<(HistoryBlock.Type Type, int Source)>();
            int packetCursor = start;
            void Observe()
            {
                foreach (var packet in capture.Packets.Skip(packetCursor))
                {
                    if (packet.Message is EntityChoices choice && choice.ChoiceType == (int)ChoiceType.GENERAL)
                        Check(blocks.Any(b => b.Type == HistoryBlock.Type.PLAY && b.Source == spell.Id), label + " choice inside original PLAY block");
                    if (packet.Message is EntityChoices general && general.ChoiceType == (int)ChoiceType.GENERAL)
                        Check(blocks.Count(b => b.Type == HistoryBlock.Type.POWER && b.Source == spell.Id) == 1, label + " one POWER spans all choices");
                    if (packet.Message is not PowerHistory original) continue;
                    using var stream = new MemoryStream(); original.Serialize(stream); stream.Position = 0;
                    var wire = new PowerHistory(); wire.Deserialize(stream);
                    foreach (var entry in wire.List)
                    {
                        if (entry.HasPowerStart) blocks.Push((entry.PowerStart.Type, entry.PowerStart.Source));
                        if (entry.HasPowerEnd) { Check(blocks.Count > 0, label + " no unmatched block end"); blocks.Pop(); }
                        if (entry.HasTagChange && entry.TagChange.Entity == spell.Id && entry.TagChange.Tag == (int)GameTag.ZONE
                            && entry.TagChange.Value == (int)SabberStoneCore.Enums.Zone.GRAVEYARD)
                            Check(blocks.Count == 0, label + " graveyard follows PLAY end, as in retail");
                    }
                }
                packetCursor = capture.Packets.Count;
            }
            while (game.Player1.Choice != null)
            {
                Check(spell[GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.PLAY && spell.Zone != player.GraveyardZone,
                    label + " pending choice keeps spell in PLAY; actual=" + (SabberStoneCore.Enums.Zone)spell[GameTag.ZONE]);
                Check(!History(capture, start).Any(e => IsZone(e, spell.Id, SabberStoneCore.Enums.Zone.GRAVEYARD)), label + " no early graveyard");
                Check(pyro.Damage == 0 && sherazin[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && player.NumSpellsPlayedThisGame == 0,
                    label + " after-cast, after-play and spell counter wait for final answer");
                Observe();
                var choice = capture.Last<EntityChoices>(17);
                selected = choice.Entities[0];
                match.SendChoices(choice.Id, new List<int> { selected });
                Check(++answers <= 3, label + " bounded choices");
            }
            Observe();
            Check(answers == (id == "TIME_448" ? 2 : scheduledRepeat ? 2 : 1), label + " expected choice count");
            Check(spell.Zone == player.GraveyardZone && player.GraveyardZone.Count(card => card.Id == spell.Id) == 1
                && History(capture, start).Count(e => IsZone(e, spell.Id, SabberStoneCore.Enums.Zone.GRAVEYARD)) == 1,
                label + " exactly one graveyard after final answer");
            Check(pyro.Damage == 1 && sherazin[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && player.NumSpellsPlayedThisGame == 1,
                label + " after-cast, after-play and counter finish once");
            var history = History(capture, start);
            int hand = history.FindIndex(e => IsZone(e, selected, SabberStoneCore.Enums.Zone.HAND));
            int reaction = history.FindIndex(e => e.HasTagChange && e.TagChange.Entity == pyro.Id && e.TagChange.Tag == (int)GameTag.DAMAGE);
            Check(hand >= 0 && reaction > hand && blocks.Count == 0, label + " chosen card enters hand before after-cast; PLAY closes");
            if (scheduledRepeat)
            {
                Check(!history.Any(e => e.HasTagChange && Generic.IsDecisionInternalTag((GameTag)e.TagChange.Tag)),"repeat private TAG_CHANGE filtered");
                Check(history.Where(e=>e.HasFullEntity || e.HasShowEntity).SelectMany(e=>e.HasFullEntity ? e.FullEntity.Tags:e.ShowEntity.Tags)
                    .All(t=>!Generic.IsDecisionInternalTag((GameTag)t.Name)),"repeat private FULL/SHOW tags filtered");
                Check(History(capture,0).Any(e=>e.HasShowEntity && e.ShowEntity.Name=="JAIL_123e"),"pinned repeat enchantment projects");
            }
            capture.SerializeAll();
            Console.WriteLine("PASS Discover lifecycle " + label);
        }
        if (filter == null)
        {
            DiscoverSpellLifecycle("UNG_029", recast: true);
            AgentOfTheOldOnesClearsPreviousIdentity();
        }
    }

    private static void AgentOfTheOldOnesClearsPreviousIdentity()
    {
        foreach (string previousId in new[] { "EX1_608", "CS2_106", "CS2_029" })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE),
                Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
            match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var player = match.Game.Player1; player.BaseMana = 10;
            var target = Generic.DrawCard(player, Cards.FromId(previousId));
            var source = Generic.DrawCard(player, Cards.FromId("CATA_200"));
            FlushHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(option => option.MainOption?.Id == source.Id);
            Check(index >= 0, "Agent native play option");
            match.SendOption(options.Id, index, 0, -1, 1);
            var choice = capture.Last<EntityChoices>(17);
            Check(choice.Source == source.Id && choice.Entities.Contains(target.Id), "Agent offers actual hand entity");
            int cursor = capture.Packets.Count;
            match.SendChoices(choice.Id, new List<int> { target.Id });
            var coin = player.HandZone.Single(card => card.Id == target.Id);
            Check(coin.Card.Id == "GAME_005" && coin.Cost == 0, "Agent replaces selected identity with zero-cost Coin");
            var change = History(capture, cursor).Single(entry => entry.HasChangeEntity
                && entry.ChangeEntity.Entity == target.Id).ChangeEntity;
            Check(change.Name == "GAME_005", "Agent sends Coin CHANGE_ENTITY");
            foreach (var tag in new[] { GameTag.ATK, GameTag.HEALTH, GameTag.RARITY, GameTag.COST })
                if (Cards.FromId(previousId)[tag] != 0)
                    Check(change.Tags.Any(value => value.Name == (int)tag && value.Value == 0),
                        "Agent Coin CHANGE_ENTITY clears previous " + previousId + " " + tag);
            // Retail SHOW/CHANGE merge tags; replay serialized snapshots without clearing old identity.
            var tags = new Dictionary<int, int>();
            foreach (var packet in capture.Packets.Where(packet => packet.Id == 19))
            {
                using var stream = new MemoryStream(); packet.Message.Serialize(stream); stream.Position = 0;
                var wire = new PowerHistory(); wire.Deserialize(stream);
                foreach (var entry in wire.List)
                {
                    var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
                        : entry.HasChangeEntity ? entry.ChangeEntity : null;
                    if (snapshot?.Entity == target.Id)
                        foreach (var tag in snapshot.Tags) tags[tag.Name] = tag.Value;
                    if (entry.HasTagChange && entry.TagChange.Entity == target.Id)
                        tags[entry.TagChange.Tag] = entry.TagChange.Value;
                }
            }
            var expected = match.SnapshotVisibleTransportTags(coin);
            foreach (var tag in new[] { GameTag.ATK, GameTag.HEALTH, GameTag.RARITY, GameTag.COST, GameTag.CARDTYPE })
                Check(tags.GetValueOrDefault((int)tag) == (tag == GameTag.CARDTYPE ? 5 : 0)
                    && StrictTransportTagMatches(tags, expected, tag), "Agent Coin serialized final parity " + previousId + "/" + tag);
            capture.SerializeAll();
            Console.WriteLine("PASS Agent of the Old Ones Coin identity reset: " + previousId);
        }
    }
}
