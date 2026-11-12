using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BonusEffectsProof
{
    private static System.Type Facility => typeof(Game).Assembly.GetType("SabberStoneCore.Actions.BonusEffects");
    private static object Call(string name, params object[] args) => Facility.GetMethod(name).Invoke(null, args);
    private static GameTag[] Eligible(Minion m) => ((IEnumerable<GameTag>)Call("Eligible", m)).ToArray();
    private static int Give(Minion m, IPlayable source, int count) => (int)Call("GiveRandom", m, source, count);
    private static readonly GameTag[] Pool = { GameTag.TAUNT, GameTag.WINDFURY, GameTag.DIVINE_SHIELD,
        GameTag.POISONOUS, (GameTag)1211, GameTag.RUSH, GameTag.LIFESTEAL, GameTag.REBORN };
    internal static Game NewGame(bool history = true)
    {
        var g = new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, SkipMulligan = true,
            Shuffle = false, History = history, Logging = false });
        g.StartGame();
        return g;
    }
    internal static bool Active(Minion m, GameTag tag) => tag == (GameTag)1211
        ? m.CantBeTargetedBySpells && m.CantBeTargetedByHeroPowers : m[tag] > 0;
    internal static int Count(Minion m) => Pool.Count(t => Active(m, t));
    internal static void Run()
    {
        Core2026Proof.Check(Facility != null, "shared Bonus Effects facility is missing");
        Unpinned_AccumulationWireSpentAndPersistence();
        RushEligibility();
        Unpinned_UniformAndIndependentRecipients();
        Unpinned_Theft();
        Unpinned_CopyExcludesExternalAura();
        Unpinned_PairReplacement();
        Unpinned_DeathRemovesGrantActors();
        BonusCopyRegressionProof.Run();
        Console.WriteLine("PASS Bonus Effects facility: eight, Rush, uniform draws, saturation, spent regrant, theft, pair replacement, wire, clone/snapshot/copy, Silence/bounce");
    }
    private static void Unpinned_DeathRemovesGrantActors()
    {
        var g = NewGame(false); var p = g.Player1; var m = Summon(p, "CS2_182");
        Give(m, p.Hero, 1); m[GameTag.REBORN] = 0;
        foreach (var next in new[] { g, g.CloneExact(history:false), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var original = (Minion)next.IdEntityDic[m.Id];
            var copy = (Minion)Generic.Copy(next.Player1,original,original,Zone.PLAY);
            Kill(next,copy); Kill(next,original);
            Core2026Proof.Check(original.AppliedEnchantments.Count == 0 && copy.AppliedEnchantments.Count == 0,
                "death removes Bonus actors after clone/snapshot/copy despite Sparkbot Magnetic metadata");
        }
        var magnetic = Summon(p, "CS2_182");
        Generic.AddEnchantmentBlock(g,Cards.FromId("TTN_719e1"),p.Hero,magnetic);
        Kill(g,magnetic);
        Core2026Proof.Check(magnetic.AppliedEnchantments.Count == 1, "ordinary Magnetic actor retention unchanged");
    }
    private static void Unpinned_PairReplacement()
    {
        bool overlap = false, rush = false;
        for (int seed = 1; seed <= 64; seed++)
        {
            var g = NewGame(false); var p = g.Player1;
            var m = (Minion)Add(p, "CATA_206");
            Generic.AddEnchantmentBlock(g, Cards.FromId("TTN_719e1"), p.Hero, m);
            Generic.AddEnchantmentBlock(g, Cards.FromId("BE_996e"), p.Hero, m);
            g.SetRandomSeed(seed);
            for (int roll = 0; roll < 3; roll++)
            {
                var old = new[] { m[BonusEffects.Keyword1], m[BonusEffects.Keyword2] }.ToHashSet();
                BonusEffects.ReplacePair(m);
                var pair = new[] { m[BonusEffects.Keyword1], m[BonusEffects.Keyword2] }.ToHashSet();
                Core2026Proof.Check(pair.Count == 2 && pair.All(k => Pool.Contains((GameTag)k)) && !pair.SetEquals(old),
                    "replacement has two pool members, rejecting identical full pair");
                overlap |= pair.Intersect(old).Count() == 1; rush |= pair.Contains((int)GameTag.RUSH);
                Core2026Proof.Check(m.HasTaunt && m.AttackDamage == 8 && m.BaseHealth == 7
                    && Count(m) == (pair.Contains((int)GameTag.TAUNT) ? 2 : 3),
                    "reroll removes only old intrinsic pair and preserves independent keyword/stat grants");
            }
        }
        Core2026Proof.Check(overlap && rush, "one previous member can remain and in-hand Rush is eligible");
    }
    private static void Unpinned_CopyExcludesExternalAura()
    {
        var g = NewGame(); var m = Summon(g.Player1, "CS2_182");
        new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, SabberStoneCore.Enchants.EffectOperator.ADD, 1).ApplyAuraTo(m);
        Give(m, g.Player1.Hero, 1);
        var copy = (Minion)Generic.Copy(g.Player1, m, m, Zone.PLAY);
        Core2026Proof.Check(m.HasLifeSteal && !copy.HasLifeSteal, "copy does not bake external keyword aura into native state");
    }
    private static void Unpinned_Theft()
    {
        var g = NewGame(false); var p = g.Player1;
        var victim = Summon(p.Opponent, "EDR_272"); var thief = Summon(p, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("UNG_952e"), victim, victim);
        int attack = victim.AttackDamage, health = victim.BaseHealth;
        thief.HasTaunt = true;
        int count = (int)Call("Steal", thief, victim);
        Core2026Proof.Check(count == 3 && Count(thief) == 3 && Count(victim) == 0, "steal printed and duplicate granted effects once, including already-owned Taunt");
        Core2026Proof.Check(victim.AttackDamage == attack && victim.BaseHealth == health && victim.HasDeathrattle,
            "theft retains combined buff stats and Deathrattle");
        var copy = (Minion)Generic.Copy(p.Opponent, victim, victim, Zone.PLAY);
        Core2026Proof.Check(Count(copy) == 0 && copy.HasDeathrattle && copy.AttackDamage == attack, "copy preserves stolen suppression and unrelated buffs");
        var spent = Summon(p.Opponent, "TIME_056"); spent.HasDivineShield = false;
        var secondThief = Summon(p, "CS2_182");
        Core2026Proof.Check((int)Call("Steal", secondThief, spent) == 1 && !secondThief.HasDivineShield && secondThief.HasLifeSteal,
            "spent Shield does not count toward theft");
        new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, SabberStoneCore.Enchants.EffectOperator.ADD, 1).ApplyAuraTo(spent);
        Core2026Proof.Check((int)Call("Steal", secondThief, spent) == 1 && spent.HasLifeSteal,
            "external ongoing aura retains its contribution after theft");
    }
    private static void Unpinned_AccumulationWireSpentAndPersistence()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = NewGame(history); var p = g.Player1;
            var m = Summon(p, "CS2_182");
            Core2026Proof.Check(Eligible(m).SequenceEqual(Pool), "fresh minion eligible for exactly eight effects");
            int start = g.PowerHistory?.Full.Count ?? 0;
            Core2026Proof.Check(Give(m, p.Hero, 20) == 8 && Count(m) == 8 && Eligible(m).Length == 0,
                "grant min(requested, available), distinct and saturated");
            Core2026Proof.Check(m.AppliedEnchantments.Count == 8, "each grant retains an enchantment even without history");
            if (history)
            {
                var entries = g.PowerHistory.Full.Skip(start).ToArray();
                foreach (var e in m.AppliedEnchantments)
                {
                    int show = Array.FindIndex(entries, x => x is PowerHistoryShowEntity s && s.Entity.Id == e.Id && s.Entity.Name == e.Card.Id);
                    Core2026Proof.Check(show >= 0 && entries.Skip(show + 1).Any(x => x is PowerHistoryTagChange t && t.EntityId == m.Id),
                        "real enchantment reveal precedes recipient keyword update");
                }
            }
            var before = Canonical.State(g);
            var randomBefore = g.Random.CaptureState();
            Core2026Proof.Check(Give(m, p.Hero, 3) == 0 && before == Canonical.State(g)
                && randomBefore.Equals(g.Random.CaptureState()), "saturated grant mutates nothing and consumes no RNG");
            m.HasDivineShield = false; m[GameTag.REBORN] = 0; m[GameTag.HAS_BEEN_REBORN] = 1;
            Core2026Proof.Check(Eligible(m).SequenceEqual(new[] { GameTag.DIVINE_SHIELD, GameTag.REBORN }), "spent keywords become eligible");
            var clone = g.CloneExact(history: history); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            foreach (var next in new[] { g, clone, loaded })
            {
                var recipient = (Minion)next.IdEntityDic[m.Id];
                Core2026Proof.Check(Give(recipient, next.Player1.Hero, 2) == 2 && recipient.HasDivineShield && recipient[GameTag.REBORN] == 1,
                    "spent shield and Reborn restore active charge");
            }
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "RNG and grant state survive clone/snapshot");
            var copy = (Minion)Generic.Copy(p, p.Hero, m, Zone.PLAY);
            Core2026Proof.Check(Count(copy) == 8 && copy.AppliedEnchantments.Count == m.AppliedEnchantments.Count, "board copy retains realized keywords and attachments once");
            copy.Silence(); Core2026Proof.Check(Count(copy) == 0 && Count(m) == 8 && copy.AppliedEnchantments.Count == 0, "Silence removes grants only on recipient");
            End(g); End(g); Core2026Proof.Check(Count(m) == 8, "ordinary grants do not expire at turn end");
            Generic.ReturnToHandBlock(p, m);
            Core2026Proof.Check(Count(m) == 0 && m.AppliedEnchantments.Count == 0, "backward move clears keywords and grant actors");
        }
    }
    private static void RushEligibility()
    {
        var g = NewGame(); var m = Summon(g.Player1, "CS2_182");
        Core2026Proof.Check(Eligible(m).Contains(GameTag.RUSH), "fresh exhausted minion can gain Rush");
        m.IsExhausted = false; Core2026Proof.Check(!Eligible(m).Contains(GameTag.RUSH), "ready minion excludes Rush");
        m.IsFrozen = true; Core2026Proof.Check(Eligible(m).Contains(GameTag.RUSH), "frozen minion cannot already attack");
        m.IsFrozen = false; m.AttackDamage = 0; Core2026Proof.Check(Eligible(m).Contains(GameTag.RUSH), "zero attack cannot already attack");
        m.AttackDamage = 2; m.NumAttacksThisTurn = 1; m.HasWindfury = true;
        Core2026Proof.Check(!Eligible(m).Contains(GameTag.RUSH), "already attacked excludes Rush even with remaining Windfury attack");
        End(g); Core2026Proof.Check(!Eligible(m).Contains(GameTag.RUSH), "opponent turn excludes Rush");
    }
    private static void Unpinned_UniformAndIndependentRecipients()
    {
        var seen = new HashSet<GameTag>();
        for (int seed = 1; seed <= 64; seed++)
        {
            var g = NewGame(false); g.SetRandomSeed(seed);
            var a = Summon(g.Player1, "CS2_182"); var b = Summon(g.Player1, "CS2_182");
            var expected = new SabberStoneCore.Model.Util.DeepCloneableRandom(seed);
            Give(a, g.Player1.Hero, 1); Give(b, g.Player1.Hero, 1);
            var first = Pool[expected.Next(8)]; var second = Pool[expected.Next(8)];
            Core2026Proof.Check(Active(a, first) && Active(b, second) && Count(a) == 1 && Count(b) == 1,
                "one uniform game-RNG index per recipient, no cross-recipient exclusions");
            seen.Add(first);
        }
        Core2026Proof.Check(seen.Count == 8, "all eight keywords reachable");
    }
}
