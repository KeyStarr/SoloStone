using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using NativeOption = PegasusGame.Option;
using EngineZone = SabberStoneCore.Enums.Zone;

static partial class Program
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new Exception("TEST: " + message); }
    static void Main()
    {
        QuestDefinitions();
        LocationProjection();
        RequiredLiveSnapshots();
        ProofPlayabilityPolicy();
        var packets = new List<(int Id, IProtoBuf Message)>();
        var match = new NativeMatch((id, packet) => packets.Add((id, packet)));
        match.Start();
        var choice = (EntityChoices)packets.Last(p => p.Id == 17).Message;
        match.SendChoices(choice.Id, new List<int>());
        match.Game.Player1.BaseMana = 10;
        match.Game.Player1.UsedMana = 0;
        SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player2,Cards.FromId("CS2_200"),zone:match.Game.Player2.BoardZone);
        var librarian = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1,
            Cards.FromId("CORE_SW_066"), zone: match.Game.Player1.HandZone);
        typeof(NativeMatch).GetMethod("SendHumanOptions", Private).Invoke(match, null);
        var options = (AllOptions)packets.Last(p => p.Id == 14).Message;
        var sameSource = options.Options.Where(o => o.Type_ == NativeOption.Type.POWER && o.MainOption?.Id == librarian.Id).ToList();
        Check(sameSource.Count == 2, "Librarian needs ordinary play and Trade options");
        Check(sameSource.Count(o => o.MainOption.PowerKeyword == 1720) == 1,
            "Trade option loses keyword1720 on native wire");
        Check(sameSource.Single(o => o.MainOption.PowerKeyword == 0).MainOption.Targets.Count > 0, "ordinary Silence targets lost");
        var trade = sameSource.Single(o => o.MainOption.PowerKeyword == 1720);
        Check(trade.MainOption.Targets.Count == 0 && trade.SubOptions.Count == 0, "Trade has targets/suboptions");
        int index = options.Options.IndexOf(trade);
        foreach (var args in new[] { (options.Id+1,index,0,-1,0), (options.Id,index,1,-1,0),
            (options.Id,index,0,0,0), (options.Id,index,0,-1,1) })
        {
            var before = Fingerprint(match); int count = packets.Count; bool rejected = false;
            try { match.SendOption(args.Item1,args.Item2,args.Item3,args.Item4,args.Item5); }
            catch (ArgumentException) { rejected = true; }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && before == Fingerprint(match) && count == packets.Count, "malformed/stale Trade mutates state");
        }
        var traces = new List<string>(); match.TradeTrace=traces.Add;
        int hand = match.Game.Player1.HandZone.Count, deck = match.Game.Player1.DeckZone.Count;
        match.SendOption(options.Id,index,0,-1,0);
        Check(librarian.Zone.Type == SabberStoneCore.Enums.Zone.DECK && match.Game.Player1.UsedMana == 1
            && match.Game.Player1.HandZone.Count == hand && match.Game.Player1.DeckZone.Count == deck,
            "Trade did not spend1 and replace hand card");
        var trace=JsonDocument.Parse(traces.Single()).RootElement;
        Check(trace.GetProperty("optionsId").GetInt32()==options.Id && trace.GetProperty("optionIndex").GetInt32()==index
            && trace.GetProperty("source").GetInt32()==librarian.Id && trace.GetProperty("humanReplacementEntity").GetInt32()!=librarian.Id,
            "Trade trace loses strict option/replacement identity");
        Check(trace.GetProperty("engineHistory").EnumerateArray().Select(e=>e.GetString()).SequenceEqual(
            trace.GetProperty("nativeHistory").EnumerateArray().Select(e=>e.GetString())), "Trade engine/native ordered histories differ");
        SerializePackets(packets);
        Console.WriteLine("PASS strict native Trade packet and ordered trace");
        HumanTradeDrawEdges();
        OrdinaryLibrarianPackets();
        OpponentTradeHistory();
        ProofLoanersAndFixture();
        DefinitionAudit();
        var previewPolicy=typeof(NativeUIProjection).GetMethod("UseOriginalProofDefinition",BindingFlags.Static|BindingFlags.NonPublic);
        Check(previewPolicy!=null,"new Core previews must bypass legacy overrides");
        foreach(string id in LocalDecks.Get("proof-mage").CardIds.Concat(LocalDecks.Get("proof-warrior").CardIds).Distinct())
            Check((bool)previewPolicy.Invoke(null,new object[]{id}) && NativeUIProjection.SupportsPreview(id),"Core original-view bypass/audit registration missing:"+id);
        foreach(string id in new[]{"GAME_005","HERO_08","HERO_08bp","CS2_222o","EX1_604o","EX1_007","CS2_034","UNKNOWN"})
            Check(!(bool)previewPolicy.Invoke(null,new object[]{id}),"Core override bypass escaped exact20:"+id);
        ProofPacketGames();
        InputTraceBinding();
    }
    // The current engine rejects empty-deck Trade; exercise its transport outcomes with real zone/draw actions.
    sealed class TradeDrawTask : PlayerTask
    {
        public bool Draw;
        public override bool Process()
        {
            Controller.UsedMana++;
            Controller.DeckZone.Add(Controller.HandZone.RemoveForTrade(Source));
            Check(Controller.DeckZone.Count == 1 && Controller.DeckZone.TopCard == Source,
                "edge Trade deck must contain only source");
            if (Draw) SabberStoneCore.Actions.Generic.Draw(Controller);
            return true;
        }
        public override string FullPrint() => "Trade draw edge";
    }
    static void HumanTradeDrawEdges()
    {
        foreach (bool draw in new[] { true, false })
        {
            var packets = new List<(int Id,IProtoBuf Message)>();
            var match = new NativeMatch((id,packet) => packets.Add((id,packet)));
            match.Start();
            match.SendChoices(((EntityChoices)packets.Last(p => p.Id == 17).Message).Id,new List<int>());
            var player = match.Game.Player1;
            player.BaseMana = 10; player.UsedMana = 0;
            foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(player,Cards.FromId("CORE_SW_066"),zone:player.HandZone);
            var traces = new List<string>(); match.TradeTrace = traces.Add;
            typeof(NativeMatch).GetMethod("Process",Private).Invoke(match,new object[] {
                new TradeDrawTask { Game=match.Game, Controller=player, Source=source, PlayerTaskType=PlayerTaskType.TRADE_CARD, Draw=draw }
            });
            typeof(NativeMatch).GetMethod("SendHistory",Private).Invoke(match,new object[] { match.Game.PowerHistory.Last });
            var trace = JsonDocument.Parse(traces.Single()).RootElement;
            Check(trace.GetProperty("source").GetInt32() == source.Id, "edge Trade trace loses source");
            if (draw)
                Check(player.HandZone.Contains(source) && trace.GetProperty("humanReplacementEntity").GetInt32() == source.Id
                    && trace.GetProperty("humanReplacementCard").GetString() == source.Card.Id, "redrawn Trade source missing from trace");
            else
                Check(player.DeckZone.Contains(source) && !trace.TryGetProperty("humanReplacementEntity",out _)
                    && !trace.TryGetProperty("humanReplacementCard",out _), "no-draw Trade trace invents replacement");
            SerializePackets(packets);
        }
        Console.WriteLine("PASS human Trade same-entity/no-draw trace");
    }
    static void QuestDefinitions()
    {
        foreach (var pair in new[] { (462, 1), (535, 6), (1089, 121956), (4238, 1) })
        {
            Check(NativeUIProjection.TryGetDefinitionTag(true, true, true, "TLC_426", pair.Item1, out int value)
                && value == pair.Item2, "quest canonical definition tag " + pair.Item1);
            Check(NativeUIProjection.TryGetDbfTag(true, "TLC_426", pair.Item1, out value)
                && value == pair.Item2, "quest DBF preview tag " + pair.Item1);
            foreach (string id in new[] { "TLC_987", "TLC_426t", "UNG_942" })
                Check(!NativeUIProjection.TryGetDefinitionTag(true, true, true, id, pair.Item1, out value),
                    "quest definition exception escaped modern registry: " + id);
            Check(!NativeUIProjection.TryGetDefinitionTag(false, true, true, "TLC_426", pair.Item1, out value), "definition context required");
        }
        Check(!NativeUIProjection.TryGetDefinitionTag(true, true, true, "TLC_426", 534, out _),
            "runtime progress not a static definition override");
        Console.WriteLine("PASS modern quest native definitions: canonical metadata and preview; scoped exceptions");
    }
    static void RequiredLiveSnapshots()
    {
        var failures = new List<string>();
        void Expect(bool ok, string text) { if (!ok) failures.Add(text); }
        var packets = new List<(int Id,IProtoBuf Message)>();
        var match = new NativeMatch((id, packet) => packets.Add((id, packet)), "proof-mage", "proof-warrior", 0);
        match.Start();
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", Private);
        var flush = typeof(NativeMatch).GetMethod("SendHistory", Private);
        PowerHistoryData Convert(SabberStoneCore.Kettle.IPowerHistoryEntry entry) =>
            (PowerHistoryData)convert.Invoke(match, new object[] { entry });
        void Send(params SabberStoneCore.Kettle.IPowerHistoryEntry[] entries) => flush.Invoke(match, new object[] { entries.ToList() });
        PowerHistoryEntity Payload(PowerHistoryData data) => data.HasFullEntity ? data.FullEntity : data.HasShowEntity ? data.ShowEntity : data.ChangeEntity;
        void Values(PowerHistoryEntity entity, int school, int tradeable, int cost, string label)
        {
            foreach (var pair in new[] { (1635, school), (1720, tradeable), (1743, cost) })
                Expect(entity.Tags.Count(tag => tag.Name == pair.Item1 && tag.Value == pair.Item2) == 1,
                    label + " missing/wrong live tag " + pair.Item1 + "=" + pair.Item2);
        }
        foreach (string id in new[] { "CORE_SW_066", "CORE_CS2_024" })
        {
            var entity = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId(id), zone: match.Game.Player1.HandZone);
            var full = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity);
            var show = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(entity);
            var change = new SabberStoneCore.Kettle.PowerHistoryChangeEntity { CardId = id, Entity = full.Entity };
            entity[(GameTag)1635] = 7; entity[(GameTag)1720] = 0; entity[(GameTag)1743] = 9;
            entity.Card = Cards.FromId(id == "CORE_SW_066" ? "CORE_CS2_024" : "CORE_SW_066");
            foreach (var entry in new SabberStoneCore.Kettle.IPowerHistoryEntry[] { full, show, change })
            {
                Send(entry);
                var emitted = ((PowerHistory)packets.Last(packet => packet.Id == 19).Message).List
                    .Single(data => data.HasFullEntity || data.HasShowEntity || data.HasChangeEntity);
                Values(Payload(emitted), id == "CORE_CS2_024" ? 3 : 0, id == "CORE_SW_066" ? 1 : 0,
                    id == "CORE_SW_066" ? 1 : 0, id + " " + entry.PowerType + " historical snapshot");
            }
            Expect(!full.Entity.Tags.ContainsKey((GameTag)1720) && !full.Entity.Tags.ContainsKey((GameTag)1635), "transport mutated original snapshot");
            full.Entity.Tags[(GameTag)1635] = 0; full.Entity.Tags[(GameTag)1720] = 0; full.Entity.Tags[(GameTag)1743] = 7;
            foreach (var entry in new SabberStoneCore.Kettle.IPowerHistoryEntry[] { full, show, change })
            {
                var snapshot = entry is SabberStoneCore.Kettle.PowerHistoryShowEntity ? show.Entity : full.Entity;
                snapshot.Tags[(GameTag)1635] = 0; snapshot.Tags[(GameTag)1720] = 0; snapshot.Tags[(GameTag)1743] = 7;
                Values(Payload(Convert(entry)), 0, 0, 7, id + " explicit overrides");
            }
            foreach (EngineZone zone in new[] { EngineZone.HAND, EngineZone.DECK })
            {
                full.Entity.Tags[GameTag.CONTROLLER] = 2; full.Entity.Tags[GameTag.ZONE] = (int)zone;
                show.Entity.Tags[GameTag.CONTROLLER] = 2; show.Entity.Tags[GameTag.ZONE] = (int)zone;
                foreach (var entry in new SabberStoneCore.Kettle.IPowerHistoryEntry[] { full, show, change })
                {
                    var hidden = Payload(Convert(entry));
                    Expect(hidden.Name == "" && hidden.Tags.All(tag => !new[] { 1635, 1720, 1743 }.Contains(tag.Name)), "hidden modern tags leaked");
                }
            }
        }
        var transformed = new SabberStoneCore.Kettle.PowerHistoryChangeEntity {
            CardId = "CORE_CS2_024", Entity = new SabberStoneCore.Kettle.PowerHistoryEntity {
                Id = match.Game.Player1.Hero.Id, Name = "CORE_SW_066", Tags = new Dictionary<GameTag,int> {
                    { GameTag.CONTROLLER, 1 }, { GameTag.ZONE, (int)EngineZone.HAND }, { GameTag.CARDTYPE, (int)CardType.SPELL }
                }
            }
        };
        Values(Payload(Convert(transformed)), 3, 0, 0, "CHANGE target identity overrides old snapshot identity");
        foreach (string id in new[] { "CS2_024", "UNKNOWN", "CORE_CS1_112" })
        {
            var snapshot = new SabberStoneCore.Kettle.PowerHistoryEntity { Id = match.Game.Player1.Hero.Id, Name = id,
                Tags = new Dictionary<GameTag,int> { { GameTag.CONTROLLER, 1 }, { GameTag.ZONE, (int)EngineZone.HAND } } };
            // Printed traits are public for ordinary cards too; only proof-required zero injection is exact27.
            Expect(StandardProofNative.RequiredLiveSnapshot(id, snapshot.Tags) == null,
                "proof-required tags escaped exact27: " + id);
            var printed = Payload(Convert(new SabberStoneCore.Kettle.PowerHistoryFullEntity { Entity = snapshot })).Tags;
            foreach (int tag in new[] { 1635, 1720, 1743 })
            {
                int expected = id == "UNKNOWN" ? 0 : Cards.FromId(id)[(GameTag)tag];
                Expect(printed.Where(value => value.Name == tag).All(value => value.Value == expected)
                    && (expected == 0 || printed.Any(value => value.Name == tag && value.Value == expected)),
                    "ordinary printed trait mismatch: " + id + ":" + tag);
            }
        }
        var oracle = typeof(NativeMatch).GetMethod("RequiredLiveTagMatches", Private);
        Expect(oracle != null, "required-live oracle missing");
        if (oracle != null)
        {
            bool Matches(IPlayable entity, int tag, bool present, int value) =>
                (bool)oracle.Invoke(match, new object[] { entity, tag, present, value });
            foreach (var pair in new[] { ("CORE_SW_066", 1720, 1), ("CORE_SW_066", 1743, 1), ("CORE_CS2_024", 1635, 3) })
            {
                var entity = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId(pair.Item1), zone: match.Game.Player1.HandZone);
                Send(SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity));
                var transport = (Dictionary<int, Dictionary<int,int>>)typeof(NativeMatch).GetField("visibleTransportTags", Private).GetValue(match);
                transport[entity.Id].Remove(pair.Item2);
                Expect(!Matches(entity, pair.Item2, false, 0) && !Matches(entity, pair.Item2, true, 0)
                    && Matches(entity, pair.Item2, true, pair.Item3), "omitted packet/native bypassed independent live oracle");
                entity[(GameTag)pair.Item2] = 0;
                Expect(Matches(entity, pair.Item2, true, 0) && !Matches(entity, pair.Item2, true, pair.Item3), "oracle ignored current explicit zero");
                entity.Zone = match.Game.Player1.GraveyardZone;
                Expect(Matches(entity, pair.Item2, true, pair.Item3), "historical oracle read future engine state");
                Send(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(entity.Id, (GameTag)pair.Item2, 7));
                Expect(Matches(entity, pair.Item2, true, 7) && !Matches(entity, pair.Item2, true, pair.Item3), "historical oracle ignored original tag change");
            }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
        SerializePackets(packets);
        Console.WriteLine("PASS required live snapshot traits, historical overrides, hidden privacy and independent omission oracle");
    }
    static void ProofPlayabilityPolicy()
    {
        var policy = typeof(StandardProofNative).GetMethod("AllowNativePlayability", BindingFlags.Static | BindingFlags.NonPublic);
        Check(policy != null, "audited isolated proof cards need native playability policy");
        var admitted = new[] {
            "CORE_EX1_011", "CORE_CS2_189", "CORE_EX1_096", "CORE_GVG_085", "CORE_LOOT_413",
            "CORE_EX1_007", "CORE_CS2_179", "CORE_SW_066", "Core_CS2_200", "CORE_CS2_222",
            "CORE_CS2_024", "CORE_CS2_023", "CORE_CS2_029", "CORE_CS2_028", "CORE_CS2_032",
            "CORE_CS2_108", "CORE_EX1_391", "CORE_AT_064", "CORE_EX1_606", "CORE_EX1_604"
        };
        bool Allows(bool active, bool isolated, bool auditing, bool passed, bool failed, string id) =>
            (bool)policy.Invoke(null, new object[] { active, isolated, auditing, passed, failed, id });
        foreach (string id in admitted)
        {
            Check(Allows(true, true, false, true, false, id), "audited proof collectible rejected: " + id);
            for (int state = 0; state < 32; state++)
                if (state != 11)
                    Check(!Allows((state & 1) != 0, (state & 2) != 0, (state & 4) != 0,
                        (state & 8) != 0, (state & 16) != 0, id), "playability escaped context gate: " + state + ":" + id);
        }
        foreach (string id in new[] { null, "", "UNKNOWN", "CORE_CS2_200", "core_sw_066", "CORE_SW_066 ",
            "EX1_011", "CS2_024", "CS2_108", "CORE_CS1_112", "CORE_CS2_091",
            "HERO_08", "HERO_01", "HERO_08bp", "HERO_01bp", "GAME_005", "CS2_222o", "EX1_604o", "CS2_034", "CS2_102" }
            .Concat(LocalCardCatalog.Current.Select(card => card.Id))
            .Concat(LocalCardCatalog.SupplementalGeneratedIds).Concat(LocalCardCatalog.LegacyGeneratedPublicIds)
            .Where(id => !admitted.Contains(id)))
            Check(!Allows(true, true, false, true, false, id), "playability escaped exact20: " + id);
        Console.WriteLine("PASS exact20 native playability and all context gates");
    }
    static void OrdinaryLibrarianPackets()
    {
        foreach (bool targeted in new[]{false,true})
        {
            var packets = new List<(int Id,IProtoBuf Message)>();
            var match = new NativeMatch((id,packet) => packets.Add((id,packet)));
            match.Start();
            match.SendChoices(((EntityChoices)packets.Last(p => p.Id == 17).Message).Id,new List<int>());
            match.Game.Player1.BaseMana = 10; match.Game.Player1.UsedMana = 0;
            var target = targeted ? SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player2,
                Cards.FromId("CS2_200"),zone:match.Game.Player2.BoardZone) : null;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1,
                Cards.FromId("CORE_SW_066"),zone:match.Game.Player1.HandZone);
            typeof(NativeMatch).GetMethod("SendHumanOptions",Private).Invoke(match,null);
            var options = (AllOptions)packets.Last(p => p.Id == 14).Message;
            var sameSource = options.Options.Where(o => o.Type_ == NativeOption.Type.POWER && o.MainOption?.Id == source.Id).ToList();
            Check(sameSource.Count == 2 && sameSource.Count(o => o.MainOption.PowerKeyword == 1720) == 1,
                "ordinary Librarian fixture lacks simultaneous Trade");
            var play = sameSource.Single(o => o.MainOption.PowerKeyword == 0);
            Check(targeted ? play.MainOption.Targets.Any(t => t.Id == target.Id) : play.MainOption.Targets.Count == 0,
                "ordinary Librarian target contract changed");
            int hand=match.Game.Player1.HandZone.Count, deck=match.Game.Player1.DeckZone.Count;
            var traces = new List<string>(); match.TradeTrace=traces.Add;
            match.SendOption(options.Id,options.Options.IndexOf(play),target?.Id??0,-1,1);
            Check(source.Zone.Type == SabberStoneCore.Enums.Zone.PLAY && match.Game.Player1.UsedMana == 4
                && match.Game.Player1.HandZone.Count == hand-1 && match.Game.Player1.DeckZone.Count == deck
                && traces.Count == 0,"ordinary Librarian resolved as Trade or lost its native target");
            SerializePackets(packets);
        }
        Console.WriteLine("PASS ordinary Librarian packet with/without target while Trade legal");
    }
    static void OpponentTradeHistory()
    {
        var packets = new List<(int Id,IProtoBuf Message)>();
        var match = new NativeMatch((id,packet) => packets.Add((id,packet)));
        match.Start();
        match.SendChoices(((EntityChoices)packets.Last(p => p.Id == 17).Message).Id,new List<int>());
        void Process(PlayerTask task) => typeof(NativeMatch).GetMethod("Process",Private).Invoke(match,new object[]{task});
        void Flush() => typeof(NativeMatch).GetMethod("SendHistory",Private).Invoke(match,new object[]{match.Game.PowerHistory.Last});
        Process(match.Game.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
        Flush();
        match.Game.Player2.BaseMana = 10; match.Game.Player2.UsedMana = 0;
        var source = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player2,Cards.FromId("CORE_SW_066"),zone:match.Game.Player2.HandZone);
        Flush();
        int cursor = packets.Count;
        var traces=new List<string>(); match.TradeTrace=traces.Add;
        var beforeHand = match.Game.Player2.HandZone.Select(c => c.Id).ToArray();
        Process(match.Game.Player2.Options().Single(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.TRADE_CARD));
        Flush();
        int replacement = match.Game.Player2.HandZone.Single(c => !beforeHand.Contains(c.Id)).Id;
        var history = packets.Skip(cursor).Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        int start = history.FindIndex(e => e.HasPowerStart && (int)e.PowerStart.Type == 13 && e.PowerStart.Source == source.Id);
        int show = history.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == source.Id && e.ShowEntity.Name == "CORE_SW_066");
        int hide = history.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == source.Id);
        int reset = history.FindIndex(e => e.HasTagChange && e.TagChange.Entity == source.Id && e.TagChange.Tag == 2045 && e.TagChange.Value == 0);
        Check(start >= 0 && show > start, "opponent Trade source not public after DECK_ACTION start");
        Check(history[show].ShowEntity.Tags.Any(t => t.Name == 2045 && t.Value == 1), "public Trade reveal lacks2045=1");
        Check(hide > show && reset > hide, "opponent Trade hide/reset lifecycle missing");
        Check(!history.Any(e => e.HasShowEntity && e.ShowEntity.Entity == replacement && e.ShowEntity.Name.Length > 0), "opponent replacement leaked");
        Check(!history.Any(e => e.HasTagChange && e.TagChange.Entity == replacement && !new[]{49,50,202,263}.Contains(e.TagChange.Tag)), "opponent replacement private tags leaked");
        var trace=JsonDocument.Parse(traces.Single()).RootElement;
        Check(!trace.TryGetProperty("humanReplacementEntity",out _) && !trace.TryGetProperty("humanReplacementCard",out _),"opponent trace exposes replacement identity");
        Check(trace.GetProperty("engineHistory").EnumerateArray().Select(e=>e.GetString()).SequenceEqual(
            trace.GetProperty("nativeHistory").EnumerateArray().Select(e=>e.GetString())),"opponent Trade engine/native history differs");
        SerializePackets(packets);
        Console.WriteLine("PASS opponent Trade history and trace privacy");
    }
    static void ProofLoanersAndFixture()
    {
        Check(LocalDecks.All.Any(d => d.Id == "proof-mage") && LocalDecks.All.Any(d => d.Id == "proof-warrior"), "candidate proof loaners missing");
        var shared = "CORE_EX1_011,CORE_CS2_189,CORE_EX1_096,CORE_GVG_085,CORE_LOOT_413,CORE_EX1_007,CORE_CS2_179,CORE_SW_066,Core_CS2_200,CORE_CS2_222".Split(',');
        foreach (var pair in new[] { ("proof-mage","HERO_08","CORE_CS2_024,CORE_CS2_023,CORE_CS2_029,CORE_CS2_028,CORE_CS2_032",900000001L),
            ("proof-warrior","HERO_01","CORE_CS2_108,CORE_EX1_391,CORE_AT_064,CORE_EX1_606,CORE_EX1_604",900000002L) })
        {
            var deck = LocalDecks.Get(pair.Item1);
            Check(deck.Hero == pair.Item2 && deck.CardIds.Count == 30 && deck.CardIds.GroupBy(id => id).All(g => g.Count() == 2)
                && deck.CardIds.Distinct().OrderBy(id => id).SequenceEqual(shared.Concat(pair.Item3.Split(',')).OrderBy(id => id)), "proof30 fingerprint changed");
            var ids = deck.CardIds; ids.Clear(); Check(deck.Build().Count == 30,"loaner mutable");
            Check(NativeUIProjection.DeckId(pair.Item1) == pair.Item4 && NativeUIProjection.IsLoanerId(pair.Item4)
                && !NativeUIProjection.IsPersonalId(pair.Item4) && NativeUIProjection.IsExactLoaner(pair.Item4,deck.Hero,deck.CardIds), "proof native mapping collides or loses fingerprint");
        }
        var ranks = NativeUIProjection.ExpectedNativeSortOrders(new[]{new KeyValuePair<long,long>(910000010,0)});
        Check(ranks[900000001] == 0 && ranks[900000002] == 1 && ranks.Values.Distinct().Count() == ranks.Count, "proof tiles not first or ranks duplicate");
        Check(LocalCardCatalog.Current.Count == 252
            && LocalEconomy.Catalog.Count == 252 + LocalCoreProgression.AvailableCards.Count
            && new HashSet<string>(LocalEconomy.Catalog.Select(c => c.Id), StringComparer.Ordinal).SetEquals(
                LocalCardCatalog.Current.Concat(LocalCoreProgression.AvailableCards).Select(c => c.Id))
            && LocalEconomy.StarterCollection.Count == 93
            && !LocalCardCatalog.Current.Any(c => c.Id == "CORE_SW_066")
            && new[] { 1, 2 }.All(version => !LocalCardCatalog.PackPool(version).Any(c => c.Id == "CORE_SW_066")),
            "proof leaked into economy or Classic/Core catalog changed");
        string root = Path.Combine(Path.GetTempPath(),"hs-standard-native-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        string path = Path.Combine(root,"profile.xml"), id;
        try
        {
            using(var profile = new LocalProfile(path))
            {
                var record = profile.Begin("proof-mage","proof-warrior",0); id = record.Id;
                profile.Settle(id,"Lost",26,false);
                profile.Settle(id,"Lost",26,false);
                Check(profile.History.Count == 1 && profile.Pending == null,"fixture settlement duplicated");
            }
            using(var profile = new LocalProfile(path))
            {
                int gold = profile.Gold;
                profile.Settle(id,"Lost",26,false);
                Check(profile.Gold == gold && profile.History.Count == 1 && profile.History[0].Deck == "proof-mage", "fixture restart loses/duplicates result");
            }
        }
        finally { Directory.Delete(root,true); }
        Console.WriteLine("PASS exact immutable loaners and fixture settlement/restart");
    }
    static void DefinitionAudit()
    {
        var seen = new HashSet<string>(); var records = new List<string>();
        var setReads = new HashSet<string>(); int rawSetReads = 0;
        int Dbid(string id) { seen.Add(id); return Cards.FromId(StandardProofNative.EngineId(id)).AssetId; }
        int CardSet(string id)
        {
            setReads.Add(id);
            return id == "HERO_08" || id == "HERO_01" ? 17
                : id == "GAME_005" ? 1637 : id == "CS2_222o" || id == "HERO_08bp" || id == "HERO_01bp" ? 1635
                : Cards.FromId(StandardProofNative.EngineId(id))[GameTag.CARD_SET];
        }
        int Direct(string id,int tag)
        {
            if(tag == 183) { rawSetReads++; return 0; } // Native sets may exist only in CardSetTiming metadata.
            return Cards.FromId(StandardProofNative.EngineId(id))[(GameTag)tag];
        }
        int Referenced(string id,int tag) => Cards.FromId(StandardProofNative.EngineId(id)).RefTags.TryGetValue((GameTag)tag,out int v)?v:0;
        StandardProofNative.AuditDefinitions(Dbid,CardSet,_ => throw new Exception("Static timing lookup bypassed nonzero semantic set"),Direct,Referenced,records.Add);
        Check(seen.Count == 27 && seen.Contains("Core_CS2_200") && seen.Contains("HERO_08bp") && seen.Contains("CS2_222o"),"audit skipped closure");
        Check(rawSetReads == 0 && setReads.SetEquals(StandardProofNative.DefinitionIds)
            && records.Count(row => row.Contains("\tcard-set\t183\t")) == 27, "integer183 did not use dedicated original set reader for exact27");
        var byDbid = StandardProofNative.DefinitionIds.ToDictionary(Dbid);
        StandardProofNative.CardSetTiming Timing(int dbid,int set,int row=1,bool exists=true) => new StandardProofNative.CardSetTiming {
            RowId=row, CardId=dbid, CardSetId=set, NativeSetExists=exists, EventType=200+row, EventsReceived=false, EventKnown=false, EventActive=false
        };
        StandardProofNative.CardSetTiming[] ExactTimings(int dbid) => new[]{Timing(dbid,CardSet(byDbid[dbid])),Timing(dbid,CardSet(byDbid[dbid]),2)};
        var offlineRows = new List<string>();
        StandardProofNative.AuditDefinitions(Dbid,_ => 0,ExactTimings,Direct,Referenced,offlineRows.Add);
        Check(offlineRows.Count(row => row.Contains("\tcard-set\t183\t") && row.Contains("\t0\t") && row.Contains("\tstatic-offline exact set")) == 27
            && offlineRows.Count(row => row.Contains("\tcard-set-timing\t") && row.Contains(";eventType=") && row.EndsWith(";eventsReceived=False;known=False;active=False")) == 54,
            "offline static evidence hid original zero or native timing rows");
        var historicalIds = new[]{"CORE_LOOT_413","CORE_EX1_007","CORE_SW_066","Core_CS2_200","CORE_CS2_024","CORE_CS2_028","CORE_CS2_032","CORE_AT_064","CORE_EX1_606","HERO_08bp","HERO_01bp","GAME_005","CS2_222o"};
        int HistoricalSet(string id) => StandardProofNative.CollectibleIds.Contains(id) ? 1810 : 2;
        StandardProofNative.CardSetTiming[] HistoricalTimings(int dbid) => new[]{Timing(dbid,CardSet(byDbid[dbid]),0),Timing(dbid,HistoricalSet(byDbid[dbid]),0)};
        var historicalRows = new List<string>();
        StandardProofNative.AuditDefinitions(Dbid,id => historicalIds.Contains(id) ? 0 : CardSet(id),HistoricalTimings,Direct,Referenced,historicalRows.Add);
        Check(historicalRows.Count(row => row.Contains("\tcard-set\t183\t") && row.Contains("\t0\t") && row.Contains("expected static membership with declared historical alternative")) == 13
            && historicalRows.Count(row => row.Contains("\tcard-set-timing\t") && row.Contains("rowId=0;") && row.Contains(";nativeSetExists=True;")) == 26,
            "observed historical timing shapes lost original zero, rows or explicit decision");
        foreach(string changed in StandardProofNative.DefinitionIds)
        {
            int dbid=Dbid(changed), set=CardSet(changed);
            foreach(var timings in new[]{new StandardProofNative.CardSetTiming[0],new[]{Timing(dbid,0)},new[]{Timing(dbid,-1)},
                new[]{Timing(dbid,set+1)},new[]{Timing(dbid,set),Timing(dbid,set+1,2)},new[]{Timing(dbid+1,set)},null,new StandardProofNative.CardSetTiming[]{null},
                new[]{Timing(dbid,set,exists:false)},new[]{Timing(dbid,set),Timing(dbid,set,2,false)}})
            {
                bool rejected=false; var invalidRows = new List<string>();
                try { StandardProofNative.AuditDefinitions(Dbid,id => id == changed ? 0 : CardSet(id),_ => timings,Direct,Referenced,invalidRows.Add); }
                catch(InvalidOperationException) { rejected=true; }
                Check(rejected && invalidRows.Any(row => row.StartsWith(changed+"\t") && row.Contains("\tcard-set\t183\t") && row.EndsWith("\tFATAL")),
                    "invalid static membership admitted:"+changed);
            }
        }
        foreach(string changed in StandardProofNative.DefinitionIds)
        foreach(int alternate in new[]{1810,2})
        {
            int dbid=Dbid(changed), set=CardSet(changed);
            bool allowed = alternate == 1810 ? StandardProofNative.CollectibleIds.Contains(changed) && set == 1637
                : new[]{"HERO_08bp","HERO_01bp","GAME_005","CS2_222o"}.Contains(changed);
            foreach(bool includeExpected in new[]{false,true})
            foreach(bool alternateExists in new[]{false,true})
            {
                var timings = includeExpected ? new[]{Timing(dbid,set),Timing(dbid,alternate,2,alternateExists)} : new[]{Timing(dbid,alternate,2,alternateExists)};
                bool rejected=false;
                try { StandardProofNative.AuditDefinitions(Dbid,id => id == changed ? 0 : CardSet(id),_ => timings,Direct,Referenced,_ => {}); }
                catch(InvalidOperationException) { rejected=true; }
                Check(rejected != (allowed && includeExpected && alternateExists),"historical allowlist/expected row/set record boundary failed:"+changed+":"+alternate);
            }
        }
        foreach(string changed in StandardProofNative.DefinitionIds)
        foreach(int value in new[]{0,CardSet(changed)+1})
        {
            bool rejected = false; var setRows = new List<string>();
            try { StandardProofNative.AuditDefinitions(Dbid,id => id == changed ? value : CardSet(id),value == 0 ? _ => new StandardProofNative.CardSetTiming[0] : ExactTimings,Direct,Referenced,setRows.Add); }
            catch(InvalidOperationException) { rejected = true; }
            Check(rejected && setRows.Any(row => row.StartsWith(changed+"\t") && row.Contains("\tcard-set\t183\t") && row.EndsWith("\tFATAL")),
                "original semantic set zero/drift waived:"+changed+":"+value);
        }
        foreach(var changed in new[]{("CORE_SW_066",1720),("CORE_SW_066",1743),("CORE_CS2_024",1635),("GAME_005",48)})
        {
            bool rejected = false;
            try { StandardProofNative.AuditDefinitions(Dbid,CardSet,ExactTimings,(id,tag) => Direct(id,tag)+(id==changed.Item1 && tag==changed.Item2?1:0),Referenced,_ => {}); }
            catch(InvalidOperationException) { rejected = true; }
            Check(rejected,"original definition mismatch waived:"+changed);
        }
        var badDbidRows=new List<string>(); bool badDbidRejected=false;
        try { StandardProofNative.AuditDefinitions(id=>Dbid(id)+(id=="Core_CS2_200"?1:0),CardSet,ExactTimings,Direct,Referenced,badDbidRows.Add); }
        catch(InvalidOperationException) { badDbidRejected=true; }
        Check(badDbidRejected && badDbidRows.Any(row=>row.StartsWith("Core_CS2_200\t") && row.Contains("\tDBID\t") && row.EndsWith("\tFATAL")),"mismatched DBID audit row not marked fatal");
        Console.WriteLine("PASS explicit27 frozen metadata closure and fatal semantic mismatches");
    }
    static void ProofPacketGames()
    {
        bool wrongPairRejected = false;
        try { new NativeMatch((_,__) => {},"proof-mage","mage",0); }
        catch(ArgumentException) { wrongPairRejected = true; }
        Check(wrongPairRejected,"proof match accepts Classic opponent");
        foreach(string player in new[]{"proof-mage","proof-warrior"})
        {
            var packets = new List<(int Id,IProtoBuf Message)>();
            var match = new NativeMatch((id,packet) => packets.Add((id,packet)),player,player=="proof-mage"?"proof-warrior":"proof-mage",0);
            match.Start();
            var expectedDeck=LocalDecks.Get(player);
            Check(match.EngineStartCount==30 && match.EngineStartHero==expectedDeck.Hero && match.EngineStartCards==string.Join(",",expectedDeck.CardIds.OrderBy(id=>id,StringComparer.Ordinal)),"native start lost selected30 fingerprint");
            match.SendChoices(((EntityChoices)packets.Last(p => p.Id == 17).Message).Id,new List<int>());
            var bot = new ProofBot(); int actions = 0, trades = 0;
            while(match.Game.State != State.COMPLETE && actions++ < 500)
            {
                var task = bot.Choose(match.Game.Player1.Options());
                var options = (AllOptions)packets.Last(p => p.Id == 14).Message;
                int keyword = task.PlayerTaskType == PlayerTaskType.TRADE_CARD?1720:0;
                int source = task.Source?.Id??0;
                if(task.PlayerTaskType == PlayerTaskType.HERO_POWER) source = match.Game.Player1.Hero.HeroPower.Id;
                if(task.PlayerTaskType == PlayerTaskType.HERO_ATTACK) source = match.Game.Player1.Hero.Id;
                int index = options.Options.FindIndex(o => task.PlayerTaskType == PlayerTaskType.END_TURN
                    ? o.Type_ == NativeOption.Type.END_TURN : o.Type_ == NativeOption.Type.POWER && o.MainOption?.Id == source && o.MainOption.PowerKeyword == keyword);
                Check(index >= 0,"proof legal task not on native wire:"+task.FullPrint());
                int position = task is PlayCardTask play && play.Source is Minion ? play.ZonePosition+1:0;
                match.SendOption(options.Id,index,task.Target?.Id??0,-1,position);
                if(keyword == 1720) trades++;
                Check(match.BotNodes == 0 && match.BotFallbacks == 0,"proof opponent still uses clone-search/fallback policy");
            }
            Check(match.Game.State == State.COMPLETE && trades > 0,"proof packet game stalled or lacked Trade");
            SerializePackets(packets);
            Console.WriteLine("PASS proof packet "+player+" seed0 turns="+match.Game.Turn+" result="+match.Game.Player1.PlayState+" humanActions="+actions+" humanTrades="+trades);
        }
    }
    static void InputTraceBinding()
    {
        string root=Path.Combine(Path.GetTempPath(),"hs-standard-input-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        string path=Path.Combine(root,"input.json");
        var input=new Dictionary<string,object> { {"Nonce","nonce"},{"ExpectedRunId","run"},{"ExpectedPid",System.Diagnostics.Process.GetCurrentProcess().Id},
            {"ExpectedSnapshotId","standard-2026-10-17-core-tradeable"},{"ExpectedMatchId","match"},{"ExpectedOptionsId",17},{"ExpectedOptionIndex",2} };
        try
        {
            File.WriteAllText(path,JsonSerializer.Serialize(input));
            Check(StandardProofNative.InputNonce(path,"run","match",17,2)=="nonce","actual ingress trace loses bound input nonce");
            foreach(var field in new[]{"ExpectedRunId","ExpectedPid","ExpectedSnapshotId","ExpectedMatchId","ExpectedOptionsId","ExpectedOptionIndex"})
            {
                var bad=new Dictionary<string,object>(input);bad[field]=field=="ExpectedPid"||field=="ExpectedOptionsId"||field=="ExpectedOptionIndex"?(object)999:"other";
                File.WriteAllText(path,JsonSerializer.Serialize(bad));
                Check(StandardProofNative.InputNonce(path,"run","match",17,2)=="","stale input nonce credited:"+field);
            }
            File.WriteAllText(path,"{");
            Check(StandardProofNative.InputNonce(path,"run","match",17,2)=="","malformed input credited");
        }
        finally { Directory.Delete(root,true); }
        Console.WriteLine("PASS trace input nonce binding and stale/malformed rejection");
    }
    static void SerializePackets(List<(int Id,IProtoBuf Message)> packets)
    {
        foreach(var packet in packets)
        {
            using var bytes=new MemoryStream();packet.Message.Serialize(bytes);
            Check(bytes.Length==packet.Message.GetSerializedSize(),"native packet serialized size mismatch:"+packet.Id);
        }
    }
    static string Fingerprint(NativeMatch match) => match.Game.Turn + ":" + match.Game.Player1.UsedMana + ":" +
        string.Join(",", match.Game.Player1.HandZone.Select(c => c.Id)) + ":" +
        string.Join(",", match.Game.Player1.DeckZone.Select(c => c.Id));
}
