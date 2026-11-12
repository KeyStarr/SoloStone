using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class SpellBodyProgressProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "SpellBodyProgress: " + message);
    private static readonly ISimpleTask[] Steps = {
        new DiscoverThenContinueTask("CS2_182", "CS2_231", "CS2_120"),
        new CustomTask((g, c, s, t, stack) => Check(c.HandZone.Single(x => x.Card.Id == "JAIL_801")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "spell body continuation precedes upgrade")),
        new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand)),
        new ArmorTask(1)
    };
    private static readonly SuspendableTask Recipe = StateTaskList.Suspendable("proof/handprogress/body/v1", Steps);
    private static readonly DecisionSpellTask Decision = new DecisionSpellTask("proof/handprogress/option/v1", Steps);
    private static readonly SuspendableTask OtherSource = StateTaskList.Suspendable("proof/handprogress/other-source/v1",
        new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand)), new ArmorTask(1));
    private static readonly CustomTask Offers = new CustomTask((g, c, s, t, stack) =>
        new EffectContext(g, c.Id, s.Id).Discover(new[] { Cards.FromId("CS2_182") }));
    private static readonly CustomTask ChainedOffers = new CustomTask((g, c, s, t, stack) => {
        var ctx = new EffectContext(g, c.Id, s.Id);
        ctx.Discover(new[] { Cards.FromId("CS2_182") }, afterChoose: new DiscoverTask(DiscoverType.OP_DECK_MINION));
        ctx.Discover(new[] { Cards.FromId("CS2_231") });
    });

    internal static void Run()
    {
        foreach (string path in new[] { "power", "wrapped", "combo", "choose-one", "choose-both", "queued-callbacks", "other-source", "opponent-choice" })
        {
            var definition = Cards.FromId(path == "combo" ? "EDR_528" : path.StartsWith("choose-") ? "EX1_160" : "GAME_005");
            var power = definition.Power;
            bool consumesHistory = definition.ConsumesHistory;
            var options = path.StartsWith("choose-") ? new[] { Cards.FromId("EX1_160a"), Cards.FromId("EX1_160b") } : Array.Empty<Card>();
            var optionPowers = options.Select(x => x.Power).ToArray();
            try
            {
                definition.ConsumesHistory = true;
                definition.Power = path == "combo" ? new Power { PowerTask = Offers, ComboTask = Recipe } :
                    new Power { PowerTask = path == "wrapped" ? new CustomTask((g, c, s, t, stack) => Recipe.Process(g, c, s, t, stack)) :
                        path == "other-source" ? new CustomTask((g, c, s, t, stack) => OtherSource.Process(g, c, c.Hero, null, stack)) :
                        path == "opponent-choice" ? new CustomTask((g, c, s, t, stack) => OtherSource.Process(g, c.Opponent, c.Opponent.Hero, null, stack)) :
                        path == "queued-callbacks" ? ChainedOffers : Recipe };
                foreach (var option in options) option.Power = new Power { PowerTask = path == "choose-both" ? Offers : Decision };
                var game = HeraldProof.New(CardClass.DRUID); var p = game.Player1;
                if (path == "combo") p.IsComboActive = true;
                if (path == "choose-both") p.ChooseBoth = true;
                if (path == "opponent-choice") Add(p.Opponent, "CS2_231");
                var pyro = Summon(p, "NEW1_020");
                var held = Add(p, "JAIL_801"); held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
                var source = Add(p, definition.Id);
                Check(game.Process(PlayCardTask.Any(p, source, chooseOne: path == "choose-one" ? 1 : 0)), "body path plays: " + path);
                int prompts = 0;
                while (game.Player1.Choice != null || game.Player2.Choice != null)
                {
                    Check(game.IdEntityDic[held.Id].Card.Id == "JAIL_801" && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 &&
                        pyro.Damage == 0 && p.NumSpellsPlayedThisGame == 0, "every prompt holds credit and after-cast: " + path);
                    int selected = (game.Player1.Choice ?? game.Player2.Choice).Choices[0];
                    var bytes = game.SaveSnapshot();
                    var clone = game.CloneExact(history: true); var loaded = Game.LoadSnapshot(bytes);
                    foreach (var next in new[] { game, clone, loaded }) PlayerDecisionProof.Answer(next, selected);
                    Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded), "body clone/restore parity: " + path);
                    prompts++;
                }
                Check(prompts == (path == "queued-callbacks" ? 3 : path is "other-source" or "opponent-choice" ? 1 : 2) && game.IdEntityDic[held.Id].Card.Id == "JAIL_801t" &&
                    p.NumSpellsPlayedThisGame == 1 && pyro.Damage == 1 && source.Zone == p.GraveyardZone,
                    "final answer completes body and cast exactly once: " + path);
                var casts = game.HistoryFacts.Query.Casts().Where(f => f.EntityId == source.Id).ToArray();
                Check(casts.Length == 2 && casts.All(f => f.CasterKind == HistoryCastKind.Player), "one Player cast, no spurious Effect cast: " + path);
                game.SaveSnapshot();
            }
            finally
            {
                definition.Power = power;
                definition.ConsumesHistory = consumesHistory;
                for (int i = 0; i < options.Length; i++) options[i].Power = optionPowers[i];
            }
            Console.WriteLine("PASS SpellBodyProgress: " + path + " multiple prompts/clone/restore; Unpinned_HandUpgradeTiming");
        }
    }
}
