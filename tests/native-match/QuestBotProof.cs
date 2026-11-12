using EngineUnknown = SabberStoneCore.Model.Entities.Unknown;
using EngineSpell = SabberStoneCore.Model.Entities.Spell;
using EngineGame = SabberStoneCore.Model.Game;
using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static QuestNativeProof;

// Kept in the native runner because it already compiles FairBotState; engine-proof has no plugin dependency.
internal static class QuestBotProof
{
    private static EngineGame SearchGame(FairBotState state) => (EngineGame)typeof(FairBotState)
        .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);

    internal static void Run()
    {
        foreach (bool opposing in new[] { false, true })
        {
            var g = Fixture(); var p = g.Player1;
            if (opposing) Check(g.Process(EndTurnTask.Any(p)), "opponent turn");
            var owner = opposing ? g.Player2 : p;
            var quest = (EngineSpell)Play(g, owner, "TLC_426");
            for (int i = 0; i < 4; i++) Summon(g, owner, "EX1_506");
            if (opposing) Check(g.Process(EndTurnTask.Any(owner)), "return bot perspective");
            p.UsedMana = 0;
            var murloc = Add(p, "CS2_168");
            var privateCard = Add(p.Opponent, "CS2_029");
            int cloneIndex = g.NextCloneIndex;
            var state = FairBotState.Create(g, p.PlayerId, 123, DeckStyle.Tempo);
            var copy = SearchGame(state);
            var copied = (EngineSpell)copy.IdEntityDic[quest.Id];
            Check(copied.Card.Id == "TLC_426" && copied.QuestProgress == 4
                && copied.QuestState.CompletionGeneration == 0 && !ReferenceEquals(copied.QuestState, quest.QuestState)
                && g.NextCloneIndex == cloneIndex, "fair view accepts public quest on either side and preserves source");
            FairViewHiddenOfferProof.AssertHiddenActors(g, copy, p.Opponent.PlayerId, privateCard.Id);
            Check(copy.Player1.DeckZone.All(c => c is EngineUnknown)
                && copy.Player2.DeckZone.All(c => c is EngineUnknown), "opponent hand and both decks masked");
            foreach (var resumed in new[] { copy.CloneExact(), SearchGame(FairBotState.Create(EngineGame.LoadSnapshot(g.SaveSnapshot()), p.PlayerId, 123, DeckStyle.Tempo)) })
            {
                var q = (EngineSpell)resumed.IdEntityDic[quest.Id];
                Check(q.QuestProgress == 4 && q.QuestState.RewardedGeneration == -1, "search clone/snapshot mid-progress equal");
                Summon(resumed, q.Controller, "EX1_506"); Summon(resumed, q.Controller, "EX1_506");
                Check(q.QuestProgress == 0 && QuestRules.PlayerEffectStacks(q.Controller, "TLC_426e") == 1,
                    "search resume threshold grants reward once");
            }
            var action = state.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == murloc.Id);
            var next = state.Apply(action); var nextGame = SearchGame(next);
            Check(((EngineSpell)nextGame.IdEntityDic[quest.Id]).QuestProgress == (opposing ? 4 : 5)
                && copied.QuestProgress == 4 && quest.QuestProgress == 4, "bot action clone progresses only owning quest independently");
            // A real secret remains unsupported even with a public quest sharing its zone.
            if (g.CurrentPlayer != owner) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "secret owner turn");
            Play(g, owner, "EX1_287");
            bool rejected = false;
            try { FairBotState.Create(g, p.PlayerId, 123, DeckStyle.Tempo); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, "real secret rejected alongside modern quest");
        }
        Console.WriteLine("PASS quest fair bot: either-side public quests; real secrets rejected; hidden hand/decks masked; independent progress; snapshot threshold");
    }
}
