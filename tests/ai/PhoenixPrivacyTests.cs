using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class PhoenixPrivacyTests
{
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool materialize in new[] { false, true })
        {
            var game = DriverTests.Fixture(seat);
            var enemy = game.CurrentPlayer.Opponent;
            var first = Generic.DrawCard(enemy, Cards.FromId("NEW1_008"));
            var second = Generic.DrawCard(enemy, Cards.FromId("CS2_029"));
            var phoenix = Entity.FromCard(enemy, Cards.FromId("CATA_EVENT_001"), zone: enemy.BoardZone);
            game.DeathProcessingAndAuraUpdate();
            var twin = game.CloneExact(history: true);
            twin.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            twin.PowerHistory.Last.AddRange(game.PowerHistory.Last);
            HandTimelineRules.Ignite(phoenix, first);
            HandTimelineRules.Ignite(twin.IdEntityDic[phoenix.Id], twin.IdEntityDic[second.Id]);
            game.DeathProcessingAndAuraUpdate(); twin.DeathProcessingAndAuraUpdate();
            var before = game.SaveSnapshot(); var twinBefore = twin.SaveSnapshot();
            FairBotState View(Game source) => FairBotState.Create(source, seat, 73, DeckStyle.Tempo,
                new FairOptions { TrackOwnDeck = materialize, MaterializeOwnDeckTriggers = materialize });
            var left = View(game); var right = View(twin);
            string References(FairBotState state) => string.Join("|", FairViewTests.Copy(state).IdEntityDic.Values
                .OrderBy(c => c.Id).Select(c => c.Id + ":" + string.Join(",", (c.AppliedEnchantments ?? new List<Enchantment>()).Select(e => e.Id))
                    + (c is Enchantment e ? ":target=" + e.Target.Id + ":creator=" + e.Creator.Id : "")));
            DriverTests.Check(References(left) == References(right), "Phoenix ignition target changes reachable fair references");
            var leftPrint = FairViewTests.Fingerprint(left); var rightPrint = FairViewTests.Fingerprint(right);
            int difference = Enumerable.Range(0, Math.Min(leftPrint.Length, rightPrint.Length))
                .FirstOrDefault(i => leftPrint[i] != rightPrint[i]);
            DriverTests.Check(leftPrint == rightPrint,
                $"Phoenix fair view differs (seat={seat}, materialize={materialize}): "
                + leftPrint.Substring(difference, Math.Min(180, leftPrint.Length - difference)) + " != "
                + rightPrint.Substring(difference, Math.Min(180, rightPrint.Length - difference)));
            DriverTests.Check(AiFingerprint.Public(left) == AiFingerprint.Public(right), "Phoenix target changes public fingerprint");
            foreach (var state in new[] { left, right, left.WithArtificialSeed(2), right.WithArtificialSeed(2) })
            {
                var copy = FairViewTests.Copy(state);
                var owner = seat == 1 ? copy.Player2 : copy.Player1;
                var fires = owner.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_EVENT_001e").ToArray() ?? Array.Empty<Enchantment>();
                DriverTests.Check(fires.Length == 1, "Phoenix public schedule missing");
                var fire = fires[0];
                DriverTests.Check(fire.Target == owner && fire[GameTag.ATTACHED] == owner.Id
                    && fire[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3,
                    "Phoenix public schedule must have no hidden target");
                DriverTests.Check(owner.HandZone.All(c => c is Unknown && !HandTimelineRules.IsBurning(c)
                    && c.ChooseOnePlayables == null), "Phoenix hand/branches remain anonymous");
            }
            var a = BotSearch.Choose(left, BotBudget.ForStars(0).WithoutClock());
            var b = BotSearch.Choose(right, BotBudget.ForStars(0).WithoutClock());
            DriverTests.Check(a.Action.Equals(b.Action) && a.Value == b.Value && a.Nodes == b.Nodes,
                "Phoenix hidden target changes bot decision");
            var ended = left.Apply(left.Actions.Single(action => action.Type == PlayerTaskType.END_TURN));
            var enemyHand = FairViewTests.Copy(ended).CurrentPlayer.HandZone.Count(c => c is Unknown);
            var reply = ended.Determinize(Enumerable.Repeat("CS2_231", enemyHand).ToArray(), 73).ForReply();
            var ticked = FairViewTests.Copy(reply.ForReplyEndTurn());
            var tickedOwner = seat == 1 ? ticked.Player2 : ticked.Player1;
            DriverTests.Check(tickedOwner.AppliedEnchantments.Single(e => e.Card.Id == "CATA_EVENT_001e")[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2,
                "Anonymous Phoenix schedule must advance without selecting hidden fuel");
            var replyOwner = FairViewTests.Copy(reply).CurrentPlayer;
            replyOwner.AppliedEnchantments.Single(e => e.Card.Id == "CATA_EVENT_001e")[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
            var stopped = BotSearch.Choose(reply, BotBudget.ForStars(0).WithoutClock());
            DriverTests.Check(stopped.Action.Resolve(replyOwner.Options()) != null,
                "Search must handle private Phoenix expiry through its existing privacy boundary");
            var own = FairViewTests.Copy(FairBotState.Create(game, enemy.PlayerId, 73, DeckStyle.Tempo));
            DriverTests.Check(HandTimelineRules.IsBurning(own.IdEntityDic[first.Id])
                && own.IdEntityDic[first.Id].AppliedEnchantments.Single().Target == own.IdEntityDic[first.Id],
                "Owner must retain observed ignition target");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()) && twinBefore.SequenceEqual(twin.SaveSnapshot()),
                "Phoenix privacy projection changed authority");
        }
    }
}
