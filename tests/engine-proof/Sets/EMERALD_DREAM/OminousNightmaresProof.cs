using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static ChooseOneProof;

internal static class OminousNightmaresProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_570", (g, p, id) => {
        OptionsSurvive(g, p, id, "EDR_570A", "EDR_570B");
        var source = Add(p, id); var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.ChooseOne == 1 && t.Target == null), "no damaged minion excludes branch B");
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, source, friend, chooseOne: 2)), "undamaged target rejected before play");
        friend.Damage = 1; enemy.Damage = 1;
        var targets = p.Options().Where(t => t.Source?.Id == source.Id && t.ChooseOne == 2).ToArray();
        Core2026Proof.Check(targets.Select(t => t.Target.Id).ToHashSet().SetEquals(new[] { friend.Id, enemy.Id }), "damaged minions on either side only");
        PlayOption(g, p, source, 2, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 6 && enemy.AppliedEnchantments.Any(e => e.Card.Id == "EDR_570e"), "enemy gets exact +2/+2 enchantment");
        PlayOption(g, p, id, 2, friend); Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 6, "friendly damaged target allowed");
        var spellpower = Summon(p, "EX1_284");
        PlayOption(g, p, id, 1);
        Core2026Proof.Check(friend.Damage == 3 && enemy.Damage == 3 && spellpower.Damage == 2 && p.Opponent.Hero.Damage == 0, "branch A deals spell-damage boosted damage to both boards only");
        Kill(g, spellpower); var fandral = Both(p);
        PlayOption(g, p, id, -1, friend);
        Core2026Proof.Check(friend.AttackDamage == 8 && friend.Health == 5 && friend.Damage == 4 && enemy.Damage == 4 && fandral.Damage == 1,
            "Choose Both deals damage then buffs selected damaged minion");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); fandral = Both(p);
        PlayOption(g, p, id, -1);
        Core2026Proof.Check(fandral.Damage == 1 && fandral.AttackDamage == 3,
            "Choose Both without damaged pre-play target resolves area damage");
    });
}
