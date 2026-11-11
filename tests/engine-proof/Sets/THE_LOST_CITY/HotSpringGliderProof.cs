using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class HotSpringGliderProof
{
    // cost-rules/glider-next-murloc-duration.yaml STILL-UNPINNED (D conflicting export text).
    // Owner assumption: persist until next committed Murloc play, including its linked Kindred Shield.
    internal static void Run() => StandardSetsProof.Case("TLC_428", Unpinned_HotSpringGlider);
    private static void Unpinned_HotSpringGlider(Game g, Controller p, string id)
    {
        Core2026Proof.Check(Cards.FromId("TLC_428e").AssetId == 122735 && Cards.FromId("TLC_428e").Implemented,
            "exact exported grant enchantment");
        foreach (bool kindred in new[] { false, true })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            Play(branch, owner, kindred ? "CS2_168" : "CS2_182"); End(branch); End(branch);
            var cheap = Add(owner, "CS2_168"); var next = Add(owner, "EX1_506");
            var other = Add(owner, "CS2_182"); var enemy = Add(owner.Opponent, "EX1_506");
            var source = (Minion)Play(branch, owner, id);
            Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 4 && !source.HasDivineShield
                && cheap.Cost == 0 && cheap[GameTag.COST] == 0 && next.Cost == 1 && other.Cost == 4 && enemy.Cost == 2,
                "Unpinned_HotSpringGlider: quote/published COST discount friendly held Murlocs only, floor zero, not Glider itself");
            source.Silence(); Kill(branch, source);
            Summon(owner, "CS2_168"); PlayEntity(branch, owner, other);
            End(branch); PlayEntity(branch, owner.Opponent, enemy); End(branch);
            Core2026Proof.Check(cheap.Cost == 0 && next.Cost == 1 && owner.AppliedEnchantments.Any(e => e.Card.Id == "TLC_428e"),
                "Unpinned_HotSpringGlider: grant survives source silence/death, summon, non-Murloc play, enemy Murloc, later turn");
            Resume(branch, resumed => {
                var controller = resumed.Player1;
                PlayEntity(resumed, controller, resumed.IdEntityDic[cheap.Id]);
                var played = (Minion)resumed.IdEntityDic[cheap.Id];
                Core2026Proof.Check(played.HasDivineShield == kindred && resumed.IdEntityDic[next.Id].Cost == 2
                    && !controller.AppliedEnchantments.Any(e => e.Card.Id == "TLC_428e"),
                    "Unpinned_HotSpringGlider: next played Murloc alone consumes discount and matched Shield together");
                played.Silence();
                Core2026Proof.Check(!played.HasDivineShield, "granted Shield removable by Silence");
            });
        }
        Summon(p, "LOE_077"); Play(g, p, id);
        var twice = Add(p, "EX1_506");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(twice.Cost == 0 && p.AppliedEnchantments.Count(e => e.Card.Id == "TLC_428e") == 2,
            $"Brann grants: cost={twice.Cost}, enchantments={p.AppliedEnchantments.Count(e => e.Card.Id == "TLC_428e")}, auras={g.Auras.Count}");
        PlayEntity(g, p, twice);
        Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_428e") && !((Minion)twice).HasDivineShield,
            "all applicable grants consumed on same play; unmatched Kindred grants no Shield");
    }
}
