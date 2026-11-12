using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SpellHealingProof
{
    [GroupProof("spell-healing")]
    internal static void Run()
    {
        Unpinned_RubyVendorWholeOperation();
        Unpinned_SpellHealing();
        ConvertedHeroHealingPreservesFatigue();
    }
    private static void ConvertedHeroHealingPreservesFatigue()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool ruby in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var owner = game.Player1;
            if (ruby) LocationProofs.Use(LocationProofs.Play(owner, "CATA_301"));
            else owner.RestoreToDamage = true;
            owner.Hero.Fatigue = 2; owner.Hero.Damage = 0;
            owner.Hero.TakeHeal(owner.Hero, 3);
            Core2026Proof.Check(owner.Hero.Damage == 3 && owner.Hero.Fatigue == 2,
                "converted hero self-healing deals damage without changing fatigue");
        }
    }
    private static void Unpinned_RubyVendorWholeOperation()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var location = LocationProofs.Play(p, "CATA_301"); LocationProofs.Use(location);
        p.Hero.Damage = 10; p.Opponent.Hero.Damage = 10;
        Play(g, p, "AT_111");
        Core2026Proof.Check(p.Hero.Damage == 14 && p.Opponent.Hero.Damage == 14,
            $"Unpinned_RubyVendorWholeOperation: expected 14/14, got {p.Hero.Damage}/{p.Opponent.Hero.Damage}");
        p.Hero.TakeHeal(p.Hero, 4);
        Core2026Proof.Check(p.Hero.Damage == 10, "Vendor consumes Ruby once; later operation heals");
    }
    private static void Unpinned_SpellHealing()
    {
        var g = HeroDivineShieldProof.NewGame(true);
        var p = g.Player1;
        var spell = Add(p, "CS2_029");
        spell[GameTag.SPELLPOWER] = 2;
        var target = Summon(p.Opponent, "CS2_182"); target.Health = 30;
        Summon(p, "CS2_052");
        Generic.DamageCharFunc(spell, target, 3, true);
        Core2026Proof.Check(target.Damage == 6, "spell-local +2 stacks once with controller +1");
        p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 4;
        p.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE] = 1;
        p.Hero.Damage = 20;
        p.Hero.TakeHeal(spell, 3);
        Core2026Proof.Check(p.Hero.Damage == 6, "Unpinned_Cleric additive bonus precedes multiplier; spell damage never heals");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var copied = (Spell)next.IdEntityDic[spell.Id];
            var victim = (Minion)next.IdEntityDic[target.Id];
            int before = victim.Damage;
            Generic.DamageCharFunc(copied, victim, 3, true);
            Core2026Proof.Check(victim.Damage - before == 12, $"clone/snapshot preserve spell-local scalar: damage={victim.Damage-before}, local={copied[GameTag.SPELLPOWER]}, player={copied.Controller.CurrentSpellPower}");
        }
        Core2026Proof.Check(target.Damage == 6, "branch damage isolated");
        // Healing conversion never takes a second spell-damage pass.
        p.RestoreToDamage = true;
        p.Hero.Damage = 0;
        p.Hero.TakeHeal(spell, 3);
        Core2026Proof.Check(p.Hero.Damage == 14, "Unpinned_ConvertedHealing excludes local and player Spell Damage");
        p.RestoreToDamage = false;
        p.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE] = 0;
        p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 0;
        int intentions = 0, successes = 0;
        SabberStoneCore.Model.TriggerManager.TriggerHandler intent = _ => intentions++;
        SabberStoneCore.Model.TriggerManager.TriggerHandler success = _ => successes++;
        g.TriggerManager.HealIntentTrigger += intent; g.TriggerManager.HealTrigger += success;
        p.Hero.Damage = 0; p.Hero.TakeHeal(spell, 3);
        Core2026Proof.Check(intentions == 1 && successes == 0, "full-health request fires intent, never successful HEAL");
        g.TriggerManager.HealIntentTrigger -= intent; g.TriggerManager.HealTrigger -= success;
        foreach (bool history in new[] { false, true })
        {
            var mode = HeroDivineShieldProof.NewGame(history); var owner = mode.Player1;
            var held = Add(owner, "CS2_029");
            Generic.AddEnchantmentBlock(mode, Cards.FromId("CATA_209e"), owner.Hero, held);
            var copied = Generic.Copy(owner, owner.Hero, held, Zone.HAND);
            Core2026Proof.Check(copied[GameTag.SPELLPOWER] == 1 && copied.AppliedEnchantments.Count == 1,
                "spell actor/scalar survive hand copy with history on/off");
            Play(mode, owner, "CATA_216");
            Core2026Proof.Check(HealingRules.Bonus(owner) == 2, "persistent Cleric grant works with history on/off");
        }
        var rewind = HeroDivineShieldProof.NewGame(true); var rewindOwner = rewind.Player1;
        var retry = Add(rewindOwner, "CS2_029"); retry[GameTag.REWIND] = 1;
        Generic.AddEnchantmentBlock(rewind, Cards.FromId("CATA_209e"), rewindOwner.Hero, retry);
        PlayEntity(rewind, rewindOwner, retry, rewindOwner.Opponent.Hero);
        Core2026Proof.Check(rewind.Rewind != null && rewindOwner.Opponent.Hero.Damage == 7, "local bonus applied on first Rewind attempt");
        var baseline = Game.LoadSnapshot(rewind.Rewind.Baseline);
        var baselineSpell = baseline.IdEntityDic[retry.Id];
        Core2026Proof.Check(baselineSpell[GameTag.SPELLPOWER] == 1 && baselineSpell.AppliedEnchantments.Count == 1,
            "Rewind baseline retains local scalar and actor");
        PlayerDecisionProof.Answer(rewind, rewind.Rewind.RewindEntityId);
        var retried = (Spell)rewind.IdEntityDic[retry.Id];
        Core2026Proof.Check(rewind.Player2.Hero.Damage == 7 && retried[GameTag.SPELLPOWER] == 0
            && retried.AppliedEnchantments.Count == 0, $"Rewind restores scalar/actor, then applies bonus once on retry: damage={rewind.Player2.Hero.Damage}, local={retried[GameTag.SPELLPOWER]}, actors={retried.AppliedEnchantments.Count}");
        Console.WriteLine("PASS SpellHealing facility");
    }
}
