using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static DriverTests;

internal static class PrivateBranchTests
{
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool materialize in new[] { false, true })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "EDR_525", "NEW1_008");
            var bot = game.CurrentPlayer;
            Entity.FromCard(bot.Opponent, Cards.FromId("EDR_525"), zone: bot.Opponent.HandZone);
            var twin = game.CloneExact();
            foreach (var player in new[] { twin.Player1, twin.Player2 })
            {
                var parents = player.DeckZone.Where(c => c.ChooseOnePlayables != null).ToArray();
                DriverTests.Check(parents.Length == 2, "Private branch fixture needs two hidden Choose One parents");
                var first = parents[0]; var second = parents[1];
                (first.Card, second.Card) = (second.Card, first.Card);
                (first.ChooseOnePlayables, second.ChooseOnePlayables) = (second.ChooseOnePlayables, first.ChooseOnePlayables);
                foreach (var parent in parents)
                    foreach (var branch in parent.ChooseOnePlayables)
                    {
                        branch[GameTag.CREATOR] = parent.Id;
                        branch[GameTag.PARENT_CARD] = parent.Id;
                    }
                // EDR_525's untagged options must be private even without creator metadata.
                if (player.PlayerId != seat)
                    foreach (var branch in player.HandZone.SelectMany(c => c.ChooseOnePlayables ?? Array.Empty<IPlayable>()))
                        branch.Card = Cards.FromId(branch.Card.Id == "EDR_525A" ? "EDR_525B" : "EDR_525A");
                player.DeckZone.Shuffle();
            }
            var options = new FairOptions { TrackOwnDeck = materialize, MaterializeOwnDeckTriggers = materialize };
            var before = game.SaveSnapshot();
            var view = FairBotState.Create(game, seat, 89, DeckStyle.Tempo, options);
            var other = FairBotState.Create(twin, seat, 89, DeckStyle.Tempo, options);
            DriverTests.Check(FairViewTests.Fingerprint(view) == FairViewTests.Fingerprint(other),
                "Private Choose One definitions or parent mappings survived masking (materialize=" + materialize + ")");
            var driver = new DriverOptions { FairViewFactory = (g, owner, seed, style) => FairBotState.Create(g, owner, seed, style, options) };
            DriverTests.Check(Trace(Decide(game, seat, driver).Trace) == Trace(Decide(twin, seat, driver).Trace), "Private branches change decision");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Branch redaction changed authoritative state");
            if (materialize)
            {
                var projected = FairViewTests.Copy(view);
                var own = projected.CurrentPlayer;
                foreach (var parent in own.DeckZone.Where(c => c.ChooseOnePlayables != null))
                {
                    DriverTests.Check(parent.ChooseOnePlayables.All(c => c != null && !(c is Unknown)
                        && c[GameTag.CREATOR] == parent.Id && c[GameTag.PARENT_CARD] == parent.Id
                        && ReferenceEquals(projected.IdEntityDic[c.Id], c)), "Sampled branches are not fresh and consistently bound");
                }
            }
        }
        Console.WriteLine("PASS private Choose One/untagged Setaside mappings: both seats, full fingerprints, decisions, sampled branches");
    }
}
