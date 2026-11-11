using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static LostCityPoolProof;

internal static class IndirectSpellProof
{
    internal static void Run()
    {
        TessReplayCompletes(PlayerDecisionProof.Fixture());
        Console.WriteLine("PASS TessReplayCompletes: Relic continuation/discount/grant; next legal action; clone/restore");
        SuspendableAutomaticCastsComplete();
        NestedAutomaticHandProgress();
        Console.WriteLine("PASS SuspendableAutomaticCastsComplete: Wanted Poster effect/PlayTask/Discover; Prepare/grant; next legal action; clone/restore");
    }

    private static void TessReplayCompletes(Game g)
    {
        var p = g.Player1;
        var relic = Play(g, p, "TLC_334");
        Answer(g, Offers(g, p, relic, c => c.Type == CardType.SPELL && c.Cost >= 8)[0]);
        PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Resume(g, next => {
            WithPool(next, "TLC_334", new[] { "TLC_835" }, () => {
                var owner = next.Player1;
                Play(next, owner, "GIL_598");
                Core2026Proof.Check(!next.Resolution.HasSuspendedEffect && owner.Choice == null && next.SpellCompletion == null,
                    "TessReplayCompletes: indirect Relic resolves its Discover cursor inline");
                var made = owner.HandZone.Single();
                Core2026Proof.Check(made.Card.Id == "TLC_835" && made.Cost == 1,
                    "TessReplayCompletes: replay runs the post-Discover discount and hand grant once");
                var power = owner.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
                Core2026Proof.Check(next.Process(power) && owner.Hero.HeroPower.IsExhausted,
                    "TessReplayCompletes: next offered hero power is accepted");
            });
        });
    }

    private static void SuspendableAutomaticCastsComplete()
    {
        foreach (string path in new[] { "effect", "PlayTask", "Discover" })
        {
            var g = PlayerDecisionProof.Fixture();
            PoolPilotProof.Resume(g, next => {
                WithPool(next, "CAP_407", new[] { "CORE_EX1_110" }, () => {
                    var p = next.Player1;
                    var held = Add(p, "JAIL_803"); held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
                    Spell source;
                    if (path == "Discover")
                    {
                        Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, ChoiceAction.CAST,
                            new[] { Cards.FromId("CAP_407") }, null);
                        source = (Spell)next.IdEntityDic[p.Choice.Choices.Single()];
                        Core2026Proof.Check(next.Process(ChooseTask.Pick(p, source.Id)), "automatic Discover cast accepts answer");
                    }
                    else
                    {
                        source = (Spell)Entity.FromCard(p, Cards.FromId("CAP_407"));
                        next.TaskQueue.Execute(new CustomTask((game, owner, caller, target, stack) => {
                            if (path == "effect") Generic.CastSpell(owner, game, source, null, 0);
                            else new PlayTask(PlayType.SPELL).Process(game, owner, caller, null,
                                new TaskStack { Playables = new List<IPlayable> { source } });
                        }), p, p.Hero, null);
                        next.DeathProcessingAndAuraUpdate();
                    }
                    Core2026Proof.Check(!next.Resolution.HasSuspendedEffect && p.Choice == null && next.SpellCompletion == null &&
                        source.Zone == p.GraveyardZone && p[Generic.AutomaticDecisionTag] == 0,
                        "SuspendableAutomaticCastsComplete: no pending cursor/completion/choice " + path);
                    var made = p.HandZone.Single(c => c.Id != held.Id);
                    Core2026Proof.Check(made.Card.Id == "CORE_EX1_110" && made[GameTag.PREPARE] == 1 &&
                        held.Card.Id == "JAIL_803" && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 &&
                        p.UsedMana == 0 && p.NumSpellsPlayedThisGame == 0,
                        "Unpinned_HandCastScope: automatic cast grants one Prepared minion without hand-cast credit " + path);
                    var power = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
                    Core2026Proof.Check(next.Process(power) && p.Hero.HeroPower.IsExhausted, "next offered hero power is accepted " + path);
                });
            });
        }
    }
    private static void NestedAutomaticHandProgress()
    {
        var card = Cards.FromId("GAME_005"); var power = card.Power;
        try
        {
            card.Power = new SabberStoneCore.Enchants.Power { PowerTask = new CustomTask((g, c, source, target, stack) => {
                foreach (string id in new[] { "TLC_334", "TIME_770" })
                    Generic.CastSpell(c, g, (Spell)Entity.FromCard(c, Cards.FromId(id)), null, 0);
                Core2026Proof.Check(c.HandZone.Single(c => c.Card.Id == "JAIL_803")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
                    "Unpinned_HandCastScope: indirect recipes do not claim outer hand-progress completion");
            }) };
            var game = PlayerDecisionProof.Fixture();
            Add(game.Player1, "JAIL_803")[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            PoolPilotProof.Resume(game, next => WithPool(next, "TLC_334", new[] { "TLC_835" }, () => {
                Play(next, next.Player1, "GAME_005");
                Core2026Proof.Check(next.SpellCompletion == null && !next.Resolution.HasSuspendedEffect && next.Player1.Choice == null &&
                    next.Player1.HandZone.Count(c => c.Card.Id == "JAIL_803t") == 1 && next.Player1.NumSpellsPlayedThisGame == 1,
                    "Unpinned_HandUpgradeTiming: outer body completes once after both indirect recipe types");
            }));
            Console.WriteLine("PASS NestedAutomaticHandProgress: SuspendableTask + DecisionSpellTask inside tracked body; Unpinned_HandCastScope Unpinned_HandUpgradeTiming");
        }
        finally { card.Power = power; }
    }

}
