using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class ObjectivesProof
{
    internal static Spell Install(Controller p, string id)
    {
        var rules = typeof(Spell).Assembly.GetType("SabberStoneCore.Actions.ObjectiveRules");
        Core2026Proof.Check(rules != null, "shared objective lifecycle missing");
        return (Spell)rules.GetMethod("Install").Invoke(null, new object[] { Entity.FromCard(p, Cards.FromId(id)) });
    }
    [GroupProof("b16-objectives")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var first = Install(p, "BE_085"); var second = Install(p, "BE_085");
            Core2026Proof.Check(p.SecretZone.Count(s => s.Card.Id == "BE_085") == 2 && p.SecretZone.OccupiedSecretSlots == 0, "independent public objectives");
            End(g); Core2026Proof.Check(first[GameTag.QUEST_PROGRESS] == 0, "opponent start never ticks"); End(g);
            Core2026Proof.Check(first[GameTag.QUEST_PROGRESS] == 1, "first subsequent owner start");
            foreach (var branch in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                End(branch); End(branch);
                Core2026Proof.Check(branch.Player1.BoardZone.Count == 4 && branch.IdEntityDic[first.Id].Zone == branch.Player1.GraveyardZone, "cloned and restored objectives expire once");
                End(branch); End(branch); Core2026Proof.Check(branch.Player1.BoardZone.Count == 4, "no retry after completion");
            }
        }
        var played = HeroDivineShieldProof.NewGame(true);
        var objective = Play(played, played.Player1, "BE_085");
        Core2026Proof.Check(objective.Zone == played.Player1.SecretZone && ObjectiveRules.IsActive(objective),
            "played objective survives tracked spell retirement");
        Lifecycle();
        var extra=HeroDivineShieldProof.NewGame(true);var extraActor=Install(extra.Player1,"BE_085");
        new ExtraTurnEffectTask().Process(extra,extra.Player1,extra.Player1.Hero,null);End(extra);
        Core2026Proof.Check(extra.CurrentPlayer==extra.Player1 && ObjectiveRules.Remaining(extraActor)==1,"extra owner start advances clock");
        new ExtraTurnEffectTask().Process(extra,extra.Player1,extra.Player1.Hero,null);End(extra);
        Core2026Proof.Check(extra.Player1.BoardZone.Count==2 && ObjectiveRules.CompletedRituals(extra.Player1)==1,"two extra starts complete exactly once");
        var countered=HeroDivineShieldProof.NewGame(true);
        Play(countered,countered.Player1,"EX1_287");End(countered);
        var owner=countered.Player2;
        var counteredSpell=(Spell)Play(countered,owner,"BE_085");
        Core2026Proof.Check(counteredSpell.Zone==owner.GraveyardZone && !owner.SecretZone.Any(ObjectiveRules.IsActive)
            && countered.Player1.SecretZone.Count==0,"Counterspell consumes itself and cancels installation");
        B16ObjectivesProof.Cycle(countered);B16ObjectivesProof.Cycle(countered);
        Core2026Proof.Check(ObjectiveRules.CompletedRituals(owner)==0,"countered ritual never completes");
        Console.WriteLine("PASS objective lifecycle, clone/snapshot, removal, Rewind, ordered decisions");
    }
    private static void Lifecycle()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        var removed=Install(p,"BE_085");p.SetasideZone.Add(p.SecretZone.Remove(removed));
        B16ObjectivesProof.Cycle(g);B16ObjectivesProof.Cycle(g);
        Core2026Proof.Check(p.BoardZone.Count==0 && ObjectiveRules.CompletedRituals(p)==0,"leave-play cancels objective");
        var original=Install(p,"BE_085");B16ObjectivesProof.Cycle(g);
        Generic.CastSpell(p,g,original,null,0);
        Core2026Proof.Check(p.SecretZone.Count(ObjectiveRules.IsActive)==2 && ObjectiveRules.Remaining(original)==1,"recast active entity creates independent timer");
        Play(g,p,"TIME_001");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            branch.IdEntityDic[original.Id][GameTag.QUEST_PROGRESS]=0;
            RewindTestHelpers.AnswerRewind(branch);
            Core2026Proof.Check(ObjectiveRules.Remaining(branch.IdEntityDic[original.Id])==1,"Rewind restores objective state");
            RewindTestHelpers.KeepIfOffered(branch);
        }
        var choices=HeroDivineShieldProof.NewGame(true);var owner=choices.Player1;
        Install(owner,"BE_726");Install(owner,"BE_726");Install(owner,"BE_085");
        B16ObjectivesProof.Cycle(choices);B16ObjectivesProof.Cycle(choices);
        Core2026Proof.Check(owner.Choice!=null && owner.BoardZone.Count==0 && ObjectiveRules.CompletedRituals(owner)==1,"first due choice suspends later objectives");
        CthunRulesProof.Pick(choices,"BE_013t4");
        Core2026Proof.Check(owner.Choice!=null && owner.BoardZone.Count==2 && ObjectiveRules.CompletedRituals(owner)==2,"second objective resumes after first choice");
        CthunRulesProof.Pick(choices,"BE_013t4");
        Core2026Proof.Check(owner.Choice==null && owner.BoardZone.Count==6 && ObjectiveRules.CompletedRituals(owner)==3,"due objective order then ordinary draw");
    }
}
