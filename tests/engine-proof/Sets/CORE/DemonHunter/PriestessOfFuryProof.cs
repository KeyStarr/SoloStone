using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PriestessOfFuryProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_493", (g, p, id) => {
        DeathrattleTiming(g.CloneExact(history: true), id);
        TriggerOrder(g.CloneExact(history: true), id, geddonFirst: false);
        TriggerOrder(g.CloneExact(history: true), id, geddonFirst: true);
        var source = (Minion)Play(g, p, id); Summon(p, "EX1_012");
        Core2026Proof.Check(source.IsRace(Race.DEMON), "printed Demon");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 6 && p.Opponent.Hero.Damage == 0, "six missiles ignore Spell Damage; clone isolated");
        var enemies = new[] { Summon(p.Opponent, "CS2_231"), Summon(p.Opponent, "CS2_231"), Summon(p.Opponent, "CS2_231") };
        var branch = g.CloneExact(history: true); End(g); End(branch);
        int total = p.Opponent.Hero.Damage + enemies.Sum(m => m.Zone.Type == Zone.GRAVEYARD ? 1 : m.Damage);
        Core2026Proof.Check(total == 6 && source.Damage == 0 && p.Hero.Damage == 0, "six single hits reselect living enemies; friendly characters untouched");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(branch), "seeded split stable across clone");
        int damage = p.Opponent.Hero.Damage; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "opponent end turn does not trigger");
        source.Silence(); ClearBoard(g, p.Opponent); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "Silence removes end-turn listener");
        End(g); Kill(g, source); var dead = Summon(p, id); Kill(g, dead); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "dead source cannot fire");
    });

    private static void DeathrattleTiming(Game game, string id)
    {
        var player = game.Player1;
        var source = Play(game, player, id);
        var golem = Summon(player.Opponent, "EX1_556"); golem.Damage = 2;
        int missiles = 0, missilesAtDeathrattle = -1;
        game.TriggerManager.DealDamageTrigger += sender => {
            if (sender == source) missiles++;
        };
        game.TriggerManager.SummonTrigger += summoned => {
            if (summoned.Controller == player.Opponent && summoned.Card.Id == "skele21")
                missilesAtDeathrattle = missiles;
        };
        End(game);
        Core2026Proof.Check(golem.Zone.Type == Zone.GRAVEYARD, "barrage kills wounded Harvest Golem");
        Core2026Proof.Check(missilesAtDeathrattle == 6, "Harvest Golem Deathrattle waits until all six missiles finish");
        var replacement = player.Opponent.BoardZone.Single();
        Core2026Proof.Check(replacement.Card.Id == "skele21" && replacement.AttackDamage == 2
            && replacement.BaseHealth == 1 && replacement.Damage == 0 && player.Opponent.Hero.Damage == 5,
            "remaining missiles exclude mortally wounded Golem; its replacement survives undamaged");
    }

    private static void TriggerOrder(Game game, string id, bool geddonFirst)
    {
        var player = game.Player1;
        var first = Play(game, player, geddonFirst ? "EX1_249" : id);
        var second = Play(game, player, geddonFirst ? id : "EX1_249");
        var damageSources = new List<int>();
        game.TriggerManager.DealDamageTrigger += source => damageSources.Add(source.Id);
        End(game);
        var expected = geddonFirst
            ? Enumerable.Repeat(first.Id, 3).Concat(Enumerable.Repeat(second.Id, 6))
            : Enumerable.Repeat(first.Id, 6).Concat(Enumerable.Repeat(second.Id, 3));
        Core2026Proof.Check(damageSources.SequenceEqual(expected),
            geddonFirst ? "Geddon played before Priestess deals damage before all six missiles"
                : "Geddon played after Priestess deals damage after all six missiles");
        Core2026Proof.Check(player.Opponent.Hero.Damage == 8 && player.Hero.Damage == 2,
            "both end-turn effects resolve exactly once");
    }
}
