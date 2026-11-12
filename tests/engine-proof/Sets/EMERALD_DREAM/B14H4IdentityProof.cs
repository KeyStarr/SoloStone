using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static partial class B14H4IdentityProof
{
    private static void EmeraldDream()
    {
        StandardSetsProof.Case("EDR_469", SlumberingSprite);
        StandardSetsProof.Case("EDR_270", HornOfPlenty);
        StandardSetsProof.Case("EDR_233", SpiritsOfTheForest);
        StandardSetsProof.Case("EDR_262", SpiritBond);
        StandardSetsProof.Case("EDR_263", GraceOfTheGreatwolf);
    }
    private static void SlumberingSprite(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id);
        Core2026Proof.Check(body.IsDormant && body.AppliedEnchantments.Any(e=>e.Card.Id=="EDR_469e"),"starts asleep with exported helper");
        Future(g,next=>{
            var owner=next.Player1; owner.UsedMana=0;
            Core2026Proof.Check(next.Process(HeroPowerTask.Any(owner)),"friendly power accepted");
            var awake=(Minion)next.IdEntityDic[body.Id];
            Core2026Proof.Check(!awake.IsDormant && awake.AttackDamage==3 && awake.Health==3 && awake.IsExhausted,"own power awakens printed body, no Charge");
            awake.Silence(); Core2026Proof.Check(awake.AttackDamage==3 && awake.Health==3 && !awake.IsDormant,"Silence preserves awake state");
        });
        var sleeper=Summon(p,id); End(g); p.Opponent.UsedMana=0;
        Core2026Proof.Check(g.Process(HeroPowerTask.Any(p.Opponent,p.Hero)),"opposing Mage power accepted");
        Core2026Proof.Check(sleeper.IsDormant,"opposing power ignored"); End(g);
        Generic.ReturnToHandBlock(p,sleeper); Core2026Proof.Check(sleeper.Zone==p.HandZone,"bounce sleeper");
        PlayEntity(g,p,sleeper); Core2026Proof.Check(sleeper.IsDormant,"replayed starts asleep");
    }
    private static void HornOfPlenty(Game g, Controller p, string id)
    {
        Pools(g,id); p.AddHeroAndPower(Cards.FromId("HERO_06"));
        var filter=OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id).Pools.Single().Filter;
        var expected=OfflineCardPools.SampleDistinct(g.CloneExact(),filter,p.HeroClass,Cards.FromId(id).AssetId).Select(c=>c.Id).ToArray();
        Play(g,p,id); Core2026Proof.Check(p.Choice.Choices.Select(i=>g.IdEntityDic[i].Card.Id).SequenceEqual(expected),"seeded Nature Discover offers");
        int selected=p.Choice.Choices.Last();
        Future(g,next=>{
            PlayerDecisionProof.Answer(next,selected); var offer=next.IdEntityDic[selected];
            var chosen=next.Player1.HandZone.Single(card=>card.Card.Id==offer.Card.Id);
            // Shatter retires the offer and copies its discount to both halves before recombining.
            int reductions=offer.Card.Shatter==null ? 1 : 2;
            int cost=Math.Max(0,chosen.Card.Cost-2*reductions);
            Core2026Proof.Check(chosen.Card[GameTag.SPELL_SCHOOL]==4 && chosen.Cost==cost
                && (reductions==1 ? chosen.Id==selected : chosen.Id!=selected && chosen[ShatterRules.AlreadyCombined]==1),
                $"selected Nature spell {chosen.Card.Id} retains discount through hand entry: cost={chosen.Cost}, expected={cost}");
            End(next); End(next); Core2026Proof.Check(chosen.Cost==cost,"discount persists across turns");
        });
        EmptyHand(p); B14K3BEProof.Pool(g,Array.Empty<string>(),()=>{ Play(g,p,id);Core2026Proof.Check(p.Choice==null,"empty Nature bucket yields nothing"); });
    }
    private static void Choose(Game g,Controller p,string id,int mode)
    {
        p.UsedMana=0; var source=Add(p,id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,source,chooseOne:mode)),"Choose One branch accepted");
    }
    private static void SpiritsOfTheForest(Game g, Controller p, string id)
    {
        Choose(g,p,id,1); Core2026Proof.Check(p.BoardZone.Count==3 && p.BoardZone.All(m=>m.Card.Id=="EX1_tk11" && m.AttackDamage==2 && m.Health==3 && m.HasTaunt),"three exact Spirit Wolves");
        var wolf=p.BoardZone.First();
        Future(g,next=>{
            var sheep=Generic.ChangeEntityBlock(next.Player1,next.IdEntityDic[wolf.Id],Cards.FromId("CS2_tk1"),true);
            Core2026Proof.Check(sheep[GameTag.CARDRACE]==20 && sheep[(GameTag)2542]==0,"transformation clears captured Wolf races");
            Future(next,restored=>{
                var body=(Minion)restored.IdEntityDic[wolf.Id]; body.Silence();
                Generic.ReturnToHandBlock(body.Controller,body);
                Core2026Proof.Check(body.Card.Id=="CS2_tk1" && body[GameTag.CARDRACE]==20 && body[(GameTag)2542]==0,
                    "Sheep races survive clone/snapshot, Silence and bounce");
            });
        });
        ParkBoard(p); Choose(g,p,id,2);
        Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.All(m=>m.Card.Id=="EDR_233t2" && m.AttackDamage==4 && m.Health==3 && m.HasWindfury),"two exact Spirit Falcons");
        var falcon=p.BoardZone.First(); falcon.Silence();Core2026Proof.Check(!falcon.HasWindfury,"token Silence removes Windfury");
        ParkBoard(p); p.ChooseBoth=true; Choose(g,p,id,0);
        Core2026Proof.Check(p.BoardZone.Count==5 && p.BoardZone.Count(m=>m.Card.Id=="EX1_tk11")==3,"Choose Both resolves wolves then falcons");
        p.ChooseBoth=false;ParkBoard(p);for(int i=0;i<6;i++)Summon(p,"CS2_182");Choose(g,p,id,1);
        Core2026Proof.Check(p.BoardZone.Last().Card.Id=="EX1_tk11" && p.BoardZone.Count==7,"one free slot preserves exact Wolf identity");
    }
    private static void SpiritBond(Game g, Controller p, string id)
    {
        var victim=Summon(p.Opponent,"CS2_182"); Play(g,p,id,victim);
        Core2026Proof.Check(victim.Damage==3 && p.BoardZone.Count==0,"surviving target gives no Pup");
        victim=Summon(p.Opponent,"CS2_231"); Future(g,next=>{
            Play(next,next.Player1,id,(Minion)next.IdEntityDic[victim.Id]);
            var pup=next.Player1.BoardZone.Single();
            Core2026Proof.Check(pup.Card.Id=="EDR_850pe" && pup.AttackDamage==3 && pup.Health==2 && pup.IsRush && pup.ValidAttackTargets.All(t=>t is Minion),"lethal summons exact Rush Pup; fresh Rush excludes hero");
        });
        var shield=Summon(p.Opponent,"CS2_231");shield.HasDivineShield=true;int count=p.BoardZone.Count;
        Play(g,p,id,shield); Core2026Proof.Check(!shield.HasDivineShield && p.BoardZone.Count==count,"shield prevents lethal bonus");
        var friend=Summon(p,"CS2_231");Play(g,p,id,friend);Core2026Proof.Check(friend.Zone==p.GraveyardZone && p.BoardZone.Count==count+1,"friendly minion legal and lethal bonus");
    }
    private static void GraceOfTheGreatwolf(Game g, Controller p, string id)
    {
        Summon(p,"CS2_142");Choose(g,p,id,1);Core2026Proof.Check(p.Opponent.Hero.Damage==5,"damage option scales with Spell Damage");
        ParkBoard(p);Choose(g,p,id,2);Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.All(m=>m.Card.Id=="EDR_850pe" && m.IsRush && m.AttackDamage==3 && m.Health==2),"two exact Rush Pups");
        ParkBoard(p);p.ChooseBoth=true;Choose(g,p,id,0);Core2026Proof.Check(p.Opponent.Hero.Damage==9 && p.BoardZone.Count==2,"Choose Both damage and summons");
        p.ChooseBoth=false;
    }
}
