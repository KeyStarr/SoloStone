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
internal static class CoreDeadlyPoisonProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_074", (g, p, id) => {
        var source = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source == source), "cannot cast without equipped weapon");
        Play(g, p, "CS2_082"); var weapon = p.Hero.Weapon; int durability = weapon.Durability;
        PlayEntity(g, p, source); Core2026Proof.Check(weapon.AttackDamage == 3 && weapon.Durability == durability && weapon.AppliedEnchantments.Any(e => e.Card.Id == "CS2_074e"), "weapon gets actual +2 Attack enchantment, durability unchanged");
        Play(g, p, id); Core2026Proof.Check(weapon.AttackDamage == 5, "permanent buffs stack");
        End(g); End(g); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 5, "buff persists across turns");
        Play(g, p, "CS2_082"); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1 && weapon.Zone == p.GraveyardZone, "replacement weapon does not inherit buff");
    }, legacyPoolMember: true);
}
