using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-fabled",ProofKind.PoweredUp)]
    private static void B16FabledGlow()
    {
        Scenario("TIME_005",(f,card,met)=> {
            if(!met) return;
            foreach(var id in FabledRules.Bundle("TIME_005"))
            {
                f.Mana(10); var companion=f.Draw(id); f.Process(PlayCardTask.Any(f.Player,companion));
                while(f.Player.Choice!=null) f.Process(ChooseTask.Pick(f.Player,f.Player.Choice.Choices[0]));
                foreach(var body in f.Player.BoardZone.ToArray()) f.Player.SetasideZone.Add(f.Player.BoardZone.Remove(body));
                f.Game.DeathProcessingAndAuraUpdate();
            }
        },(f,card)=>PlayCardTask.Any(f.Player,card),(f,card)=>f.Game.Player2.Hero.ToBeDestroyed || f.Game.State==State.COMPLETE);
        foreach(var id in new[]{"TIME_005t4","TIME_005t5"})
            Scenario(id,(f,c,met)=>{ if(met) f.Draw("TIME_005t1"); },(f,c)=>PlayCardTask.Any(f.Player,c),
                (f,c)=>id=="TIME_005t4"?f.Player.Hero.Armor==10:f.Player.BoardZone.Count==2);
        Scenario("TIME_005t7",(f,c,met)=> {
            if(met) { var other=f.Draw("TIME_005t4"); f.Process(PlayCardTask.Any(f.Player,other)); }
        },(f,c)=>PlayCardTask.Any(f.Player,c),(f,c)=>f.Player.UsedMana==7);
        var fixture=new Fixture();
        foreach(var id in new[]{"TIME_020","TIME_209","TIME_211","TIME_619","TIME_852","TIME_875"})
        { fixture.ClearHand(); Check(!PoweredUp.IsMet(fixture.Draw(id)),"Fabled source no conditional hand glow: "+id); }
    }
    [GroupProof("native-b16-fabled",ProofKind.Native)]
    private static void B16FabledPrivacy()
    {
        var validate=typeof(NativeUIProbe).GetMethod("ValidateNativeDeckCards",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Check(validate!=null,"native Fabled+ size preflight exists");
        var deck=FabledRules.ExpandForDeckbuilding(new[]{"TIME_005"}.Concat(Enumerable.Repeat("CS2_182",30))).ToArray();
        validate.Invoke(null,new object[]{deck});
        foreach(var invalid in new[]{deck.Concat(new[]{"CS2_182"}).ToArray(),Enumerable.Repeat("CS2_182",31).ToArray()})
        {
            bool rejected=false;
            try {validate.Invoke(null,new object[]{invalid});}
            catch(System.Reflection.TargetInvocationException e) when(e.InnerException is InvalidOperationException) {rejected=true;}
            Check(rejected,"native preflight rejects oversize ordinary/Fabled+ drafts");
        }

        foreach(bool enemy in new[]{false,true})
        {
            var f=new Fixture(); var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(f.Player));
            var p=enemy?g.Player2:g.Player1;
            var original=Generic.DrawCard(p,Cards.FromId("TIME_020"));
            FabledRules.Vanish(original); f.Flush();
            FabledRules.ReturnVanished(p,120074); f.Flush();
            p.BaseMana=10; p.UsedMana=0;
            var well=Generic.DrawCard(p,Cards.FromId("TIME_211t1t"));
            f.Process(PlayCardTask.Any(p,well));
            Check(LocationRules.Activate(p,well),"native empowered Well activation"); f.Flush();
            var hidden=p.HandZone.Select(c=>c.Id).ToArray();
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if(enemy) Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent returned/generated hand identities stay private: "+string.Join(";",actors.Where(e=>hidden.Contains(e.Entity) && !string.IsNullOrEmpty(e.Name)).Select(e=>e.Entity+":"+e.Name+":"+string.Join(",",e.Tags.Select(t=>t.Name+"="+t.Value)))));
            Check(actors.All(e=>e.Tags.All(tag=>!PayloadRules.IsInternalTag((GameTag)tag.Name) && !RepeatRules.IsInternalTag((GameTag)tag.Name))),"private tags absent from entity projection");
            Check(f.Entries.Where(e=>e.HasTagChange).All(e=>!PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag) && !RepeatRules.IsInternalTag((GameTag)e.TagChange.Tag)),"private tags absent from incremental projection");
        }
        Console.WriteLine("PASS B16 Fabled native privacy");
    }
}
