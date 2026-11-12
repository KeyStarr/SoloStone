using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class DarkDeaconProof
{
    internal static void Run() => StandardSetsProof.Case("BE_012",Unpinned_HeldShadowGate);
    private static void Unpinned_HeldShadowGate(Game fixture,SabberStoneCore.Model.Entities.Controller owner,string id)
    {
        foreach(bool legacy in new[] { false,true })
        foreach(int shadows in new[] { 0,1,2 })
        {
            var g = fixture.CloneExact(history:true); var p = g.Player1;
            var anchor = legacy ? Summon(p,"CS2_182") : null;
            string shadow = legacy ? "EX1_302" : "BE_071";
            Play(g,p,shadow,anchor);
            var source = Add(p,id);
            Play(g,p,"GAME_005");
            for(int i=0;i<shadows;i++) Play(g,p,shadow,anchor);
            Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2-shadows,"counter counts only held owner Shadow casts, ignores earlier Shadow and non-Shadow");
            PlayEntity(g,p,source);
            Core2026Proof.Check((p.Choice != null) == (shadows == 2),"battlecry requires two held Shadow casts without starting C'Thun");
            if(shadows == 2)
            {
                CthunRulesProof.Pick(g,"BE_013t4");
                Core2026Proof.Check(p.BoardZone.Count == (legacy ? 4 : 3),"ready Deacon resolves base Servants");
                new ReturnHandTask(EntityType.TARGET).Process(g,p,source,source);
                g.RefreshHistoryDisplays();
                Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,"new hand residence resets held progress");
            }
        }
    }
}
