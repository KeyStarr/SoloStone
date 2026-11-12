using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ArchaedasPayloadProof
{
    internal const int Key = 18055;
    [GroupProof("b18-t-archaedas")]
    internal static void Run()
    {
        Core2026Proof.Check(Cards.All.Any(c => c.Id == "BE_055t" && c.Implemented), "Archaedas weapon payload facility registered");
        Unpinned_Archaedas_WeaponCopyPayload();
        Unpinned_Archaedas_SeededTargets();
        Unpinned_Archaedas_FinalSwingAndRemoval();
        Unpinned_Archaedas_MissingPayloadAndAutomaticChoice();
        Unpinned_Archaedas_Rewind();
        Console.WriteLine("PASS Archaedas payload: copy/clone/load/Rewind, final swing/removal, automatic choices and seeded targets");
    }

    internal static Weapon Hammer(Controller p, string spellId = "CS2_024")
    {
        var spell = Add(p, spellId); var definition = spell.Card; int cost = spell.Cost;
        var hammer = (Weapon)Generic.ChangeEntityBlock(p, spell, Cards.FromId("BE_055t"), true);
        PayloadRules.StoreCard(hammer, Key, definition);
        CostRules.AddModifier(hammer, SabberStoneCore.Enchants.EffectOperator.SET, cost);
        p.Game.AuraUpdate();
        return hammer;
    }

    private static void Unpinned_Archaedas_WeaponCopyPayload()
    {
        var g = ImbueProof.Make(CardClass.WARRIOR); var p = g.Player1;
        var hammer = Hammer(p, "CS2_024");
        var copy = Generic.Copy(p, p.Hero, hammer, Zone.HAND); g.AuraUpdate();
        Core2026Proof.Check(PayloadRules.Entries(copy, Key).Single().Id != PayloadRules.Entries(hammer, Key).Single().Id,
            "weapon copy owns independent spell payload");
        var copiedSpell = PayloadRules.Entries(copy, Key).Single();
        Core2026Proof.Check(copy.Cost == 2 && copiedSpell.Card.Id == "CS2_024"
            && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == copiedSpell.Id,
            "weapon copy preserves selected Cost and references its own spell entity (client reads tag 2 as an entity id)");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1; var weapon = (Weapon)next.IdEntityDic[copy.Id];
            PayloadRules.Clear(next.IdEntityDic[hammer.Id], PayloadPolicy.Silence);
            PlayEntity(next, owner, weapon);
            int before = next.Player1.Hero.Damage + next.Player2.Hero.Damage;
            Core2026Proof.Check(next.Process(HeroAttackTask.Any(owner, next.Player2.Hero)), "payload weapon attack accepted");
            Core2026Proof.Check(next.Player1.Hero.Damage + next.Player2.Hero.Damage == before + 6,
                "3 weapon combat plus independent 3 damage spell after attack");
            Core2026Proof.Check(next.Player1.Choice == null && next.Player2.Choice == null, "cast never leaves a manual decision");
        }
    }

    private static void Unpinned_Archaedas_SeededTargets()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (var sample in new[] { (Seed: 3L, Index: 3), (Seed: 11L, Index: 2), (Seed: 29L, Index: 1) })
        {
            var g = ImbueProof.Make(CardClass.WARRIOR); var p = g.Player1;
            var weapon = Hammer(p); PlayEntity(g, p, weapon);
            var own = Summon(p, reverse ? "CS2_200" : "CS2_182");
            var enemy = Summon(p.Opponent, reverse ? "CS2_182" : "CS2_200");
            var ordered = new ICharacter[] { own, enemy, p.Hero, p.Opponent.Hero };
            g.SetRandomSeed(sample.Seed);
            var random = g.CloneExact().Random; random.NextLong();
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "seeded payload attack");
                var frozen = next.PowerHistory.Last.OfType<PowerHistoryTagChange>()
                    .Where(t => t.Tag == GameTag.FROZEN && t.Value == 1).Select(t => t.EntityId).ToArray();
                Core2026Proof.Check(frozen.SequenceEqual(new[] { ordered[sample.Index].Id }),
                    "seeded random cast selects exact ordered character, not always first");
                Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState()), "random cast consumes exactly one target draw");
            }
        }
    }

    private static void Unpinned_Archaedas_FinalSwingAndRemoval()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var g = ImbueProof.Make(CardClass.WARRIOR); if (opponent) End(g);
            var p = g.CurrentPlayer; p.UsedMana = 0;
            var weapon = Hammer(p, "EX1_606"); PlayEntity(g, p, weapon); weapon.Durability = 1;
            p.Opponent.Hero.AttackDamage = 1;
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = next.ControllerById(p.Id);
                Core2026Proof.Check(next.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)), "final payload swing accepted");
                Core2026Proof.Check(owner.Hero.Damage == 1 && owner.Hero.Armor == 5,
                    "Shield Block is cast AFTER combat even on final durability");
                Core2026Proof.Check(owner.Hero.Weapon == null && PayloadRules.Entries(next.IdEntityDic[weapon.Id]).Length == 0,
                    "destroyed weapon retires payload");
                var empty = Generic.Copy(owner, owner.Hero, next.IdEntityDic[weapon.Id], Zone.HAND);
                Core2026Proof.Check(PayloadRules.Entries(empty).Length == 0 && empty[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
                    "graveyard identity copy cannot resurrect destroyed payload");
            }
        }
        {
            var g = ImbueProof.Make(CardClass.WARRIOR); var p = g.Player1;
            var weapon = Hammer(p, "EX1_606"); PlayEntity(g, p, weapon);
            End(g); var other = (Weapon)Play(g, p.Opponent, "CS2_106");
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && p.Hero.Armor == 0,
                "opposing hero attack cannot cast owned weapon payload");
            End(g);
            Core2026Proof.Check(PayloadRules.Entries(weapon).Length == 1, "payload has no turn expiry");
            p.UsedMana = 0; Play(g, p, "CS2_106");
            Core2026Proof.Check(PayloadRules.Entries(weapon).Length == 0, "replacement clears removed weapon payload");
        }
    }

    private static void Unpinned_Archaedas_MissingPayloadAndAutomaticChoice()
    {
        foreach (string mode in new[] { "missing", "invalid", "cleared", "decision", "no-target", "full-board" })
        {
            var g = ImbueProof.Make(CardClass.WARRIOR); var p = g.Player1;
            string spell = mode switch { "decision" => "BE_555", "no-target" => "CS2_108", "full-board" => "CS2_027", _ => "EX1_606" };
            var weapon = Hammer(p, spell);
            if (mode == "invalid") { PayloadRules.Clear(weapon); PayloadRules.StoreCard(weapon, Key, Cards.FromId("CS2_182")); }
            if (mode is "missing" or "cleared") PayloadRules.Clear(weapon, PayloadPolicy.Silence);
            if (mode == "full-board") while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            g.DeathProcessingAndAuraUpdate();
            var baseline = g.SaveSnapshot();
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(baseline), g })
            {
                var owner = next.Player1; var held = next.IdEntityDic[weapon.Id];
                PlayEntity(next, owner, held);
                Core2026Proof.Check(next.Process(HeroAttackTask.Any(owner, next.Player2.Hero)), "automatic/missing payload attack accepted");
                Core2026Proof.Check(owner.Hero.Armor == 0 && owner.Choice == null && next.Player2.Choice == null
                    && !next.Resolution.HasSuspendedEffect, "invalid payload is inert; automatic decisions/no-target/capacity never stall");
                if (mode == "decision") Core2026Proof.Check(owner.HandZone.Count > 0, "Reload automatically Discovered a spell");
                if (mode == "full-board") Core2026Proof.Check(owner.BoardZone.Count == 7, "full-board spell does not exceed capacity");
            }
        }
    }

    private static void Unpinned_Archaedas_Rewind()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var source = Add(p, "BE_055");
        source[GameTag.REWIND] = 1; PlayEntity(g, p, source);
        B18ArchaedasProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(g.Rewind != null, "Archaedas completes into real Rewind transaction");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            B18ArchaedasProof.Answer(next, next.Rewind.RewindEntityId);
            B18ArchaedasProof.Answer(next, next.Player1.Choice.Choices[0]);
            if (next.Rewind != null) B18ArchaedasProof.Answer(next, next.Rewind.KeepEntityId);
            var weapon = next.Player1.HandZone.Single(c => c.Card.Id == "BE_055t");
            Core2026Proof.Check(next.Rewind == null && next.Player1.Choice == null && PayloadRules.Entries(weapon, Key).Length == 1,
                "retry/keep restores one independent spell payload without duplicate weapon");
        }
    }
}
