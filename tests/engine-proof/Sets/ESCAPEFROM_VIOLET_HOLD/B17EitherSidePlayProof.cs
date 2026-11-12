using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B17EitherSidePlayESCAPEFROM_VIOLET_HOLDProof
{
    [GroupProof("b17-either-side-play-escapefrom-violet-hold-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CAP_004", Operator);
        StandardSetsProof.Case("JAIL_442", Doctor);
        StandardSetsProof.Case("JAIL_452", Detective);
        StandardSetsProof.Case("JAIL_455", Watchman);
        StandardSetsProof.Case("JAIL_461", Executioner);
    }
    private static Minion EnemyPlay(Game g, Controller p, string id)
    {
        p.UsedMana=0;
        var card=(Minion)Add(p,id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card,zonePosition:p.Opponent.BoardZone.SlotCount,chooseOne:2)), "enemy play accepted");
        return card;
    }
    private static void Operator(Game g, Controller p, string id)
    {
        var card=EnemyPlay(g,p,id);
        Core2026Proof.Check(card.IsRush && card.Controller==p.Opponent, "Operator Rush and destination control");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            int own=next.Player1.HandZone.Count, enemy=next.Player2.HandZone.Count;
            Kill(next,(Minion)next.IdEntityDic[card.Id]);
            Core2026Proof.Check(next.Player1.HandZone.Count==own+2 && next.Player2.HandZone.Count==enemy,
                "Operator draws for opponent of death controller after clone/snapshot");
        }
        var silenced=EnemyPlay(g,p,id); silenced.Silence(); int before=p.HandZone.Count; Kill(g,silenced);
        Core2026Proof.Check(p.HandZone.Count==before,"Silence removes Operator Deathrattle");
        var burn=EnemyPlay(g,p,id);
        while(!p.HandZone.IsFull) Add(p,"CS2_182");
        int deck=p.DeckZone.Count; Kill(g,burn);
        Core2026Proof.Check(p.HandZone.Count==10 && p.DeckZone.Count==deck-2,"Operator burns both draws at full hand");
    }
    private static void Doctor(Game g, Controller p, string id)
    {
        while(p.Opponent.DeckZone.Count<97) Entity.FromCard(p.Opponent,Cards.FromId("CS2_182"),zone:p.Opponent.DeckZone);
        var card=EnemyPlay(g,p,id); int ownDeck=p.DeckZone.Count;
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            Kill(next,(Minion)next.IdEntityDic[card.Id]);
            var deck=next.Player2.DeckZone;
            Core2026Proof.Check(deck.Count==99 && deck.Count(c=>c.Card.Id=="JAIL_443t")==2 && next.Player1.DeckZone.Count==ownDeck,
                "Doctor inserts only free slots into death controller deck, capped at 99");
            var blight=deck.First(c=>c.Card.Id=="JAIL_443t");
            int damage=next.Player2.Hero.Damage;
            Generic.Draw(next.Player2,blight);
            Core2026Proof.Check(next.Player2.Hero.Damage>=damage+2 && blight.Zone.Type==Zone.GRAVEYARD,
                "Blight casts when drawn through existing recipe");
        }
    }
    private static void Detective(Game g, Controller p, string id)
    {
        Summon(p,"LOE_077");
        var card=EnemyPlay(g,p,id);
        Core2026Proof.Check(p.OverloadOwed==0 && p.Opponent.OverloadOwed==2 && p.Opponent.OverloadThisGame==2,
            "Detective overloads destination once even with caster Brann");
        End(g);
        Core2026Proof.Check(p.Opponent.OverloadLocked==2 && p.Opponent.OverloadOwed==0,"destination overload locks next turn");
    }
    private static void Watchman(Game g, Controller p, string id)
    {
        var spider=Summon(p.Opponent,"FP1_002"); spider.Damage=1;
        var friendly=Summon(p,"CS2_182");
        var card=EnemyPlay(g,p,id);
        Core2026Proof.Check(p.Opponent.BoardZone.Single()==card && friendly.Damage==0 && card.Damage==0,
            "two destination waves settle Deathrattles between hits and exclude self");
    }
    private static void Executioner(Game g, Controller p, string id)
    {
        var left=Summon(p.Opponent,"CS2_182");
        var card=EnemyPlay(g,p,id);
        Core2026Proof.Check(left.Zone.Type==Zone.GRAVEYARD && card.ZonePosition==0,"Executioner destroys sole adjacent enemy-board minion");
        Core2026Proof.Check(Generic.ReturnToHandBlock(p.Opponent,card),"bounce accepted");
        End(g); p.Opponent.BaseMana=10;
        var options=p.Opponent.Options().OfType<PlayCardTask>().Where(t=>t.Source.Id==card.Id).ToArray();
        Core2026Proof.Check(options.Any(t=>t.ChooseOne==1) && options.Any(t=>t.ChooseOne==2),"bounce retains side identities for new controller");
    }
}
