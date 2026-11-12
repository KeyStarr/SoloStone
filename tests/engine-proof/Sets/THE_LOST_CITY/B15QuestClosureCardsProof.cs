using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.CardSets.Client.Sets;
using static Core2026Proof;
internal static class B15QuestClosureCardsProof
{
    [GroupProof("b15-quest-closure-cards", ProofKind.Cards)]
    internal static void Run()
    {
        var dependencyFailures = new List<string>();
        foreach (string token in new[] { "TLC_229t14", "TLC_229t14e", "TLC_602t", "TLC_817t", "TLC_817t2", "TLC_817t3", "TLC_817t4", "TLC_817t5", "UNG_067t1e", "UNG_067t1e2", "UNG_920t2", "UNG_829t2", "UNG_829t3" }
            .Concat(AshalonRidgeGuardian.Adaptations).Concat(AshalonRidgeGuardian.Adaptations.Select(x => x + "e"))
            .Concat(LatorviusGazeOfTheCity.Rewards))
            try { CostSliceProof.Definition(token); } catch (Exception e) { dependencyFailures.Add(e.Message); }
        Core2026Proof.Check(dependencyFailures.Count == 0, string.Join("; ", dependencyFailures));
        StandardSetsProof.Case("TLC_229", Unpinned_SpiritMatching);
        StandardSetsProof.Case("TLC_602", Unpinned_OwnerTurnsAndRewardClosure);
        StandardSetsProof.Case("TLC_817", Unpinned_IndependentSchoolsAndFreshCombination);
        Unpinned_AshalonMemory(true); Unpinned_AshalonMemory(false);
        Unpinned_LatorviusRandomOrder();
        SoletosRewards();
        Unpinned_TransformedHalvesMergeBeforeDraw();
        CurrentUngoroRewards(); FullDeckRewards();
    }
    private static void Unpinned_SpiritMatching(Game g, Controller p, string id)
    {
        Play(g, p, "EX1_506"); StandardSetsProof.ParkBoard(p);
        var q = (Spell)Play(g, p, id);
        Play(g, p, "CS2_231");
        var beast = Summon(p, "CS2_172");
        Play(g, p, "EX1_564", beast);
        Core2026Proof.Check(q.QuestProgress == 0, "untyped Faceless play does not become typed retroactively");
        StandardSetsProof.ParkBoard(p);
        Core2026Proof.Check(q.QuestProgress == 0, "Unpinned_SpiritMatching: untyped and prequest excluded");
        Play(g, p, "TLC_225"); Play(g, p, "EX1_506");
        Core2026Proof.Check(q.QuestProgress == 2, "dual play reassigned from Murloc to Elemental");
        Play(g, p, "EX1_506"); Play(g, p, "CS2_168");
        Core2026Proof.Check(q.QuestProgress == 2, "third play cannot reuse occupied types");
        StandardSetsProof.ParkBoard(p);
        var bytes = g.SaveSnapshot();
        foreach (var branch in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) })
        {
            var owner = branch.Player1; var quest = (Spell)branch.IdEntityDic[q.Id];
            for (int i = 0; i < 4; i++) { Play(branch, owner, "BE_734"); StandardSetsProof.ParkBoard(owner); }
            Core2026Proof.Check(quest.QuestProgress == 6 && quest.Zone == owner.GraveyardZone
                && owner.HandZone.Count(c => c.Card.Id == "TLC_229t14") == 1,
                "Unpinned_SpiritMatching: each All play adds one after clone/snapshot");
        }
        var other = QuestLifecycleProof.Fixture(); var otherQuest = (Spell)Play(other, other.Player1, id);
        End(other); Play(other, other.Player2, "EX1_506");
        Core2026Proof.Check(otherQuest.QuestProgress == 0, "opponent typed play excluded");
        End(other); Play(other, other.Player1, "TLC_225"); Play(other, other.Player1, "CS2_033");
        Core2026Proof.Check(otherQuest.QuestProgress == 2, "maximum matching must reassign Elemental/Murloc after pure Elemental");
    }
    private static void Unpinned_OwnerTurnsAndRewardClosure(Game g, Controller p, string id)
    {
        End(g); End(g); // no backfill
        var q = (Spell)Play(g, p, id);
        for (int i = 1; i <= 10; i++)
        {
            End(g);
            Core2026Proof.Check(q.QuestProgress == i, "Unpinned_OwnerTurns: completed owner end including installation");
            if (i == 4)
                foreach (var branch in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
                {
                    End(branch); End(branch);
                    Core2026Proof.Check(((Spell)branch.IdEntityDic[q.Id]).QuestProgress == 5 && q.QuestProgress == 4,
                        "mid-turn-count clone/snapshot progresses independently");
                }
            if (i < 10) { End(g); Core2026Proof.Check(q.QuestProgress == i, "opponent end excluded"); }
        }
        if (p.HandZone.IsFull) p.SetasideZone.Add(p.HandZone.Remove(0));
        Core2026Proof.Check(q.Zone == p.GraveyardZone && p.HandZone.Count(c => c.Card.Id == "TLC_602t") == 1,
            "ten completed owner turns grant fixed Latorvius");
        Core2026Proof.Check(LatorviusGazeOfTheCity.Rewards.Length == 9
            && LatorviusGazeOfTheCity.Rewards.All(x => Cards.FromId(x).Implemented), "fixed reward bucket has nine playable legacy recipes");
    }
    private static void Unpinned_IndependentSchoolsAndFreshCombination(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var holy = p.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t");
        var shadow = p.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t2");
        for (int i = 0; i < 3; i++) Play(g, p, "CORE_CS2_093");
        Core2026Proof.Check(holy.QuestProgress == 3 && shadow.QuestProgress == 0, "only Holy component advances");
        var bytes = g.SaveSnapshot();
        foreach (var branch in new[] { g, g.CloneExact(), Game.LoadSnapshot(bytes) })
        {
            var owner = branch.Player1;
            Play(branch, owner, "CORE_CS2_093");
            var life = owner.HandZone.Single(c => c.Card.Id == "TLC_817t3");
            life[GameTag.ATK] = 20; life[GameTag.HEALTH] = 21; life.Cost = 0;
            Generic.AddEnchantmentBlock(branch, Cards.FromId("UNG_999t13e"), life, life);
            var victim = Summon(owner.Opponent, "CS2_182");
            for (int i = 0; i < 4; i++) Play(branch, owner, "CORE_EX1_302", victim);
            var combined = (Minion)owner.HandZone.Single(c => c.Card.Id == "TLC_817t5");
            Core2026Proof.Check(owner.SecretZone.ModernQuests.Count == 0 && combined.Cost == 5
                && combined.AttackDamage == 8 && combined.Health == 8 && !combined.Poisonous,
                "Unpinned_FreshCombination: halves' changes discarded; independent components complete");
        }
        var full = QuestLifecycleProof.Fixture(); var fp = full.Player1;
        var half = Add(fp, "TLC_817t3");
        Play(full, fp, "TLC_817");
        var fq = fp.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t2");
        while (!fp.HandZone.IsFull) Add(fp, "CS2_231");
        QuestRules.Advance(fq, 4);
        Core2026Proof.Check(fp.HandZone.Contains(half) && !fp.HandZone.Any(c => c.Card.Id == "TLC_817t5"), "pending half cannot merge through full hand");
        fp.SetasideZone.Add(fp.HandZone.Remove(fp.HandZone.First(c => c.Id != half.Id)));
        Core2026Proof.Check(fp.HandZone.Count == 9 && fp.HandZone.Count(c => c.Card.Id == "TLC_817t5") == 1,
            "first free slot delivers and combines two halves into one");
    }
    private static void Unpinned_TransformedHalvesMergeBeforeDraw()
    {
        foreach (bool history in new[] { true, false })
        foreach (string source in new[] { "GIL_142", "GAME_005" })
        foreach (int player in new[] { 1, 2 })
        {
            var g = QuestLifecycleProof.Fixture(history);
            var p = player == 1 ? g.Player1 : g.Player2;
            var life = Add(p, "TLC_817t3"); var changing = Add(p, source);
            Add(p.Opponent, "TLC_817t4");
            while (p.HandZone.Count < 10) Add(p, "CS2_231");
            foreach (var branch in new[] { g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = player == 1 ? branch.Player1 : branch.Player2;
                int graveyard = owner.GraveyardZone.Count;
                var next = owner.DeckZone.TopCard;
                Generic.ChangeEntityBlock(owner, branch.IdEntityDic[changing.Id], Cards.FromId("TLC_817t4"), true);
                Core2026Proof.Check(owner.HandZone.Count == 9 && owner.HandZone.Count(c => c.Card.Id == "TLC_817t5") == 1
                    && !owner.HandZone.Any(c => c.Id == life.Id || c.Id == changing.Id),
                    "Unpinned_TransformedHalvesMergeBeforeDraw: same/cross-type transformation settles pair immediately in its owner's hand");
                Generic.Draw(owner);
                Core2026Proof.Check(owner.HandZone.Count == 10 && next.Zone == owner.HandZone
                    && owner.GraveyardZone.Count == graveyard && owner.Opponent.HandZone.Single().Card.Id == "TLC_817t4",
                    "merge frees slot before next draw, without burn or opponent-hand change");
            }
        }

        var turns = QuestLifecycleProof.Fixture(); End(turns);
        var holder = turns.Player1;
        Add(holder, "TLC_817t3"); Add(holder, "GIL_142");
        while (holder.HandZone.Count < 10) Add(holder, "CS2_231");
        foreach (var held in holder.Opponent.HandZone.ToArray()) holder.Opponent.SetasideZone.Add(holder.Opponent.HandZone.Remove(held));
        Add(holder.Opponent, "TLC_817t4");
        foreach (var branch in new[] { turns.CloneExact(), Game.LoadSnapshot(turns.SaveSnapshot()), turns })
        {
            var owner = branch.Player1; var next = owner.DeckZone.TopCard;
            int graveyard = owner.GraveyardZone.Count;
            End(branch);
            Core2026Proof.Check(owner.HandZone.Count == 10 && owner.HandZone.Count(c => c.Card.Id == "TLC_817t5") == 1
                && next.Zone == owner.HandZone && owner.GraveyardZone.Count == graveyard,
                "Chameleos turn-start transformation merges full-hand halves before automatic draw");
        }
    }
    private static void Pick(Game g, int chosen)
    {
        var p = g.CurrentPlayer; var choice = p.Choice;
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(choice.DecisionId, p.Id, new[] { chosen }), out _), "Adapt answer accepted");
    }
    private static void Unpinned_AshalonMemory(bool history)
    {
        var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
        // Find a seed offering +3 Attack twice, retaining the actual selections.
        long seed = Enumerable.Range(1, 1000).First(n => {
            var random = new SabberStoneCore.Model.Util.DeepCloneableRandom(n);
            return AshalonRidgeGuardian.Adaptations.ChooseNElements(3, random).Contains("UNG_999t3")
                && AshalonRidgeGuardian.Adaptations.ChooseNElements(3, random).Contains("UNG_999t3");
        });
        g.SetRandomSeed(seed);
        var source = (Minion)Play(g, p, "TLC_229t14");
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).Distinct().Count() == 3,
            "Adapt offers distinct, ten fixed options");
        Pick(g, p.Choice.Choices.Single(i => g.IdEntityDic[i].Card.Id == "UNG_999t3"));
        var saved = g.SaveSnapshot();
        foreach (var branch in new[] { g, g.CloneExact(), Game.LoadSnapshot(saved) })
        {
            var owner = branch.Player1;
            Pick(branch, owner.Choice.Choices.Single(i => branch.IdEntityDic[i].Card.Id == "UNG_999t3"));
            var ashalon = (Minion)branch.IdEntityDic[source.Id];
            Core2026Proof.Check(ashalon.AttackDamage == 14, "actual duplicate Adapt choices buff Ashalon twice");
            Play(branch, owner, "CS2_203", ashalon); Kill(branch, ashalon);
            var summoned = Summon(owner, "CS2_231");
            Core2026Proof.Check(summoned.AttackDamage == 1, "ordinary summon does not gain memory");
            var later = (Minion)Play(branch, owner, "CS2_231");
            Core2026Proof.Check(later.AttackDamage == 7, "Unpinned_AshalonMemory: survives source Silence/death and snapshot");
            Play(branch, owner, "CS2_203", later);
            Core2026Proof.Check(later.AttackDamage == 1, "Silence removes received Adapt buffs");
            StandardSetsProof.ParkBoard(owner);
            var ooze = (Minion)Play(branch, owner, "FP1_003"); End(branch);
            Core2026Proof.Check(owner.BoardZone.Count == 2 && owner.BoardZone.All(m => m.AttackDamage == 7),
                "memory applies before Echoing Ooze Battlecry captures its buffed state");
            var opponent = (Minion)Play(branch, owner.Opponent, "CS2_231");
            Core2026Proof.Check(opponent.AttackDamage == 1, "opponent plays do not receive controller memory");
            End(branch); StandardSetsProof.ParkBoard(owner);
            Play(branch, owner, "TLC_229t14");
            Pick(branch, owner.Choice.Choices[0]); Pick(branch, owner.Choice.Choices[0]);
            Core2026Proof.Check(owner.AppliedEnchantments.Count(e => e.Card.Id == "TLC_229t14e") == 4,
                "later Ashalon Battlecry appends both actual choices");
        }
        Console.WriteLine("PASS Unpinned_AshalonMemory: ten-option bucket; sequential distinct offers; duplicate memory; clone/snapshot");
    }
    private static void Unpinned_LatorviusRandomOrder()
    {
        foreach (int seed in new[] { 1, 19, 43 })
        foreach (int occupied in new[] { 0, 9 })
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            while (p.HandZone.Count < occupied) Add(p, "CS2_231");
            g.SetRandomSeed(seed);
            var random = new SabberStoneCore.Model.Util.DeepCloneableRandom(seed);
            var expected = LatorviusGazeOfTheCity.Rewards.ChooseNElements(2, random);
            var source = Play(g, p, "TLC_602t");
            var generated = g.IdEntityDic.Values.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
            Core2026Proof.Check(generated.Take(2).Select(c => c.Card.Id).SequenceEqual(expected), "seeded ordered two distinct rewards");
            Core2026Proof.Check(generated.Skip(2).Select(c => c.Card.Id).SequenceEqual(LatorviusGazeOfTheCity.Rewards.Except(expected))
                && generated.Skip(2).All(c => c.Zone == p.DeckZone), "exact remaining seven shuffled to deck");
            Core2026Proof.Check(generated.Count(c => c.Zone == p.HandZone) == (occupied == 0 ? 2 : 1), "ordinary reward generation overflow burns");
            Core2026Proof.Check(generated.Select(c => c.Card.Id).Distinct().Count() == 9, "fixed nine-reward bucket, no Quest spells");
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(g.CloneExact())
                && Canonical.State(g) == Canonical.State(Game.LoadSnapshot(g.SaveSnapshot())), "generated hand/deck rewards clone and snapshot exactly");
        }
    }
    private static void FullDeckRewards()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        while (!p.DeckZone.IsFull) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        g.SetRandomSeed(71);
        var expected = new Util.DeepCloneableRandom(71);
        LatorviusGazeOfTheCity.Rewards.ChooseNElements(2, expected);
        var source = Play(g, p, "TLC_602t");
        Core2026Proof.Check(g.IdEntityDic.Values.Count(c => c[GameTag.DISPLAYED_CREATOR] == source.Id) == 2,
            "full deck does not create orphaned unshufflable rewards");
        Core2026Proof.Check(g.Random.CaptureState().Equals(expected.CaptureState()), "full deck consumes selection RNG only");
        Game.LoadSnapshot(g.SaveSnapshot());
    }

    private static void CurrentUngoroRewards()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        Play(g, p, "UNG_028t");
        int turns = p.NumTurnsLeft;
        Core2026Proof.Check(turns == 2, "current Time Warp grants an extra turn");
        foreach (var branch in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Play(branch, branch.Player1, "UNG_028t");
            Core2026Proof.Check(branch.Player1.NumTurnsLeft == turns, "Time Warp once per game persists through clone/snapshot");
        }
        B15QuestClosureProof.RewindState(g);
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        Play(g, p, "UNG_116t");
        Core2026Proof.Check(p.DeckZone.All(c => c.Cost == 0), "Barnabus sets current deck minions to zero Cost");
        p.Hero.Damage = 20;
        Play(g, p, "UNG_940t8");
        Core2026Proof.Check(p.Hero.Health == 40, "Amara sets hero Health to forty");
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        var small = Summon(p, "CS2_231"); var held = Add(p, "CS2_231");
        Play(g, p, "UNG_067t1");
        Core2026Proof.Check(small.AttackDamage == 5 && small.Health == 5 && ((Minion)held).AttackDamage == 5,
            "current Crystal Core sets existing board and hand minions to 5/5 through aura layer");
        foreach (var branch in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            Core2026Proof.Check(branch.Player1.BoardZone.Single().AttackDamage == 5, "Crystal Core aura survives clone/snapshot");
        var later = (Minion)Play(g, p, "CS2_231");
        Core2026Proof.Check(later.AttackDamage == 5 && later.Health == 5, "Crystal Core affects later plays");
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        int deck = p.DeckZone.Count;
        var queen = (Minion)Play(g, p, "UNG_920t1");
        Core2026Proof.Check(queen.IsRush && p.DeckZone.Count == deck + 20
            && p.DeckZone.Count(c => c.Card.Id == "UNG_920t2") == 20, "current Carnassa shuffles twenty draw-Battlecry Raptors and has Rush");
        Play(g, p, "UNG_934t1");
        Core2026Proof.Check(p.Hero.Weapon.Durability == 2 && p.Hero.HeroPower.Card.Id == "UNG_934t2", "Sulfuras printed durability and Die Insect replacement");
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        var galvadon = (Minion)Play(g, p, "UNG_954t1");
        Core2026Proof.Check(galvadon.AttackDamage == 8 && galvadon.Health == 8, "current Galvadon begins at 8/8");
        for (int i = 0; i < 5; i++)
        {
            Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).Distinct().Count() == 3,
                "Galvadon sequential Adapt offers distinct");
            Pick(g, p.Choice.Choices[0]);
        }
        Core2026Proof.Check(p.Choice == null, "all five Galvadon decisions finish");
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        Play(g, p, "UNG_829t1"); End(g);
        Core2026Proof.Check(p.BoardZone.Count(c => c.Card.Id == "UNG_829t3") == 2, "Nether Portal summons two Imps at owner end");
        End(g);
        Core2026Proof.Check(p.BoardZone.Count(c => c.Card.Id == "UNG_829t3") == 2, "Nether Portal ignores opponent end");
        g = QuestLifecycleProof.Fixture(); p = g.Player1;
        int count = OfflineCardPools.Candidates(g, p.HeroClass, LostCityQuestRewards.Murlocs(), 41498).Count;
        Console.WriteLine("POOL quest-reward-murlocs=" + count + "; fixed UnGoro rewards=9; Adapt options=10");
        Core2026Proof.Check(count >= 3, "Megafin has admitted Murloc candidates");
        Play(g, p, "UNG_942t");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.IsRace(Race.MURLOC)), "Megafin fills hand from admitted inclusive Murloc pool");
    }

    private static void SoletosRewards()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var life = (Minion)Play(g, p, "TLC_817t3");
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.HasTaunt), "Life half summons ordinary copy");
        StandardSetsProof.ParkBoard(p);
        var death = (Minion)Play(g, p, "TLC_817t4");
        Kill(g, death);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && p.BoardZone.Single().Health == 1, "Death half damage then ordinary Reborn");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_231");
        Play(g, p, "TLC_817t5");
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_817t5") == 1, "full board loses Battlecry copy");
    }
}
