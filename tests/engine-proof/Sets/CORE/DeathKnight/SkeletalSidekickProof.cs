using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SkeletalSidekickProof
{
    internal static void Run() => StandardSetsProof.Case("RLK_958", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2 && source.IsRace(Race.UNDEAD),
            "no-target Battlecry playable; cannot buff itself");
        Kill(g, source);
        var friendly = Summon(p, "RLK_708");
        var ordinary = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "RLK_708");
        source = (Minion)Add(p, id);
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(targets.Length > 0 && targets.All(t => t.Target?.Id == friendly.Id),
            "friendly Undead required when available; enemy, ordinary minion, hero, self excluded");
        PlayEntity(g, p, source, friendly);
        Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.Health == 2
            && friendly.AppliedEnchantments.Single().Card.Id == "RLK_958e"
            && ordinary.AttackDamage == 4 && enemy.AttackDamage == 2 && source.AttackDamage == 1,
            "exact +2 Attack enchantment only on chosen dual-type Undead");
        End(g); End(g);
        Core2026Proof.Check(friendly.AttackDamage == 4, "buff persists past end of turn");
        Kill(g, source);
        Core2026Proof.Check(friendly.AttackDamage == 4, "buff survives source death");
        friendly.Silence();
        Core2026Proof.Check(friendly.AttackDamage == 2 && friendly.Health == 2, "Silence removes attack buff");
    });
}
