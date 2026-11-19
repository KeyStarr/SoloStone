using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using SabberStoneCore.Tasks.PlayerTasks;
using System.Reflection;

internal static partial class Program
{
    private static void MorchieRewindChoices()
    {
        foreach (string card in new[] { "CORE_EDR_004_2026", "TIME_EVENT_999" })
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
            var game = match.Game; var player = game.Player1;
            Generic.SummonBlock(game, (Minion)EngineEntity.FromCard(player, Cards.FromId("END_036")), -1, null);
            game.DeathProcessingAndAuraUpdate();
            var source = Generic.DrawCard(player, Cards.FromId(card));
            FlushFieldHistory(match);
            var first = ChoiceRetirementPlay(match, capture, source, card == "TIME_EVENT_999" ? 0 : 1);
            while (player.Choice?.ChoiceAction != ChoiceAction.REWIND)
            {
                var choice = capture.Last<EntityChoices>(17);
                match.SendChoices(choice.Id, new List<int> { choice.Entities[0] });
            }
            Check(game.Rewind?.KeepBoth == true, "Morchie active on wire");
            var offer = capture.Last<EntityChoices>(17);
            match.SendChoices(offer.Id, new List<int> { game.Rewind.RewindEntityId });
            var repeated = capture.Last<EntityChoices>(17);
            Check(player.Choice != null && player.Choice.ChoiceAction != ChoiceAction.REWIND && repeated.Id != first.Id && repeated.Id != offer.Id,
                card + " second effect opens fresh native choice");
            Check(repeated.Entities.Count > 0 && repeated.Entities.All(id => DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0)).ContainsKey(id)),
                card + " repeated offers exist on wire before click");
            while (player.Choice != null)
            {
                var choice = capture.Last<EntityChoices>(17);
                match.SendChoices(choice.Id, new List<int> { choice.Entities.Last() });
            }
            Check(game.Rewind == null && capture.Last<AllOptions>(14).Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN),
                card + " second choice resumes human options");
            capture.SerializeAll();
            match.StopNpc();
        }
        Console.WriteLine("PASS #158 Morchie native choices: Herald and Sands, second Discover packet, offers before click, resumes End Turn");
        BronzeRewindBoundary();
    }

    private static void BronzeRewindBoundary()
    {
        var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
        var game = match.Game; var player = game.Player1;
        var traces = new List<string>(); match.BotTrace = traces.Add;
        new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("END_000p")).Process(game, player, player.Hero, null);
        var eggs = Enumerable.Range(0, 3).Select(_ => (Minion)EngineEntity.FromCard(player.Opponent, Cards.FromId("DINO_410t3"))).ToArray();
        foreach (var egg in eggs) Generic.SummonBlock(game, egg, -1, null);
        game.DeathProcessingAndAuraUpdate();
        foreach (var egg in eggs) egg.ToBeDestroyed = true;
        game.DeathProcessingAndAuraUpdate();
        InvokeProcess(match, SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask.Any(player));
        FlushFieldHistory(match);
        Check(game.Rewind != null && traces.Any(s => s.StartsWith("REWIND_BOUNDARY Settling") && s.Contains("summoned=3")),
            "#159 native log identifies overdue deathrattle summons and hero power offers Rewind");
        match.StopNpc();
        Console.WriteLine("PASS #159 native boundary diagnostics: three Egg summons, live trace, Bronze Rewind offer");
    }

    private static void RewindSettlementNoOp(string only = null)
    {
        foreach (string scenario in new[] { "hand", "target", "lethal" }.Where(s => only == null || only == s))
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: NpcCatalog.All[0].DeckId);
            var game = match.Game; var player = game.CurrentPlayer;
            foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
            IPlayable source; Minion target = null;
            if (scenario == "target")
            {
                target = (Minion)EngineEntity.FromCard(player.Opponent, Cards.FromId("CS2_231"));
                Generic.SummonBlock(game, target, -1, null);
                Generic.SummonBlock(game, (Minion)EngineEntity.FromCard(player, Cards.FromId("NEW1_019")), -1, null);
                source = Generic.DrawCard(player, Cards.FromId("CS2_029")); source[GameTag.REWIND] = 1;
            }
            else
            {
                Generic.SummonBlock(game, (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_237")), -1, null);
                if (scenario == "hand")
                {
                    new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("END_000p")).Process(game, player, player.Hero, null);
                    while (player.HandZone.Count < 9) Generic.DrawCard(player, Cards.FromId("CS2_182"));
                    source = player.Hero.HeroPower;
                }
                else
                {
                    foreach (var held in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(held));
                    player.Hero.Health = 1;
                    source = Generic.DrawCard(player, Cards.FromId("TIME_001"));
                }
            }
            game.DeathProcessingAndAuraUpdate();
            game.SetRandomSeed(3);
            Generic.SummonBlock(game, (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_172")), -1, null);
            FlushFieldHistory(match);
            var task = player.Options().First(t => (scenario == "hand" ? t is HeroPowerTask : t.Source?.Id == source.Id)
                && t.ActionTarget?.Id == target?.Id);
            int rewards = match.RewardFacts.DistinctCardsPlayed;
            int cursor = capture.Packets.Count;
            if (bot)
                match.ActNpc(new GoldenDecision(BotAction.From(task), 0, 1, 1, "Exhausted", null, null));
            else
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int index = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(source.Id) && o.MainOption.PowerKeyword == 0);
                Check(index >= 0, "F1 native stale action offered");
                match.SendOption(options.Id, index, target?.Id ?? 0, -1, 0);
                if (scenario != "lethal") Check(capture.Last<AllOptions>(14).Id != options.Id &&
                    capture.Last<AllOptions>(14).Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN), "F1 native fresh options sent");
            }
            Check(game.Rewind == null && player.UsedMana == 0 && player.NumCardsPlayedThisTurn == 0 &&
                player.NumSpellsPlayedThisGame == 0 && player.NumTimesHeroPowerUsedThisGame == 0 && match.RewardFacts.DistinctCardsPlayed == rewards,
                $"F1 native {scenario} handled no-op without payment/play/reward: bot={bot}, mana={player.UsedMana}, rewards={match.RewardFacts.DistinctCardsPlayed}");
            Check(scenario == "hand" ? player.HandZone.Count == 10 && !player.Hero.HeroPower.IsExhausted
                : scenario == "target" ? target.Zone == player.Opponent.GraveyardZone && source.Zone == player.HandZone
                : game.State == State.COMPLETE && source.Zone == player.HandZone, "F1 native settled state kept");
            Check(History(capture, cursor).Any(e => scenario == "hand" ? e.HasTagChange && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.HAND
                : scenario == "target" ? IsZone(e, target.Id, SabberStoneCore.Enums.Zone.GRAVEYARD)
                : e.HasTagChange && e.TagChange.Tag == (int)GameTag.STATE && e.TagChange.Value == (int)State.COMPLETE), "F1 settlement sent to client");
            capture.SerializeAll(); match.StopNpc();
            Console.WriteLine($"PASS F1/F4 native {scenario}: bot={bot}, settled no-op, history flushed, no phantom reward");
        }
        if (only == null)
        {
            string skin = HeroSkinData.All.First(pair => pair.Value.BaseHero == "HERO_08").Key;
            var traces = new List<string>();
            var recreated = new NativeMatch((id, packet) => { }, "mage", "warrior", 7) {
                DebugScenario = DebugScenario.Parse("humanSkin=HERO_01bb\nnpcSkin=" + skin), BotTrace = traces.Add };
            recreated.Start();
            Check(recreated.DebugScenario == null && recreated.Game.Player2.Hero.Card.Id == "HERO_01" &&
                recreated.Game.RewindBoundaryDiagnostic != null, "F7 rejected NPC skin override recreates game with diagnostic sink");
            recreated.Game.RewindBoundaryDiagnostic("recreated");
            Check(traces.Contains("REWIND_BOUNDARY recreated"), "F7 recreated sink reaches live native trace");
            recreated.StopNpc();
            Console.WriteLine("PASS F7 native diagnostic sink survives rejected-scenario recreation");
        }
    }
}
