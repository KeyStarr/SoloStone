using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    // #80: a Death Knight's Herald Soldier handed out Stadium Announcer that cost Health this turn; choosing Rewind froze the
    // board. Both payment kinds, from the human's seat, through the native choice path.
    private static void AnnouncerRewindHistory()
    {
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("HS_ANNOUNCER_SEEDS"), out int n) ? n : 1;
        // scenario: the live hook humanHandHealth stages the Soldier's Health cost (same override, placed before turn 1).
        foreach (string payment in new[] { "mana", "wing", "soldier", "scenario", "scenario-wing" })
        for (int seed = 0; seed < seeds; seed++)
        {
            var capture = new Capture();
            var scenario = payment.StartsWith("scenario", StringComparison.Ordinal) ? DebugScenario.Parse("humanHand=TIME_034\n"
                + (payment == "scenario" ? "humanHandHealth" : "humanHandHealthWing") + "=TIME_034\nhumanSeat=first") : null;
            var match = new NativeMatch(capture.Send, "mage", "mage", 8427) { DebugScenario = scenario, StartingPlayer = scenario?.FirstPlayer };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var player = game.Player1;
            player.BaseMana = 10; player.UsedMana = 0;
            if (seed > 0) game.SetRandomSeed(seed);
            var announcer = scenario != null ? (Playable)player.HandZone.Single(c => c.Card.Id == "TIME_034")
                : (Playable)Generic.DrawCard(player, Cards.FromId("TIME_034"));
            if (scenario != null)
                Check(match.DebugScenarioApplied.Contains(payment == "scenario" ? "humanHandHealth=TIME_034" : "humanHandHealthWing=TIME_034") && CostRules.PaymentKind(announcer) == CostPaymentKind.Health,
                    "scenario stages the Announcer at a Health cost: " + match.DebugScenarioApplied);
            if (payment == "wing")
            {
                var actor = SabberStoneCore.Model.Entities.Enchantment.GetInstance(player, player.Hero, announcer, Cards.FromId("CATA_155e2"));
                CostRules.AddPaymentOverride(announcer, CostPaymentKind.Health, game.Turn, actor.Id);
            }
            else if (payment == "soldier") CostRules.AddPaymentOverride(announcer, CostPaymentKind.Health, game.Turn);
            FlushFieldHistory(match);
            string name = "announcer-rewind-" + payment + (seed > 0 ? "-" + seed : "");
            int cursor = capture.Packets.Count;
            typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(match, null);
            var before = capture.Last<AllOptions>(14);
            int play = before.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(announcer.Id) && o.MainOption.PowerKeyword == 0);
            Check(play >= 0, name + ": native play option");
            match.SendOption(before.Id, play, 0, -1, 1);
            Check(game.Rewind != null && player.Choice?.ChoiceAction == ChoiceAction.REWIND, name + ": Rewind offered");
            var offer = capture.Last<EntityChoices>(17);
            match.SendChoices(offer.Id, new List<int> { game.Rewind.RewindEntityId });
            FlushFieldHistory(match);
            if (seed == 0) DumpDrawHistory(name, capture, cursor);
            Check(game.Rewind == null && player.Choice == null, name + ": Rewind settled");
            var options = capture.Last<AllOptions>(14);
            Check(options.Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN), name + ": End Turn offered after Rewind");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Stadium Announcer Rewind: mana, Wing and Soldier Health payment, native choice path (#80)");
    }
}
