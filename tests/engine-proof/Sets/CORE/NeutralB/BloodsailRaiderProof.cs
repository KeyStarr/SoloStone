using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class BloodsailRaiderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_018", (g, p, id) => {
        var noWeapon = (Minion)Play(g, p, id); Core2026Proof.Check(noWeapon.AttackDamage == 2 && noWeapon.Health == 3, "no weapon grants zero Attack");
        Play(g, p, "CS2_106"); p.Hero.AttackDamage += 4;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 3 && source.IsRace(Race.PIRATE)
            && source.AppliedEnchantments.Single().Card.Id == "NEW1_018e", "gain weapon's three Attack, not hero total Attack");
        p.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 5, "Battlecry gain persists after weapon removal");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 2, "Silence removes weapon-derived enchantment");
        End(g); Play(g, p.Opponent, "CS2_106"); End(g);
        source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 2, "opponent weapon grants no Attack");
        Play(g, p, "EX1_567"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4, "uses current equipped weapon Attack after replacement");
    }, legacyPoolMember: true);
}
