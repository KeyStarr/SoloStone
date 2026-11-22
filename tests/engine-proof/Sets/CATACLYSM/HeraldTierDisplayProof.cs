using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// #192. Which cards follow the Herald tier, their text variant as a function of the count (the transport sends it; the engine
// stores nothing), and the resummon tier rule.
internal static class HeraldTierDisplayProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "Herald tier display: " + message);

    [GroupProof("herald-tier-display", ProofKind.Cards)]
    internal static void Run()
    {
        Lean_VariantOfCount();
        Unpinned_ResummonTakesCurrentTier();
        Console.WriteLine("PASS Herald tier display: tiered cards, variant 0,1,0,1,2 by count (not stored), resummon at current tier, copy keeps factor");
    }

    // Lean, derived from the "@" texts ("Herald twice/once to upgrade", final), upgrades at 2 and 4 Heralds.
    private static void Lean_VariantOfCount()
    {
        foreach (string id in new[] { "CATA_725t", "CATA_565t", "CATA_780t", "CATA_525t", "CATA_158t", "CATA_580t",
            "CATA_726t", "CATA_726t1", "CATA_150t", "CATA_150t1", "CATA_151t", "CATA_151t1", "CATA_153t", "CATA_153t1",
            "CATA_154t", "CATA_154t1", "CATA_155t", "CATA_155t1" })
            Check(HeraldRules.IsTiered(Cards.FromId(id)), id + " follows the Herald tier");
        foreach (string id in new[] { "CATA_432t1", "CS2_182", "CATA_726", "CATA_580" })
            Check(!HeraldRules.IsTiered(Cards.FromId(id)), id + " does not");
        var g = HeraldProof.New(CardClass.WARRIOR); var p = g.Player1;
        var held = Add(p, "CATA_580t");
        for (int count = 0; count <= 6; count++)
        {
            p[HeraldRules.Progress] = count;
            Check(HeraldRules.Variant(p) == new[] { 0, 1, 0, 1, 2, 2, 2 }[count], "variant after " + count + " Heralds");
        }
        Check(held[(GameTag)955] == 0, "the engine stores no text variant");
    }

    // UNPINNED rule (coordinator, from wiki Herald: upgrades apply only after the Soldier is summoned; Herald cards generated
    // mid-game take the current count): a Soldier entering the board takes its controller's tier at that moment, a resummon
    // (Ectoplasm, Deathrattle) included; a copy keeps its source's realized factor. Lean until a retail trace pins it.
    private static void Unpinned_ResummonTakesCurrentTier()
    {
        var g = HeraldProof.New(CardClass.WARRIOR); var p = g.Player1;
        var source = Summon(p, "CS2_182");
        var soldier = HeraldProof.Herald(g, p, source);
        Check(soldier.BaseHealth == soldier.Card.Health && soldier[HeraldRules.RealizedFactor] == 1, "first Soldier at base tier");
        Play(g, p, "CAP_805");
        var ectoplasm = p.HandZone.Single(c => c.Card.Id == "CAP_805t");
        Check(soldier.Zone == p.GraveyardZone, "Slime 'em! destroys the Soldier");
        HeraldProof.Herald(g, p); HeraldProof.Herald(g, p);
        foreach (var m in p.BoardZone.ToArray()) Kill(g, m);
        Check(HeraldRules.Count(p) == 3 && HeraldRules.Tier(p) == 1, "two more Heralds reach tier 2");
        p.UsedMana = 0;
        PlayEntity(g, p, ectoplasm);
        var back = p.BoardZone.Single(m => m.Card.Id == "CATA_580t");
        Check(back[HeraldRules.RealizedFactor] == 2 && back.AttackDamage == 2 * back.Card.ATK && back.BaseHealth == 2 * back.Card.Health,
            "Unpinned lean: the resummoned Soldier comes back at the current tier");
        p[HeraldRules.Progress] = 4;
        var copy = (Minion)Generic.Copy(p, back, back, Zone.PLAY); g.DeathProcessingAndAuraUpdate();
        Check(copy[HeraldRules.RealizedFactor] == 2 && copy.BaseHealth == back.BaseHealth,
            "Unpinned lean: a copy keeps its source's factor, not the current tier");
    }
}
