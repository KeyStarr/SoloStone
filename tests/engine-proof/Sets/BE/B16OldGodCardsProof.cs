using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16OldGodCardsProof
{
    [GroupProof("b16-old-gods-cards", ProofKind.Cards)]
    internal static void Run()
    {
        Unpinned_NzothSummonTimeDeathrattle();
        StandardSetsProof.Case("BE_013",Unpinned_Cthun);
        StandardSetsProof.Case("BE_017",Unpinned_Nzoth);
        StandardSetsProof.Case("BE_784",Unpinned_Yshaarj);
    }
    private static void Unpinned_NzothSummonTimeDeathrattle()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        var god = Summon(p,"BE_017"); var held = Add(p,"BE_017");
        var raptor = Summon(p,"CS2_172");
        B16OldGodProof.Use(g,god,"BE_017t6"); CoreCostProof.Answer(g,raptor.Id);
        Core2026Proof.Check(raptor.HasDeathrattle && held.Cost == 15,"Domain does not count retroactively");
        PoolPilotProof.Resume(g,copy => {
            Play(copy,copy.Player1,"UNG_948",(Minion)copy.IdEntityDic[raptor.Id]);
            Core2026Proof.Check(copy.Player1.BoardZone.Last().HasDeathrattle && copy.IdEntityDic[held.Id].Cost == 14,
                "Domain-given Bloodfen Raptor copied by Molten Reflection reduces N'Zoth 15 to 14");
        });
        PoolPilotProof.Resume(g,copy => {
            var owner = copy.Player1; var body = owner.BoardZone.Last();
            body.Silence(); Kill(copy,body);
            Core2026Proof.Check(copy.IdEntityDic[held.Id].Cost == 14,"later Silence/death cannot undo summon-time discount");
            var printed = Summon(owner,"EX1_556"); printed.Silence();
            Core2026Proof.Check(copy.IdEntityDic[held.Id].Cost == 13,"printed Deathrattle counted at original summon");
            Generic.Copy(owner,owner.Hero,printed,Zone.PLAY,preserveBoardMinionState:true);
            copy.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.IdEntityDic[held.Id].Cost == 13,"silenced printed Deathrattle copy does not count");
            Generic.Copy(owner.Opponent,owner.Opponent.Hero,copy.IdEntityDic[raptor.Id],Zone.PLAY);
            copy.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.IdEntityDic[held.Id].Cost == 13 && Add(owner.Opponent,"BE_017").Cost == 14,
                "opponent granted summon counts only for opponent, even for N'Zoth generated later");
        });
        Play(g,p,"TIME_001"); RewindTestHelpers.AnswerRewind(g);
        Core2026Proof.Check(g.IdEntityDic[held.Id].Cost == 13,"Rewind retains summon-time correction");
    }

    private static void Unpinned_Cthun(Game g, Controller p, string id) {
        var god = Summon(p,id);
        B16OldGodProof.Use(g,god,"BE_013t1");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 7,"C'Thun on-board Wrath deals seven base damage");
        while (p.BoardZone.Count < 7) Summon(p,"CS2_182");
        B16OldGodProof.Use(g,god,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Count == 7 && CthunRules.Progress(p) == 9,"full board still spends ability and records bit");
    }
    private static void Unpinned_Nzoth(Game g, Controller p, string id) {
        var god = Summon(p,id); var held = Add(p,id);
        var enemy = Summon(p.Opponent,"CS2_200"); enemy[GameTag.HEALTH] = 30;
        var ally = Summon(p,"CS2_182");
        B16OldGodProof.Use(g,god,"BE_017t6"); CoreCostProof.Answer(g,ally.Id);
        Core2026Proof.Check(ally.HasDeathrattle && held.Cost == 15,"granted Deathrattle applies, not retroactive summon discount");
        B16OldGodProof.Use(g,god,"BE_017t4");
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] {ally.Id}),"Mastery offers only other Deathrattle minions");
        PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,ally.Id));
        Core2026Proof.Check(enemy.Damage == 4,"Mastery triggers granted Deathrattle twice");
        Kill(g,ally); Core2026Proof.Check(enemy.Damage == 6,"normal death triggers granted effect");
        var printed = Summon(p,"EX1_556"); Core2026Proof.Check(held.Cost == 14,"successful printed Deathrattle summon reduces cost");
        printed.Silence(); Kill(g,printed);
        End(g); End(g);
        B16OldGodProof.Use(g,god,"BE_017t5");
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "CS2_182") && p.BoardZone.Count(m => m.Card.Id == "EX1_556") == 1,
            "Army uses printed Deathrattle identity: silenced originals qualify, granted Deathrattle alone does not");
        var returned = p.BoardZone.Single(m => m.Card.Id == "EX1_556");
        Core2026Proof.Check(returned.HasDeathrattle && returned.AttackDamage == 2 && returned.Health == 3,"resurrection creates fresh printed body");
        int handCount = p.HandZone.Count;
        B16OldGodProof.Activate(g,god); B16OldGodProof.Pick(g,"BE_017t3");
        if (p.Choice != null) {
            var choices = p.Choice.Choices.ToArray();
            Core2026Proof.Check(choices.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == choices.Length,"Discover identities distinct");
            PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,choices[0]));
            Core2026Proof.Check(p.HandZone.Count == handCount,"Call summons directly without occupying hand slot");
        }
        var count = OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Discover(type:CardType.MINION,minCost:5,keyword:GameTag.DEATHRATTLE),128389).Count;
        Console.WriteLine("B16 N'Zoth bucket=" + count);
    }
    private static void Unpinned_Yshaarj(Game g, Controller p, string id) {
        // Twenty real Mana paid on minions; the Old God's own ten reaches thirty.
        foreach(string prior in new[] { "CS2_222","CS2_222","CS2_200" }) { Play(g,p,prior); StandardSetsProof.ParkBoard(p); }
        p.UsedMana = 0; var god = (Minion)Play(g,p,id);
        Core2026Proof.Check(god.IsImmune && !p.Hero.IsImmune,"Battlecry immunity attaches to body, not hero");
        var left = Summon(p,"CS2_182"); var right = Summon(p,"CS2_200"); right.Damage = 2;
        int attack = god.AttackDamage, health = god.Health;
        int gainAttack = god.GetAdjacentMinions().Sum(m => m.AttackDamage);
        int gainHealth = god.GetAdjacentMinions().Sum(m => m.Health);
        B16OldGodProof.Use(g,god,"BE_784t3");
        Core2026Proof.Check(god.AttackDamage == attack + gainAttack && god.Health == health + gainHealth,"Might grants current adjacent stats as buff layer");
        B16OldGodProof.Use(g,god,"BE_784t4");
        var next = Add(p,"CS2_186"); g.AuraUpdate(); Core2026Proof.Check(next.Cost == 2,"Domain reduces next card by five");
        PoolPilotProof.Resume(g,copy => {
            End(copy); Core2026Proof.Check(((Minion)copy.IdEntityDic[god.Id]).IsImmune,"immunity persists through opponent turn");
            End(copy); Core2026Proof.Check(!((Minion)copy.IdEntityDic[god.Id]).IsImmune,"immunity expires at next owner start");
        });
        Core2026Proof.Check(next.Cost == 2,"next-card Domain has no end-turn expiry");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p,next)),"discounted next minion played");
        var later = Add(p,"CS2_186"); g.AuraUpdate(); Core2026Proof.Check(later.Cost == 7,"Domain consumed once");
        while(p.BoardZone.Count < 7) Summon(p,"CS2_182");
        while(p.HandZone.Count < 10) Add(p,"CS2_182");
        B16OldGodProof.Use(g,god,"BE_784t2");
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.HandZone.Count == 10,"Spawn handles both full zones without extra slots");
        var first = Summon(p.Opponent,"CS2_182"); var second = Summon(p.Opponent,"CS2_182");
        first.IsFrozen = true; first.NumAttacksThisTurn = 1; second.CantAttack = true;
        B16OldGodProof.Use(g,god,"BE_784t1");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && first.NumAttacksThisTurn == 1 && second.NumAttacksThisTurn == 0,
            "Gaze forces original enemies despite Frozen, spent attacks and Can't attack, preserving ordinary allowances");
        god.Damage = 0;
        god.Silence(); Core2026Proof.Check(god.AttackDamage == 10 && god.Health == 10,"Silence removes Might stats");
        Console.WriteLine("B16 Y'Shaarj bucket="+OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.MINION,minCost:8),131851).Count);
    }
}
