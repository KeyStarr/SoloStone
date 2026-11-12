using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("agent-coin-choice-native", ProofKind.Native)]
    private static void AgentCoinChoiceHistory()
    {
        ChoicePresentationDetection();
        BanishedChoiceCreator();
        TargetChoiceSourceFallback();
        foreach (bool map in new[] { false, true })
        foreach (string finish in map ? new[] { "pick", "watchdog", "pre-focus", "pre-pause", "concede" } : new[] { "pick" })
        {
            // Lifecycle callbacks can arrive during matchmaking, before any NativeMatch exists.
            if (finish == "pre-focus") NativeMatch.ChoiceApplicationFocus(false);
            if (finish == "pre-pause") NativeMatch.ChoiceApplicationPause(true);
            var (match, capture) = AnimationMatch(deck: "showcase-rogue-imbue");
            var g = match.Game; var p = g.Player1;
            foreach (var old in p.HandZone.Concat(p.DeckZone).ToArray()) p.SetasideZone.Add(old.Zone.Remove(old));
            IPlayable Add(string id, bool deck = false) => EngineEntity.FromCard(p, Cards.FromId(id), zone: deck ? p.DeckZone : p.HandZone);
            void Play(IPlayable card)
            {
                p.UsedMana = 0;
                FlushFieldHistory(match);
                InvokeProcess(match, PlayCardTask.Any(p, card));
                FlushFieldHistory(match);
                if (p.Choice != null) typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            }
            void Pick(int id) => match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int> { id });
            Play(Add("JAIL_504"));
            Pick(p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Id == "JAIL_504t"));
            var target = Add("CS2_182");
            IPlayable source = Add("CATA_200", deck: map);
            int mapSource = 0;
            if (map)
            {
                Add("TLC_515", deck: true); Add("CATA_203", deck: true);
                var spell = Add("TLC_515"); mapSource = spell.Id;
                Play(spell);
                Pick(p.Choice.Choices.Single(id => g.IdEntityDic[id][DiscoverRealRules.BoundEntity] == source.Id));
            }
            int before = capture.Packets.Count;
            Play(source);
            Check(p.Choice.ChoiceType == ChoiceType.TARGET && p.Choice.SourceId == source.Id, "Agent opens its one hand selector");
            Pick(target.Id);
            Check(g.IdEntityDic[target.Id].Card.Id == "JAIL_504t", "Aya replaces Agent's in-place Coin");
            var prompts = capture.Packets.Skip(before).Where(x => x.Id == 17).Select(x => (EntityChoices)x.Message).ToArray();
            Check(prompts.Count(c => c.Source == source.Id) == 1, "Agent continuation does not run twice");
            if (!map) Check(p.Choice == null && prompts.Length == 1, "Aya plus Agent has no phantom choice");
            else
            {
                var choice = p.Choice;
                Check(g.IdEntityDic[choice.SourceId].Card.Id == "TLC_515e", "bonus belongs to Map watcher");
                var native = capture.Last<EntityChoices>(17);
                Check(native.Source == mapSource, "native Map choice uses surviving printed creator, never removed enchantment");
                foreach (int offer in native.Entities)
                    Check(History(capture, before).Any(e => e.HasFullEntity && e.FullEntity.Entity == offer && !string.IsNullOrEmpty(e.FullEntity.Name)),
                        "Map offer definition projected before prompt");
                int chosen = choice.Choices[0], original = g.IdEntityDic[chosen][DiscoverRealRules.BoundEntity];
                if (finish == "concede")
                {
                    match.Concede();
                    Check(g.State == State.COMPLETE && p.Choice == null && match.HumanConceded
                        && p.PlayState == PlayState.LOST, "concede cancels suspended Map and settles through engine");
                    Check(History(capture, before).Any(e => e.HasTagChange && e.TagChange.Entity == p.Id
                        && e.TagChange.Tag == (int)GameTag.PLAYSTATE && e.TagChange.Value == (int)PlayState.LOST),
                        "concede projects actual terminal tags while choice actors are absent");
                }
                else
                {
                    if (finish == "watchdog" || finish.StartsWith("pre-"))
                    {
                        double now = 0;
                        var presentation = MissingChoicePresentation(capture.Last<EntityChoices>(17).Id);
                        match.NpcClock = () => now; match.NativeHumanChoicePresentation = () => presentation;
                        var traces = new List<string>(); match.BotTrace = traces.Add;
                        void Tick(double time) { now = time; match.TickChoiceWatchdog(); }
                        void Hold(double start, double end)
                        { for (double time = start; time <= end; time += 500) Tick(time); }
                        if (finish.StartsWith("pre-"))
                        {
                            Hold(-20000, -500);
                            Check(p.Choice == choice, "lifecycle loss before match construction suppresses watchdog: " + finish);
                            if (finish == "pre-focus") NativeMatch.ChoiceApplicationFocus(true);
                            else NativeMatch.ChoiceApplicationPause(false);
                        }
                        Hold(0, 14500);
                        Check(p.Choice == choice, "missing-actor grace does not answer early");
                        Tick(30000); // Sleep/suspend is not observed continuous failure.
                        Check(p.Choice == choice, "15s sampling gap restarts watchdog");
                        presentation.Settled = false; Tick(30500);
                        presentation.Settled = true; Hold(31000, 45000);
                        NativeMatch.ChoiceApplicationPause(true); Tick(45500);
                        NativeMatch.ChoiceApplicationPause(false); Hold(46000, 60000);
                        Check(p.Choice == choice, "pause/resume resets full grace");
                        NativeMatch.ChoiceApplicationFocus(false); Tick(60500);
                        NativeMatch.ChoiceApplicationFocus(true); Hold(61000, 75000);
                        Check(p.Choice == choice, "focus resets full grace");
                        presentation.Offers = new[] { ReadyChoiceOffer(), default(NativeMatch.OfferPresentation) };
                        presentation.SourceExists = false;
                        Hold(75500, 95500);
                        Check(p.Choice == choice, "mixed ready/missing picker with missing source remains human-owned beyond grace");
                        presentation.SourceExists = true; presentation.Offers[0] = default; Hold(96000, 110500);
                        Check(p.Choice == choice, "valid picker resets continuous failure");
                        Tick(111000); Tick(112000);
                        Check(traces.Count(t => t.StartsWith("CHOICE_STALL_RECOVERY")) == 1,
                            "settled unrenderable choice auto-resolves once with diagnostic");
                    }
                    else Pick(chosen);
                    Check(p.Choice == null && g.IdEntityDic[original].Zone == p.HandZone, "Map bonus draws bound original and completes");
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Aya/Agent: one selector; retained Map bonus has surviving source and projected offers");
    }
}
