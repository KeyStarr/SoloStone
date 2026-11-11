using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
internal static class B18ONativeIllusionProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_780",(g,p,id)=>B18ONativeProof.Illusionist());
    internal static void Unpinned_Edges()
    {
        foreach(int seed in new[]{1,8,19}) foreach(bool enemy in new[]{false,true})
        {
            var g=LocationProofs.Game(seed);if(enemy) End(g);var p=g.CurrentPlayer;
            var source=Summon(p,"EDR_780"); g.SetRandomSeed(seed); var rng=g.Random.Clone();int selected=rng.Next(2);
            NativeTailRules.CopyIllusion(source);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();var copy=p.BoardZone.Single(m=>m!=source);
            var chosen=selected==0?source:copy;var other=selected==0?copy:source;
            Core2026Proof.Check(g.Random.CaptureState().Equals(rng.CaptureState()),"pair consumes one seeded draw");
            Core2026Proof.Check(PayloadRules.Entries(chosen,NativeTailRules.IllusionKey).Single()[PayloadRules.Value]==1,"seed picks exact designated member");
            var snapshot=g.SaveSnapshot();
            foreach(string treatment in new[]{"zero","shield","immune","silence","bounce","transform","copy","repeat","full","expiry"})
            {
                var next=Game.LoadSnapshot(snapshot);var c=next.ControllerById(p.Id);var body=(Minion)next.IdEntityDic[chosen.Id];
                if(treatment=="zero")body.TakeDamage(c.Opponent.Hero,0);
                if(treatment=="shield"){body.HasDivineShield=true;body.TakeDamage(c.Opponent.Hero,1);}
                if(treatment=="immune"){body.IsImmune=true;body.TakeDamage(c.Opponent.Hero,1);}
                if(treatment=="silence"){body.Silence();body.TakeDamage(c.Opponent.Hero,1);}
                if(treatment=="bounce")
                {
                    Generic.ReturnToHandBlock(c,body);PlayEntity(next,c,body);
                    Core2026Proof.Check(body.AppliedEnchantments.Count(e=>e.Card.Id=="EDR_780e1")<=1,"bounce removes old ambiguous actor");
                    // Replay made a new pair; no old payload survives backward movement.
                    Core2026Proof.Check(PayloadRules.Entries(body,NativeTailRules.IllusionKey).Length==1,"bounce discards prior designation");
                    continue;
                }
                if(treatment=="transform")
                {
                    int position=body.ZonePosition;Generic.TransformBlock(c,Cards.FromId("CS2_182"),body);
                    var fresh=c.BoardZone.Single(m=>m.ZonePosition==position);
                    Core2026Proof.Check(PayloadRules.Entries(fresh,NativeTailRules.IllusionKey).Length==0,"transform replacement has no designation");fresh.TakeDamage(c.Opponent.Hero,1);next.DeathProcessingAndAuraUpdate();
                    Core2026Proof.Check(fresh.Zone==c.BoardZone,"transformed body survives damage");continue;
                }
                if(treatment=="copy")
                {
                    var copied=(Minion)Generic.Copy(c,c.Hero,body,Zone.PLAY);copied.TakeDamage(c.Opponent.Hero,1);
                    next.DeathProcessingAndAuraUpdate();Core2026Proof.Check(copied.Zone==c.GraveyardZone && body.Zone==c.BoardZone,"ordinary copy inherits independently");continue;
                }
                if(treatment=="repeat")
                {
                    NativeTailRules.CopyIllusion(body);Core2026Proof.Check(c.BoardZone.Count==3,"repeat summons third body");
                    foreach(var m in c.BoardZone.ToArray())m.TakeDamage(c.Opponent.Hero,1);
                    next.DeathProcessingAndAuraUpdate();Core2026Proof.Check(c.BoardZone.Count==1,"designated source and inherited copy both die, earlier other survives");continue;
                }
                if(treatment=="full")
                {
                    while(!c.BoardZone.IsFull)Summon(c,"CS2_182");var before=next.Random.CaptureState();int count=PayloadRules.Entries(body,NativeTailRules.IllusionKey).Length;
                    NativeTailRules.CopyIllusion(body);Core2026Proof.Check(next.Random.CaptureState().Equals(before) && PayloadRules.Entries(body,NativeTailRules.IllusionKey).Length==count,"failed pair neither rolls nor changes old flag");continue;
                }
                if(treatment=="expiry")
                {
                    End(next);End(next);body.TakeDamage(c.Opponent.Hero,1);next.DeathProcessingAndAuraUpdate();
                    Core2026Proof.Check(body.Zone==c.GraveyardZone,"designation survives both turn boundaries");continue;
                }
                next.ProcessTasks();next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(body.Zone==c.BoardZone,"prevention/Silence keeps body alive: "+treatment);
                Core2026Proof.Check(((Minion)next.IdEntityDic[other.Id]).Zone==c.BoardZone,"partner unaffected");
            }
            var fair=FairBotState.Create(g,p.PlayerId,123,default);
            var field=typeof(FairBotState).GetField("game",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            string State(FairBotState f)=>string.Join(",",((Game)field.GetValue(f)).IdEntityDic.Values.Where(e=>e[PayloadRules.Key]==NativeTailRules.IllusionKey).Select(e=>e[PayloadRules.Value]));
            string beforeFair=State(fair);
            foreach(var e in g.IdEntityDic.Values.Where(e=>e[PayloadRules.Key]==NativeTailRules.IllusionKey))e[PayloadRules.Value]=1-e[PayloadRules.Value];
            Core2026Proof.Check(beforeFair==State(FairBotState.Create(g,p.PlayerId,123,default)),"fair search never reads either player's true designation");
        }
        var full=LocationProofs.Game();while(full.Player1.BoardZone.Count<6)Summon(full.Player1,"CS2_182");
        var last=(Minion)Play(full,full.Player1,"EDR_780");Core2026Proof.Check(PayloadRules.Entries(last,NativeTailRules.IllusionKey).Length==0,"first full-board pair creates no designation");
        last.TakeDamage(full.Player2.Hero,1);full.DeathProcessingAndAuraUpdate();Core2026Proof.Check(last.Zone==full.Player1.BoardZone,"failed first pair cannot kill original");
    }
}
