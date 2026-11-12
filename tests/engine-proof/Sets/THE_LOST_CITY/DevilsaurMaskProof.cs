using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DevilsaurMaskProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_403", (g, p, id) => {
        var friend = (Minion)Play(g, p, "CS2_182");
        friend.Damage = 3;
        var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false);
        int deck = p.DeckZone.Count;
        Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 8 && friend.Health == 8 && friend.Damage == 0 && friend.HasCharge
            && friend.AppliedEnchantments.Any(e => e.Card.Id == "DINO_403e"), "pinned enchantment sets damaged minion to 8/8 and grants Charge");
        Core2026Proof.Check(p.DeckZone.Count == deck, "Mask does not draw cards");
        AttackOptionsProof.Lifecycle(g, friend, enemy, rush: false,
            grantOnPreviousTurnMinion: (copy, target) => {
                target.Silence();
                Play(copy, target.Controller, id, target);
                Core2026Proof.Check(target.AttackDamage == 8 && target.Health == 8, "Mask sets previous-turn minion stats");
            });
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 8 && enemy.Health == 8 && enemy.HasCharge, "enemy target receives set stats and Charge");
    });
}
