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

internal static class B14H3TimeProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("TIME_049",Variant);
        StandardSetsProof.Case("TIME_055",Voyager);
        StandardSetsProof.Case("TIME_103",Unpinned_ChromieDisplayMultiplicityOrder);
        StandardSetsProof.Case("END_006",Unpinned_ChronikarOwnTurns);
    }
    private static void Variant(Game g,Controller p,string id)
    {
        var m = (Minion)Play(g,p,id); var enemy = Summon(p.Opponent,id);
        End(g); B14H3LifecycleProof.Check(m.Card.Id == id && enemy.Card.Cost == 5,"only new turn owner's Variant transforms");
        End(g); B14H3LifecycleProof.Check(m.Card.Cost == 5 && m.IsExhausted == !(m.HasCharge || m.IsRush),"Variant gains normal transform exhaustion after own-start refresh");
        var silenced = Summon(p,id); silenced.Silence(); End(g); End(g);
        B14H3LifecycleProof.Check(silenced.Card.Id == id,"silenced Variant never transforms");
        var bounced = Summon(p,id); Generic.ReturnToHandBlock(p,bounced); End(g); End(g);
        B14H3LifecycleProof.Check(bounced.Card.Id == id,"offboard Variant never transforms");
    }
    private static void Voyager(Game g,Controller p,string id)
    {
        var regression = HistoryFactsProof.Fixture(); var owner = regression.Player1;
        var voyager = (Minion)Play(regression,owner,id); Play(regression,owner,"CAP_804",voyager);
        B14K3BEProof.Pool(regression,new[]{"DINO_409"},()=>PoolPilotProof.Resume(regression,next => {
            var body = (Minion)next.IdEntityDic[voyager.Id];
            body.TakeDamage(next.Player2.Hero,1); next.DeathProcessingAndAuraUpdate();
            B14H3LifecycleProof.Check(body.Card.Id == "DINO_409" && body[GameTag.REBORN] == 0,
                "Voyager into DINO_409 drops Specter Specialist Reborn");
        }));
        var m = (Minion)Play(g,p,id); m.HasCharge = true;
        PoolPilotProof.Resume(g,next => {
            var body = (Minion)next.IdEntityDic[m.Id]; body.TakeDamage(next.Player2.Hero,1); next.DeathProcessingAndAuraUpdate();
            B14H3LifecycleProof.Check(body.Card.Cost == 7 && body.Health == body.BaseHealth && body.HasCharge == body.Card.Charge && body.IsExhausted == !(body.HasCharge || body.IsRush),
                "fresh Voyager survives, clears Charge/damage, keeps sickness");
        });
        ParkBoard(p); m = Summon(p,id); End(g); End(g);
        m.TakeDamage(p.Hero,1); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(m.Card.Cost == 7 && m.IsExhausted == !(m.HasCharge || m.IsRush),"aged Voyager gains transform exhaustion");
        ParkBoard(p); m = Summon(p,id); m.TakeDamage(p.Hero,5); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(m.Card.Id == id && m.Zone == p.GraveyardZone,"lethal Voyager does not transform");
        m = Summon(p,id); m.Silence(); m.TakeDamage(p.Hero,1); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(m.Card.Id == id,"silenced Voyager no trigger");
        var enemy = Summon(p.Opponent,id); enemy.TakeDamage(p.Hero,1); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(enemy.Card.Cost == 7 && enemy.Controller == p.Opponent,"opponent Voyager transforms on damage");
    }
    private static void Unpinned_ChromieDisplayMultiplicityOrder(Game g,Controller p,string id)
    {
        PoolPilotProof.ClassDeck(p);
        Play(g,p,"CS2_231"); Play(g,p,"GAME_005"); Play(g,p,"CS2_231");
        foreach (string card in new[] { "CS2_231","CS2_231","CS2_231","CS2_231","GAME_005",id })
            p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(card)));
        var wisps = p.DeckZone.Where(x => x.Card.Id == "CS2_231").ToArray();
        for (int i = 0; i < wisps.Length; i++)
            Generic.AddEnchantmentBlock(g,Cards.FromId("DINO_430e"),p.Hero,wisps[i],i+1,i+1);
        var coin = p.DeckZone.Single(x => x.Card.Id == "GAME_005");
        var deckChromie = p.DeckZone.Single(x => x.Card.Id == id);
        var chromie = (Minion)Play(g,p,id); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(chromie[GameTag.TAG_SCRIPT_DATA_NUM_1] == 6,"deck counter counts physical matches once, including played Chromie");
        var draw = p.DeckZone.First(x => x.Card.Id == "CS2_231"); Generic.Draw(p,draw); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(chromie[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5,"board counter refreshes after deck removal");
        var copy = Generic.Copy(p,p.Hero,chromie,Zone.HAND); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5,"hand copy recomputes display");
        int before = p.HandZone.Count;
        // Seed 2 selects indices 1/0/1/0 at bounds 3/1/2/1: third then fourth original Wisp.
        g.SetRandomSeed(2);
        PoolPilotProof.Resume(g,next => {
            Kill(next,(Minion)next.IdEntityDic[chromie.Id]);
            B14H3LifecycleProof.Check(next.Player1.HandZone.Skip(before).Select(x => x.Card.Id).SequenceEqual(new[] {"CS2_231","GAME_005","CS2_231",id}),
                "chronological per-play selected draws; no duplicate multiplication");
            B14H3LifecycleProof.Check(next.Player1.HandZone.Skip(before).Select(x => x.Id)
                .SequenceEqual(new[] {wisps[2].Id,coin.Id,wisps[3].Id,deckChromie.Id}),
                "seeded matching-entity selection must not always draw first copy");
            B14H3LifecycleProof.Check(((Minion)next.IdEntityDic[wisps[2].Id]).AttackDamage == 4
                && ((Minion)next.IdEntityDic[wisps[3].Id]).AttackDamage == 5
                && next.Player1.DeckZone.Single().Id == wisps[1].Id,
                "exact selected Wisp buff layers survive drawing; second original Wisp remains");
            B14H3LifecycleProof.Check(next.Player1.DeckZone.Count == 1,"one Wisp remains after two qualifying plays");
        });
        var blank = Summon(p,id); blank.Silence(); int deck = p.DeckZone.Count; Kill(g,blank);
        B14H3LifecycleProof.Check(p.DeckZone.Count == deck,"Silence removes Chromie Deathrattle");
        PoolPilotProof.FillHand(p); int damage = p.Hero.Damage;
        var overflow = Summon(p,id); Kill(g,overflow);
        B14H3LifecycleProof.Check(p.HandZone.Count == 10 && p.DeckZone.IsEmpty && p.Hero.Damage == damage,
            "full-hand match burns; missing repeated matches skip without fatigue");
    }
    private static void Unpinned_ChronikarOwnTurns(Game g,Controller p,string id)
    {
        var source = (Minion)Play(g,p,id); Kill(g,source);
        B14H3LifecycleProof.Check(p.Hero.AttackDamage == 3,"immediate hero Attack survives source death");
        foreach (var next in new[] { g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot()),g })
        {
            End(next); B14H3LifecycleProof.Check(next.Player1.Hero.AttackDamage == 0,"opponent turn receives no hero Attack");
            End(next); B14H3LifecycleProof.Check(next.Player1.Hero.AttackDamage == 3,"next own turn grants three");
            TurnQueueRules.Prepend(next,next.Player1);
            End(next); B14H3LifecycleProof.Check(next.CurrentPlayer == next.Player1 && next.Player1.Hero.AttackDamage == 3,"extra own turn consumes final grant");
            End(next); End(next); B14H3LifecycleProof.Check(next.Player1.Hero.AttackDamage == 0,"exact expiry after three own turns");
        }
        var replacement = HistoryFactsProof.Fixture();
        Play(replacement,replacement.Player1,id); Play(replacement,replacement.Player1,"CATA_190h"); Answer(replacement,"CATA_190t11");
        End(replacement); End(replacement);
        B14H3LifecycleProof.Check(replacement.Player1.Hero.AttackDamage == 3,"pending timer grants to replacement hero");
        End(g); var other = (Minion)Play(g,p.Opponent,id);
        B14H3LifecycleProof.Check(p.Opponent.Hero.AttackDamage == 3 && p.Hero.AttackDamage == 0,"opponent timer belongs to opponent");
        other.Silence(); End(g); End(g);
        B14H3LifecycleProof.Check(p.Opponent.Hero.AttackDamage == 3,"Silence cannot cancel committed Battlecry timer");
    }
}
