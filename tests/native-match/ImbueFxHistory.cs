using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Presentation;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineHistory = SabberStoneCore.Kettle;

internal static partial class Program
{
    // Owner 2026-11-03: the first Imbue animates, re-Imbuing an imbued hero power showed nothing.
    private static void ImbueFxHistory()
    {
        foreach (var (hero, blessing) in new[] {
            ("HERO_06", "EDR_847p"), ("HERO_05", "EDR_850p"), ("HERO_08", "EDR_851p"), ("HERO_04", "EDR_445p"),
            ("HERO_02", "EDR_448p"), ("HERO_09", "EDR_449p"), ("HERO_03", "END_000p"), ("HERO_11", "END_003p") })
        foreach (bool enemy in new[] { false, true })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game; var owner = enemy ? game.CurrentPlayer.Opponent : game.CurrentPlayer;
            owner.Hero.Card = Cards.FromId(hero);
            Check(ImbueRules.Blessing(owner.Hero.Card.Class) == blessing, hero + " maps to " + blessing);
            int cursor = capture.WirePackets.Count;
            ImbueRules.Imbue(owner, owner.Hero); FlushFieldHistory(match);
            var power = owner.Hero.HeroPower;
            Check(power.Card.Id == blessing && !ImbueRows(capture, cursor).Any(IsImbueFx),
                blessing + ": first Imbue keeps the native hero-power replacement only");
            // History on/off must not change state.
            var on = game.CloneExact(history: true); var off = game.CloneExact(history: false);
            foreach (var clone in new[] { on, off })
            {
                var player = owner.PlayerId == 1 ? clone.Player1 : clone.Player2;
                ImbueRules.Imbue(player, player.Hero);
            }
            Check(on.Hash() == off.Hash(), blessing + ": upgrade presentation leaves state unchanged");
            cursor = capture.WirePackets.Count;
            ImbueRules.Imbue(owner, owner.Hero); FlushFieldHistory(match);
            AssertImbueUpgrade(ImbueRows(capture, cursor), owner, power, new[] { 2 }, blessing + " second Imbue");
            cursor = capture.WirePackets.Count;
            ImbueRules.Imbue(owner, owner.Hero, 2); FlushFieldHistory(match);
            AssertImbueUpgrade(ImbueRows(capture, cursor), owner, power, new[] { 3, 4 }, blessing + " repeated Imbue");
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
            ImbueFxHandPlay(bot);
            ImbueFxOpenBlockPacket(bot);
            ImbueFxFinalityFromZero(bot);
            ImbueFxWisprider(bot);
            ImbueFxGoldpetalDeathrattle(bot);
            ImbueFxHamuulTrigger(bot);
            TrailingSubSpellEndGuard(bot);
        }
        Console.WriteLine("PASS Imbue FX: atomic upgrade pulse for all eight blessings and both seats; repeated, hand-played, suspended Living Garden, Finality replacement, Wisprider, Goldpetal deathrattle, Hamuul trigger; trailing SUB_SPELL_END guard");
    }

    private static (NativeMatch Match, Capture Capture, Controller Owner) ImbueSeat(string hero, bool bot, bool imbued = true)
    {
        var (match, capture) = AnimationMatch(botTurn: bot);
        var owner = match.Game.CurrentPlayer;
        owner.Hero.Card = Cards.FromId(hero);
        if (imbued) ImbueRules.Imbue(owner, owner.Hero);
        FlushFieldHistory(match);
        return (match, capture, owner);
    }

    private static void ImbueFxPlay(NativeMatch match, Controller owner, IPlayable card, bool flush = true)
    {
        owner.UsedMana = 0;
        FlushFieldHistory(match);
        InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id
            && (t.Target == null || t.Target.Id == owner.Opponent.Hero.Id)));
        if (flush) FlushFieldHistory(match);
    }

    // Real hand-play path inside a PLAY block, both controllers.
    private static void ImbueFxHandPlay(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_03", bot, imbued: false);
        HeroPower power = null;
        for (int play = 1; play <= 3; play++)
        {
            var source = EngineEntity.FromCard(owner, Cards.FromId("END_000"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.WirePackets.Count;
            ImbueFxPlay(match, owner, source);
            var rows = ImbueRows(capture, cursor);
            if (play == 1) { power = owner.Hero.HeroPower; Check(!rows.Any(IsImbueFx), "Eventuality first Imbue: no upgrade FX"); continue; }
            AssertImbueUpgrade(rows, owner, power, new[] { play }, $"Eventuality Imbue {play} bot={bot}");
            int block = rows.FindIndex(r => r.HasPowerStart && r.PowerStart.Source == source.Id && (int)r.PowerStart.Type == 7);
            Check(block >= 0 && rows.FindIndex(IsImbueFx) > block, "upgrade FX resolves inside the Eventuality PLAY block");
        }
        capture.SerializeAll();
    }

    // Living Garden suspends on its hand selection right after the Imbue; that packet must not end on the pulse.
    private static void ImbueFxOpenBlockPacket(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_02", bot);
        var power = owner.Hero.HeroPower;
        EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.HandZone);
        var garden = EngineEntity.FromCard(owner, Cards.FromId("EDR_518"), zone: owner.HandZone);
        FlushFieldHistory(match);
        int cursor = capture.WirePackets.Count;
        ImbueFxPlay(match, owner, garden);
        Check(owner.Choice != null, "Living Garden suspended on its hand selection");
        var packets = capture.HistorySnapshots(cursor).ToList();
        var packet = packets.Single(p => p.List.Any(IsImbueFx));
        Check(packets.All(p => !p.List.Last().HasSubSpellEnd), "suspended packet does not end on SUB_SPELL_END");
        AssertImbueUpgrade(packet.List, owner, power, new[] { 2 }, $"Living Garden bot={bot}");
        InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices[0])); FlushFieldHistory(match);
        Check(owner.Choice == null && owner[GameTag.IMBUE] == 2, "Living Garden finishes after the selection");
        capture.SerializeAll();
    }

    // Imbue twice from zero: replacement and pulse on the new entity share one packet.
    private static void ImbueFxFinalityFromZero(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_11", bot, imbued: false);
        Check(owner[GameTag.IMBUE] == 0, "Finality fixture starts unimbued");
        var finality = EngineEntity.FromCard(owner, Cards.FromId("END_003"), zone: owner.HandZone);
        FlushFieldHistory(match);
        int cursor = capture.WirePackets.Count;
        ImbueFxPlay(match, owner, finality);
        var power = owner.Hero.HeroPower;
        Check(power.Card.Id == "END_003p", "Finality installs the blessing");
        var packet = capture.HistorySnapshots(cursor).Single(p => p.List.Any(IsImbueFx));
        AssertImbueUpgrade(packet.List, owner, power, new[] { 2 }, $"Finality bot={bot}");
        int full = packet.List.FindIndex(r => r.HasFullEntity && r.FullEntity.Entity == power.Id);
        Check(full >= 0 && full < packet.List.FindIndex(IsImbueFx), "replacement FULL precedes its upgrade pulse in one packet");
        capture.SerializeAll();
    }

    // The direct hero-power effect runs after the pulse and sees the upgraded magnitude.
    private static void ImbueFxWisprider(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_08", bot);
        var power = owner.Hero.HeroPower;
        var rider = EngineEntity.FromCard(owner, Cards.FromId("EDR_519"), zone: owner.HandZone);
        FlushFieldHistory(match);
        int cursor = capture.WirePackets.Count;
        ImbueFxPlay(match, owner, rider);
        var rows = ImbueRows(capture, cursor);
        AssertImbueUpgrade(rows, owner, power, new[] { 2 }, $"Wisprider bot={bot}");
        int pulse = rows.FindIndex(IsImbueFx);
        var hits = rows.Select((r, i) => (r, i)).Where(x => x.r.HasMetaData && (int)x.r.MetaData.Type == 1).ToArray();
        Check(hits.Length == 2 && hits.All(x => x.i > pulse + 3), "Wisprider power fires after the pulse and tags, at magnitude 2");
        capture.SerializeAll();
    }

    private static void ImbueFxGoldpetalDeathrattle(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_04", bot);
        var power = owner.Hero.HeroPower;
        var drake = (Minion)EngineEntity.FromCard(owner, Cards.FromId("EDR_451"), zone: owner.BoardZone);
        FlushFieldHistory(match);
        int cursor = capture.WirePackets.Count;
        drake.ToBeDestroyed = true;
        match.Game.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match);
        var rows = ImbueRows(capture, cursor);
        AssertImbueUpgrade(rows, owner, power, new[] { 2 }, $"Goldpetal bot={bot}");
        var open = new Stack<PowerHistoryStart>();
        foreach (var row in rows.TakeWhile(r => !IsImbueFx(r)))
        {
            if (row.HasPowerStart) open.Push(row.PowerStart);
            if (row.HasPowerEnd) open.Pop();
        }
        Check(open.Count > 0 && open.Peek().Source == drake.Id && (int)open.Peek().Type == 5
            && open.Peek().TriggerKeyword == (int)GameTag.DEATHRATTLE, "pulse resolves inside the deathrattle TRIGGER block");
        capture.SerializeAll();
    }

    // Hamuul's Favor lives on the controller: its block may be hidden, the power's pulse stays public.
    private static void ImbueFxHamuulTrigger(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_06", bot);
        var power = owner.Hero.HeroPower;
        var hamuul = EngineEntity.FromCard(owner, Cards.FromId("EDR_845"), zone: owner.DeckZone);
        var favor = SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, hamuul, owner, Cards.FromId("EDR_845e1"), 3);
        favor[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        favor.Power.Trigger.Activate(favor);
        var spell = EngineEntity.FromCard(owner, Cards.FromId("GAME_005"), zone: owner.HandZone);
        FlushFieldHistory(match);
        int cursor = capture.WirePackets.Count;
        ImbueFxPlay(match, owner, spell);
        Check(owner[GameTag.IMBUE] == 2, "Favor Imbues on its third cast");
        var rows = ImbueRows(capture, cursor);
        AssertImbueUpgrade(rows, owner, power, new[] { 2 }, $"Hamuul Favor bot={bot}");
        if (bot)
        {
            // Favor's own FULL is public from creation; the deck card behind it never is, and the trigger reveals nothing.
            Check(!EmittedIds(ImbueRows(capture, 0)).Contains("EDR_845"), "enemy deck Hamuul identity stays hidden");
            var emitted = EmittedIds(rows);
            Check(!emitted.Contains("EDR_845") && !emitted.Contains("EDR_845e1"), "enemy Favor trigger reveals no identity");
            Check(!rows.Any(r => r.HasSubSpellStart && (r.SubSpellStart.SourceEntityId == hamuul.Id || r.SubSpellStart.SourceEntityId == favor.Id)),
                "upgrade pulse never names the hidden source");
        }
        capture.SerializeAll();
    }

    // A SUB_SPELL_END ending a packet makes PowerProcessor enqueue the list twice and replay the prefab.
    private static void TrailingSubSpellEndGuard(bool bot)
    {
        var (match, capture, owner) = ImbueSeat("HERO_08", bot, imbued: false);
        var game = match.Game;
        int cursor = capture.WirePackets.Count;
        game.PowerHistory.Add(new EngineHistory.PowerHistoryBlockStart { BlockType = BlockType.TRIGGER, Source = owner.Id, EffectIndex = -1 });
        game.PowerHistory.Add(new EngineHistory.PowerHistorySubSpellStart { Source = owner.Hero.Id,
            SpellPrefabGuid = ImbueHistory.Upgrade, Targets = new List<int> { owner.Hero.Id } });
        game.PowerHistory.Add(new EngineHistory.PowerHistorySubSpellEnd());
        FlushFieldHistory(match);
        var list = capture.HistorySnapshots(cursor).Single().List;
        var last = list.Last();
        Check(list[list.Count - 2].HasSubSpellEnd && last.HasMetaData && last.MetaData.Type == HistoryMeta.Type.SLUSH_TIME
            && last.MetaData.Data == 0 && last.MetaData.Info.Count == 0, "inert row keeps SUB_SPELL_END off the packet's last row");
        cursor = capture.WirePackets.Count;
        game.PowerHistory.Add(EngineHistory.PowerHistoryBuilder.BlockEnd()); FlushFieldHistory(match);
        Check(!capture.HistorySnapshots(cursor).SelectMany(p => p.List).Any(r => r.HasMetaData && r.MetaData.Type == HistoryMeta.Type.SLUSH_TIME),
            "guard stays out of ordinary packets");
        capture.SerializeAll();
    }

    private static List<PowerHistoryData> ImbueRows(Capture capture, int cursor)
    {
        var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
        if (Environment.GetEnvironmentVariable("HS_IMBUE_FX_TRACE") == "1")
            foreach (var row in rows)
                Console.WriteLine(row.HasPowerStart ? $"BLOCK {row.PowerStart.Type} source={row.PowerStart.Source}"
                    : row.HasPowerEnd ? "BLOCK_END"
                    : row.HasTagChange ? $"TAG {row.TagChange.Entity} {row.TagChange.Tag}={row.TagChange.Value}"
                    : row.HasSubSpellStart ? $"SUB_SPELL {row.SubSpellStart.SpellPrefabGuid} source={row.SubSpellStart.SourceEntityId} targets={string.Join(',', row.SubSpellStart.TargetEntityIds)}"
                    : row.HasSubSpellEnd ? "SUB_SPELL_END"
                    : row.HasMetaData ? $"META {row.MetaData.Type} data={row.MetaData.Data} info={string.Join(',', row.MetaData.Info)}"
                    : row.HasFullEntity ? $"FULL {row.FullEntity.Entity} {row.FullEntity.Name}" : "OTHER");
        return rows;
    }

    private static bool IsImbueFx(PowerHistoryData row) => row.HasSubSpellStart && row.SubSpellStart.SpellPrefabGuid == ImbueHistory.Upgrade;

    private static void AssertImbueUpgrade(List<PowerHistoryData> rows, Controller owner, HeroPower power, int[] counts, string label)
    {
        Check(owner.Hero.HeroPower == power && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == counts.Last(),
            label + ": upgrade keeps the same power entity and magnitude");
        var starts = rows.Select((r, i) => (r, i)).Where(x => IsImbueFx(x.r)).Select(x => x.i).ToArray();
        Check(starts.Length == counts.Length, label + ": one upgrade pulse per Imbue");
        bool second = power.Card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2);
        bool Tag(int at, int entity, GameTag tag, int value) => at < rows.Count && rows[at].HasTagChange
            && rows[at].TagChange.Entity == entity && rows[at].TagChange.Tag == (int)tag && rows[at].TagChange.Value == value;
        for (int n = 0; n < starts.Length; n++)
        {
            int at = starts[n];
            var start = rows[at].SubSpellStart;
            Check(start.SourceEntityId == power.Id && start.TargetEntityIds.SequenceEqual(new[] { power.Id }),
                label + ": impact anchored on the hero power (source=target=power)");
            Check(rows[at + 1].HasSubSpellEnd, label + ": pulse is atomic");
            Check(Tag(at + 2, owner.Id, GameTag.IMBUE, counts[n]) && Tag(at + 3, power.Id, GameTag.TAG_SCRIPT_DATA_NUM_1, counts[n]),
                label + ": controller IMBUE and magnitude immediately follow the pulse");
            if (second) Check(Tag(at + 4, power.Id, GameTag.TAG_SCRIPT_DATA_NUM_2, counts[n]), label + ": exported second magnitude follows");
            else Check(!rows.Any(r => r.HasTagChange && r.TagChange.Entity == power.Id && r.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2),
                label + ": power without a second magnitude never publishes one");
        }
    }
}
