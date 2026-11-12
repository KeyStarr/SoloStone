using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17LifecycleSteedProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CAP_800", Unpinned_SteedFullEnchantedRebirth);
    }
    internal static void Unpinned_SteedFullEnchantedRebirth(Game g, Controller p, string id)
    {
        var steed = (Minion)Play(g,p,id);
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_009e"),steed,steed);
        int atk=steed.AttackDamage, health=steed.BaseHealth;
        PoolPilotProof.Resume(g.CloneExact(history: true),next => {
            var owner=next.Player1; var old=(Minion)next.IdEntityDic[steed.Id]; Kill(next,old);
            var reborn=owner.BoardZone.Single();
            Core2026Proof.Check(reborn.Id!=old.Id && reborn.Health==health && reborn.AttackDamage==atk && reborn[GameTag.REBORN]==0,
                "Steed full enchanted rebirth, Reborn consumed");
            reborn.Silence(); Core2026Proof.Check(reborn.Health==3 && reborn.AttackDamage==2,"reborn buffs remain removable");
        });
        steed.Silence(); Kill(g,steed);
        Core2026Proof.Check(p.BoardZone.Count==0,"silence disables intrinsic rebirth");
    }
}
