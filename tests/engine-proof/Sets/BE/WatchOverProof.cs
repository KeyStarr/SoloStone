using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
internal static class WatchOverProof
{
    internal static void Run() => StandardSetsProof.Case("BE_778",Unpinned_SequentialDistinctAbilities);
    private static void Unpinned_SequentialDistinctAbilities(Game g,SabberStoneCore.Model.Entities.Controller p,string id)
    {
        var enemy = Summon(p.Opponent,"CS2_200"); enemy[GameTag.HEALTH] = 20;
        var source = Play(g,p,id);
        CthunRulesProof.Pick(g,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.Choice.Choices.Count == 3
            && p.Choice.Choices.All(e => g.IdEntityDic[e].Card.Id != "BE_013t4") && source[GameTag.ZONE] == (int)Zone.PLAY,"first effect finishes before second prompt, first ability excluded, spell stays in play");
        CthunRulesProof.Pick(g,"BE_013t3"); CthunRulesProof.PickTarget(g,enemy.Id);
        Core2026Proof.Check(enemy.Damage == 5 && p.Choice == null && source.Zone == p.GraveyardZone,"targeted second effect finishes before spell retirement");
        source = Play(g,p,id); CthunRulesProof.Pick(g,"BE_013t4"); CthunRulesProof.Pick(g,"BE_013t2");
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.Count(m => m.AttackDamage == 4 && m.Health == 6) == 2,"prior game choices remain eligible and Domain buffs two distinct Fanatics");
        var indirect = Add(p,id); p.SetasideZone.Add(p.HandZone.Remove(indirect));
        PoolPilotProof.Resolve(g,indirect);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect,"indirect Watch Over finishes both choices inline");
    }
}
