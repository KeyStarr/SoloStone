using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_SinestraProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_154", (g,p,id) => {

        p.Opponent.Hero[GameTag.HEALTH]=1000;
        var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count(x=>x.Card.Id=="CATA_154t" || x.Card.Id=="CATA_154t1")==2 && p.HandZone.Count==2,
            "Sinestra complete Colossal wing generation");
        Core2026Proof.Check(p.HandZone.All(x=>x.Card.Type==CardType.SPELL && !CardClassRules.IsAllowedInDeck(x.Card,p.HeroClass)),"wings generate other-class spells");
        Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==12,"Unpinned_SinestraClass: other-class spell repeats");
        var originalHero=p.Hero.Card; p.Hero.Card=Cards.FromId("HERO_08");
        Play(g,p,"CS2_073",source);
        Core2026Proof.Check(source.AttackDamage==13,"Unpinned_SinestraClass: Rogue spell qualifies after hero becomes Mage");
        p.Hero.Card=originalHero;
        source.Silence(); Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==18,"Sinestra Silence removes aura");
        Kill(g,source); source=(Minion)Play(g,p,id); source.Damage=4;
        Play(g,p,"CS2_062");
        Core2026Proof.Check(source.Zone==p.GraveyardZone && p.Opponent.Hero.Damage==24,
            "Unpinned_QualificationSnapshot: second Hellfire survives removal of granting Sinestra in first body");
    });
}
