using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B15GameSetupCases
{
    internal static void Card(Game g, Controller p, string id)
    {
        if (id == "CORE_REV_946") {
            var original = p.DeckZone.First();
            var added = Entity.FromCard(p, original.Card, zone:p.DeckZone);
            var other = Entity.FromCard(p.Opponent, original.Card, zone:p.Opponent.DeckZone);
            Play(g,p,id);
            Core2026Proof.Check(original.Zone == p.DeckZone && added.Zone.Type == Zone.GRAVEYARD && other.Zone.Type == Zone.GRAVEYARD,
                "Steamcleaner physical provenance on both sides");
        } else if (id == "EDR_000") {
            p[GameTag.MAXRESOURCES] = 15; p.BaseMana = 10;
            Play(g,p,id);
            Core2026Proof.Check(p.BaseMana == 13, "Ysera three full crystals use increased cap");
        } else if (id == "EDR_260") {
            var body = (Minion)Play(g,p,id); int before=p.DeckZone.Count; Kill(g,body);
            Core2026Proof.Check(p.DeckZone.Count == before+2 && p.DeckZone.Count(e=>e.Card.Id=="EDR_260t")==2,"Greenwing two Illusions");
            var illusion=p.DeckZone.First(e=>e.Card.Id=="EDR_260t"); Generic.Draw(p,illusion); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(illusion.Zone == p.BoardZone && p.HandZone.Count==1,"Illusion summons and replaces draw");
        } else if (id == "CORE_ETC_111") {
            Play(g,p,id); int before=p.Opponent.DeckZone.Count; End(g);
            Core2026Proof.Check(p.Opponent.DeckZone.Count==before,"Merch insertion followed by opponent turn draw");
            Core2026Proof.Check(p.Opponent.HandZone.Any(e=>e.Card.Type==CardType.SPELL && e.Card.Id!="GAME_005"),"Merch puts spell on top");
        } else if (id == "JAIL_443") {
            var plague=(Minion)Play(g,p,id); p.Opponent.Hero.Armor=20; int before=p.Opponent.DeckZone.Count;
            Generic.DamageCharFunc(plague,p.Opponent.Hero,8,false);
            Core2026Proof.Check(p.Opponent.Hero.Armor==20 && p.Opponent.Hero.Damage==0 && p.Opponent.DeckZone.Count==before+8,
                "Unpinned_PlaguePreventionCount replaces damage before Armor");
        } else if (id == "TIME_730") {
            Play(g,p,id);
            while(p.Choice!=null) { var d=g.GetPendingDecisions().Single(); Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,new[]{d.Options[0].EntityId}),out _),"answer setup choice"); }
        } else { Play(g,p,id); }
    }
}
