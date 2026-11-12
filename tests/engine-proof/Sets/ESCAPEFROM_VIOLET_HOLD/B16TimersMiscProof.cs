using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B16TimersMiscCardsProof
{
    private static void Unpinned_Chef(Game g,Controller p,string id)
    {
        var opening=B15GameSetupProof.Opening(new[]{id}.Concat(Enumerable.Repeat("CS2_231",29)).ToArray());
        opening.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(opening.Player1,new()));
        opening.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(opening.Player2,new()));
        opening.MainBegin();
        for(int turn=1;turn<=4;turn++)
        {
            Core2026Proof.Check(opening.Player1.BaseMana==turn,"Chef not before fifth end");
            End(opening);
            Core2026Proof.Check(opening.Player1.BaseMana==turn,"Unpinned_Chef not at fourth end");
            End(opening);
        }
        foreach(var branch in new[]{opening,opening.CloneExact(history:true),Game.LoadSnapshot(opening.SaveSnapshot())})
        {
            End(branch);
            Core2026Proof.Check(branch.Player1.BaseMana==10 && branch.Player1.RemainingMana==10,"Unpinned_Chef fifth end before opponent turn survives clone/load");
            End(branch);
            Core2026Proof.Check(branch.Player1.RemainingMana==10,"Pinned_Chef first ten mana turn six");
        }
    }
}
