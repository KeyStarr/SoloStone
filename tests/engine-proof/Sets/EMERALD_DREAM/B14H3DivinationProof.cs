using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B14H3LifecycleProof;
using static StandardSetsProof;

internal static class B14H3DivinationProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_804",Proof);
    private static void Proof(Game g,Controller p,string id)
    {
        var source = Add(p,id); var friendly = Summon(p,"CS2_231"); var enemy = Summon(p.Opponent,"CS2_231");
        var other = Summon(p,"CS2_101t"); LocationProofs.Location(p);
        B14H3LifecycleProof.Check(source.ValidPlayTargets.Select(x => x.Id).SequenceEqual(new[] {friendly.Id})
            && source.IsValidPlayTarget(friendly) && !source.IsValidPlayTarget(enemy) && !source.IsValidPlayTarget(other)
            && !source.IsValidPlayTarget(null),"Wisp-only friendly target agrees across APIs");
        int deck = p.DeckZone.Count;
        PoolPilotProof.Resume(g,next => {
            PlayEntity(next,next.Player1,next.IdEntityDic[source.Id],(Minion)next.IdEntityDic[friendly.Id]);
            B14H3LifecycleProof.Check(next.IdEntityDic[friendly.Id].Zone == next.Player1.GraveyardZone && next.Player1.DeckZone.Count == deck-3,
                "destroy friendly Wisp then draw three");
        });
        var absent = Add(p,id); B14H3LifecycleProof.Check(!absent.HasAnyValidPlayTargets && !p.Options().Any(t => t.Source?.Id == absent.Id),"no Wisp blocks spell play");
        // Every Wisp definition supported by the engine in the 37.0 export, including generated tokens.
        foreach (string wispId in new[] { "EDR_851t","CORE_CS2_231","CS2_231","OG_195c","GIL_553t","GILA_BOSS_55t" })
        {
            var wisp = Summon(p,wispId);
            B14H3LifecycleProof.Check(absent.ValidPlayTargets.Select(x => x.Id).SequenceEqual(new[] {wisp.Id}) && absent.IsValidPlayTarget(wisp),
                "Divination accepts exported Wisp identity "+wispId);
            PoolPilotProof.Resume(g,next => {
                var owner = next.Player1; int before = owner.DeckZone.Count;
                PlayEntity(next,owner,next.IdEntityDic[absent.Id],(Minion)next.IdEntityDic[wisp.Id]);
                B14H3LifecycleProof.Check(next.IdEntityDic[wisp.Id].Zone == owner.GraveyardZone && owner.DeckZone.Count == before-3,
                    "exported Wisp destruction and three draws: "+wispId);
            });
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            absent = Add(p,id);
        }
        var transformed = Summon(p,"CS2_168");
        Generic.ChangeEntityBlock(p,transformed,Cards.FromId("EDR_851t"),true); transformed.Silence();
        B14H3LifecycleProof.Check(absent.IsValidPlayTarget(transformed),"transformed and silenced Mage Wisp retains identity");
        ParkBoard(p);
        End(g); var enemySpell = Add(p.Opponent,id);
        B14H3LifecycleProof.Check(enemySpell.ValidPlayTargets.Single().Id == enemy.Id,"opponent-side Wisp targeting");
    }
}
