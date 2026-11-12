using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EternalFireboltProof
{
    internal static void Run() => StandardSetsProof.Case("END_025", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var target = Summon(p.Opponent, "CS2_182"); TargetRules(g, p, id, target, friendly, false);
        p.Hero.Damage = 10; var nonlethal = Play(g, p, id, target); Core2026Proof.Check(target.Health == 2 && p.Hero.Damage == 7, "three damage with Lifesteal");
        End(g); End(g); Core2026Proof.Check(nonlethal.Zone.Type == Zone.GRAVEYARD, "nonlethal spell never returns");
        var spell = Play(g, p, id, target); Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && spell.Zone.Type == Zone.GRAVEYARD && p.AppliedEnchantments.Any(e => e.Card.Id == "END_025e"), "kill schedules delayed return");
        var restored = Game.LoadSnapshot(g.SaveSnapshot()); End(restored);
        Core2026Proof.Check(restored.IdEntityDic[spell.Id].Zone.Type == Zone.HAND && spell.Zone.Type == Zone.GRAVEYARD, "pending return survives snapshot");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.IdEntityDic[spell.Id].Zone.Type == Zone.HAND && spell.Zone.Type == Zone.GRAVEYARD, "clone returns original spell independently");
        End(g); Core2026Proof.Check(spell.Zone == p.HandZone && p.AppliedEnchantments.All(e => e.Card.Id != "END_025e"), "own end returns same entity and removes timer");
        End(g); var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        var blocked = Play(g, p, id, shield); End(g); End(g); Core2026Proof.Check(blocked.Zone.Type == Zone.GRAVEYARD, "shield prevents kill and return");
        var victim = Summon(p.Opponent, "CS2_168"); var full = Play(g, p, id, victim);
        while (!p.HandZone.IsFull) Add(p, "CS2_182"); End(g);
        Core2026Proof.Check(full.Zone.Type == Zone.GRAVEYARD && !p.AppliedEnchantments.Any(e => e.Card.Id == "END_025e"), "full hand loses return without repeat timer");
    });
}
