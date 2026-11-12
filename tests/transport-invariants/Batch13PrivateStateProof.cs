using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal static class Batch13PrivateStateProof
{
    internal static void Run()
    {
        DecisionMinionReadiness();
        foreach (bool opponent in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame(opponent, hero: CardClass.HUNTER);
            var owner = game.CurrentPlayer;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            var source = Generic.DrawCard(owner, Cards.FromId("MEND_303"));
            owner.BaseMana = 10;
            session.Flush(); game.PowerHistory.Last.Clear();
            session.Process(PlayCardTask.Any(owner, source));
            SelfChecks.Check(owner[CompanionRules.Cost] == 4, "companion roster really upgraded");
            // Engine-only rule state with no client counterpart: companion extra summons (31074), completed rituals
            // (32901), the Sorry unlock (33200) and the autonomous-turn counter (33420). None may ever reach the client.
            foreach (int tag in new[] { 31074, 32901, 33200, 33420 }) owner[(GameTag)tag] = 1;
            session.Flush();
            SelfChecks.Check(Invariants.Compare(game, session.Client).All(v => v.Tag != "PRIVATE_RULE_TAG"),
                "objective, play-lock and autonomous-turn tags stay private before reset");
            var reset = new List<IPowerHistoryEntry> {
                PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, owner.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
            reset.AddRange(game.IdEntityDic.Values.Select(entity => PowerHistoryBuilder.FullEntity(entity)));
            reset.Add(PowerHistoryBuilder.BlockEnd());
            session.Invoke("SendHistory", reset);
            var violations = Invariants.CompareReset(game, session.Client).ToArray();
            SelfChecks.Check(violations.Length == 0, "companion private state is absent from reset oracle: " + string.Join("; ", violations));
            foreach (var tag in owner.NativeTags.Keys.Where(t => CompanionRules.IsInternalTag(t) || (int)t is 32901 or 33200 or 33420))
                SelfChecks.Check(session.Client.Entities[owner.Id].Get(tag) == 0, "companion state never reaches native player");
        }
        Console.WriteLine("PASS batch 13 private companion state: both owners, wire and reset oracle");
    }

    private static void DecisionMinionReadiness()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame(opponent, hero: CardClass.HUNTER);
            var owner = game.CurrentPlayer;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            var niri = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("TLC_836"));
            Generic.SummonBlock(game, niri, -1, null);
            var source = Generic.DrawCard(owner, Cards.FromId("BE_036"));
            owner.BaseMana = 10;
            session.Flush(); game.PowerHistory.Last.Clear();
            session.Process(PlayCardTask.Any(owner, source));
            SelfChecks.Check(owner.Choice != null && source.IsExhausted
                && session.Client.Entities[source.Id].Get(GameTag.EXHAUSTED) == 1, "decision minion publishes exhaustion before its choice");
            session.Process(ChooseTask.Pick(owner, owner.Choice.Choices[0]));
            SelfChecks.Check(source.IsExhausted && session.Client.Entities[source.Id].Get(GameTag.EXHAUSTED) == 1,
                "decision minion remains exhausted after its choice");
        }
    }
}
