using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B18AyaProof
{
    private static void PlayCoin(Game game, Controller player, string id)
    {
        var coin=Add(player,id);player.BaseMana=9;player.UsedMana=0;player.TemporaryMana=0;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(player,coin)),"counterfeit play accepted");
    }
    internal static void Run()
    {
        foreach(string option in new[]{"JAIL_504t","JAIL_504t2","JAIL_504t3"})
        foreach(bool full in new[]{false,true}) {
            var g=ImbueProof.Make(CardClass.ROGUE);var p=g.Player1;
            var coin=Add(p,"GAME_005");var enemyCoin=Add(p.Opponent,"GAME_005");
            var aya=(Minion)Play(g,p,"JAIL_504");
            Core2026Proof.Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id)
                .SequenceEqual(new[]{"JAIL_504t","JAIL_504t2","JAIL_504t3"}),"three fixed counterfeit choices");
            Kill(g,aya);
            if(full) while(p.HandZone.Count<9)Add(p,"CS2_231");
            var clone=g.CloneExact(history:true);var loaded=Game.LoadSnapshot(g.SaveSnapshot());
            foreach(var next in new[]{g,clone,loaded}) {
                var owner=next.Player1;
                B18GeneratedProof.Answer(next,owner.Choice.Choices.Single(id=>next.IdEntityDic[id].Card.Id==option));
                Core2026Proof.Check(owner.HandZone.Count(c=>c.Card.Id==option)==(full?2:4),"held Coin replaces in place; Get 3 respects hand cap");
                Core2026Proof.Check(next.IdEntityDic[coin.Id].Card.Id==option && next.IdEntityDic[enemyCoin.Id].Card.Id=="GAME_005","Coin identity retained; opponent unchanged");
                Core2026Proof.Check(owner[PayloadRules.CoinReplacement]==Cards.FromId(option).AssetId,"chosen policy persists after Aya leaves");
            }
            Core2026Proof.Check(Canonical.State(g)==Canonical.State(clone)&&Canonical.State(g)==Canonical.State(loaded),"paused Aya clone/load future identical");
        }
        var game=ImbueProof.Make(CardClass.ROGUE);var player=game.Player1;
        PlayCoin(game,player,"JAIL_504t");PlayCoin(game,player,"JAIL_504t");
        Core2026Proof.Check(player.BoardZone.Select(m=>m.AttackDamage).SequenceEqual(new[]{1,2}),"Jade coins reuse increasing Jade baseline");
        while(!player.BoardZone.IsFull)Summon(player,"CS2_231");
        PlayCoin(game,player,"JAIL_504t");Core2026Proof.Check(player.BoardZone.Count==7 && player.TemporaryMana==1,"full board still grants coin mana");
        var enemy=Summon(player.Opponent,"CS2_182");
        PlayCoin(game,player,"JAIL_504t2");Core2026Proof.Check(enemy.Damage==2,"Grimy Coin hits enemy minion");
        Kill(game,enemy);PlayCoin(game,player,"JAIL_504t2");
        Core2026Proof.Check(player.Opponent.Hero.Damage==0 && player.TemporaryMana==1,"Grimy empty board grants mana without hero damage");
    }
}
