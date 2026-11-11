using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class HerbivoreAssistantProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_419", (g, p, id) => {
        var enemyBeast = Summon(p.Opponent, "CS2_172");
        var friendlyNonBeast = (Minion)Play(g, p, "CS2_182");
        var noTarget = (Minion)Play(g, p, id);
        Core2026Proof.Check(noTarget.AttackDamage == 3 && noTarget.Health == 2 && !noTarget.IsRush, "optional Battlecry with no friendly Beast, enemy Beast and friendly non-Beast excluded");
        var beast = (Minion)Play(g, p, "CS2_172");
        var source = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target?.Id == beast.Id), "friendly Beast required when available; excludes enemy Beast, friendly non-Beasts, heroes and no target");
        PlayEntity(g, p, source, beast);
        Core2026Proof.Check(beast.Card.Id == "CS2_172" && beast.AttackDamage == 5 && beast.Health == 4 && beast.IsRush
            && beast.AppliedEnchantments.Any(e => e.Card.Id == "DINO_419e"), "pinned enchantment grants Beast +2/+2 and Rush");
        Core2026Proof.Check(CanAttack(beast, enemyBeast) && !CanAttack(beast, p.Opponent.Hero), "fresh buffed Beast can attack only minions");
        AttackOptionsProof.Lifecycle(g, beast, enemyBeast, rush: true,
            grantOnPreviousTurnMinion: (copy, target) => {
                target.Silence();
                Play(copy, target.Controller, id, target);
                Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 4, "Battlecry buffs previous-turn Beast");
            });
        Play(g, p, "BE_228", beast);
        Core2026Proof.Check(beast.IsSilenced && beast.AttackDamage == 3 && beast.Health == 2 && !beast.IsRush && !beast.AttackableByRush
            && !CanAttack(beast, enemyBeast) && !CanAttack(beast, p.Opponent.Hero), "silence removes stats and Rush; no attack this turn");
        End(g); End(g);
        Play(g, p, id, beast);
        Core2026Proof.Check(beast.IsSilenced && beast.IsRush && beast.AttackDamage == 5 && beast.Health == 4
            && CanAttack(beast, p.Opponent.Hero), "silenced Beast can gain new Rush; older Beast can still attack hero");
        Core2026Proof.Check(CanAttack(beast, enemyBeast), "older Beast granted Rush retains minion option");
        Kill(g, noTarget);
        Play(g, p, "BE_228", beast);
        Core2026Proof.Check(!beast.IsRush && beast.AttackDamage == 3 && beast.Health == 2 && CanAttack(beast, p.Opponent.Hero), "second silence removes new buff without exhausting older Beast");
    });
}
