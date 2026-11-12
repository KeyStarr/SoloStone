using HsSim;
using SabberStoneCore.Kettle;
using SabberStoneCore.Auras;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class B15HeroPowersProof
{
    internal static Game Make(CardClass cls = CardClass.DEATHKNIGHT, int seed = 1, bool history = true)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE, EnableCorpses = true,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), History = history, Logging = false });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 }) {
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            p.BaseMana = 10;
        }
        return g;
    }
    internal static void Replay(Game g, Action<Game> action)
    {
        var clone = g.CloneExact(history: true); var load = Game.LoadSnapshot(g.SaveSnapshot());
        string before = Canonical.State(g);
        action(clone);
        Core2026Proof.Check(Canonical.State(g) == before, "branch cannot mutate parent");
        action(load); action(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(load),
            "same future actions/RNG after exact clone and snapshot");
    }
    internal static void Use(Game g, HeroPower power, ICharacter target = null)
    {
        var p = power.Controller;
        var task = p.Options().SingleOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER
            && (t.Source?.Id ?? p.Hero.HeroPower.Id) == power.Id && t.Target?.Id == target?.Id);
        Core2026Proof.Check(task != null && g.Process(task), "source-specific legal power use");
    }
    [GroupProof("b15-heropowers")]
    internal static void Run()
    {
        Unpinned_ThalenaRepeatRetainsSecondaryExhaustion();
        Unpinned_SulfurasRefreshAndRestore();
        Unpinned_SulfurasRestoresActiveCostAurasExactlyOnce();
        CorpseCardPayment();
        SecondaryCostAuras();
        SecondarySurvivesRewind();
        GennStartingClasses();
        GhoulFrenzyInitializesAttackBeforeSummonTriggers();
        ExistingPowerAuras();
        UnrelatedTargetTriggersPreserveLegacyPowerState();
        Unpinned_PortalDrawUsesSpellLifecycle();
        Unpinned_GennHandEntryAndRemovalCheckpoint();
        GennRetainsBuffLayersWithoutHistory();
        Unpinned_PortalPoolsAndDraw();
        Console.WriteLine("PASS b15 Hero Power facility: dual slots/payment/refresh; restore; clone/snapshot");
    }
    private static void Unpinned_ThalenaRepeatRetainsSecondaryExhaustion()
    {
        var g = Make(); var p = g.Player1; var primary = p.Hero.HeroPower;
        var body = Summon(p, "CS2_182");
        var second = BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp");
        Core2026Proof.Check(BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp") == second && p.HeroPowers.Count() == 2
            && p.Hero.HeroPower == primary && !primary.IsExhausted && !second.IsExhausted, "idempotent ready installation");
        p.BaseMana = 0; p[GameTag.CORPSES] = 2;
        Core2026Proof.Check(!second.IsPlayable && !p.Options().Any(t => t.Source == second), "insufficient corpses hides option with zero mana");
        p[GameTag.CORPSES] = 6;
        var stale = p.Options().First(t => t.Source == second && t.Target == body);
        var native = SabberStoneCore.Kettle.PowerOptionsBuilder.AllOptions(g, p.Options());
        Core2026Proof.Check(native.PowerOptionList.Any(o => o.MainOption?.EntityId == second.Id
            && o.MainOption.Targets.Contains(body.Id)), "Kettle slot selection is by entity source");
        Replay(g, x => Use(x, x.Player1.SecondaryHeroPower, (ICharacter)x.IdEntityDic[body.Id]));
        Core2026Proof.Check(body.AttackDamage == 7 && p.UsedMana == 0 && CorpseRules.Available(p) == 3
            && second.IsExhausted && !primary.IsExhausted, "one corpse payment; secondary independent exhaustion");
        string before = Canonical.State(g);
        Core2026Proof.Check(!g.Process(stale) && Canonical.State(g) == before, "stale secondary option cannot spend twice");
        BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp");
        Core2026Proof.Check(second.IsExhausted && p.SecondaryHeroPower.Id == second.Id, "repeat preserves used power");
        body.Silence(); Core2026Proof.Check(body.AttackDamage == 4, "Kiss Attack is removable enchantment");
        End(g); Core2026Proof.Check(second.IsExhausted, "opponent start never refreshes owner power");
        End(g); Core2026Proof.Check(!second.IsExhausted, "next owner turn refreshes second slot");
        p.BaseMana = 10; p.UsedMana = 0;
        Use(g, primary); Use(g, second, body);
        Core2026Proof.Check(primary.IsExhausted && second.IsExhausted, "both powers independently usable in one turn");
        var detached = (HeroPower)Entity.FromCard(p, Cards.FromId("JAIL_446hp"));
        Core2026Proof.Check(!Generic.HeroPower(p, body, power: detached), "uninstalled power cannot activate");
        Core2026Proof.Check(!Generic.HeroPower(p.Opponent, body, power: second), "opponent cannot spend owner's power");
    }
    private static void Unpinned_SulfurasRefreshAndRestore()
    {
        var g = Make(CardClass.WARRIOR); var p = g.Player1; var original = p.Hero.HeroPower;
        SabberStoneCore.Actions.CostRules.AddModifier((Playable)original, EffectOperator.ADD, -1);
        Use(g, original);
        BasicHeroPowerRules.SwapForTwoUses(p, p.Hero);
        Use(g, p.Hero.HeroPower);
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "TLC_632t2" && p.Hero.HeroPower.IsExhausted, "first use changes last-use identity without refreshing");
        BasicHeroPowerRules.SwapForTwoUses(p, p.Hero);
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "TLC_632t" && !p.Hero.HeroPower.IsExhausted, "recast starts two fresh uses");
        Replay(g, x => {
            Use(x, x.Player1.Hero.HeroPower); End(x); End(x);
            x.Player1.BaseMana = 10; x.Player1.UsedMana = 0;
            Use(x, x.Player1.Hero.HeroPower);
            Core2026Proof.Check(x.Player1.Hero.HeroPower.Id == original.Id && !x.Player1.Hero.HeroPower.IsExhausted
                && x.Player1.Hero.HeroPower.Cost == 1, "restore original entity/modifiers with fresh allowance, no nested layer");
            Use(x, x.Player1.Hero.HeroPower);
        });
        BasicHeroPowerRules.SwapForTwoUses(p, p.Hero);
        var parked = p.Hero.HeroPower;
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(g, p, p.Hero, null);
        BasicHeroPowerRules.AfterUse(parked);
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "CS2_034", "permanent replacement interrupts restoration");
    }
    private static void Unpinned_SulfurasRestoresActiveCostAurasExactlyOnce()
    {
        foreach (bool beforeSwap in new[] { false, true })
        foreach (bool maiden in new[] { true, false })
        {
            var g = Make(CardClass.WARRIOR); var p = g.Player1; int original = p.Hero.HeroPower.Id;
            Minion source = null;
            void AddAura()
            {
                source = Summon(p, maiden ? "AT_085" : "CS2_182");
                if (!maiden) new Aura(AuraType.HEROPOWER, Effects.ReduceCost(1)).Activate(source);
                g.DeathProcessingAndAuraUpdate();
            }
            if (beforeSwap) AddAura();
            Play(g, p, "TLC_632");
            if (!beforeSwap) AddAura();
            p.UsedMana = 0;
            Use(g, p.Hero.HeroPower);
            Replay(g, x => {
                End(x); End(x); x.Player1.BaseMana = 10; x.Player1.UsedMana = 0;
                Use(x, x.Player1.Hero.HeroPower);
                var restored = x.Player1.Hero.HeroPower;
                Core2026Proof.Check(restored.Id == original && restored.Cost == 1,
                    "Sulfuras restores active cost aura exactly once; before=" + beforeSwap + "; Maiden=" + maiden + "; cost=" + restored.Cost);
                BasicHeroPowerRules.EnrollPowerAuras(x.Player1, restored);
                BasicHeroPowerRules.EnrollPowerAuras(x.Player1, restored);
                x.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(restored.Cost == 1, "repeated enrollment cannot stack a retained cost aura");
                ((Minion)x.IdEntityDic[source.Id]).Silence(); x.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(restored.Cost == 2, "Silence removes exactly one restored cost aura");
            });
        }
    }
    private static void CorpseCardPayment()
    {
        var g = Make(); var p = g.Player1; var card = Add(p, "TLC_436"); p.BaseMana = 0;
        p[GameTag.CORPSES] = 4;
        Core2026Proof.Check(!card.IsPlayableByPlayer && !Generic.PlayCard(p, card), "corpse minion cannot use mana or underpay");
        p[GameTag.CORPSES] = 5;
        Replay(g, x => {
            var c = x.Player1;
            Core2026Proof.Check(x.Process(PlayCardTask.Minion(c, x.IdEntityDic[card.Id])), "corpse minion admitted without mana");
            Core2026Proof.Check(CorpseRules.Available(c) == 0 && c.UsedMana == 0, "corpse minion debits exact shared quote");
        });
    }
    private static void Unpinned_PortalPoolsAndDraw()
    {
        var sample = Make(CardClass.PALADIN);
        OfflinePoolAdmission.Evaluate(sample.OfflinePools).ValidateRegistered();
        var dragons = OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DRAGON);
        Console.WriteLine("b15 Portal Dragon buckets 1..10: " + string.Join(",", Enumerable.Range(1, 10)
            .Select(n => OfflineCardPools.Candidates(sample, CardClass.PALADIN, dragons.WithCost(n), 114070).Count)));
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = Make(CardClass.PALADIN, seed); var p = g.Player1;
            var filter = OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);
            var candidates = OfflineCardPools.Candidates(g, CardClass.PALADIN, filter, 116675);
            Console.WriteLine("b15 Aegis 2-Cost bucket: " + candidates.Count);
            var spell = Add(p, "EDR_264"); g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            string expected = candidates[oracle.Random.Next(candidates.Count)].Id;
            PlayEntity(g, p, spell);
            Core2026Proof.Check(p.BoardZone.Single().Card.Id == expected, "Aegis seeded pool order known answer");
        }
        foreach (bool fullBoard in new[] { false, true })
        foreach (bool fullHand in new[] { false, true })
        {
            var g = Make(CardClass.PALADIN); var p = g.Player1;
            ImbueRules.Imbue(p, p.Hero);
            if (fullBoard) while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            if (fullHand) while (p.HandZone.Count < 10) Add(p, "CS2_182");
            var portal = Entity.FromCard(p, Cards.FromId("EDR_445pt3"), zone: p.DeckZone);
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, board = p.BoardZone.Count;
            Replay(g, x => Generic.Draw(x.Player1, x.IdEntityDic[portal.Id]));
            Core2026Proof.Check(p.UsedMana == 0 && p.HandZone.Count == (fullHand ? hand : hand + 1)
                && p.DeckZone.Count == deck - (fullHand ? 1 : 2)
                && p.BoardZone.Count == board + (!fullHand && !fullBoard ? 1 : 0),
                "drawn Portal: free cast/redraw; burn no cast/redraw; capacity affects only summon");
        }
        var empty = Make(CardClass.PALADIN); var owner = empty.Player1;
        ImbueRules.Imbue(owner, owner.Hero, 30);
        Core2026Proof.Check(OfflineCardPools.Candidates(empty, CardClass.PALADIN, dragons.WithCost(30), 114070).Count == 0,
            "D2 30-Cost bucket is empty");
        Play(empty, owner, "EDR_445pt3");
        Core2026Proof.Check(owner.BoardZone.Count == 0, "empty exact bucket yields nothing, never nearest cost");
    }

    private static void SecondaryCostAuras()
    {
        var g = Make(); var p = g.Player1;
        var fletcher = Summon(p, "TIME_606");
        var second = BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.HeroPower.Cost == 0 && second.Cost == 0, "existing conditional power-cost aura reaches installed second slot");
        Replay(g, x => {
            ((Minion)x.IdEntityDic[fletcher.Id]).Silence(); x.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(x.Player1.Hero.HeroPower.Cost == 2 && x.Player1.SecondaryHeroPower.Cost == 3,
                "Silence removes each power-cost aura without residue");
            CostRules.AddNextHeroPowerRule(x.Player1, setCost: 0, expiryTurn: x.Turn);
            Use(x, x.Player1.SecondaryHeroPower, x.Player1.BoardZone.First());
            Core2026Proof.Check(x.Player1.SecondaryHeroPower.Cost == 3 && CorpseRules.Available(x.Player1) == 0,
                "effective zero-Corpse use consumes next-power cost rule");
        });
    }

    private static void SecondarySurvivesRewind()
    {
        var g = Make(CardClass.ROGUE); var p = g.Player1;
        var body = Summon(p, "CS2_182"); p[GameTag.CORPSES] = 6;
        var second = BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp");
        ImbueRules.Imbue(p, p.Hero);
        Use(g, second, body);
        Core2026Proof.Check(g.Rewind == null, "secondary use never starts primary Bronze Rewind");
        Use(g, p.Hero.HeroPower);
        Core2026Proof.Check(g.Rewind != null, "primary Bronze opens normal Rewind beside secondary");
        Replay(g, x => {
            var transaction = x.Rewind;
            Core2026Proof.Check(x.TryAnswer(new DecisionAnswer(transaction.OfferChoiceId, x.Player1.Id,
                new[] { transaction.RewindEntityId }), out var error), "Rewind accepted: " + error);
            Core2026Proof.Check(x.Rewind == null && x.Player1.SecondaryHeroPower.Id == second.Id
                && x.Player1.SecondaryHeroPower.IsExhausted && CorpseRules.Available(x.Player1) == 3
                && ((Minion)x.IdEntityDic[body.Id]).AttackDamage == 7,
                "baseline import preserves independent secondary state, Corpse payment and buff");
        });
    }

    private static void GennStartingClasses()
    {
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, (CardClass)14, CardClass.DRUID, CardClass.HUNTER,
            CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR })
        {
            var g = Make(cls); var p = g.Player1; int old = p.Hero.HeroPower.Id;
            Play(g, p, "CATA_615t");
            Core2026Proof.Check(p.Hero.HeroPower.Id != old && p.Hero.HeroPower.Cost == 1, "Genn upgrades starting basic class " + cls);
            if (cls == CardClass.DEATHKNIGHT || (int)cls == 14)
            {
                Use(g, p.Hero.HeroPower);
                Core2026Proof.Check(cls == CardClass.DEATHKNIGHT ? p.BoardZone.Last().Card.Id == "HERO_11bpt" && p.BoardZone.Last().AttackDamage == 2
                    : p.Hero.AttackDamage == 2, "new upgraded basic power exact effect " + cls);
                End(g);
                Core2026Proof.Check(cls == CardClass.DEATHKNIGHT ? p.BoardZone.All(m => m.Card.Id != "HERO_11bpt")
                    : p.Hero.AttackDamage == 0, "upgraded basic power normal end-of-turn expiry");
            }
        }
    }

    private static void GhoulFrenzyInitializesAttackBeforeSummonTriggers()
    {
        var g = Make(); var p = g.Player1;
        Play(g, p, "CATA_615t"); Play(g, p, "EX1_366");
        int durability = p.Hero.Weapon.Durability;
        Replay(g, x => {
            Use(x, x.Player1.Hero.HeroPower);
            var ghoul = x.Player1.BoardZone.Last();
            Core2026Proof.Check(ghoul.Card.Id == "HERO_11bpt" && ghoul.AttackDamage == 3 && ghoul.Health == 2,
                "Ghoul Frenzy 2/1 plus Sword of Justice must be 3/2; got " + ghoul.AttackDamage + "/" + ghoul.Health);
            Core2026Proof.Check(x.Player1.Hero.Weapon.Durability == durability - 1, "Ghoul summon triggers Sword exactly once");
            ghoul.Silence();
            Core2026Proof.Check(ghoul.AttackDamage == 1 && ghoul.Health == 1, "Silence restores printed Ghoul stats");
        });
    }

    private static void ExistingPowerAuras()
    {
        var g = Make(); var p = g.Player1; var body = Summon(p, "CS2_182");
        new Aura(AuraType.HEROPOWER, Effects.ReduceCost(1)).Activate(body);
        g.DeathProcessingAndAuraUpdate();
        var second = BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.HeroPower.Cost == 1 && second.Cost == 2, "already-active power aura admits new secondary");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.HeroPower.Cost == 2 && second.Cost == 3, "general aura cleanup removes both power recipients");
    }

    private static void UnrelatedTargetTriggersPreserveLegacyPowerState()
    {
        var g = Make(CardClass.MAGE); var p = g.Player1;
        g.TriggerManager.TargetTrigger += _ => { };
        Use(g, p.Hero.HeroPower, p.Opponent.Hero);
        Core2026Proof.Check(!p.Hero.HeroPower.NativeTags.ContainsKey(GameTag.CARD_TARGET),
            "unrelated Target listeners must not add a Hero Power target tag");
    }

    private static void Unpinned_PortalDrawUsesSpellLifecycle()
    {
        var g = Make(CardClass.PALADIN); var p = g.Player1; ImbueRules.Imbue(p, p.Hero);
        var portal = Entity.FromCard(p, Cards.FromId("EDR_445pt3"), zone: p.DeckZone);
        Generic.Draw(p, portal);
        Core2026Proof.Check(portal.Zone == p.GraveyardZone && g.PowerHistory.Full.OfType<PowerHistoryBlockStart>()
            .Count(b => b.Source == portal.Id && b.BlockType == BlockType.POWER) == 1,
            "drawn Portal uses normal automatic spell POWER/exit before replacement draw");
    }

    private static void Unpinned_GennHandEntryAndRemovalCheckpoint()
    {
        var g = Make(CardClass.ROGUE); var p = g.Player1;
        var only = Add(p, "CATA_615");
        Core2026Proof.Check(only.Card.Id == "CATA_615t", "Genn drawn into empty hand transforms at hand entry");
        Add(p, "CS2_171"); Add(p, "CS2_182");
        Core2026Proof.Check(only.Card.Id == "CATA_615t", "later mixed entries cannot revert completed transform");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var odd = Add(p, "CS2_171"); Add(p, "CS2_182"); var genn = Add(p, "CATA_615");
        Core2026Proof.Check(genn.Card.Id == "CATA_615", "mixed hand keeps Cursed King");
        p.SetasideZone.Add(p.HandZone.Remove(odd));
        Core2026Proof.Check(genn.Card.Id == "CATA_615t", "last parity breaker removal transforms before next effect");
    }

    private static void GennRetainsBuffLayersWithoutHistory()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = Make(CardClass.ROGUE, history: history); var p = g.Player1;
            var odd = Add(p, "CS2_171"); Add(p, "CS2_182"); var genn = (Minion)Add(p, "CATA_615");
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_092e"), p.Hero, genn);
            Core2026Proof.Check(genn.AttackDamage == 9 && genn.Health == 8, "Kings fixture +4/+4 before hand transform");
            Replay(g, x => {
                var owner = x.Player1; owner.SetasideZone.Add(owner.HandZone.Remove(x.IdEntityDic[odd.Id]));
                var transformed = (Minion)x.IdEntityDic[genn.Id];
                Core2026Proof.Check(transformed.Card.Id == "CATA_615t" && transformed.AttackDamage == 10 && transformed.Health == 9,
                    "hand transform retains removable buff layers, History=" + history);
                PlayEntity(x, owner, transformed); transformed.Silence();
                Core2026Proof.Check(transformed.AttackDamage == 6 && transformed.Health == 5, "Silence restores new-form base stats");
            });
        }
    }

}
