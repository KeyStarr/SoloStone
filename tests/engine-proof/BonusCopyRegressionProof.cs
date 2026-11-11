using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class BonusCopyRegressionProof
{
    internal static void Unpinned_StranglevineDormant()
    {
        foreach (bool activeRecipient in new[] { false, true })
        for (int seed = 1; seed <= 16; seed++)
        {
            var g = BonusEffectsProof.NewGame(); var p = g.Player1;
            var sleeper = Summon(p, "TIME_046"); var source = Summon(p, "TLC_465");
            var active = activeRecipient ? Summon(p, "CS2_182") : null;
            int before = sleeper.AppliedEnchantments.Count;
            Core2026Proof.Check(sleeper.IsDormant, "fixture asleep");
            g.SetRandomSeed(seed); var rng = g.Random.CaptureState();
            Kill(g, source);
            Core2026Proof.Check(sleeper.AppliedEnchantments.Count == before && !sleeper.HasDeathrattle,
                "Stranglevine must not grant a Bonus Effect or Deathrattle to Dormant minions");
            Core2026Proof.Check(active != null
                ? BonusEffectsProof.Count(active) == 1 && active.HasDeathrattle
                : rng.Equals(g.Random.CaptureState()),
                "filter recipients before selection; empty eligible board consumes no RNG");
        }
    }

    internal static void Unpinned_RaptorDormant()
    {
        var g = BonusEffectsProof.NewGame(); var p = g.Player1;
        Play(g, p, "EDR_849"); var sleeper = (Minion)Play(g, p, "TIME_046");
        Core2026Proof.Check(sleeper.IsDormant, "fixture asleep");
        Core2026Proof.Check(!sleeper.AppliedEnchantments.Any(e => e.Card.Id.StartsWith("TTN_719")),
            "Raptor must not grant a Bonus Effect to a played Dormant minion");
    }

    private static Minion Copy(Game g, Controller p, Minion original, string path)
    {
        if (path == "generic") return (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY);
        if (path == "mirage")
        {
            Play(g, p, "UNG_022", original);
            return p.BoardZone.Single(m => m.Card.Id == original.Card.Id && m.Id != original.Id);
        }
        if (path == "faceless") return (Minion)Play(g, p, "EX1_564", original);
        if (path == "molten")
        {
            Play(g, p, "UNG_948", original);
            return p.BoardZone.Single(m => m.Id != original.Id);
        }
        var stack = new TaskStack();
        new SummonCopyTask(EntityType.TARGET, addToStack: true).Process(g, p, p.Hero, original, stack);
        return (Minion)stack.Playables.Single();
    }

    internal static void Run()
    {
        int failed = 0;
        void Test(string name, Action test)
        {
            try { test(); Console.WriteLine("PASS Bonus regression " + name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL Bonus regression " + name + ": " + e.Message); }
        }
        Test("DormantGrantNoOp", () => {
            var g = BonusEffectsProof.NewGame(); var p = g.Player1;
            var sleeper = Summon(p, "TIME_046");
            int before = sleeper.AppliedEnchantments.Count;
            var rng = g.Random.CaptureState();
            Core2026Proof.Check(BonusEffects.Eligible(sleeper).Length == 0
                && BonusEffects.GiveRandom(sleeper, p.Hero, 8) == 0
                && sleeper.AppliedEnchantments.Count == before && rng.Equals(g.Random.CaptureState()),
                "Dormant grant must not change attachments or consume RNG");
        });
        Test("StranglevineDormant", Unpinned_StranglevineDormant);
        Test("RaptorDormant", Unpinned_RaptorDormant);
        foreach (bool history in new[] { false, true })
        foreach (string path in new[] { "generic", "mirage", "faceless", "molten", "summon" })
        Test($"RegrantedShield/{path}/history={history}", () => {
            var g = BonusEffectsProof.NewGame(history); var p = g.Player1;
            var original = Summon(p, "EX1_008"); original.Silence();
            Play(g, p, "EX1_371", original);
            Core2026Proof.Check(original.IsSilenced && original.HasDivineShield, "fixture re-granted Shield");
            var copy = Copy(g, p, original, path);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.IsSilenced && copy.HasDivineShield, "copy retains Shield granted after Silence");
            foreach (var branch in new[] { g, g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)branch.IdEntityDic[copy.Id];
                Core2026Proof.Check(copied.IsSilenced && copied.HasDivineShield,
                    "copy retains Shield granted after Silence, including after clone/snapshot");
                int health = copied.Health;
                copied.TakeDamage(branch.Player2.Hero, 1);
                Core2026Proof.Check(!copied.HasDivineShield && copied.Health == health && original.HasDivineShield,
                    "copied Shield absorbs damage independently");
                copied.Silence();
                Core2026Proof.Check(!copied.HasDivineShield, "second Silence keeps spent Shield removed");
            }
        });
        foreach (bool history in new[] { false, true })
        foreach (bool granted in new[] { false, true })
        Test($"RealizedSilencedKeywords/granted={granted}/history={history}", () => {
            var g = BonusEffectsProof.NewGame(history); var p = g.Player1;
            var original = Summon(p, "CS2_182"); original.Silence();
            var keywords = new[] { GameTag.CHARGE, GameTag.RUSH, GameTag.WINDFURY, GameTag.POISONOUS,
                GameTag.LIFESTEAL, GameTag.REBORN, GameTag.SPELLPOWER };
            if (granted) foreach (var keyword in keywords) original[keyword] = 1;
            var copy = Copy(g, p, original, "generic");
            Core2026Proof.Check(copy.IsSilenced && keywords.All(k => copy[k] == (granted ? 1 : 0)),
                "suppress printed powers without erasing realized post-Silence keyword grants");
        });
        foreach (bool history in new[] { false, true })
        foreach (string path in new[] { "generic", "faceless", "summon" })
        Test($"BonusCleanup/{path}/history={history}", () => {
            var g = BonusEffectsProof.NewGame(history); var p = g.Player1;
            var original = Summon(p, "GVG_085");
            BonusEffects.GiveRandom(original, p.Hero, 8); original[GameTag.REBORN] = 0;
            var copy = Copy(g, p, original, path);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.Card.Id == original.Card.Id && BonusEffectsProof.Count(copy) == 7,
                "copy retains realized keywords and spent Reborn");
            foreach (var branch in new[] { g, g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)branch.IdEntityDic[copy.Id];
                Kill(branch, copied);
                Core2026Proof.Check(copied.AppliedEnchantments.Count == 0, path + " death removes all Bonus actors");
                Play(branch, branch.Player1, "BOT_912");
                var revived = branch.Player1.BoardZone.Single(m => m.Id != original.Id);
                Core2026Proof.Check(BonusEffectsProof.Count(revived) == 2,
                    path + " Kangor resurrects only printed Taunt/Shield, without fake Magnetic upgrades");
            }
        });
        foreach (bool history in new[] { false, true })
        foreach (string path in new[] { "generic", "faceless", "molten" })
        Test($"SilencedPrintedDeathrattle/{path}/history={history}", () => {
            var g = BonusEffectsProof.NewGame(history); var p = g.Player1;
            var original = Summon(p, "EX1_029"); original.Silence();
            var copy = Copy(g, p, original, path);
            Core2026Proof.Check(copy.IsSilenced && !copy.HasDeathrattle,
                path + " must not restore a silenced Leper Gnome's printed Deathrattle");
            int health = p.Opponent.Hero.Health;
            Kill(g, copy);
            Core2026Proof.Check(p.Opponent.Hero.Health == health, "copied silenced Leper does not damage enemy hero");
        });
        foreach (bool history in new[] { false, true })
        foreach (string path in new[] { "generic", "faceless", "molten" })
        Test($"SilencedIronbornCopy/{path}/history={history}", () => {
            var g = BonusEffectsProof.NewGame(history); var p = g.Player1;
            var original = Summon(p, "BE_996");
            BonusEffects.GiveRandom(original, p.Hero, 1); original.Silence();
            var copy = Copy(g, p, original, path);
            g.DeathProcessingAndAuraUpdate();
            foreach (var branch in new[] { g, g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)branch.IdEntityDic[copy.Id];
                Kill(branch, Summon(branch.Player2, "CS2_182"));
                Core2026Proof.Check(copied.IsSilenced && copied.AttackDamage == 3 && BonusEffectsProof.Count(copied) == 0,
                    path + " must not restore silenced Ironborn's printed trigger on copy entry or resume");
            }
        });
        if (failed > 0) throw new Exception($"{failed} Bonus regression failures");
    }
}
