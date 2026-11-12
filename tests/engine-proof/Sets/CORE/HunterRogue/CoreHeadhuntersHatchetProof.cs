using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreHeadhuntersHatchetProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TRL_111", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.Durability == 2 && p.Hero.Weapon.AttackDamage == 2, "no Beast gives printed two durability");
        Summon(p.Opponent, "CS2_172"); Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.Durability == 2, "opponent Beast and friendly non-Beast do not qualify");
        var beast = Summon(p, "CS2_172"); var secondBeast = Summon(p, "CS2_172"); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.Durability == 3 && p.Hero.Weapon.AttackDamage == 2
            && p.Hero.Weapon.AppliedEnchantments.Any(e => e.Card.Id == "TRL_111e1"), "friendly Beast grants actual +1 Durability enchantment");
        Kill(g, beast); Kill(g, secondBeast); Core2026Proof.Check(p.Hero.Weapon.Durability == 3, "Battlecry bonus persists when Beast leaves");
    }, legacyPoolMember: true);
}
