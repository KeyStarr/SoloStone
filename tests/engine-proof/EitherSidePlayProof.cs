using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class EitherSidePlayProof
{
    [GroupProof("b17-either-side-play")]
    internal static void Run()
    {
        Unpinned_AshalonPlayingController();
        RewindSideAndCopies();
        ControlAndBounce();
        CapacityAndInvalidCommands();
        PinnedRule_WatchmanDestinationSide();
        Unpinned_ExecutionerRandomAdjacency();
        var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
        var source = Add(p, "JAIL_461");
        Core2026Proof.Check(p.Options().OfType<PlayCardTask>().Any(t => t.Source == source && t.ChooseOne == 2), "enemy-side option missing");
        foreach (var next in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var owner = next.Player1; var card = next.IdEntityDic[source.Id];
            var left = Summon(owner.Opponent, "CS2_182");
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner, card, zonePosition: 1, chooseOne: 2)), "enemy placement accepted");
            Core2026Proof.Check(card.Controller == owner.Opponent && card.Zone == owner.Opponent.BoardZone && owner.UsedMana == 3,
                "same entity, destination control, caster payment");
            Core2026Proof.Check(left.Zone.Type == Zone.GRAVEYARD, "enemy adjacency resolved");
        }
    }

    private static void Unpinned_AshalonPlayingController()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool casterMemory in new[] { true, false })
        foreach (int side in new[] { 1, 2 })
        {
            var g = PlayLockRulesProof.Fixture(history); var p = g.Player1;
            if (casterMemory)
                foreach (string adaptation in new[] { "UNG_999t3", "UNG_999t3", "UNG_999t2" })
                    Enchantment.GetInstance(p, p.Hero, p, Cards.FromId("TLC_229t14e"), Cards.FromId(adaptation).AssetId);
            Enchantment.GetInstance(p.Opponent, p.Opponent.Hero, p.Opponent, Cards.FromId("TLC_229t14e"),
                Cards.FromId("UNG_999t6").AssetId);
            var card = Add(p, "CAP_004");
            foreach (var next in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var body = (Minion)next.IdEntityDic[card.Id]; var caster = next.Player1;
                Core2026Proof.Check(next.Process(PlayCardTask.Any(caster, body, zonePosition: 0, chooseOne: side)),
                    "Ashalon either-side play accepted");
                Core2026Proof.Check(body.AttackDamage == (casterMemory ? 10 : 4)
                    && !body.HasTaunt && (body.AppliedEnchantments?.Count(e => e.Card.Id == "UNG_999t2e") ?? 0) == (casterMemory ? 1 : 0),
                    "Unpinned_AshalonPlayingController: caster memories follow either-side play; destination memories excluded");
                Core2026Proof.Check(body.Controller == (side == 1 ? caster : caster.Opponent),
                    "Ashalon does not change destination control");
                var ordinary = Summon(body.Controller, "CS2_231");
                Core2026Proof.Check(ordinary.AttackDamage == 1 && !ordinary.HasTaunt,
                    "Ashalon memories do not apply to ordinary destination summons");
                body.Silence(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(body.AttackDamage == 4
                    && !(body.AppliedEnchantments?.Any(e => e.Card.Id == "UNG_999t2e") ?? false),
                    "Silence removes received caster adaptations");
            }
        }
    }

    private static void ControlAndBounce()
    {
        var g=PlayLockRulesProof.Fixture(); var p=g.Player1;
        var body=(Minion)Add(p,"CAP_004");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,body,zonePosition:0,chooseOne:2)),"enemy side before theft");
        g.TaskQueue.Execute(new ControlTask(EntityType.TARGET),p,p.Hero,body);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Controller==p && body.ChooseOnePlayables.All(o=>o.Controller==p
            && o.Zone==p.SetasideZone && o[Generic.PrivateDecisionOfferTag]==p.PlayerId),
            "theft transfers private side helpers with their parent");
        Core2026Proof.Check(Generic.ReturnToHandBlock(p,body),"stolen side minion bounces");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var card=(Minion)next.IdEntityDic[body.Id]; next.Player1.UsedMana=0;
            Core2026Proof.Check(card.ChooseOnePlayables.All(o=>o.Controller==next.Player1
                && o[Generic.PrivateDecisionOfferTag]==next.Player1.PlayerId),"helper ownership survives bounce and clone/snapshot");
            Core2026Proof.Check(next.Player1.Options().OfType<PlayCardTask>().Where(t=>t.Source.Id==card.Id)
                .Select(t=>t.ChooseOne).Distinct().Count()==2,"both sides remain selectable after theft and bounce");
        }
    }

    private static void RewindSideAndCopies()
    {
        var g=PlayLockRulesProof.Fixture(); var p=g.Player1;
        var foreign=Add(p,"CAP_004");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,foreign,zonePosition:0,chooseOne:2)),"Rewind fixture foreign placement");
        var khadgar=Summon(p,"CORE_DAL_575");
        Generic.SummonBlock(g,(Minion)Entity.FromCard(p,Cards.FromId("CS2_182")),-1,p.Hero);
        g.DeathProcessingAndAuraUpdate(); var duplicate=p.BoardZone.Last();
        Play(g,p,"TIME_001");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            ((Minion)next.IdEntityDic[foreign.Id]).Silence();
            ((Minion)next.IdEntityDic[khadgar.Id]).Silence();
            Kill(next,(Minion)next.IdEntityDic[duplicate.Id]);
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(next.IdEntityDic[foreign.Id].Controller==next.Player2
                && ((Minion)next.IdEntityDic[foreign.Id]).IsRush
                && !((Minion)next.IdEntityDic[khadgar.Id]).IsSilenced
                && next.IdEntityDic[duplicate.Id].Zone==next.Player1.BoardZone,
                "Rewind restores selected control, copied board state and active multiplier");
            RewindTestHelpers.KeepIfOffered(next);
        }
    }

    private static void CapacityAndInvalidCommands()
    {
        foreach (bool ownFull in new[] { false, true })
        foreach (bool enemyFull in new[] { false, true })
        {
            var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
            var card = Add(p, "CAP_004");
            if (ownFull) for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
            if (enemyFull) for (int i = 0; i < 7; i++) Summon(p.Opponent, "CS2_182");
            var tasks = p.Options().OfType<PlayCardTask>().Where(t => t.Source == card).ToArray();
            Core2026Proof.Check(tasks.Count(t => t.ChooseOne == 1) == (ownFull ? 0 : 1)
                && tasks.Count(t => t.ChooseOne == 2) == (enemyFull ? 0 : 1), "omit full sides independently");
            foreach (var invalid in new[] {
                PlayCardTask.Any(p,card,zonePosition:0,chooseOne:0,skipPrePhase:true),
                PlayCardTask.Any(p,card,zonePosition:0,chooseOne:3,skipPrePhase:true),
                PlayCardTask.Any(p,card,p.Opponent.Hero,0,2,true),
                PlayCardTask.Any(p,card,zonePosition:8,chooseOne:2,skipPrePhase:true) })
            {
                string before = Canonical.State(g);
                Core2026Proof.Check(!g.Process(invalid) && Canonical.State(g) == before, "invalid placement rejected before payment");
            }
            if (!enemyFull)
            {
                var stale = tasks.Single(t => t.ChooseOne == 2);
                while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
                string before = Canonical.State(g);
                Core2026Proof.Check(!g.Process(stale) && Canonical.State(g) == before, "cached newly-full destination rejected");
            }
        }
    }

    private static void PinnedRule_WatchmanDestinationSide()
    {
        foreach (bool casterBrann in new[] { false, true })
        {
            var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
            var friendly = Summon(p, "CS2_182");
            var enemy = Summon(p.Opponent, "CS2_182");
            Summon(casterBrann ? p : p.Opponent, "LOE_077");
            var card = Add(p, "JAIL_455");
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card, zonePosition:1, chooseOne:2)), "Watchman enemy play");
            Core2026Proof.Check(friendly.Damage == 0 && enemy.Damage == (casterBrann ? 4 : 2), "destination recipients, caster Brann only");
            Core2026Proof.Check(g.HistoryFacts.Query.Plays().Last().ControllerId == p.Id,
                "enemy placement retains caster play credit");
        }
    }

    private static void Unpinned_ExecutionerRandomAdjacency()
    {
        var selected = new System.Collections.Generic.HashSet<int>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var g = PlayLockRulesProof.Fixture(seed:seed); var p = g.Player1;
            var left = Summon(p.Opponent, "CS2_182"); var right = Summon(p.Opponent, "CS2_200");
            var card = Add(p, "JAIL_461");
            int expected = g.CloneExact().Random.Next(2); selected.Add(expected);
            foreach (var next in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                Core2026Proof.Check(next.Process(PlayCardTask.Any(next.Player1,next.IdEntityDic[card.Id],zonePosition:1,chooseOne:2)), "random enemy placement");
                Core2026Proof.Check(next.IdEntityDic[expected == 0 ? left.Id : right.Id].Zone.Type == Zone.GRAVEYARD
                    && next.IdEntityDic[expected == 0 ? right.Id : left.Id].Zone.Type == Zone.PLAY, "seeded ordered neighbor choice survives clone/snapshot");
            }
        }
        Core2026Proof.Check(selected.Count == 2, "seed corpus covers both neighbors");
        {
            var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
            var beyond = Summon(p.Opponent,"CS2_182"); LocationProofs.Location(p.Opponent);
            var card = Add(p,"JAIL_461");
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card,zonePosition:2,chooseOne:2)) && beyond.Zone.Type == Zone.PLAY,
                "location blocks physical adjacency");
        }
    }

    [GroupProof("b17-summon-multiplicity")]
    internal static void Unpinned_Khadgar_SummonedState()
    {
        KhadgarLifecycle();
        Unpinned_KhadgarRebornAndExpiry();
        Pinned_KhadgarRandomIdentity();
        KhadgarReactionOrder();
        Unpinned_Khadgar_GrantedDeathrattleState();
        var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
        Summon(p, "CORE_DAL_575");
        var body = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"));
        body.Damage = 1;
        Generic.SummonBlock(g, body, -1, p.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 3, "Khadgar doubles a card summon");
        Core2026Proof.Check(p.BoardZone.Last().Damage == 1, "Khadgar copies damage");
    }

    private static void KhadgarLifecycle()
    {
        foreach (string id in new[] { "CORE_DAL_575", "DAL_575" })
        foreach (bool history in new[] { false, true })
        {
            var g = PlayLockRulesProof.Fixture(history); var p = g.Player1;
            var khadgar = Summon(p,id);
            var body = (Minion)Entity.FromCard(p,Cards.FromId("CS2_182"));
            Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_009e"),p.Hero,body);
            body.Damage = 1; body.NumAttacksThisTurn = 1;
            Generic.SummonBlock(g,body,-1,p.Hero); g.DeathProcessingAndAuraUpdate();
            var copy = p.BoardZone.Last();
            Core2026Proof.Check(copy.Id != body.Id && copy.Card == body.Card && copy.AttackDamage == 6
                && copy.Damage == 1 && copy.NumAttacksThisTurn == 0, "stateful independent summon, not fresh definition");
            foreach (var next in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var original = (Minion)next.IdEntityDic[body.Id]; var duplicate = (Minion)next.IdEntityDic[copy.Id];
                duplicate.Silence(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(duplicate.AttackDamage == 4 && original.AttackDamage == 6, "copied removable buffs independent");
                ((Minion)next.IdEntityDic[khadgar.Id]).Silence();
                int count = next.Player1.BoardZone.Count;
                Generic.SummonBlock(next,(Minion)Entity.FromCard(next.Player1,Cards.FromId("CS2_182")),-1,next.Player1.Hero);
                Core2026Proof.Check(next.Player1.BoardZone.Count == count + 1, "Silence removes summon multiplier");
            }
        }
        {
            var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
            Summon(p,"CORE_DAL_575"); Summon(p,"CORE_DAL_575");
            var body=(Minion)Entity.FromCard(p,Cards.FromId("CS2_182"));
            Generic.SummonBlock(g,body,-1,p.Hero);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 4, "two providers multiply once each");
            Generic.SummonBlock(g,(Minion)Entity.FromCard(p,Cards.FromId("CS2_200")),-1,p.Hero);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7, "extra summon lost at physical capacity");
        }
        {
            var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
            Summon(p.Opponent,"CORE_DAL_575");
            Generic.SummonBlock(g,(Minion)Entity.FromCard(p,Cards.FromId("CS2_182")),-1,p.Hero);
            Core2026Proof.Check(p.BoardZone.Count == 1, "opponent provider does not trigger");
            Summon(p,"CORE_DAL_575");
            Play(g,p,"CS2_182");
            Core2026Proof.Check(p.BoardZone.Count == 3, "ordinary hand play is not a card summon");
        }
        {
            var g=PlayLockRulesProof.Fixture(); var p=g.Player1;
            var khadgar=Summon(p,"CORE_DAL_575"); var raid=Summon(p,"CS2_122");
            var body=(Minion)Entity.FromCard(p,Cards.FromId("CS2_182")); body.Silence();
            Generic.SummonBlock(g,body,-1,p.Hero); g.DeathProcessingAndAuraUpdate();
            var copy=p.BoardZone.Last();
            Core2026Proof.Check(body.AttackDamage==5 && copy.AttackDamage==5 && copy.IsSilenced,
                "copy preserves Silence and recalculates external aura once");
            raid.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(body.AttackDamage==4 && copy.AttackDamage==4,"external aura never becomes copied base stats");
            Kill(g,khadgar); int count=p.BoardZone.Count;
            Generic.SummonBlock(g,(Minion)Entity.FromCard(p,Cards.FromId("CS2_182")),-1,p.Hero);
            Core2026Proof.Check(p.BoardZone.Count==count+1,"leaving play removes multiplier");
        }
    }

    private static void Unpinned_Khadgar_GrantedDeathrattleState()
    {
        // Vectus-style boundary: assigned Deathrattle and stored payload exist before entry.
        var g=PlayLockRulesProof.Fixture(); var p=g.Player1; var khadgar=Summon(p,"CORE_DAL_575");
        var original=(Minion)Entity.FromCard(p,Cards.FromId("CS2_231"));
        Generic.AddEnchantmentBlock(g,Cards.FromId("UNG_999t2e"),p.Hero,original);
        PayloadRules.StoreCard(original,17,Cards.FromId("CS2_182"));
        Generic.SummonBlock(g,original,-1,p.Hero); g.DeathProcessingAndAuraUpdate();
        var copy=p.BoardZone.Last();
        Core2026Proof.Check(PayloadRules.Entries(copy,17).Length==1 && PayloadRules.Entries(copy,17)[0].Id!=PayloadRules.Entries(original,17)[0].Id,
            "Khadgar copies preassigned stored payload independently");
        khadgar.Silence(); original.Silence();
        Kill(g,copy);
        Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="UNG_999t2t1")==2,
            "Khadgar copy retains assigned Deathrattle after source Silence");
    }

    private static void KhadgarReactionOrder()
    {
        var g=PlayLockRulesProof.Fixture(); var p=g.Player1; Summon(p,"CORE_DAL_575");
        var order=new System.Collections.Generic.List<int>();
        g.TriggerManager.AfterSummonTrigger += source => order.Add(source.Id);
        var body=(Minion)Entity.FromCard(p,Cards.FromId("CS2_182"));
        Generic.SummonBlock(g,body,-1,p.Hero);
        Core2026Proof.Check(order.SequenceEqual(new[]{body.Id,p.BoardZone.Last().Id}), "original after-summon reaction precedes copied reaction");
    }

    private static void Unpinned_KhadgarRebornAndExpiry()
    {
        var g=PlayLockRulesProof.Fixture(); var p=g.Player1;
        Summon(p,"CORE_DAL_575");
        var reborn=Summon(p,"ULD_723"); Kill(g,reborn);
        var bodies=p.BoardZone.Where(m=>m.Card.Id=="ULD_723").ToArray();
        Core2026Proof.Check(bodies.Length==2 && bodies.All(m=>m.Health==1) && bodies.Count(m=>m[GameTag.REBORN]==1)==1,
            "Unpinned_Khadgar_Reborn254477: extra restores definition Reborn at one Health");
        g=PlayLockRulesProof.Fixture(); p=g.Player1; Summon(p,"CORE_DAL_575");
        var original=(Minion)Entity.FromCard(p,Cards.FromId("CS2_182"));
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_046e"),p.Hero,original);
        Generic.SummonBlock(g,original,-1,p.Hero); g.DeathProcessingAndAuraUpdate(); var copy=p.BoardZone.Last();
        Core2026Proof.Check(copy.AttackDamage==7,"copied temporary buff present");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            End(next);
            Core2026Proof.Check(((Minion)next.IdEntityDic[copy.Id]).AttackDamage==4 && ((Minion)next.IdEntityDic[original.Id]).AttackDamage==4,
                "copied temporary buff expires independently through clone/snapshot");
        }
    }

    private static void Pinned_KhadgarRandomIdentity()
    {
        for(int seed=1;seed<=5;seed++)
        {
            var g=PlayLockRulesProof.Fixture(seed:seed); var p=g.Player1;
            Summon(p,"CORE_DAL_575"); var expected=g.CloneExact();
            var choices=new[]{"CS2_182","CS2_200"};
            var first=choices[expected.Random.Next(2)]; var second=choices[expected.Random.Next(2)];
            for(int i=0;i<2;i++)
            {
                var card=Cards.FromId(choices[g.Random.Next(2)]);
                Generic.SummonBlock(g,(Minion)Entity.FromCard(p,card),-1,p.Hero);
            }
            Core2026Proof.Check(p.BoardZone.Skip(1).Select(m=>m.Card.Id).SequenceEqual(new[]{first,first,second,second}),
                "random producer rolls twice; duplicates retain identity in order");
            Core2026Proof.Check(g.Random.Next(10000)==expected.Random.Next(10000),"extra summons consume no producer RNG");
        }
    }

    [GroupProof("b17-battlecry-combo")]
    internal static void Unpinned_Opu_IndependentBranches()
    {
        foreach (bool active in new[] { false, true })
        {
            var next=PlayLockRulesProof.Fixture(); var owner=next.Player1; owner.IsComboActive=active;
            var spider=Summon(owner.Opponent,"FP1_002"); spider.Damage=1;
            Play(next,owner,"TLC_522");
            Core2026Proof.Check(owner.Opponent.BoardZone.Count==(active?0:2) && owner.HandZone.Count==(active?2:1),
                "Pinned_OpuBetweenBranchDeaths: Deathrattle replacements exist before Combo Fan");
        }
        var g = PlayLockRulesProof.Fixture(); var p = g.Player1;
        p.IsComboActive = true;
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "TLC_522");
        Core2026Proof.Check(enemy.Damage == 2 && p.HandZone.Count == 2, "Battlecry plus independent Combo cast two Fans");
    }
}
