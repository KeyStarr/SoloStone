using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-timers-misc", ProofKind.PoweredUp)]
    private static void B16TimersGlow()
    {
        var f=new Fixture();
        foreach(var id in new[]{"BE_327","CATA_496","FIR_927","JAIL_860","TIME_706"})
        { f.ClearHand(); Check(Cards.FromId(id).Implemented && !PoweredUp.IsMet(f.Draw(id)),"timers unconditional/no hand glow: "+id); }
    }
    [GroupProof("native-b16-timers-misc", ProofKind.Native)]
    private static void B16TimersNative()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var f=new Fixture();var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(f.Player));
            var p=enemy?g.Player2:g.Player1;
            var tome=Generic.DrawCard(p,Cards.FromId("BE_327"));f.Flush();
            Check(tome[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"native Tome countdown");
            if(!enemy) Check(f.Match.SnapshotVisibleTransportTags(tome).Keys.All(tag=>!B16PrivateTag(tag)),"live timer snapshots omit private tags");
            f.Process(EndTurnTask.Any(g.CurrentPlayer));f.Process(EndTurnTask.Any(g.CurrentPlayer));
            f.Process(EndTurnTask.Any(g.CurrentPlayer));f.Process(EndTurnTask.Any(g.CurrentPlayer));
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            Check(actors.All(e=>e.Tags.All(t=>!B16PrivateTag((int)t.Name))) &&
                f.Entries.Where(e=>e.HasTagChange).All(e=>!B16PrivateTag((int)e.TagChange.Tag)),"internal timer tags never projected");
            Check(actors.Any(e=>e.Entity==tome.Id),"Tome has native history actors");
            if(enemy) Check(actors.Where(e=>e.Entity==tome.Id).All(e=>string.IsNullOrEmpty(e.Name)),"opponent Tome hidden even after decipher");
        }
        B16FinsPrivacy();
        Console.WriteLine("PASS B16 native countdown, hand escrow and private identities");
    }
    private static bool B16PrivateTag(int tag) => (tag >= 32800 && tag <= 32819) || tag >= 40000;

    private static void B16FinsPrivacy()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var f=new Fixture();var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(g.CurrentPlayer));
            var p=g.CurrentPlayer;
            foreach(var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            // Native fixture supplies a captured opening; engine proofs exercise the real setup boundary.
            p[(GameTag)32804]=2;p[(GameTag)60000]=Cards.FromId("CS2_182").AssetId;p[(GameTag)60001]=Cards.FromId("GAME_005").AssetId;
            var saved=Generic.DrawCard(p,Cards.FromId("CS2_029"));
            var fins=Generic.DrawCard(p,Cards.FromId("TIME_706"));p.BaseMana=10;p.UsedMana=0;
            f.Process(PlayCardTask.Any(p,fins));
            var temporary=p.HandZone.Select(c=>c.Id).ToArray();
            Check(temporary.Length==2,"native Fins creates both starting-hand copies");
            var added=Generic.DrawCard(p,Cards.FromId("CS2_024"));f.Flush();
            f.Process(EndTurnTask.Any(p));
            Check(p.HandZone.Select(c=>c.Id).SequenceEqual(new[]{added.Id,saved.Id}),"native Fins restores originals and retains new card");
            var hidden=temporary.Concat(new[]{saved.Id,added.Id}).ToArray();
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null && hidden.Contains(e.Entity)).ToArray();
            Check(actors.Length>0,"escrow has native history actors");
            if(enemy) Check(actors.All(e=>string.IsNullOrEmpty(e.Name)),"opponent escrow, generated hand, and restoration identities stay private");
            Check(f.Entries.Where(e=>e.HasTagChange).All(e=>!B16PrivateTag((int)e.TagChange.Tag)),"opening identity tags absent from controller transport");
        }
    }
}
