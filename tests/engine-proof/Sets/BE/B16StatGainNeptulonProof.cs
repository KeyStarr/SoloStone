using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16StatGainNeptulonProof
{
    internal static void Run() => StandardSetsProof.Case("BE_209", Unpinned_NeptulonPreEntry);
    private static void Unpinned_NeptulonPreEntry(Game g, Controller p, string id)
    {
        Unpinned_NeptulonAfterHeraldScaling();
        var old = Summon(p,"CS2_042");
        var body = (Minion)Play(g,p,id);
        Core2026Proof.Check(body.AttackDamage == 7 && old.AttackDamage == 6,"first Battlecry is not retroactive");
        body.Silence(); Kill(g,body); Kill(g,old);
        Play(g,p,"TIME_003");
        var rewind=g.Rewind;
        Core2026Proof.Check(rewind != null && g.TryAnswer(new DecisionAnswer(rewind.OfferChoiceId,rewind.ControllerId,new[]{rewind.RewindEntityId}),out _),
            "Neptulon controller effect crosses real Rewind");
        Kill(g,g.Player1.BoardZone.Single());
        foreach (var future in B16StatGainEventProof.Futures(g))
        {
            var owner=future.Player1;
            var raid=Summon(owner,"CS2_122");
            var held=(Minion)Add(owner,"CS2_042");
            Generic.AddEnchantmentBlock(future,Cards.FromId("CS2_092e"),owner.Hero,held);
            Play(future,owner,"EX1_366"); // Sword of Justice adds a later summon buff.
            int before=owner.Opponent.Hero.Damage;
            PlayEntity(future,owner,held,owner.Opponent.Hero);
            Core2026Proof.Check(held.AttackDamage == 7 && held.BaseHealth == 6 && owner.Opponent.Hero.Damage==before+3,
                "entry replaces hand buffs, retains separate Raid Leader aura and later Sword +1/+1; Battlecry runs");
            new Enchant(Effects.AttackHealth_N(2)).ActivateTo(held); future.AuraUpdate();
            Core2026Proof.Check(held.AttackDamage==9 && held.BaseHealth==8,"later grants are never reset");
            raid.Silence(); future.AuraUpdate();
            Core2026Proof.Check(held.AttackDamage==8 && held.BaseHealth==8,"aura was never folded into base stats");
            var other=Summon(owner.Opponent,"CS2_042");
            Core2026Proof.Check(other.AttackDamage==6 && other.BaseHealth==5,"opponent summon is unaffected");
            new ControlTask(EntityType.TARGET).Process(future,owner,owner.Hero,other);future.AuraUpdate();
            Core2026Proof.Check(other.AttackDamage==6,"control transfer is not a summon");
            owner.Hero.RemoveWeapon();
            var later=(Minion)Play(future,owner,id);
            Core2026Proof.Check(later.AttackDamage==5 && later.BaseHealth==5,"later Neptulon enters under previous effect");
            var summoned=Summon(owner,"CS2_042");
            Core2026Proof.Check(summoned.AttackDamage==5 && summoned.BaseHealth==5,"repeated setter effects do not multiply stats");
        }
        foreach(bool history in new[]{false,true})
        foreach(bool opponent in new[]{false,true})
        {
            var next=B14H1StatsProof.NewGame(history);if(opponent) End(next);
            var own=next.CurrentPlayer; Play(next,own,id);
            var m=Summon(own,"CS2_042");
            Core2026Proof.Check(m.AttackDamage==5 && m.BaseHealth==5,"setter survives without history on either controller");
            var temporary=(Minion)Add(own,"CS2_042");
            new Enchant(Effects.Health_N(4)){IsOneTurnEffect=true}.ActivateTo(temporary);
            PlayEntity(next,own,temporary,own.Opponent.Hero);
            End(next);
            Core2026Proof.Check(temporary.AttackDamage==5 && temporary.BaseHealth==5,"entry setter supersedes earlier temporary hand stat layers and their expiry");
            End(next);
            for(int i=own.BoardZone.Count;i<7;i++) Summon(own,"CS2_231");
            var held=(Minion)Add(own,"CS2_042");
            Core2026Proof.Check(!next.Process(PlayCardTask.Any(own,held,own.Opponent.Hero)) && held.AttackDamage==6,
                "full board rejects play without setting hidden card stats");
        }
    }

    private static void Unpinned_NeptulonAfterHeraldScaling()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool opponent in new[] { false, true })
        {
            var g = B14H1StatsProof.NewGame(history);
            if (opponent) End(g);
            var p = g.CurrentPlayer;
            p[HeraldRules.Profile] = (int)CardClass.SHAMAN;
            for (int i = 0; i < 4; i++)
            {
                int soldier = new EffectContext(g, p.Id, p.Hero.Id).Herald();
                Kill(g, (Minion)g.IdEntityDic[soldier]);
            }
            Play(g, p, "BE_209");
            foreach (var future in B16StatGainEventProof.Futures(g))
            {
                var owner = future.CurrentPlayer;
                var entries = new List<(int Attack, int Health)>();
                TriggerManager.TriggerHandler observe = entity => {
                    if (entity is Minion m && m.Card.Id == "CATA_565t" && m.Zone == owner.BoardZone)
                        entries.Add((m.AttackDamage, m.BaseHealth));
                };
                future.TriggerManager.ZoneTrigger += observe;
                Play(future, owner, "BRM_017");
                var raised = owner.BoardZone.Single(m => m.Card.Id == "CATA_565t");
                Core2026Proof.Check(raised.AttackDamage == 5 && raised.BaseHealth == 5,
                    $"Neptulon supersedes four-Herald scaling on resurrection: got {raised.AttackDamage}/{raised.BaseHealth}");
                var played = (Minion)Play(future, owner, "CATA_565t");
                Core2026Proof.Check(played.AttackDamage == 5 && played.BaseHealth == 5,
                    $"Neptulon supersedes four-Herald scaling on play: got {played.AttackDamage}/{played.BaseHealth}");
                future.TriggerManager.ZoneTrigger -= observe;
                Core2026Proof.Check(entries.SequenceEqual(new[] { (5, 5), (5, 5) }),
                    "both entry paths expose 5/5 to board zone reactions");
            }
        }
    }
}
