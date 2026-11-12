using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;
using static StandardSetsProof;

internal static class SearingReflectionProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_941", (g, p, id) => {
        EmptyDeck(p);
        var spell = InDeck(p, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && spell.Zone == p.DeckZone, "no minion draws nothing and does not draw a spell");
        p.DeckZone.Remove(spell); p.SetasideZone.Add(spell);
        int fatigue = p.Hero.Fatigue;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Fatigue == fatigue && p.BoardZone.Count == 0, "empty filtered draw causes no fatigue or summon");
        var drawn = (Minion)InDeck(p, "CS2_182");
        Generic.AddEnchantmentBlock(g, SabberStoneCore.Model.Cards.FromId("CS2_009e"), p.Hero, drawn);
        var left = Summon(p, "CS2_171");
        Play(g, p, id);
        var copy = p.BoardZone.Last();
        Core2026Proof.Check(drawn.Zone == p.HandZone && drawn.AttackDamage == 6 && drawn.BaseHealth == 7 && !drawn.HasDivineShield,
            "drawn enchanted minion retained unchanged in hand");
        Core2026Proof.Check(copy.Card.Id == drawn.Card.Id && copy.Id != drawn.Id && copy.AttackDamage == 8 && copy.Health == 8
            && copy.HasDivineShield && copy.HasTaunt && p.BoardZone.First() == left, "8/8 shielded copy appends and retains other abilities");
        Generic.DamageCharFunc(p.Opponent.Hero, copy, 3, false);
        Core2026Proof.Check(!copy.HasDivineShield && copy.Damage == 0, "copy shield absorbs next damage then drops");
        Generic.DamageCharFunc(p.Opponent.Hero, copy, 1, false);
        Core2026Proof.Check(copy.Damage == 1, "damage after shield loss applies normally");
        copy.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.AttackDamage == 4 && copy.BaseHealth == 5 && copy.Damage == 0 && !copy.HasTaunt,
            "Silence removes copy stat enchantment and inherited buff, clamping Health to printed maximum");
        ClearBoard(g, p);
        Play(g, p, "EX1_366");
        drawn = (Minion)InDeck(p, "CS2_182");
        var branch = g.CloneExact(history: true);
        Play(branch, branch.Player1, id);
        Core2026Proof.Check(branch.Player1.BoardZone.Single().AttackDamage == 9 && p.BoardZone.Count == 0 && drawn.Zone == p.DeckZone,
            "clone draw and summon isolated from original");
        Play(g, p, id); copy = p.BoardZone.Single();
        Core2026Proof.Check(copy.Card.Id == "CS2_182" && copy.AttackDamage == 9 && copy.BaseHealth == 9 && copy.HasDivineShield
            && p.Hero.Weapon.Durability == 4, "8/8 initialization precedes exactly one Sword of Justice summon buff");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_171");
        drawn = (Minion)InDeck(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(drawn.Zone == p.HandZone && p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Id != drawn.Id),
            "full board still draws but does not summon");
    });
}
