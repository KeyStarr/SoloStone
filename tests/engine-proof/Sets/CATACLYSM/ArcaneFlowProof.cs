using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ArcaneFlowProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_489", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId("CATA_489t").AssetId == 123743 && Cards.FromId("CATA_489t2").AssetId == 123744 &&
            Cards.FromId("CATA_489t").Cost == 4 && Cards.FromId("CATA_489t2").Cost == 4 &&
            Cards.FromId("CATA_489t").Text == "<b>Shattered</b>\nDeal $4 damage." &&
            Cards.FromId("CATA_489t2").Text == "<b>Shattered</b>\nDeal $2 damage to\nall enemies.", "exact exported half closure");
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        var combined = ShatterRules.GiveCombined(p, id, p.Hero);
        int enemyHero = p.Opponent.Hero.Damage;
        PlayEntity(g, p, combined, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == enemyHero + 6 && enemy.Damage == 2 && friendly.Damage == 0,
            "combined targeted four then all-enemy two, including hero");
        Summon(p, "CS2_052"); // Wrath of Air Totem: Spell Damage +1.
        var first = Add(p, "CATA_489t");
        PlayEntity(g, p, first, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == enemyHero + 11, "first half targeted four plus Spell Damage");
        var second = Add(p, "CATA_489t2");
        Core2026Proof.Check(second.ValidPlayTargets == null || !second.ValidPlayTargets.Any(), "second half has no target requirement");
        PlayEntity(g, p, second);
        Core2026Proof.Check(p.Opponent.Hero.Damage == enemyHero + 14 && enemy.Zone == p.Opponent.GraveyardZone && friendly.Damage == 0,
            "second half all-enemy two plus Spell Damage");
        Core2026Proof.Check(Cards.FromId(id)[GameTag.TAG_SCRIPT_DATA_NUM_1] == 123743 && Cards.FromId(id)[GameTag.TAG_SCRIPT_DATA_NUM_2] == 123744,
            ShatterRulesProof.Leans + ": static DBF links never treated as counters");
    });
}
