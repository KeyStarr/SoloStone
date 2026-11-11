using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
internal static class EvColossalsSindragosaProof
{
    internal static void Unpinned_Freeze(Game g,Controller p,Minion body)
    {
        var enemy=Summon(p.Opponent,"CS2_200");var friendly=Summon(p,"CS2_182");
        friendly.IsFrozen=true;g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friendly.Zone==p.BoardZone,"friendly Freeze excluded");
        var wing=ColossalRules.AppendagesOf(body).First();
        Resume(g,next=>Attack(next,(Minion)next.IdEntityDic[wing.Id],(ICharacter)next.IdEntityDic[enemy.Id]));
        Core2026Proof.Check(enemy.Zone==p.Opponent.GraveyardZone,"damaging Wing freezes and body destroys durable enemy");
        body.Silence();g.DeathProcessingAndAuraUpdate();enemy=Summon(p.Opponent,"CS2_200");enemy.IsFrozen=true;g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Zone==p.Opponent.BoardZone,"silenced body no Freeze destruction");
        enemy.IsFrozen=false;wing=ColossalRules.AppendagesOf(body).Single();wing.Silence();g.DeathProcessingAndAuraUpdate();
        Attack(g,wing,enemy);Core2026Proof.Check(!enemy.IsFrozen,"silenced Wing cannot Freeze damaged character");
        p.Opponent.Hero.IsFrozen=true;g.ProcessTasks();g.DeathProcessingAndAuraUpdate();Core2026Proof.Check(!p.Opponent.Hero.ToBeDestroyed,"enemy heroes never destroyed by Freeze");
    }
}
