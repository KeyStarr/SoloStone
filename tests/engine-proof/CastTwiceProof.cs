using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CastTwiceProof
{
    [GroupProof("cast-twice")]
    internal static void Run()
    {
        var game = HeraldProof.New(CardClass.MAGE); var p = game.Player1;
        var spell = Add(p, "CS2_029");
        // Scheduled body count is entity state, deliberately independent of a card module.
        spell[(GameTag)32100] = 1;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(p, spell, p.Opponent.Hero)), "scheduled spell plays");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 12, "CastTwice: scheduled extra body retains selected target");
        Core2026Proof.Check(p.NumSpellsPlayedThisGame == 1, "CastTwice: repeat does not become another hand play");
        Decisions(false);
        Decisions(true);
        Unpinned_DeadTargetDoesNotRandomize();
        RepeatLostTargetProof.Run();
        Fizzle();
        Fizzle(randomFallback: true);
        Unpinned_LostTargetKeepsIndependentHandBuff();
        UntargetedRandomPortion();
        Rewind();
        Modes();
        Expiry();
        SameDefinitionTransform();
        Pools();
        System.Console.WriteLine("PASS CastTwice Unpinned_TargetRetention");
    }
    private static readonly DecisionSpellTask Decision = new DecisionSpellTask("proof/casttwice/decisions/v1",
        new DiscoverThenContinueTask("CS2_182", "CS2_231", "CS2_120"), new AddStackTo(EntityType.HAND), new ArmorTask(1));
    private static readonly CustomTask Ordinary = new CustomTask((g,c,s,t,k) =>
        new EffectContext(g,c.Id,s.Id).Discover(new[] { Cards.FromId("CS2_182"), Cards.FromId("CS2_231") }));

    private static void Decisions(bool ordinary)
    {
        var definition = Cards.FromId("GAME_005"); var old = definition.Power; bool consumes = definition.ConsumesHistory;
        try
        {
            definition.ConsumesHistory = true;
            definition.Power = new Power { PowerTask = ordinary ? Ordinary : Decision };
            var game = HeraldProof.New(CardClass.MAGE); var p = game.Player1;
            var spell = Add(p, definition.Id); spell[(GameTag)32100] = 1;
            Core2026Proof.Check(game.Process(PlayCardTask.Any(p,spell)), "Unpinned_Decisions: first body starts");
            for (int body=0; body<2; body++)
            {
                Core2026Proof.Check(p.Choice != null && p.NumSpellsPlayedThisGame == 0 && spell.Zone != p.GraveyardZone,
                    "Unpinned_Decisions: each body waits for separate answer and defers after-play");
                int selected = p.Choice.Choices[body];
                var clone = game.CloneExact(history:true); var loaded = Game.LoadSnapshot(game.SaveSnapshot());
                foreach (var next in new[] { game,clone,loaded }) PlayerDecisionProof.Answer(next,selected);
                Core2026Proof.Check(Canonical.State(game)==Canonical.State(clone) && Canonical.State(game)==Canonical.State(loaded),
                    "Unpinned_Decisions: clone/snapshot preserves remaining bodies and choices");
            }
            Core2026Proof.Check(p.Choice == null && p.NumSpellsPlayedThisGame == 1 && spell.Zone == p.GraveyardZone &&
                p.HandZone.Any(x=>x.Card.Id=="CS2_182") && p.HandZone.Any(x=>x.Card.Id=="CS2_231"),
                "Unpinned_Decisions: independently selected results, one hand play");
            var casts=game.HistoryFacts.Query.Casts().Where(f=>f.EntityId==spell.Id).ToArray();
            Core2026Proof.Check(casts.Length==4 && casts.Count(f=>f.CasterKind==HistoryCastKind.Player)==2 &&
                casts.Count(f=>f.CasterKind==HistoryCastKind.Repeat)==2,"Unpinned_Decisions: separate Player and Repeat cast facts");
        }
        finally { definition.Power=old; definition.ConsumesHistory=consumes; }
    }

    private static void Rewind()
    {
        var game=HeraldProof.New(CardClass.PRIEST); var p=game.Player1;
        Play(game,p,"EDR_464"); p.UsedMana=0;
        var spell=Add(p,"CS2_029"); spell[GameTag.REWIND]=1;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(p,spell,p.Opponent.Hero)) && p.Opponent.Hero.Damage==12,"Rewind repeated preview resolves both bodies");
        var clone=game.CloneExact(history:true); var loaded=Game.LoadSnapshot(game.SaveSnapshot());
        foreach(var next in new[]{game,clone,loaded})
        {
            PlayerDecisionProof.Answer(next,next.Rewind.RewindEntityId);
            if (next.Rewind != null) PlayerDecisionProof.Answer(next,next.Rewind.KeepEntityId);
            Core2026Proof.Check(next.Player2.Hero.Damage==12 && next.Player1.AppliedEnchantments.Single(e=>e.Card.Id=="EDR_464e2")[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,
                "Rewind retry restores charges; one accepted play consumes one, not two");
        }
        Core2026Proof.Check(Canonical.State(game)==Canonical.State(clone) && Canonical.State(game)==Canonical.State(loaded),"Rewind repeat restore parity");
    }

    private static readonly CustomTask Mode = new CustomTask((g,c,s,t,k) => { new ArmorTask(1).Process(g,c,s,t,k); Summon(c,"OG_044"); });
    private static void Modes()
    {
        var options=new[]{Cards.FromId("EX1_160a"),Cards.FromId("EX1_160b")}; var old=options.Select(c=>c.Power).ToArray();
        try
        {
            options[0].Power=new Power { PowerTask=Mode };
            options[1].Power=new Power { PowerTask=new DamageTask(3,EntityType.OP_HERO) };
            var game=HeraldProof.New(CardClass.DRUID); var p=game.Player1;
            var spell=Add(p,"EX1_160"); spell[(GameTag)32100]=1;
            Core2026Proof.Check(game.Process(PlayCardTask.Any(p,spell,chooseOne:1)),"Unpinned_Mode: Choose One play");
            Core2026Proof.Check(p.Hero.Armor==2 && p.Opponent.Hero.Damage==0,"Unpinned_Mode: later ChooseBoth aura cannot replace retained branch");
        }
        finally { for(int i=0;i<options.Length;i++) options[i].Power=old[i]; }
    }

    private static readonly CustomTask TransformOnce = new CustomTask((g,c,s,t,k) => {
        if (t == null || s[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0) return;
        s[GameTag.TAG_SCRIPT_DATA_NUM_1]=1;
        Generic.ChangeEntityBlock(t.Controller,t,t.Card,true);
    });
    private static void SameDefinitionTransform()
    {
        var card=Cards.FromId("CS2_029"); var old=card.Power;
        try
        {
            card.Power=new Power { PowerTask=StateTaskList.Chain(TransformOnce,new DamageTask(1,EntityType.TARGET,true),new ArmorTask(1)) };
            var game=HeraldProof.New(CardClass.MAGE); var p=game.Player1;
            var target=Summon(p.Opponent,"CS2_200"); var spell=Add(p,card.Id); spell[(GameTag)32100]=1;
            Core2026Proof.Check(game.Process(PlayCardTask.Any(p,spell,target)),"same-definition transform play");
            Core2026Proof.Check(target.Damage==1 && p.Hero.Armor==2,"Unpinned_TargetTransform: same-ID same-definition transformation invalidates retained target only");
        }
        finally { card.Power=old; }
    }

    private static void Expiry()
    {
        var game=HeraldProof.New(CardClass.MAGE); var p=game.Player1;
        var spell=Add(p,"CS2_029"); RepeatRules.Mark(spell,p.Hero,"JAIL_123e");
        spell.AppliedEnchantments.Single(e=>e.Card.Id=="JAIL_123e").ExpireAtEndOfTurn();
        End(game); End(game); PlayEntity(game,p,spell,p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==6,"expired instance mark cannot schedule a repeat");
    }

    private static void Pools()
    {
        var wanted=new[]{"JAIL_123","CATA_154t","CATA_154t1","END_037"};
        var report=OfflinePoolAdmission.Evaluate(OfflinePoolPolicy.Load().BuildManifest());
        var cards=report.Cards.Where(c=>wanted.Contains(c.CardId)).ToArray();
        Core2026Proof.Check(cards.Length==4 && cards.All(c=>c.Registered && c.PoolRequirements.All(r=>r.Count>=3)),"cast-twice complete dependency pools admitted for every declared class");
        foreach(var card in cards) Console.WriteLine("CastTwice pool "+card.CardId+": "+string.Join(",",card.PoolRequirements.Select(r=>r.Class+"="+r.Count)));
    }

    private static void Unpinned_DeadTargetDoesNotRandomize()
    {
        var game = HeraldProof.New(CardClass.PRIEST); var p = game.Player1;
        Play(game, p, "EDR_464");
        var tyrande = p.BoardZone.Single();
        var victim = Summon(p.Opponent, "CS2_231");
        var survivor = Summon(p.Opponent, "CS2_200");
        var rng = game.Random.DrawCount;
        Play(game, p, "CATA_557", victim);
        Core2026Proof.Check(victim.Zone == p.Opponent.GraveyardZone && survivor.Damage == 0 &&
            tyrande.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0 && game.Random.DrawCount == rng,
            "Unpinned_DeadTargetDoesNotRandomize: Tyrande's Triumph repeat never replaces its dead target or consumes target RNG");
    }

    private static readonly CustomTask RandomFallback = new CustomTask((g,c,s,t,k) => {
        var ctx = new EffectContext(g,c.Id,s.Id,t?.Id,k);
        int? selected = t?.Id ?? ctx.RandomLegalTarget(s.Id);
        if (selected.HasValue) ctx.Damage(selected.Value,6,spellDamage:true);
    });

    private static void Unpinned_LostTargetKeepsIndependentHandBuff()
    {
        var card = Cards.FromId("BE_094"); var old = card.Power;
        try
        {
            // Force departure between bodies while retaining the real recipe's automatic-target fallback.
            card.Power = new Power { PowerTask = StateTaskList.Chain(old.PowerTask, new ReturnHandTask(EntityType.TARGET)) };
            var game = HeraldProof.New(CardClass.MAGE); var p = game.Player1;
            var victim = Summon(p.Opponent, "CS2_200");
            var survivor = Summon(p.Opponent, "CS2_200");
            var held = (Minion)Add(p, "CS2_182");
            var spell = Add(p, card.Id); RepeatRules.Mark(spell, p.Hero, "JAIL_123e");
            foreach (var next in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], (ICharacter)next.IdEntityDic[victim.Id]);
                Core2026Proof.Check(next.IdEntityDic[victim.Id].Zone == next.Player2.HandZone &&
                    ((Minion)next.IdEntityDic[survivor.Id]).AttackDamage == 6 &&
                    ((Minion)next.IdEntityDic[held.Id]).AttackDamage == held.Card.ATK + 4,
                    "Unpinned_LostTargetKeepsIndependentHandBuff: bounce prevents replacement board buff, preserves both independent hand buffs");
            }
        }
        finally { card.Power = old; }
    }

    private static readonly CustomTask RandomUntargeted = new CustomTask((g,c,s,t,k) => {
        var ctx = new EffectContext(g,c.Id,s.Id,stack:k);
        int? selected = ctx.RandomLegalTarget(s.Id,new[] { c.Opponent.Hero.Id });
        if (selected.HasValue) ctx.Damage(selected.Value,1);
    });

    private static void UntargetedRandomPortion()
    {
        var card = Cards.FromId("GAME_005"); var old = card.Power;
        try
        {
            card.Power = new Power { PowerTask = RandomUntargeted };
            var game = HeraldProof.New(CardClass.MAGE); var p = game.Player1;
            var spell = Add(p,card.Id); spell[(GameTag)32100] = 1;
            PlayEntity(game,p,spell);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2,
                "untargeted random effect still selects independently in each spell body");
        }
        finally { card.Power = old; }
    }

    private static void Fizzle(bool randomFallback = false)
    {
        var card=Cards.FromId("CS2_029"); var old=card.Power;
        try
        {
            card.Power=new Power { PowerTask=StateTaskList.Chain(randomFallback ? RandomFallback : new DamageTask(6,EntityType.TARGET,true),new ArmorTask(1)) };
            var game=HeraldProof.New(CardClass.MAGE); var p=game.Player1;
            var victim=Summon(p.Opponent,"CS2_231"); var survivor=Summon(p.Opponent,"CS2_200");
            var spell=Add(p,card.Id); spell[(GameTag)32100]=1;
            Core2026Proof.Check(game.Process(PlayCardTask.Any(p,spell,victim)),"Unpinned_TargetFizzle: play accepted");
            Core2026Proof.Check(victim.Zone==p.Opponent.GraveyardZone && survivor.Damage==0 && p.Hero.Armor==2,
                "Unpinned_TargetFizzle: dead target receives no replacement, independent untargeted portion repeats");
        }
        finally { card.Power=old; }
    }
}
