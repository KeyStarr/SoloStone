using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineHistory = SabberStoneCore.Kettle;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // Start of Game effects used to apply silently (the engine resolves them in the opening draw with an anonymous
    // source), so the client showed no reveal or history tile. They are now replayed after both mulligans, before
    // STEP=MAIN_READY (retail timing), as a source reveal plus a TRIGGER with keyword 968. A playtest showed the reveal
    // playing over flying mulligan cards, so the NEXT_STEP=MAIN_READY barrier follows the final mulligan DONE.
    [GroupProof("start-of-game-reveal", ProofKind.Native)]
    private static void StartOfGameRevealHistory()
    {
        SogPostMulliganBarrier();
        foreach (string card in new[] { "JAIL_800", "JAIL_384", "EDR_000", "EDR_845", "JAIL_860", "JAIL_509", "TIME_020", "TIME_875", "CS3_035" })
        foreach (bool enemy in new[] { false, true })
        {
            var own = card == "CS3_035" || !enemy ? new[] { card } : Array.Empty<string>();
            var bot = card == "CS3_035" || enemy ? new[] { card } : Array.Empty<string>();
            var (match, capture) = SogStart(own, bot);
            SogComplete(match, capture);
            int[] sources = SogResolvedSources(match);
            string actual = card == "TIME_875" ? "TIME_875t" : card;
            Check(sources.Length == (card == "CS3_035" ? 2 : 1)
                && sources.All(id => match.Game.IdEntityDic[id].Card.Id == actual), "fixture must resolve " + actual);
            SogAssertPresentation(match, capture, sources);
            if (card == "JAIL_800")
            {
                int source = sources.Single();
                var powers = match.Game.IdEntityDic[source].Controller.HeroPowers
                    .Where(p => p.Card.Id.StartsWith("JAIL_800hp", StringComparison.Ordinal)).Select(p => p.Id).ToArray();
                Check(powers.Length == 2, "Mug'Zee fixture must install both public hero powers");
                var history = SogHistory(capture);
                int effect = history.FindIndex(x => SogEffect(x) && x.PowerStart.Source == source);
                Check(SogBody(history, effect).Any(x => x.HasMetaData && (int)x.MetaData.Type == 7
                    && x.MetaData.Info.ToHashSet().IsSupersetOf(powers)), "Mug'Zee history targets omit public powers");
            }
            int packetCount = capture.Packets.Count;
            SogSendHistory(match, match.Game.PowerHistory.Last);
            SogSendHistory(match, match.Game.PowerHistory.Full);
            Check(!SogHistorySince(capture, packetCount).Any(SogEffect), "resend must not repeat presentation");
            capture.SerializeAll();
        }
        foreach (bool enemy in new[] { false, true })
        {
            var (match, capture) = SogStart(enemy ? Array.Empty<string>() : new[] { "EDR_845", "CS2_029" },
                enemy ? new[] { "EDR_845", "CS2_029" } : Array.Empty<string>());
            SogComplete(match, capture);
            Check(SogResolvedSources(match).Length == 0, "non-Nature spell must disqualify Hamuul");
            Check(!SogHistory(capture).Any(SogEffect), "ineligible printed keyword must not be presented");
            SogAssertPrivacy(SogHistory(capture), new HashSet<int>());
            capture.SerializeAll();
        }
        SogCopiedSourcePrivacy();
        SogMugZeeSingleBranches();
        SogSkipMulligan();
        SogHandAndMulliganCases();
        SogOrderAndLifecycle();
        SogEngineParity();
        SogNoSourceBaseline();
        Console.WriteLine("PASS Start of Game reveal history");
    }

    private static void SogPostMulliganBarrier()
    {
        var (match, capture) = SogStart(new[] { "JAIL_800" }, Array.Empty<string>());
        Check(!SogHistory(capture).Any(x => x.HasFullEntity && x.FullEntity.Name.StartsWith("JAIL_800hp", StringComparison.Ordinal)),
            "Mug'Zee gameplay must wait for mulligans");
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var history = SogHistory(capture);
        int done = history.FindLastIndex(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.MULLIGAN_STATE
            && x.TagChange.Value == (int)Mulligan.DONE);
        int barrier = history.FindIndex(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.NEXT_STEP
            && x.TagChange.Value == (int)Step.MAIN_READY);
        int effect = history.FindIndex(SogEffect);
        Check(done >= 0 && barrier > done && effect > barrier,
            "post-mulligan real effect must follow native busy barrier");
    }

    private static (NativeMatch Match, Capture Capture) SogStart(string[] own, string[] bot, int seed = 23, bool humanFirstMulligan = false, int firstPlayer = 1)
    {
        string[] Deck(string[] ids) {
            var expanded = FabledRules.ExpandForDeckbuilding(ids).ToArray();
            // Weapons let Mug'Zee satisfy both predicates; cheap minions let Chef qualify.
            string filler = ids.Contains("JAIL_800") ? "CS2_106" : "CS2_231";
            return expanded.Concat(Enumerable.Repeat(filler, 30 - expanded.Length)).ToArray();
        }
        var capture = new Capture();
        NativeMatch match = null;
        SabberStoneCore.Model.Game opening = null;
        match = new NativeMatch((id, message) => {
            capture.Send(id, message);
            if (humanFirstMulligan && opening == null && id == 19) {
                opening = match.Game.CloneExact(history: true);
                // CloneExact starts a fresh log; retain the real opening packet entries for this fixture.
                opening.PowerHistory.Full.AddRange(match.Game.PowerHistory.Full);
                opening.PowerHistory.Last.AddRange(match.Game.PowerHistory.Last);
            }
        }, Cards.FromId("HERO_01"), Enumerable.Repeat(Cards.FromId("CS2_231"), 30), seed);
        typeof(NativeMatch).GetField("playerCards", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(match, Deck(own).Select(Cards.FromId).ToList());
        typeof(NativeMatch).GetField("opponentDeck", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(match, new LocalDeck("Start of Game fixture", "HERO_01", Deck(bot)));
        match.Start();
        if (firstPlayer == 2) {
            // NativeMatch fixes StartPlayer=1. Project a real engine opening for the other first-player order.
            var config = ((SabberStoneCore.Config.GameConfig)typeof(SabberStoneCore.Model.Game)
                .GetField("_gameConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match.Game)).Clone();
            config.StartPlayer = 2;
            var game = new SabberStoneCore.Model.Game(config);
            game.StartGame();
            match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty("Game").SetValue(match, game);
            capture.Packets.Clear();
            SogSendHistory(match, game.PowerHistory.Last);
        }
        // NativeMatch always answers bot first. Restore its real opening boundary to exercise the other order.
        else if (humanFirstMulligan) {
            match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty("Game").SetValue(match, opening);
            capture.Packets.Clear();
            SogSendHistory(match, opening.PowerHistory.Full);
        }
        Check(!SogHistory(capture).Any(SogEffect), "Start of Game presentation must wait for both mulligans");
        Check(SogResolvedSources(match).Length == 0, "gameplay must wait for both mulligans");
        return (match, capture);
    }

    private static int[] SogResolvedSources(NativeMatch match) => match.Game.PowerHistory.Full
        .OfType<EngineHistory.PowerHistoryBlockStart>()
        .Where(s => s.BlockType == BlockType.TRIGGER && s.EffectIndex == -1
            && match.Game.IdEntityDic.TryGetValue(s.Source, out var e) && StartOfGameRules.HasEffect(e.Card))
        .Select(s => s.Source).ToArray();

    private static void SogComplete(NativeMatch match, Capture capture, List<int> replace = null, bool humanFirstMulligan = true)
    {
        if (match.Game.Player2.Choice != null) {
            var owners = humanFirstMulligan ? new[] { match.Game.Player1, match.Game.Player2 } : new[] { match.Game.Player2, match.Game.Player1 };
            foreach (var owner in owners) {
                var decision = match.Game.GetPendingDecisions().Single(d => d.ControllerId == owner.Id);
                Check(match.Game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
                    Array.Empty<int>()), out var error), "reverse mulligan: " + error);
                SogSendHistory(match, match.Game.PowerHistory.Last);
                if (owner == owners[0]) Check(!SogHistory(capture).Any(SogEffect), "presentation must wait for final DONE scope");
            }
            Check(!SogHistory(capture).Any(SogEffect), "final DONE alone must not synthesize gameplay or presentation");
            match.Game.PowerHistory.Last.Clear();
            match.Game.MainBegin();
            SogSendHistory(match, match.Game.PowerHistory.Last);
        }
        else
            match.SendChoices(capture.Last<EntityChoices>(17).Id, replace ?? new List<int>());
    }

    private static bool SogEffect(PowerHistoryData x) => x.HasPowerStart && x.PowerStart.HasTriggerKeyword
        && x.PowerStart.TriggerKeyword == 968;

    // Expected CardDef trigger slots, pinned independently of the transport table (data/fx/start-of-game-trigger-slots-*.tsv).
    private static (int Reveal, int Effect) SogSlots(NativeMatch match, int source)
    {
        var entity = match.Game.IdEntityDic[source];
        bool mug = entity.Controller.HeroPowers.Any(p => p.Card.Id == "JAIL_800hp1");
        bool zee = entity.Controller.HeroPowers.Any(p => p.Card.Id == "JAIL_800hp2");
        switch (entity.Card.Id)
        {
            case "JAIL_800": return (10, mug && zee ? 8 : zee ? 0 : -1);
            case "JAIL_384": case "EDR_845": return (3, 1);
            case "EDR_000": return (4, 0);
            case "JAIL_860": case "CS3_035": return (2, 0);
            case "TIME_020": return (2, 1);
            case "TIME_875t": return (1, 0);
            default: return (-1, -1);
        }
    }

    private static List<PowerHistoryData> SogBody(List<PowerHistoryData> history, int start)
    {
        int depth = 1, end = start + 1;
        for (; end < history.Count; end++) {
            if (history[end].HasPowerStart) depth++;
            if (history[end].HasPowerEnd && --depth == 0) break;
        }
        Check(end < history.Count, "unterminated SoG effect");
        return history.Skip(start + 1).Take(end - start - 1).ToList();
    }

    private static void SogAssertPresentation(NativeMatch match, Capture capture, int[] sources)
    {
        var history = SogHistory(capture);
        var effects = history.Select((data, index) => (data, index)).Where(p => SogEffect(p.data)).ToArray();
        Check(effects.Select(p => p.data.PowerStart.Source).SequenceEqual(sources), "presentation order differs from engine resolution");
        int ready = history.FindIndex(x => SogTag(x, match.Game.Id, GameTag.STEP, (int)Step.MAIN_READY));
        int barrier = history.FindIndex(x => SogTag(x, match.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_READY));
        int done = history.FindLastIndex(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.MULLIGAN_STATE
            && x.TagChange.Value == (int)Mulligan.DONE);
        Check(barrier > done && barrier < ready
            && history.Count(x => SogTag(x, match.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_READY)) == 1,
            "exactly one busy barrier must follow final DONE and precede MAIN_READY");
        int depth = 0;
        var depths = new List<int>();
        foreach (var data in history) {
            depths.Add(depth);
            if (data.HasPowerStart) depth++;
            if (data.HasPowerEnd) depth--;
            Check(depth >= 0, "unbalanced native block end");
        }
        Check(depth == 0, "unbalanced native block start");
        if (done >= 0)
        {
            // The END that closes the block holding the final DONE tag, not just any enclosing block.
            int doneEnd = -1;
            for (int i = done + 1, rel = 0; i < history.Count && doneEnd < 0; i++)
            {
                if (history[i].HasPowerStart) rel++;
                else if (history[i].HasPowerEnd && rel-- == 0) doneEnd = i;
            }
            Check(depths[done] >= 1 && barrier == doneEnd - 1 && depths[barrier] == depths[done],
                "busy barrier must be inside final DONE immediately before END");
        }
        foreach (var (data, index) in effects)
        {
            int source = data.PowerStart.Source;
            Check(data.PowerStart.Type == HistoryBlock.Type.TRIGGER && data.PowerStart.HasShowInHistory && data.PowerStart.ShowInHistory
                && data.PowerStart.HasEffectIndex && data.PowerStart.EffectIndex == SogSlots(match, source).Effect
                && data.PowerStart.EffectCardId == "", "Start of Game effect header incomplete");
            Check(index > barrier && index < ready && depths[index] == 0, "effect outside post-barrier/pre-MAIN_READY interval");
            var preceding = history.Take(index).Where(x => SnapshotEntity(x) == source || x.HasTagChange && x.TagChange.Entity == source).ToArray();
            int zone = preceding.Where(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.ZONE
                || SnapshotEntity(x) == source && SnapshotTag(x, GameTag.ZONE) != 0)
                .Select(x => x.HasTagChange ? x.TagChange.Value : SnapshotTag(x, GameTag.ZONE)).Last();
            int owner = preceding.Where(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.CONTROLLER
                || SnapshotEntity(x) == source && SnapshotTag(x, GameTag.CONTROLLER) != 0)
                .Select(x => x.HasTagChange ? x.TagChange.Value : SnapshotTag(x, GameTag.CONTROLLER)).Last();
            bool hidden = zone == (int)EngineZone.DECK || zone == (int)EngineZone.SETASIDE
                || zone == (int)EngineZone.HAND && owner == 2;
            if (hidden) {
                Check(index >= 3 && history[index - 3].HasPowerStart && history[index - 3].PowerStart.Source == source
                    && history[index - 3].PowerStart.Type == HistoryBlock.Type.TRIGGER
                    && !history[index - 3].PowerStart.HasTriggerKeyword && !history[index - 3].PowerStart.ShowInHistory
                    && history[index - 3].PowerStart.HasEffectIndex
                    && history[index - 3].PowerStart.EffectIndex == SogSlots(match, source).Reveal
                    && history[index - 3].PowerStart.EffectCardId == "" && history[index - 1].HasPowerEnd,
                    "missing source reveal TRIGGER before effect");
                Check(index - 3 > barrier, "reveal precedes native busy barrier");
                var show = history[index - 2].ShowEntity;
                Check(show != null && show.Entity == source && show.Name == match.Game.IdEntityDic[source].Card.Id
                    && TagValue(show.Tags, GameTag.REVEALED) == 1 && TagValue(show.Tags, GameTag.ZONE) == zone
                    && TagValue(show.Tags, GameTag.CONTROLLER) == owner && TagValue(show.Tags, GameTag.CARDTYPE) == (int)CardType.MINION
                    && TagValue(show.Tags, GameTag.HEALTH) > 0 && show.Tags.All(t => t.Name != (int)GameTag.ZONE_POSITION),
                    "source SHOW must carry historical actor tags without position: source=" + source
                    + " zone=" + zone + " owner=" + owner + " " + show?.Name + " "
                    + string.Join(",", show?.Tags.Select(t => t.Name + "=" + t.Value) ?? Array.Empty<string>()));
            }
            var body = SogBody(history, index);
            // Admission may prepend TARGET; history attribution still precedes every effect task.
            var historySource = body.TakeWhile(x => x.HasMetaData).SingleOrDefault(x => (int)x.MetaData.Type == 8);
            Check(historySource != null && historySource.MetaData.Info.SequenceEqual(new[] { source }),
                "OVERRIDE_HISTORY must name original source before effect tasks");
            if (hidden) {
                Check(body.Count >= 3 && body[^2].HasHideEntity && body[^2].HideEntity.Entity == source
                    && SogTag(body[^1], source, GameTag.REVEALED, 0), "revealed hidden source must HIDE/reset at effect tail");
                Check(body.Count(x => x.HasHideEntity && x.HideEntity.Entity == source) == 1,
                    "source must HIDE exactly once per effect");
            }
            Check(index + body.Count + 1 < ready, "effect must close before MAIN_READY");
            Check(!history.Take(done >= 0 ? done : barrier).Any(x => x.HasPowerStart && x.PowerStart.Source == source),
                "gameplay source block must not run before mulligan");
            switch (match.Game.IdEntityDic[source].Card.Id) {
                case "JAIL_800":
                    Check(body.Any(x => x.HasFullEntity && x.FullEntity.Name.StartsWith("JAIL_800hp", StringComparison.Ordinal)),
                        "real hero power creation must be inside 968 block"); break;
                case "EDR_000" when !effects.TakeWhile(p => p.index < index)
                    .Any(p => match.Game.IdEntityDic[p.data.PowerStart.Source].Card.Id == "EDR_000"):
                    Check(body.Count(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.MAXRESOURCES && x.TagChange.Value == 15) == 2,
                        "real Ysera MAXRESOURCES changes must be inside 968 block"); break;
                case "TIME_020":
                    Check(zone == (int)EngineZone.DECK && body.Any(x => SogTag(x, source, GameTag.ZONE, (int)EngineZone.SETASIDE))
                        && !body.Any(x => x.HasHideEntity && x.HideEntity.Entity == source && x.HideEntity.Zone != (int)EngineZone.SETASIDE),
                        "Broxigar must vanish inside effect without HIDE back to deck"); break;
                case "TIME_875t":
                    Check(zone == (int)EngineZone.DECK && body.Any(x => SogTag(x, source, GameTag.CONTROLLER, owner == 1 ? 2 : 1))
                        && body.Any(x => x.HasHideEntity && x.HideEntity.Entity == source && x.HideEntity.Zone == (int)EngineZone.DECK),
                        "King Llane must reveal old owner then hide in destination deck: owner=" + owner + " "
                        + string.Join(",", body.Select(x => x.HasTagChange ? $"tag:{x.TagChange.Entity}/{x.TagChange.Tag}={x.TagChange.Value}"
                            : x.HasHideEntity ? $"hide:{x.HideEntity.Entity}/{x.HideEntity.Zone}" : x.ToString()))); break;
            }
        }
        int draw = history.FindIndex(ready + 1, x => x.HasPowerStart && x.PowerStart.Source == match.Game.FirstPlayer.Id
            && x.PowerStart.SubOption == 0);
        int nextAction = history.FindIndex(x => SogTag(x, match.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_ACTION));
        int action = history.FindIndex(x => SogTag(x, match.Game.Id, GameTag.STEP, (int)Step.MAIN_ACTION));
        Check(ready < draw && draw < nextAction && nextAction < action, "MAIN_READY, turn draw, NEXT_STEP/STEP MAIN_ACTION order");
        var flat = capture.Packets.SelectMany((p, packet) => p.Id == 19
            ? ((PowerHistory)p.Message).List.Select((x, index) => (packet, index, data: x))
            : Enumerable.Empty<(int packet, int index, PowerHistoryData data)>()).ToList();
        int actionPacket = flat.First(x => SogTag(x.data, match.Game.Id, GameTag.STEP, (int)Step.MAIN_ACTION)).packet;
        int optionsPacket = capture.Packets.FindIndex(p => p.Id == 14);
        if (optionsPacket >= 0) Check(optionsPacket > actionPacket, "AllOptions must follow opening MAIN_ACTION packet");
        SogAssertPrivacy(history, sources.ToHashSet());
        capture.SerializeAll();
    }

    private static bool SogTag(PowerHistoryData data, int entity, GameTag tag, int value) => data.HasTagChange
        && data.TagChange.Entity == entity && data.TagChange.Tag == (int)tag && data.TagChange.Value == value;

    private static void SogAssertPrivacy(List<PowerHistoryData> history, HashSet<int> sources)
    {
        var zones = new Dictionary<int, int>();
        var owners = new Dictionary<int, int>();
        var named = new HashSet<int>();
        foreach (var data in history) {
            var snapshot = data.HasFullEntity ? data.FullEntity : data.HasShowEntity ? data.ShowEntity : data.HasChangeEntity ? data.ChangeEntity : null;
            if (snapshot != null) {
                int zone = TagValue(snapshot.Tags, GameTag.ZONE), owner = TagValue(snapshot.Tags, GameTag.CONTROLLER);
                if (zone != 0) zones[snapshot.Entity] = zone;
                if (owner != 0) owners[snapshot.Entity] = owner;
                bool hidden = zone == (int)EngineZone.DECK || zone == (int)EngineZone.HAND && owner == 2;
                if (hidden && snapshot.Name.Length > 0)
                    Check(data.HasShowEntity && sources.Contains(snapshot.Entity) && TagValue(snapshot.Tags, GameTag.REVEALED) == 1,
                        "non-source hidden identity leaked: " + snapshot.Name);
                if (hidden && sources.Contains(snapshot.Entity) && zone == (int)EngineZone.DECK)
                    Check(snapshot.Tags.All(t => t.Name != (int)GameTag.ZONE_POSITION), "source deck position leaked before reveal");
                Check(snapshot.Tags.All(t => t.Name < 30000), "internal snapshot tag leaked");
                if (snapshot.Name.Length > 0) named.Add(snapshot.Entity); else named.Remove(snapshot.Entity);
            }
            if (data.HasTagChange) {
                var tag = data.TagChange;
                Check(tag.Tag < 30000, "internal TagChange leaked");
                if (tag.Tag == (int)GameTag.ZONE) zones[tag.Entity] = tag.Value;
                if (tag.Tag == (int)GameTag.CONTROLLER) owners[tag.Entity] = tag.Value;
                if (tag.Tag == (int)GameTag.ZONE_POSITION && sources.Contains(tag.Entity)
                    && zones.GetValueOrDefault(tag.Entity) == (int)EngineZone.DECK)
                    Check(tag.Value == 0, "source deck position leaked in TagChange");
            }
            if (data.HasHideEntity) named.Remove(data.HideEntity.Entity);
            if (data.HasMetaData && (int)data.MetaData.Type == 7)
                Check(data.MetaData.Info.All(id => named.Contains(id) && zones.GetValueOrDefault(id) == (int)EngineZone.PLAY),
                    "HISTORY_TARGET leaked hidden entity");
            if (data.HasPowerStart && sources.Contains(data.PowerStart.Source))
                Check(data.PowerStart.EffectCardId.Length == 0, "source identity leaked through EffectCardId");
        }
    }

    private static void SogCopiedSourcePrivacy()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
                Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
                StartPlayer = 1, RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false
            });
            // Keep entity ids distinct from printed scalar values (Hogger's cost 8, stats/class 10).
            game.SetIndexer(1000, 0);
            foreach (var owner in new[] { game.Player1, game.Player2 })
            foreach (string card in new[] { owner == game.Player1 ? "JAIL_430" : "JAIL_384" }.Concat(Enumerable.Repeat("CS2_231", 29)))
            {
                var definition = Cards.FromId(card);
                owner.DeckCards.Add(definition);
                SabberStoneCore.Model.Entities.Entity.FromCard(owner, definition, null, owner.DeckZone);
            }
            game.StartGame();
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty("Game").SetValue(match, game);
            SogSendHistory(match, game.PowerHistory.Last);
            int[] sources = match.Game.IdEntityDic.Values.Where(e => e[StartOfGameRules.Pending] == 1).Select(e => e.Id).ToArray();
            var copied = sources.Select(id => match.Game.IdEntityDic[id]).FirstOrDefault(e => e.Controller == match.Game.Player1
                && e.Card.Id == "JAIL_384" && e[GameTag.CREATOR] != 0 && e.Zone.Type == EngineZone.DECK);
            if (copied == null) continue;
            int creator = copied[GameTag.CREATOR];
            Check(match.Game.IdEntityDic[creator].Card.Id == "JAIL_430"
                && match.Game.IdEntityDic[creator].Zone.Type == EngineZone.DECK
                && copied[GameTag.DISPLAYED_CREATOR] == creator, "copied source must point at hidden Azalina");
            var hidden = match.Game.IdEntityDic.Values.Where(e => e.Id != copied.Id && e.Zone != null
                && (e.Zone.Type == EngineZone.DECK || e.Zone.Type == EngineZone.SETASIDE
                    || e.Zone.Type == EngineZone.HAND && e.Controller == match.Game.Player2)).Select(e => e.Id).ToHashSet();
            SogComplete(match, capture);
            SogAssertPresentation(match, capture, SogResolvedSources(match));
            var show = SogHistory(capture).Single(x => x.HasShowEntity && x.ShowEntity.Entity == copied.Id
                && TagValue(x.ShowEntity.Tags, GameTag.REVEALED) == 1).ShowEntity;
            // CREATOR_DBID is 1284 in this engine; reject the review's 2207 value too.
            var forbidden = new[] { 313, 385, 1284, 2207, (int)GameTag.ATTACHED, (int)GameTag.LINKED_ENTITY,
                (int)GameTag.ZONE_POSITION, (int)GameTag.TAG_SCRIPT_DATA_NUM_1, (int)GameTag.TAG_SCRIPT_DATA_NUM_2 };
            Check(show.Tags.All(t => !forbidden.Contains(t.Name)), "presentation SHOW leaked private source tags: "
                + string.Join(",", show.Tags.Where(t => forbidden.Contains(t.Name)).Select(t => t.Name)));
            Check(show.Tags.All(t => !hidden.Contains(t.Value)), "presentation SHOW contains a hidden entity id: "
                + string.Join(",", show.Tags.Where(t => hidden.Contains(t.Value)).Select(t => t.Name + "=" + t.Value)));
            return;
        }
        Check(false, "could not resolve copied Start of Game source with hidden Azalina");
    }

    private static void SogMugZeeSingleBranches()
    {
        foreach (bool zee in new[] { true, false })
        foreach (bool enemy in new[] { false, true })
        {
            var cards = zee ? new[] { "JAIL_800", "CS2_231" } : new[] { "JAIL_800", "CS2_029" };
            var (match, capture) = SogStart(enemy ? Array.Empty<string>() : cards, enemy ? cards : Array.Empty<string>());
            int source = match.Game.IdEntityDic.Values.Single(e => e[StartOfGameRules.Pending] == 1).Id;
            SogComplete(match, capture);
            string power = zee ? "JAIL_800hp2" : "JAIL_800hp1";
            Check(match.Game.IdEntityDic[source].Controller.HeroPowers.Select(p => p.Card.Id).SequenceEqual(new[] { power }),
                "single-branch fixture must install only " + power);
            SogAssertPresentation(match, capture, new[] { source });
            var history = SogHistory(capture);
            int effect = history.FindIndex(SogEffect);
            Check(history[effect].PowerStart.EffectIndex == (zee ? 0 : -1), "single-branch effect slot incorrect");
            Check(history[effect - 3].HasPowerStart && history[effect - 3].PowerStart.EffectIndex == 10,
                "single-branch reveal slot must be 10");
        }
    }

    private static void SogSkipMulligan()
    {
        // NativeMatch.Start requires mulligans; submit an actual skipped engine opening to the same converter.
        foreach (bool mainBegin in new[] { true, false })
        foreach (bool hidden in new[] { true, false })
        {
            var deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 29).ToList();
            deck.Insert(hidden ? deck.Count : 0, Cards.FromId("EDR_000"));
            var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
                Player1Deck = deck,
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
                StartPlayer = 1, RandomSeed = 23, Shuffle = false, SkipMulligan = true, History = true, Logging = false
            });
            game.StartGame();
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty("Game").SetValue(match, game);
            // NEXT_STEP MAIN_BEGIN still identifies a skipped opening if STEP was omitted.
            if (!mainBegin) game.PowerHistory.Last.RemoveAll(e => e is EngineHistory.PowerHistoryTagChange t
                && t.EntityId == game.Id && t.Tag == GameTag.STEP && t.Value == (int)Step.MAIN_BEGIN);
            int source = SogResolvedSources(match).Single();
            SogSendHistory(match, game.PowerHistory.Last);
            SogAssertPresentation(match, capture, new[] { source });
            var history = SogHistory(capture);
            Check(!history.Any(x => x.HasTagChange && x.TagChange.Tag == (int)GameTag.MULLIGAN_STATE),
                "skipped fixture must not emit mulligan state");
            int effect = history.FindIndex(SogEffect);
            Check(history.Count(x => x.HasShowEntity && x.ShowEntity.Entity == source
                && TagValue(x.ShowEntity.Tags, GameTag.REVEALED) == 1) == (hidden ? 1 : 0),
                "skipped opening must reveal hidden source only: hidden=" + hidden + " "
                + string.Join(",", history.Where(x => x.HasShowEntity && x.ShowEntity.Entity == source)
                    .Select(x => "zone=" + TagValue(x.ShowEntity.Tags, GameTag.ZONE) + "/reveal=" + TagValue(x.ShowEntity.Tags, GameTag.REVEALED))));
            int boundary = history.FindIndex(x => x.HasTagChange && x.TagChange.Entity == game.Id
                && x.TagChange.Tag == (int)GameTag.STEP && x.TagChange.Value == (int)Step.MAIN_READY);
            int draw = history.FindIndex(boundary + 1, x => x.HasPowerStart && x.PowerStart.Type == HistoryBlock.Type.TRIGGER
                && x.PowerStart.Source == game.CurrentPlayer.Id && x.PowerStart.SubOption == 0);
            Check(effect >= 0 && effect < boundary && boundary < draw, "in-place skipped opening effect must precede MAIN_READY and turn draw");
            Check(history.Skip(draw + 1).TakeWhile(x => !x.HasPowerEnd).Any(x => x.HasShowEntity
                && TagValue(x.ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND), "turn-one draw must reveal its card");
            int before = capture.Packets.Count;
            SogSendHistory(match, game.PowerHistory.Last);
            SogSendHistory(match, game.PowerHistory.Full);
            Check(!SogHistorySince(capture, before).Any(SogEffect), "skipped mulligan presentation repeated on resend");
            capture.SerializeAll();
        }
    }

    private static void SogHandAndMulliganCases()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (bool replace in new[] { false, true })
        {
            if (enemy && replace) continue;
            bool found = false;
            for (int seed = 1; seed <= 40 && !found; seed++) {
                var (match, capture) = SogStart(enemy ? Array.Empty<string>() : new[] { "EDR_000" },
                    enemy ? new[] { "EDR_000" } : Array.Empty<string>(), seed);
                int source = match.Game.IdEntityDic.Values.Single(e => e[StartOfGameRules.Pending] == 1).Id;
                if (match.Game.IdEntityDic[source].Zone.Type != EngineZone.HAND) continue;
                found = true;
                int before = capture.Packets.Count;
                // Native mulligan choices are kept cards, not rejects.
                SogComplete(match, capture, !enemy && !replace ? new List<int> { source } : null);
                SogAssertPresentation(match, capture, new[] { source });
                var post = SogHistorySince(capture, before);
                var reveals = post.Where(x => x.HasShowEntity && x.ShowEntity.Entity == source && TagValue(x.ShowEntity.Tags, GameTag.REVEALED) == 1).ToArray();
                Check(reveals.Length == (enemy || replace ? 1 : 0), "human hand must stay visible; enemy hand/mulligan reject must reveal"
                    + $" enemy={enemy} replace={replace} seed={seed} reveals={reveals.Length} finalZone={match.Game.IdEntityDic[source].Zone.Type}");
                if (enemy) Check(TagValue(reveals.Single().ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND, "enemy hand source uses HAND");
                if (replace) Check(TagValue(reveals.Single().ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK, "mulligan reject uses boundary DECK");
            }
            Check(found, "could not find deterministic opening-hand source fixture");
        }
    }

    private static void SogOrderAndLifecycle()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (bool botFirst in new[] { false, true }) {
            var own = new[] { "EDR_000", "JAIL_509", "JAIL_384" };
            var (match, capture) = SogStart(own, own, humanFirstMulligan: reverse, firstPlayer: botFirst ? 2 : 1);
            SogComplete(match, capture, humanFirstMulligan: reverse);
            int[] sources = SogResolvedSources(match);
            int first = botFirst ? 2 : 1, second = botFirst ? 1 : 2;
            Check(match.Game.FirstPlayer.PlayerId == first, "fixture must select intended first player");
            Check(sources.Select(id => match.Game.IdEntityDic[id].Controller.PlayerId).SequenceEqual(new[] { first, first, second, second, first, second })
                && sources.Take(2).SequenceEqual(sources.Take(2).OrderBy(id => id))
                && sources.Skip(2).Take(2).SequenceEqual(sources.Skip(2).Take(2).OrderBy(id => id)),
                "presentation must preserve priority, first player, then original entity order");
            Check(sources.TakeLast(2).All(id => match.Game.IdEntityDic[id].Card.Id == "JAIL_384"), "Hogger must resolve last");
            SogAssertPresentation(match, capture, sources);
            int before = capture.Packets.Count;
            match.Game.Process(EndTurnTask.Any(match.Game.CurrentPlayer));
            SogSendHistory(match, match.Game.PowerHistory.Last);
            Check(!SogHistorySince(capture, before).Any(SogEffect), "later turn must not repeat setup presentation");
            Check(SogHistorySince(capture, before).Count(x => SogTag(x, match.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_READY)) == 1,
                "later turn keeps its own NEXT_STEP=MAIN_READY");
            SogSendHistory(match, new List<EngineHistory.IPowerHistoryEntry> {
                EngineHistory.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, sources[0], "", -1, 0, effectIndex: -1),
                EngineHistory.PowerHistoryBuilder.BlockEnd()
            });
            Check(!SogHistorySince(capture, before).Any(SogEffect), "later source TRIGGER must not be classified as opening setup");
        }
        var (drawMatch, drawCapture) = SogStart(new[] { "EDR_000" }, Array.Empty<string>());
        SogComplete(drawMatch, drawCapture);
        int drawSource = SogResolvedSources(drawMatch).Single();
        Check(drawMatch.Game.IdEntityDic[drawSource].Zone.Type == EngineZone.DECK, "later draw fixture source must remain in deck");
        int drawBefore = drawCapture.Packets.Count;
        Generic.Draw(drawMatch.Game.Player1, drawMatch.Game.IdEntityDic[drawSource]);
        SogSendHistory(drawMatch, drawMatch.Game.PowerHistory.Last);
        Check(SogHistorySince(drawCapture, drawBefore).Any(x => x.HasShowEntity && x.ShowEntity.Entity == drawSource
            && x.ShowEntity.Name == "EDR_000" && TagValue(x.ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND),
            "normal draw must reveal source again after synthetic HIDE");
        drawCapture.SerializeAll();
        int resetBefore = drawCapture.Packets.Count;
        SogReset(drawMatch);
        SogSendHistory(drawMatch, drawMatch.Game.PowerHistory.Full);
        Check(!SogHistorySince(drawCapture, resetBefore).Any(SogEffect), "reset/resend must not replay an already presented source");
        var (concede, conceded) = SogStart(new[] { "EDR_000" }, Array.Empty<string>());
        concede.Concede();
        Check(!SogHistory(conceded).Any(SogEffect), "concede during mulligan must discard pending presentation");
        var (resetMatch, resetCapture) = SogStart(new[] { "EDR_000" }, Array.Empty<string>());
        SogReset(resetMatch);
        var decision = resetMatch.Game.GetPendingDecisions().Single();
        Check(resetMatch.Game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, Array.Empty<int>()), out var resetError), resetError);
        resetMatch.Game.MainBegin();
        SogSendHistory(resetMatch, resetMatch.Game.PowerHistory.Last);
        Check(!SogHistory(resetCapture).Any(SogEffect), "snapshot reset must discard pending presentation");
        resetCapture.SerializeAll();
    }

    private static void SogReset(NativeMatch match)
    {
        var reset = new List<EngineHistory.IPowerHistoryEntry> {
            EngineHistory.PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, match.Game.Id, "", -1, 0),
            EngineHistory.PowerHistoryBuilder.ResetGame(match.Game)
        };
        reset.AddRange(match.Game.IdEntityDic.Values.OrderBy(e => e.Id)
            .Select(e => EngineHistory.PowerHistoryBuilder.FullEntity(e, reset: true)));
        reset.Add(EngineHistory.PowerHistoryBuilder.BlockEnd());
        SogSendHistory(match, reset);
    }

    private static void SogEngineParity()
    {
        var (match, capture) = SogStart(new[] { "JAIL_384", "EDR_000" }, new[] { "JAIL_384", "EDR_000" });
        var config = ((SabberStoneCore.Config.GameConfig)typeof(SabberStoneCore.Model.Game)
            .GetField("_gameConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match.Game)).Clone();
        var baseline = new SabberStoneCore.Model.Game(config);
        baseline.StartGame();
        var botDecision = baseline.GetPendingDecisions().Single(d => d.ControllerId == baseline.Player2.Id);
        Check(baseline.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(botDecision.Id, botDecision.ControllerId, Array.Empty<int>()), out var botError), botError);
        Check(match.Game.Hash() == baseline.Hash(), "opening presentation capture changed engine state");
        var decision = baseline.GetPendingDecisions().Single();
        Check(baseline.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, Array.Empty<int>()), out var error), error);
        baseline.MainBegin();
        SogComplete(match, capture);
        Check(match.Game.Hash() == baseline.Hash(), "presentation changed engine state hash");
        var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic);
        object Rng(SabberStoneCore.Model.Game game) {
            object value = random.GetValue(game);
            return value.GetType().GetMethod("CaptureState").Invoke(value, null);
        }
        Check(Rng(match.Game).Equals(Rng(baseline)), "presentation changed RNG state");
        string History(IEnumerable<EngineHistory.IPowerHistoryEntry> entries) => string.Join("\n", entries.Select(e =>
            System.Text.Json.JsonSerializer.Serialize(e, e.GetType(), new System.Text.Json.JsonSerializerOptions { IncludeFields = true })));
        Check(History(match.Game.PowerHistory.Full) == History(baseline.PowerHistory.Full), "presentation changed engine PowerHistory");
        string historyBefore = History(match.Game.PowerHistory.Full);
        string hashBefore = match.Game.Hash();
        object rngBefore = Rng(match.Game);
        SogSendHistory(match, match.Game.PowerHistory.Last);
        Check(historyBefore == History(match.Game.PowerHistory.Full)
            && hashBefore == match.Game.Hash() && rngBefore.Equals(Rng(match.Game)), "history transport mutated engine history/state/RNG");
    }

    private static void SogNoSourceBaseline()
    {
        foreach (var picks in new[] { 0, 1, 3 }) {
            var choiceCounter = typeof(EngineHistory.PowerEntityChoices).GetField("_index", BindingFlags.Static | BindingFlags.NonPublic);
            var optionsCounter = typeof(EngineHistory.PowerAllOptions).GetField("_index", BindingFlags.Static | BindingFlags.NonPublic);
            object choices = choiceCounter.GetValue(null), options = optionsCounter.GetValue(null);
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            match.SendChoices(capture.Last<EntityChoices>(17).Id, capture.Last<EntityChoices>(17).Entities.Take(picks).ToList());
            // Packet ids are process-wide counters; replay the identical fixture with identical ids.
            choiceCounter.SetValue(null, choices); optionsCounter.SetValue(null, options);
            var (baseline, original) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            // Disable only opening projection to retain the engine's original NEXT_STEP position.
            typeof(NativeMatch).GetField("startOfGameClosed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(baseline, true);
            baseline.SendChoices(original.Last<EntityChoices>(17).Id, original.Last<EntityChoices>(17).Entities.Take(picks).ToList());
            Check(!SogHistory(capture).Any(SogEffect), "no-source game must not gain SoG blocks");
            SogAssertPresentation(match, capture, Array.Empty<int>());
            var moved = SogHistory(capture).Single(x => SogTag(x, match.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_READY));
            var old = SogHistory(original).Single(x => SogTag(x, baseline.Game.Id, GameTag.NEXT_STEP, (int)Step.MAIN_READY));
            Check(capture.Packets.Count == original.Packets.Count, "barrier move changed packet count");
            for (int i = 0; i < capture.Packets.Count; i++) {
                var actualPacket = capture.Packets[i]; var beforePacket = original.Packets[i];
                Check(actualPacket.Id == beforePacket.Id, "barrier move changed packet type");
                if (actualPacket.Message is PowerHistory actualHistory) {
                    actualHistory.List.Remove(moved);
                    ((PowerHistory)beforePacket.Message).List.Remove(old);
                }
                byte[] Bytes(IProtoBuf packet) {
                    using var stream = new System.IO.MemoryStream(); packet.Serialize(stream); return stream.ToArray();
                }
                byte[] afterBytes = Bytes(actualPacket.Message), beforeBytes = Bytes(beforePacket.Message);
                Check(afterBytes.SequenceEqual(beforeBytes),
                    "no-source packets changed beyond barrier position, packet " + i
                    + " after=" + Convert.ToHexString(afterBytes) + " before=" + Convert.ToHexString(beforeBytes));
            }
        }
    }

    private static void SogSendHistory(NativeMatch match, List<EngineHistory.IPowerHistoryEntry> entries) =>
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { entries });

    private static List<PowerHistoryData> SogHistorySince(Capture capture, int packetCount) => capture.Packets.Skip(packetCount)
        .Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();

    private static List<PowerHistoryData> SogHistory(Capture capture) => capture.Packets
        .Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
}
