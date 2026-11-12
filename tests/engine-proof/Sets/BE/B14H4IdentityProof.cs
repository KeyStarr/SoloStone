using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
internal static partial class B14H4IdentityProof
{
    private static void Beyond()
    { StandardSetsProof.Case("BE_780",Pinned_UldirQuarantine);StandardSetsProof.Case("BE_781",Unpinned_FetidDevourer); }
    private static void Pinned_UldirQuarantine(Game g,Controller p,string id)
    {
        Pools(g,id);Play(g,p,"RLK_060");ParkMinions(p);Play(g,p,"CORE_RLK_506");Play(g,p,"RLK_511");ParkBoard(p);EmptyHand(p);
        var location=(BoardObject)Play(g,p,id);
        Core2026Proof.Check(location.Location.ConditionLocked && !LocationRules.CanActivate(p,location,null),"Pinned_UldirQuarantineNoPriorPlayCredit: prior runes and self-play ignored");
        Play(g,p,"RLK_060");ParkMinions(p);Play(g,p,"CORE_RLK_506");
        Core2026Proof.Check(location.Location.ConditionLocked,"two runes stay locked");Play(g,p,"RLK_511");
        Core2026Proof.Check(!location.Location.ConditionLocked,"all three subsequent runes unlock");
        Future(g,next=>{
            var l=(BoardObject)next.IdEntityDic[location.Id];var owner=next.Player1;ParkMinions(owner);
            Core2026Proof.Check(next.Process(new ActivateLocationTask(owner,l)),"unlocked location activates");
            Core2026Proof.Check(owner.BoardZone.Single().Card.Cost==5 && l.Durability==2 && !l.Location.ConditionLocked,"summon five-Cost and retain unlock");
            End(next);End(next);Core2026Proof.Check(!LocationRules.CanActivate(owner,l,null),"ordinary cooldown skips next turn");
            End(next);End(next);Core2026Proof.Check(next.Process(new ActivateLocationTask(owner,l)),"Pinned_UldirQuarantineUnlockSurvivesUse: no new runes needed");
        });
    }
    private static void ParkMinions(Controller p)
    { foreach(var m in p.BoardZone.ToArray())p.SetasideZone.Add(p.BoardZone.Remove(m)); }
    private static void Unpinned_FetidDevourer(Game g,Controller p,string id)
    {
        Pinned_FetidLatestRuneMap(g.Seed);
        Pools(g,id);var fetid=Add(p,id);int entity=fetid.Id;
        Play(g,p,"RLK_060");ParkMinions(p);Core2026Proof.Check(fetid.Card.Id=="BE_781t8","Pinned_Fetid: U map");
        Play(g,p,"CORE_RLK_506");Core2026Proof.Check(fetid.Card.Id=="BE_781t2","Pinned_Fetid: latest B replaces U");
        Play(g,p,"CS2_182");Core2026Proof.Check(fetid.Card.Id=="BE_781t2","rune-less play unchanged");
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_009e"),fetid,fetid);fetid.Cost=1;
        Play(g,p,"RLK_060");ParkMinions(p);Core2026Proof.Check(fetid.Id==entity && fetid.Cost==7 && ((Minion)fetid).AttackDamage==9 && ((Minion)fetid).Health==6 && fetid.AppliedEnchantments?.Count==0,"Unpinned_FetidFormTransitionState: same entity, outside buff removed");
        ParkBoard(p);Future(g,next=>{
            PlayEntity(next,next.Player1,next.IdEntityDic[entity]);
            Core2026Proof.Check(next.Player1.BoardZone.Count==3 && next.Player1.BoardZone.Where(m=>m.Id!=entity).All(m=>m.Card.Cost==3),"U form summons two exact three-Cost minions");
        });
        ParkBoard(p);EmptyHand(p);fetid=Add(p,id);Play(g,p,"CORE_RLK_506");
        var enemy=Summon(p.Opponent,"CS2_200");
        PlayEntity(g,p,fetid);PlayerDecisionProof.Answer(g,enemy.Id);Core2026Proof.Check(enemy.Zone==p.Opponent.GraveyardZone,"B form destroys chosen enemy");
    }
    private static void Pinned_FetidLatestRuneMap(long seed)
    {
        foreach(string form in Enumerable.Range(1,19).Select(n=>"BE_781t"+n))
        {
            var game=DeathKnightC1Proof.NewGame(seed,CardClass.ROGUE);var owner=game.Player1;
            var source=Add(owner,"BE_781");Play(game,owner,form);
            Core2026Proof.Check(source.Card.Id==form && ((Minion)source).AttackDamage==9 && ((Minion)source).Health==6,
                "Pinned_FetidLatestRuneMap: all 19 exported vectors select exact form: "+form);
        }
        var g=DeathKnightC1Proof.NewGame(seed,CardClass.ROGUE);var p=g.Player1;
        var held=Add(p,"BE_781");Play(g,p,"RLK_067");
        Core2026Proof.Check(held.Card.Id=="BE_781t3","Pinned BB selects destroy-two");
        var copy=Generic.Copy(p,held,held,Zone.HAND);
        while(p.HandZone.Count<9)Add(p,"CS2_231");
        Play(g,p,"CORE_RLK_506");
        Core2026Proof.Check(held.Card.Id=="BE_781t2" && copy.Card.Id=="BE_781t2" && held.Id!=copy.Id,
            "Pinned BB to B downgrade; independent hand copy; full-hand play changes both definitions");
        var before=held.Card.Id;End(g);Play(g,p.Opponent,"RLK_067");
        Core2026Proof.Check(held.Card.Id==before,"opponent rune play does not transform held Fetid");
        End(g);ParkBoard(p);EmptyHand(p);
        var body=Add(p,"BE_781t13");var first=Summon(p.Opponent,"EX1_556");Summon(p.Opponent,"CS2_182");
        int deck=p.DeckZone.Count;PlayEntity(g,p,body);
        Core2026Proof.Check(p.Choice.Choices.Contains(first.Id),"Unpinned_FetidBattlecryChoices: first legal destruction offer");
        PlayerDecisionProof.Answer(g,first.Id);
        var token=p.Opponent.BoardZone.Single(m=>m.Card.Id=="skele21");
        Core2026Proof.Check(p.Choice.Choices.Contains(token.Id) && p.DeckZone.Count==deck,
            "Unpinned_FetidBattlecryChoices: Deathrattle token enters fresh second offer before drawing");
        Future(g,next=>{
            PlayerDecisionProof.Answer(next,token.Id);
            var drawn=next.Player1.HandZone.Single();
            Core2026Proof.Check(next.Player1.DeckZone.Count==deck-1 && drawn.Cost==Math.Max(0,drawn.Card.Cost-3),
                "Unpinned_FetidBattlecryChoices: draw and discount follow both settled destructions");
            var played=(Minion)next.IdEntityDic[body.Id];played.Silence();
            Core2026Proof.Check(played.Card.Id=="BE_781t13" && played.AttackDamage==9 && played.Health==6,
                "Silence retains transformed printed body");
        });
    }

}
