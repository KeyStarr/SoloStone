using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PantherMaskProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_432", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.Damage = 3; var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); int deck = p.DeckZone.Count; Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 4 && friend.Damage == 0 && friend.HasStealth && p.DeckZone.Count == deck - 2 && p.HandZone.Count == 3, "sets damaged minion to 5/4, grants Stealth and draws two");
        Core2026Proof.Check(friend.AppliedEnchantments.Any(e => e.Card.Id == "DINO_432e"), "pinned Panther enchantment");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.AttackDamage == 5 && enemy.Health == 4 && enemy.HasStealth, "enemy target gets same set stats and Stealth");
        Play(g, p, "BE_228", friend); Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5 && !friend.HasStealth, "silence restores stats and removes Stealth");
        while (p.HandZone.Count < 9) Add(p, "CS2_182"); Play(g, p, id, friend);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 6, "near-full hand draws one and burns second");
        var spell = Cards.FromId(id); var source = Entity.FromCard(p, spell);
        spell.Power.PowerTask.Process(g, p, source, friend, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 8 && friend.Health == 4, "full hand burns both draws while still setting stats");
    });
}
