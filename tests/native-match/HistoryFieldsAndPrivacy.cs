using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void FlushFieldHistory(NativeMatch match)
    {
        FlushHistory(match);
        match.Game.PowerHistory.Last.Clear();
    }

    private static void HistoryFieldsAndPrivacy()
    {
        HandPlayRevealHistory();
        ChangedPlaySnapshotHistory();
        SelectedOfferPublicHistory();
        PublicExitHistory();
        ChoiceOfferHistory();
        KeywordTriggerHistory();
        Console.WriteLine("PASS public exits, private choice offers, deathrattle/Lifesteal headers");
    }

    private static void HandPlayRevealHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            var card = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            int cost = card.Cost;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id));
            card[GameTag.COST] = cost + 7;
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            var shows = data.Where(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id && e.ShowEntity.Name == card.Card.Id).ToArray();
            Check(shows.Length == 1, "Ordinary hand play needs exactly one named SHOW: bot=" + bot + " count=" + shows.Length);
            int play = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == card.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY);
            Check(!bot || data.IndexOf(shows[0]) < play && SnapshotTag(shows[0], GameTag.ZONE) == (int)EngineZone.HAND,
                "Bot play reveal must precede PLAY with historical HAND zone");
            Check(SnapshotTag(shows[0], GameTag.COST) == cost, "Play reveal must use frozen cost, not final engine getter");
            Check(owner.BoardZone.Contains(card), "Reveal must not change played minion result");
            capture.SerializeAll();
        }
    }

    private static void ChangedPlaySnapshotHistory()
    {
        foreach (bool transform in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var owner = match.Game.Player2;
            var card = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id));
            var entries = match.Game.PowerHistory.Last;
            int exit = entries.FindIndex(e => e is PowerHistoryShowEntity show && show.Entity.Id == card.Id);
            Check(exit >= 0, "Fixture needs frozen public-exit SHOW");
            var frozen = ((PowerHistoryShowEntity)entries[exit]).Entity;
            // Model an intervening transform or stat update before the frozen snapshot is reapplied.
            if (transform)
                entries.Insert(exit, new PowerHistoryChangeEntity { CardId = "CS2_231", Entity = new PowerHistoryEntity {
                    Id = card.Id, Name = "CS2_231", Tags = new Dictionary<GameTag, int>(frozen.Tags) } });
            else entries.Insert(exit, PowerHistoryBuilder.TagChange(card.Id, GameTag.ZONE_POSITION, 9));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            var shows = data.Where(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id && e.ShowEntity.Name == "CS2_182").ToArray();
            Check(shows.Length == 2, "Changed native identity/stats must retain public-exit snapshot: transform=" + transform);
            Check(SnapshotTag(shows[1], GameTag.COST) == 4, "Retained snapshot restores frozen cost");
            capture.SerializeAll();
        }
    }

    private static void SelectedOfferPublicHistory()
    {
        foreach (var action in new[] { ChoiceAction.CAST, ChoiceAction.SUMMON, ChoiceAction.HAND })
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var owner = match.Game.Player2;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CS2_120"), zone: owner.BoardZone);
            if (action == ChoiceAction.HAND)
                while (!owner.HandZone.IsFull) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var cards = action == ChoiceAction.CAST
                ? new[] { Cards.FromId("CS2_027"), Cards.FromId("CS2_023"), Cards.FromId("CS2_025") }
                : new[] { Cards.FromId("CS2_182"), Cards.FromId("CS2_231"), Cards.FromId("CS2_168") };
            Generic.CreateChoiceCards(owner, source, null, ChoiceType.GENERAL, action, cards, null);
            var offers = owner.Choice.Choices.Select(id => match.Game.IdEntityDic[id]).ToArray();
            var chosen = offers[0];
            InvokeProcess(match, ChooseTask.Pick(owner, chosen.Id));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(data.Where(e => e.HasFullEntity && offers.Any(o => o.Id == e.FullEntity.Entity))
                .All(e => e.FullEntity.Name.Length == 0), "Bot offers must start anonymous: " + action);
            int reveal = data.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == chosen.Id && e.ShowEntity.Name == chosen.Card.Id);
            int resolution = data.FindIndex(e => action == ChoiceAction.CAST
                ? e.HasPowerStart && e.PowerStart.Source == chosen.Id
                : IsZone(e, chosen.Id, action == ChoiceAction.SUMMON ? EngineZone.PLAY : EngineZone.GRAVEYARD));
            Check(reveal >= 0 && resolution > reveal, "Selected bot offer must reveal before public resolution: " + action);
            Check(!data.Any(e => offers.Skip(1).Any(o => o.Id == SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                "Rejected offers must stay private: " + action);
            Check(chosen.Zone.Type == (action == ChoiceAction.SUMMON ? EngineZone.PLAY : EngineZone.GRAVEYARD),
                "Selected offer result unchanged: " + action);
            Check(chosen[GameTag.REVEALED] == 0, "Public history reveal must not mutate gameplay tags");
            capture.SerializeAll();
        }
    }

    private static void PublicExitHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (var before in new[] { EngineZone.DECK, EngineZone.HAND })
        foreach (var after in new[] { EngineZone.PLAY, EngineZone.GRAVEYARD, EngineZone.REMOVEDFROMGAME })
        {
            var (match, capture) = AnimationMatch();
            var owner = bot ? match.Game.Player2 : match.Game.Player1;
            var victim = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"),
                zone: before == EngineZone.DECK ? owner.DeckZone : owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            int cost = victim.Cost;
            // Exercise raw zone edits as well as the shared zone containers.
            if (before == EngineZone.HAND && after == EngineZone.GRAVEYARD) Generic.DiscardBlock(owner, victim);
            else
            {
                victim.Zone.Remove(victim);
                if (after == EngineZone.PLAY) owner.BoardZone.Add((Minion)victim);
                else if (after == EngineZone.GRAVEYARD) owner.GraveyardZone.Add(victim);
                else victim[GameTag.ZONE] = (int)after;
            }
            victim[GameTag.COST] = cost + 7;
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            int move = data.FindIndex(e => IsZone(e, victim.Id, after));
            var reveal = data.Single(e => e.HasShowEntity && e.ShowEntity.Entity == victim.Id);
            Check(reveal.ShowEntity.Name == "CS2_182" && TagValue(reveal.ShowEntity.Tags, GameTag.COST) == cost
                && (after == EngineZone.GRAVEYARD
                    ? move < 0 && TagValue(reveal.ShowEntity.Tags, GameTag.ZONE) == (int)after
                    : move > 0 && ReferenceEquals(data[move - 1], reveal) && TagValue(reveal.ShowEntity.Tags, GameTag.ZONE) == (int)before),
                "Public exit preserves immutable identity; burn SHOW owns destination, other exits reveal before move");
            Check(data.Count(e => e.HasShowEntity && e.ShowEntity.Entity == victim.Id) == 1,
                "Public exit has exactly one reveal: " + bot + ":" + before + ":" + after + " count=" + data.Count(e => e.HasShowEntity && e.ShowEntity.Entity == victim.Id));
            capture.SerializeAll();
        }
    }

    private static void ChoiceOfferHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool lazy in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = bot ? match.Game.Player2 : match.Game.Player1;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CS2_120"), zone: owner.BoardZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var cards = new[] { Cards.FromId("CS2_182"), Cards.FromId("CS2_231"), Cards.FromId("CS2_168") };
            if (lazy)
            {
                owner.Choice = new Choice(owner, new[] { cards }) {
                    SourceId = source.Id, ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND };
                typeof(Choice).GetMethod("TryPrepare", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner.Choice, null);
            }
            else Generic.CreateChoiceCards(owner, source, null, ChoiceType.GENERAL, ChoiceAction.HAND, cards, null);
            var offers = owner.Choice.Choices.Select(id => match.Game.IdEntityDic[id]).ToArray();
            // No new gameplay tags: offer privacy belongs to frozen history.
            Check(offers.All(e => e[Generic.PrivateDecisionOfferTag] == 0), "Offer annotation must not mutate gameplay state");
            FlushFieldHistory(match);
            var data = History(capture, cursor).Where(e => offers.Any(o => o.Id == SnapshotEntity(e))).ToArray();
            Check(data.Count(e => e.HasFullEntity) == 3 && data.All(e => bot
                ? string.IsNullOrEmpty(SnapshotName(e)) && SnapshotTag(e, GameTag.CARDTYPE) == 0 && SnapshotTag(e, GameTag.COST) == 0
                : !string.IsNullOrEmpty(SnapshotName(e))), "Only bot SETASIDE offers must be masked");
            cursor = capture.Packets.Count;
            match.Game.PowerHistory.Add(new PowerHistoryChangeEntity {
                CardId = offers[0].Card.Id, Entity = PowerHistoryBuilder.ShowEntity(offers[0]).Entity });
            FlushFieldHistory(match);
            Check(History(capture, cursor).Where(e => e.HasChangeEntity).All(e => bot
                ? string.IsNullOrEmpty(e.ChangeEntity.Name) : e.ChangeEntity.Name == offers[0].Card.Id),
                "Cached private-offer provenance must also mask later CHANGE snapshots");
            var chosen = offers[0];
            InvokeProcess(match, ChooseTask.Pick(owner, chosen.Id));
            FlushFieldHistory(match);
            cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == chosen.Id));
            FlushFieldHistory(match);
            data = History(capture, cursor).ToArray();
            int play = Array.FindIndex(data, e => e.HasPowerStart && e.PowerStart.Source == chosen.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY);
            Check(play >= 0 && (!bot || data.Take(play).Any(e => e.HasShowEntity && e.ShowEntity.Entity == chosen.Id && e.ShowEntity.Name == chosen.Card.Id)),
                "Chosen bot offer must reveal normally before play");
            Check(!History(capture, cursor).Any(e => offers.Skip(1).Any(o => o.Id == SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                "Playing chosen offer cannot reveal rejected offers");
            capture.SerializeAll();
        }
    }

    private static void KeywordTriggerHistory()
    {
        foreach (string card in new[] { "CORE_DMF_067", "EDR_847" })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var host = EngineEntity.FromCard(game.Player1, Cards.FromId(card), zone: game.Player1.BoardZone);
            FlushFieldHistory(match);
            foreach (bool deathrattle in new[] { true, false })
            {
                int cursor = capture.Packets.Count;
                // The minion is alive: a manual Deathrattle activation, not death cleanup (no death fact to link).
                host.ActivateTask(deathrattle ? PowerActivation.DEATHRATTLE : PowerActivation.POWER, fromDeath: false);
                while (!game.TaskQueue.IsEmpty) game.TaskQueue.Process(); FlushFieldHistory(match);
                var start = History(capture, cursor).First(e => e.HasPowerStart && e.PowerStart.Source == host.Id).PowerStart;
                Check((int)start.Type == (deathrattle ? 5 : 3) && start.HasEffectIndex && start.EffectIndex == 0
                    && (deathrattle ? start.HasTriggerKeyword && start.TriggerKeyword == 217 : !start.HasTriggerKeyword),
                    "Shared task needs instance-local Deathrattle TRIGGER; later Battlecry stays POWER");
            }
            int deathStart = capture.Packets.Count;
            host.ToBeDestroyed = true;
            game.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match);
            AssertDeathrattleAfterDeaths(History(capture, deathStart), host.Id);
        }
        foreach (bool swimmer in new[] { false, true })
        {
            var (match, capture) = AnimationMatch();
            var g = match.Game;
            var host = EngineEntity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone: g.Player1.BoardZone);
            Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_906e"), host, host);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            if (swimmer) Cards.FromId("JAIL_395").Power.PowerTask.Process(g, g.Player1, host, host, new TaskStack());
            else new ActivateDeathrattleTask(EntityType.SOURCE).Process(g, g.Player1, host, null, new TaskStack());
            while (!g.TaskQueue.IsEmpty) g.TaskQueue.Process(); FlushFieldHistory(match);
            Check(History(capture, cursor).Any(e => e.HasPowerStart && e.PowerStart.Source == host.Id
                && (int)e.PowerStart.Type == 5 && e.PowerStart.TriggerKeyword == 217 && e.PowerStart.EffectIndex == 0),
                "Explicit granted Deathrattle needs keyword 217 on host");
            cursor = capture.Packets.Count;
            host.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match);
            AssertDeathrattleAfterDeaths(History(capture, cursor), host.Id);
        }
        var (life, packets) = AnimationMatch();
        var source = EngineEntity.FromCard(life.Game.Player1, Cards.FromId("TIME_056"), zone: life.Game.Player1.BoardZone);
        life.Game.Player1.Hero.Damage = 5;
        FlushFieldHistory(life); int from = packets.Packets.Count;
        life.Game.Player2.Hero.TakeDamage(source, 2); FlushFieldHistory(life);
        Check(History(packets, from).Any(e => e.HasPowerStart && e.PowerStart.Source == source.Id
            && (int)e.PowerStart.Type == 5 && e.PowerStart.HasTriggerKeyword && e.PowerStart.TriggerKeyword == 685
            && e.PowerStart.HasEffectIndex && e.PowerStart.EffectIndex == 0), "Lifesteal needs TRIGGER keyword 685");
        Check(life.Game.Player1.Hero.Damage == 3, "Lifesteal result unchanged");
        from = packets.Packets.Count;
        SendHistoryEntries(life, new List<IPowerHistoryEntry> {
            new PowerHistoryBlockStart { BlockType = BlockType.POWER, Source = source.Id, Index = 7 },
            PowerHistoryBuilder.BlockEnd(),
            PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", 9, 0, effectIndex: 1),
            PowerHistoryBuilder.BlockEnd(),
            PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, life.Game.Player1.Id, "", 8, 0, effectIndex: -1),
            PowerHistoryBuilder.TagChange(life.Game.Player1.Id, GameTag.RESOURCES_USED, life.Game.Player1.UsedMana),
            PowerHistoryBuilder.BlockEnd() });
        var headers = History(packets, from).Where(e => e.HasPowerStart).Select(e => e.PowerStart).ToArray();
        Check(!headers[0].HasEffectIndex && !headers[0].HasTriggerKeyword && headers[0].SubOption == 7
            && headers[1].HasEffectIndex && headers[1].EffectIndex == 1 && headers[1].SubOption == 9
            && !headers[1].HasTriggerKeyword && headers[2].EffectIndex == -1 && headers[2].SubOption == 8,
            "Optional native fields preserve absence, explicit effect selector, and independent SubOption");
        packets.SerializeAll();
    }

    private static void AssertDeathrattleAfterDeaths(List<PegasusGame.PowerHistoryData> data, int host)
    {
        int depth = 0, deathsDepth = -1, deathsEnd = -1;
        for (int i = 0; i < data.Count; i++)
        {
            if (data[i].HasPowerStart)
            {
                if ((int)data[i].PowerStart.Type == (int)BlockType.DEATHS) deathsDepth = depth;
                depth++;
            }
            else if (data[i].HasPowerEnd && --depth == deathsDepth) { deathsEnd = i; deathsDepth = -1; }
        }
        int trigger = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == host && (int)e.PowerStart.Type == 5
            && e.PowerStart.TriggerKeyword == 217 && e.PowerStart.HasEffectIndex && e.PowerStart.EffectIndex == 0);
        Check(deathsEnd >= 0 && trigger > deathsEnd, "Printed/granted deathrattle starts after closed DEATHS block");
    }
}
