using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Zone = SabberStoneCore.Enums.Zone;
using Enchantment = SabberStoneCore.Model.Entities.Enchantment;
using Card = SabberStoneCore.Model.Card;

internal static class CardStateChecks
{
    private static void Check(bool value, string label) => SelfChecks.Check(value, "card state: " + label);
    internal static IEnumerable<Violation> Compare(Game game, ClientModel client)
    {
        foreach (var minion in game.IdEntityDic.Values.OfType<Minion>().Where(m => m.Card.Dormant != null || m.NativeTags.ContainsKey((GameTag)32000)))
        {
            if (minion.Zone != minion.Controller.BoardZone || !client.Entities.TryGetValue(client.WireId(minion.Id), out var shown)) continue;
            foreach (var tag in new[] { GameTag.DORMANT, GameTag.DORMANT_VISUAL, GameTag.UNTOUCHABLE, GameTag.DORMANT_AWAKENED_THIS_TURN })
                if (shown.Get(tag) != minion[tag]) yield return new(minion.Card.Id, minion.Id, tag.ToString(), minion[tag].ToString(), shown.Get(tag).ToString());
            if (minion.Card.Dormant != null && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) != minion[GameTag.TAG_SCRIPT_DATA_NUM_1])
                yield return new(minion.Card.Id, minion.Id, "DORMANT_TIMER", minion[GameTag.TAG_SCRIPT_DATA_NUM_1].ToString(), shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1).ToString());
        }
        // Native TagVisualConfiguration reads GHOSTLY=785; 3630 is engine-only. Echo supplies GHOSTLY independently.
        foreach (var card in game.Player1.HandZone)
        {
            int expected = Math.Max(card[TemporaryRules.Temporary], card[GameTag.GHOSTLY]);
            if (client.Entities.TryGetValue(client.WireId(card.Id), out var shown) && shown.Get(GameTag.GHOSTLY) != expected)
                yield return new(card.Card.Id, card.Id, "GHOSTLY", expected.ToString(), shown.Get(GameTag.GHOSTLY).ToString());
        }
        foreach (var pair in client.Entities.Where(p => p.Value.Tags.ContainsKey((int)TemporaryRules.Temporary)))
            yield return new(pair.Value.CardId, pair.Key, "RAW_TEMPORARY", "absent", pair.Value.Get(TemporaryRules.Temporary).ToString());
        foreach (var actor in game.IdEntityDic.Values.OfType<Enchantment>().Where(e => e.Card.Id == "TIME_046e" || e.Card.Id == "GBL_999e"))
        {
            bool live = actor.Target.AppliedEnchantments?.Contains(actor) ?? false;
            // A retired helper keeps a historical Target reference after its former card changes owner/zone.
            bool hidden = live && actor.Card.Id == "GBL_999e" && actor.Target.Controller == game.Player2 && actor.Target.Zone == game.Player2.HandZone;
            if (!client.Entities.TryGetValue(client.WireId(actor.Id), out var shown))
            {
                if (live && !hidden) yield return new(actor.Card.Id, actor.Id, "ENTITY", "public helper", "missing");
                continue;
            }
            if (hidden)
            {
                // A once-public helper retains zeroed keys after native HIDE/TagMap merge.
                if (shown.CardId != "" || shown.Tags.Any(t => t.Key != 53 && t.Key != 50 && t.Key != 49 && t.Key != 263 && t.Value != 0))
                    yield return new(actor.Card.Id, actor.Id, "PRIVATE_HELPER", "structural only", shown.CardId);
                continue;
            }
            if (live)
            {
                if (shown.CardId != actor.Card.Id) yield return new(actor.Card.Id, actor.Id, "CARD_ID", actor.Card.Id, shown.CardId);
                foreach (var tag in new[] { GameTag.CONTROLLER, GameTag.ZONE, GameTag.ATTACHED, GameTag.TAG_SCRIPT_DATA_NUM_1, (GameTag)451 })
                {
                    int expected = tag == GameTag.ATTACHED ? client.WireId(actor[tag]) : actor[tag];
                    if (shown.Get(tag) != expected) yield return new(actor.Card.Id, actor.Id, tag.ToString(), expected.ToString(), shown.Get(tag).ToString());
                }
            }
            else if (shown.Get(GameTag.ATTACHED) != 0 || shown.Get(GameTag.ZONE) != (int)Zone.REMOVEDFROMGAME)
                yield return new(actor.Card.Id, actor.Id, "RETIRED_HELPER", "detached/removed", shown.Get(GameTag.ATTACHED).ToString());
        }
    }
    private static bool ReferencesFollowFull(IEnumerable<PowerHistoryData> entries, IEnumerable<int> existing)
    {
        var known = existing.ToHashSet();
        foreach (var entry in entries)
        {
            if (entry.HasFullEntity) known.Add(entry.FullEntity.Entity);
            if (entry.HasShowEntity && !known.Contains(entry.ShowEntity.Entity)) return false;
            if (entry.HasChangeEntity && !known.Contains(entry.ChangeEntity.Entity)) return false;
            if (entry.HasTagChange && !known.Contains(entry.TagChange.Entity)) return false;
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (snapshot != null && snapshot.Tags.Any(t => t.Name == 40 && t.Value != 0 && !known.Contains(t.Value))) return false;
        }
        return true;
    }
    internal static void Run()
    {
        RetiredTemporaryHelper();
        DormantLifecycle();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var p = game.Player1;
        game.PowerHistory.Last.Clear();
        var known = session.Client.Entities.Keys.ToArray(); int cursor = session.Packets.Count;
        var m = (Minion)Entity.FromCard(p, Cards.FromId("TIME_046")); Generic.SummonBlock(game, m, -1, p.Hero);
        session.Flush(); game.PowerHistory.Last.Clear();
        var batch = new List<PowerHistoryData>();
        foreach (var packet in session.Packets.Skip(cursor).Where(x => x.Id == 19))
        {
            using var bytes = new MemoryStream(Convert.FromBase64String(packet.Base64));
            var history = new PegasusGame.PowerHistory(); history.Deserialize(bytes); batch.AddRange(history.List);
        }
        Check(ReferencesFollowFull(batch, known), "FULL precedes references and attachment parent");
        Check(!ReferencesFollowFull(batch.Where(e => !e.HasFullEntity || e.FullEntity.Entity != m.Id), known), "missing FULL fault rejected");
        Check(!Invariants.Compare(game, session.Client).Any(), "natural sleeper/helper serialized projection");
        var helper = m.AppliedEnchantments.Single();
        session.Client.Entities[helper.Id].Tags[451] = 99;
        Check(Compare(game, session.Client).Any(v => v.Entity == helper.Id && v.Tag == ((GameTag)451).ToString()), "timer fault rejected");
        session.Client.Entities[helper.Id].Tags[451] = 3;
        DormantRules.Accelerate(m, 2);
        var historical = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        DormantRules.Accelerate(m, 1); var wake = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        session.Invoke("SendHistory", historical);
        Check(session.Client.Entities[helper.Id].Get((GameTag)451) == 1 && session.Client.Entities[m.Id].Get(GameTag.DORMANT) == 1,
            "historical timer stays asleep despite later live wake");
        session.Invoke("SendHistory", wake);
        Check(!Invariants.Compare(game, session.Client).Any(), "wake clears tags and removes helper");
        session.Client.Entities[helper.Id].Tags[40] = m.Id;
        Check(Compare(game, session.Client).Any(v => v.Tag == "RETIRED_HELPER"), "missing helper removal fault rejected");
        session.Client.Entities[helper.Id].Tags[40] = 0;
        session.Client.Entities[m.Id].Tags[1518] = 1;
        Check(Compare(game, session.Client).Any(v => v.Tag == "DORMANT"), "missing zero clear fault rejected");
        session.Client.Entities[m.Id].Tags[1518] = 0;
        foreach (var owner in new[] { p, p.Opponent })
        {
            game.PowerHistory.Last.Clear();
            CostRules.AddNextTemporaryRule(owner, 2);
            var card = Entity.FromCard(owner, Cards.FromId("CS2_182")); TemporaryRules.Make(card); Generic.AddHandPhase(owner, card);
            session.Flush(); game.PowerHistory.Last.Clear();
            var actor = card.AppliedEnchantments.Single(); var shown = session.Client.Entities[card.Id];
            if (owner == p)
            {
                Check(shown.Get(GameTag.GHOSTLY) == 1 && shown.Get(GameTag.COST) == 2, "owner visual and post-admission COST");
                shown.Tags[(int)GameTag.GHOSTLY] = 0;
                Check(Compare(game, session.Client).Any(v => v.Entity == card.Id && v.Tag == "GHOSTLY"), "missing Temporary visual fault rejected");
                shown.Tags[(int)GameTag.GHOSTLY] = 1;
                shown.Tags[(int)TemporaryRules.Temporary] = 1;
                Check(Compare(game, session.Client).Any(v => v.Entity == card.Id && v.Tag == "RAW_TEMPORARY"), "raw Temporary tag fault rejected");
                shown.Tags.Remove((int)TemporaryRules.Temporary);
            }
            else
            {
                Check(shown.CardId == "" && shown.Tags.Keys.All(t => t == 53 || t == 50 || t == 49 || t == 263), "hidden hand structural tags only");
                Check(!Compare(game, session.Client).Any(v => v.Entity == actor.Id), "opponent helper graph private");
                session.Client.Entities[actor.Id].Tags[(int)GameTag.GHOSTLY] = 1;
                Check(Compare(game, session.Client).Any(v => v.Tag == "PRIVATE_HELPER"), "injected hidden secret tag rejected");
                session.Client.Entities[actor.Id].Tags.Remove((int)GameTag.GHOSTLY);
            }
            Check(!Invariants.Compare(game, session.Client).Any(), "Temporary owner/opponent invariants");
            Generic.DiscardBlock(owner, card); session.Flush(); game.PowerHistory.Last.Clear();
            Check(session.Client.Entities[card.Id].CardId == card.Card.Id && shown.Get(GameTag.GHOSTLY) == (owner == p ? 1 : 0)
                && !TemporaryRules.IsTemporary(card), "discard reveals card; friendly ghost burn retains visual after gameplay mark clears");
        }
        game.PowerHistory.Last.Clear();
        int sameBatchCursor = session.Packets.Count;
        var hiddenGrant = Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
        TemporaryRules.Make(hiddenGrant); Generic.AddHandPhase(p.Opponent, hiddenGrant);
        int hiddenHelper = hiddenGrant.AppliedEnchantments.Single().Id;
        Generic.DiscardBlock(p.Opponent, hiddenGrant); session.Flush();
        bool discarded = false;
        foreach (var wire in session.Packets.Skip(sameBatchCursor).Where(w => w.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
            var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
            foreach (var entry in history.List)
            {
                var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                if (snapshot == null) continue;
                if (snapshot.Entity == hiddenGrant.Id && snapshot.Name != "")
                {
                    Check(entry.HasShowEntity, "same-batch identity first revealed by discard SHOW"); discarded = true;
                }
                if (!discarded && (snapshot.Entity == hiddenGrant.Id || snapshot.Entity == hiddenHelper))
                    Check(snapshot.Name == "" && snapshot.Tags.All(t => t.Name == 53 || t.Name == 50 || t.Name == 49 || t.Name == 263), "same-batch creation remains private until discard");
                Check(snapshot.Tags.All(t => !DormantRules.IsPrivateTag((GameTag)t.Name) && !TemporaryRules.IsPrivateTag((GameTag)t.Name)), "private state never in snapshots");
            }
        }
        Check(discarded, "same-batch discard reveal emitted");
        PrivateGrantOrigins(session, game);
        MissingPublicHelper(session, game);
        TemporaryTransfer();
        Console.WriteLine("PASS card-state transport: serialized Dormant/Temporary; private helpers; historical timer; missing FULL/helper/zero and secret-tag faults");
    }
    private static void RetiredTemporaryHelper()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var p = game.Player1; p.BaseMana = 10; p.UsedMana = 0;
        game.PowerHistory.Last.Clear();
        // Saved seed-4 replay: Temporary Totem Golem is played, then stolen/shuffled and redrawn.
        var card = Entity.FromCard(p, Cards.FromId("CORE_AT_052"));
        TemporaryRules.Make(card); Generic.AddHandPhase(p, card);
        var helper = card.AppliedEnchantments.Single(e => e.Card.Id == "GBL_999e");
        session.Flush();
        session.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, card));
        Check(!TemporaryRules.IsTemporary(card) && !card.AppliedEnchantments.Contains(helper), "played Temporary retires helper");
        game.PowerHistory.Last.Clear();
        new SabberStoneCore.Tasks.SimpleTasks.MoveToDeck(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET, opponent: true)
            .Process(game, p, p.Hero, card);
        Generic.Draw(p.Opponent, card);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(card.Zone == p.Opponent.HandZone && session.Client.Entities[card.Id].CardId == "", "former Temporary now hidden opponent hand");
        Check(!Invariants.Compare(game, session.Client).Any(), "retired helper is not a live private attachment");
        var shown = session.Client.Entities[helper.Id];
        Check(shown.Get(GameTag.ATTACHED) == 0 && shown.Get(GameTag.ZONE) == (int)Zone.REMOVEDFROMGAME, "retired helper detached and removed");
        shown.Tags[(int)GameTag.ATTACHED] = card.Id;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == helper.Id && v.Tag == "RETIRED_HELPER"), "retired attachment fault rejected even when parent hidden");
        shown.Tags[(int)GameTag.ATTACHED] = 0;
        shown.Tags[(int)GameTag.ZONE] = (int)Zone.PLAY;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == helper.Id && v.Tag == "RETIRED_HELPER"), "retired zone fault rejected even when parent hidden");
        Console.WriteLine("PASS RetiredTemporaryHelper: play/shuffle/opponent draw; retirement faults rejected");
    }
    private static void DormantLifecycle()
    {
        foreach (string cardId in new[] { "TIME_046", "CS2_182", "CORE_BT_156", "EDR_469" })
        foreach (bool bot in new[] { false, true })
        foreach (bool awake in new[] { true, false })
        foreach (string exit in new[] { "bounce", "shuffle", "change", "replace" })
        {
            var game = OfflinePoolTestRecipes.NewGame(); var owner = bot ? game.Player2 : game.Player1;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            var minion = (Minion)Entity.FromCard(owner, Cards.FromId(cardId));
            Generic.SummonBlock(game, minion, -1, owner.Hero);
            if (cardId != "TIME_046") Check(DormantRules.Make(minion, 3, "TIME_046e", owner.Hero), "external modern sleep");
            if (awake) Check(DormantRules.Awaken(minion), "modern awakening");
            session.Flush(); game.PowerHistory.Last.Clear();
            string label = $"Unpinned_DormantLifecycle {cardId}/{bot}/{awake}/{exit}";
            Check(session.Client.Entities[minion.Id].Get(GameTag.DORMANT_AWAKENED_THIS_TURN) == (awake ? 1 : 0), label + " wake visible");
            var omitted = session.Client.Copy(); int cursor = session.Packets.Count;
            int timerBefore = minion[GameTag.TAG_SCRIPT_DATA_NUM_1];
            if (exit == "bounce") Check(Generic.ReturnToHandBlock(owner, minion), label + " bounce");
            else if (exit == "shuffle")
            {
                owner.BoardZone.Remove(minion); minion.Reset();
                Check(Generic.ShuffleIntoDeck(owner, owner.Hero, minion), label + " shuffle");
            }
            else if (exit == "change") Generic.ChangeEntityBlock(owner, minion, Cards.FromId("CS2_120"), true);
            else Generic.TransformBlock(owner, Cards.FromId("CS2_120"), minion);
            if (timerBefore > 0 && cardId != "CS2_182")
                Check(game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Any(e => e.EntityId == minion.Id
                    && e.Tag == GameTag.TAG_SCRIPT_DATA_NUM_1 && e.Value == 0), label + " raw history publishes cleared natural timer");
            session.Flush(); game.PowerHistory.Last.Clear();
            var tags = new[] { GameTag.DORMANT, GameTag.DORMANT_VISUAL, GameTag.UNTOUCHABLE, GameTag.DORMANT_AWAKENED_THIS_TURN };
            Check(tags.All(t => minion[t] == 0), label + " engine exit zeros");
            if (!bot || exit == "change" || exit == "replace")
                Check(tags.All(t => session.Client.Entities[minion.Id].Get(t) == 0), label + " serialized exit zeros");
            if (cardId == "TIME_046") Check(minion[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0
                && session.Client.Entities[minion.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0, label + " parent timer cleared");
            bool publishedZero = false;
            foreach (var wire in session.Packets.Skip(cursor).Where(w => w.Id == 19))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
                publishedZero |= history.List.Any(e => e.HasTagChange && e.TagChange.Entity == minion.Id
                    && e.TagChange.Tag == 4606 && e.TagChange.Value == 0);
            }
            if (awake) Check(publishedZero, label + " explicit published 4606 zero before reset/replacement");
            if (exit == "bounce" || exit == "shuffle")
            {
                if (exit == "shuffle") Generic.AddHandPhase(owner, owner.DeckZone.Remove(minion));
                owner.HandZone.Remove(minion); Generic.SummonBlock(game, minion, -1, owner.Hero);
                session.Flush(); game.PowerHistory.Last.Clear();
                Check(minion.Card.Id == cardId && session.Client.Entities[minion.Id].Get(GameTag.DORMANT_AWAKENED_THIS_TURN) == 0,
                    label + " replay cannot retain awakening flag");
                if (cardId == "TIME_046") Check(minion[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
                    && session.Client.Entities[minion.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 3, label + " natural timer restarts");
                if (awake && exit == "bounce")
                {
                    foreach (var wire in session.Packets.Skip(cursor).Where(w => w.Id == 19))
                    {
                        using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                        var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
                        history.List.RemoveAll(e => e.HasTagChange && e.TagChange.Entity == minion.Id && e.TagChange.Tag == 4606 && e.TagChange.Value == 0);
                        foreach (var e in history.List.Where(e => e.HasShowEntity && e.ShowEntity.Entity == minion.Id))
                            e.ShowEntity.Tags.RemoveAll(t => t.Name == 4606 && t.Value == 0);
                        omitted.Apply(history);
                    }
                    // Only TIME_046 is declared modern after replay; external sleep expires at exit.
                    if (cardId == "TIME_046") Check(Compare(game, omitted).Any(v => v.Entity == minion.Id && v.Tag == "DORMANT_AWAKENED_THIS_TURN"),
                        label + " serialized missing-zero fault rejected after replay");
                }
            }
            Check(!Invariants.Compare(game, session.Client).Any(), label + " strict parity");
            var replayed = new ClientModel();
            foreach (var wire in session.Packets.Where(w => w.Id == 19))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream); replayed.Apply(history);
            }
            Check(!Invariants.Compare(game, replayed).Any(), label + " complete packet replay parity");
        }
        Console.WriteLine("PASS DormantLifecycleTransport: Unpinned_DormantLifecycle Unpinned_DormantCounterProjection; asleep/awakened bounce/shuffle/transform/replay; both views; external modern sleep; missing-4606-zero fault");
    }
    private static void TemporaryTransfer()
    {
        foreach (bool toHuman in new[] { false, true })
        {
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
            session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
            var game = match.Game; var p = game.Player1;
            game.PowerHistory.Last.Clear();
            IPlayable card;
            if (toHuman)
            {
                var thief = (Minion)Entity.FromCard(p, Cards.FromId("JAIL_205"));
                Generic.SummonBlock(game, thief, -1, p.Hero);
                card = Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
                TemporaryRules.Make(card); Generic.AddHandPhase(p.Opponent, card);
            }
            else
            {
                p.BaseMana = 10; p.UsedMana = 0;
                var bribe = Generic.DrawCard(p, Cards.FromId("JAIL_206"));
                Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, bribe)), "Dark Bribe starts");
                card = game.IdEntityDic[p.Choice.Choices.First()]; TemporaryRules.Make(card);
            }
            var helper = card.AppliedEnchantments.Single(e => e.Card.Id == "GBL_999e");
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(session.Client.Entities[helper.Id].CardId == (toHuman ? "" : "GBL_999e"), "helper initial visibility");
            var omitted = session.Client.Copy(); int cursor = session.Packets.Count;
            if (toHuman) Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(p)), "Rat Burglar end");
            else
            {
                var decision = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { card.Id }), out var error), "Dark Bribe gift: " + error);
            }
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(card.Controller == (toHuman ? p : p.Opponent) && TemporaryRules.IsTemporary(card)
                && helper.Controller == card.Controller, "transfer retains mark and helper ownership");
            var violations = Invariants.Compare(game, session.Client).Where(v => v.Entity == helper.Id).ToArray();
            Check(violations.Length == 0, "TemporaryTransfer Unpinned_TemporaryProjection " + (toHuman ? "bot-to-human" : "human-to-bot")
                + " helper: " + string.Join("; ", violations.Select(v => v.Tag + "=" + v.Shown)));
            var shown = session.Client.Entities[helper.Id];
            Check(shown.CardId == (toHuman ? "GBL_999e" : "") && shown.Get(GameTag.ATTACHED) == (toHuman ? card.Id : 0),
                "transferred helper identity and attachment");
            foreach (var wire in session.Packets.Skip(cursor).Where(w => w.Id == 19))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
                foreach (var entry in history.List)
                {
                    var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                    if (!toHuman && snapshot?.Entity == helper.Id)
                        Check(snapshot.Name == "" && snapshot.Tags.All(t => t.Name == 53 || t.Name == 50 || t.Name == 49 || t.Name == 263),
                            "transferred hidden helper snapshots remain structural only");
                    if (!toHuman && entry.HasTagChange && entry.TagChange.Entity == helper.Id)
                        Check(entry.TagChange.Value == 0 || entry.TagChange.Tag == 53 || entry.TagChange.Tag == 50 || entry.TagChange.Tag == 49 || entry.TagChange.Tag == 263,
                            "hidden transfer only clears formerly visible fields");
                }
                history.List.RemoveAll(e => toHuman ? e.HasShowEntity && e.ShowEntity.Entity == helper.Id
                    : e.HasTagChange && e.TagChange.Entity == helper.Id && e.TagChange.Value == 0
                        && e.TagChange.Tag != 53 && e.TagChange.Tag != 50 && e.TagChange.Tag != 49 && e.TagChange.Tag != 263);
                omitted.Apply(history);
            }
            Check(Invariants.Compare(game, omitted).Any(v => v.Entity == helper.Id && v.Tag == (toHuman ? "CARD_ID" : "PRIVATE_HELPER")),
                "serialized missing helper " + (toHuman ? "reveal" : "private-field clear") + " rejected");
        }
        Console.WriteLine("PASS TemporaryTransfer: serialized Rat Burglar reveal and Dark Bribe hide; helper identity/attachment/privacy");
    }
    private static void MissingPublicHelper(NativeSession session, Game game)
    {
        game.PowerHistory.Last.Clear();
        var omitted = session.Client.Copy(); int cursor = session.Packets.Count;
        var parent = (Minion)Entity.FromCard(game.Player2, Cards.FromId("TIME_046"));
        Generic.SummonBlock(game, parent, -1, game.Player2.Hero);
        int helper = parent.AppliedEnchantments.Single().Id;
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(!Invariants.Compare(game, session.Client).Any(), "opponent sleeper/helper public serialized projection");
        foreach (var wire in session.Packets.Skip(cursor).Where(w => w.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
            var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
            history.List.RemoveAll(e => e.HasFullEntity && e.FullEntity.Entity == helper
                || e.HasShowEntity && e.ShowEntity.Entity == helper || e.HasChangeEntity && e.ChangeEntity.Entity == helper
                || e.HasTagChange && e.TagChange.Entity == helper || e.HasHideEntity && e.HideEntity.Entity == helper);
            omitted.Apply(history);
        }
        Check(!omitted.Entities.ContainsKey(helper) && omitted.Entities.ContainsKey(parent.Id), "fault omits entire helper while retaining parent");
        Check(Invariants.Compare(game, omitted).Any(v => v.Entity == helper && v.Tag == "ENTITY"),
            "MissingPublicHelper general oracle rejects complete opponent Dormant helper omission");
        Console.WriteLine("PASS MissingPublicHelper: complete serialized opponent helper omission rejected by general oracle");
    }
    private static void PrivateGrantOrigins(NativeSession session, Game game)
    {
        foreach (bool setaside in new[] { false, true })
        foreach (bool immediateDiscard in new[] { false, true })
        {
            var owner = game.Player2;
            game.PowerHistory.Last.Clear(); int cursor = session.Packets.Count;
            var card = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: setaside ? owner.SetasideZone : null);
            if (setaside) owner.SetasideZone.Remove(card);
            TemporaryRules.Make(card); Generic.AddHandPhase(owner, card);
            int helper = card.AppliedEnchantments.Single().Id;
            if (immediateDiscard) Generic.DiscardBlock(owner, card);
            session.Flush(); game.PowerHistory.Last.Clear();
            if (!immediateDiscard)
            {
                Check(session.Client.Entities[card.Id].CardId == "", "private grant remains a hidden hand actor");
                Generic.DiscardBlock(owner, card); session.Flush(); game.PowerHistory.Last.Clear();
            }
            bool revealed = false, sawFull = false;
            foreach (var wire in session.Packets.Skip(cursor).Where(w => w.Id == 19))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
                foreach (var entry in history.List)
                {
                    var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                    if (snapshot != null && snapshot.Entity == card.Id && snapshot.Name != "")
                    {
                        Check(entry.HasShowEntity && snapshot.Tags.All(t => t.Name != (int)TemporaryRules.Temporary
                            && (t.Name != (int)GameTag.GHOSTLY || t.Value == 0)),
                            "private identity first revealed by explicit discard SHOW");
                        revealed = true;
                    }
                    if (snapshot != null && (snapshot.Entity == card.Id || snapshot.Entity == helper) && !revealed)
                    {
                        if (entry.HasFullEntity && snapshot.Entity == card.Id) sawFull = true;
                        Check(snapshot.Name == "" && snapshot.Tags.All(t => t.Name == 53 || t.Name == 50 || t.Name == 49 || t.Name == 263),
                            "PrivateGrantOrigins Unpinned_TemporaryProjection masks initial FULL/SHOW, including SETASIDE");
                    }
                    if (!revealed && entry.HasTagChange && (entry.TagChange.Entity == card.Id || entry.TagChange.Entity == helper))
                        Check(entry.TagChange.Tag == 53 || entry.TagChange.Tag == 50 || entry.TagChange.Tag == 49 || entry.TagChange.Tag == 263,
                            "private grant tag changes structural only");
                }
            }
            Check(sawFull && revealed, "private grant emits masked FULL then discard reveal");
        }
        Console.WriteLine("PASS PrivateGrantOrigins: serialized INVALID/SETASIDE creation; hidden hand; same/later-batch discard reveal");
    }
}
