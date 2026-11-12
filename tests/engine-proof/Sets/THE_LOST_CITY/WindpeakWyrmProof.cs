using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WindpeakWyrmProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_600", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); var friend = Summon(p, "CS2_200");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(source.Cost == 8 && source[GameTag.COST] == 8 && source.IsRace(Race.DRAGON), "printed Dragon and visible cost without Kindred");
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == friend.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id), "Battlecry targets any character");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 5 && p.Hero.Armor == 5, "five damage then five Armor");
        var hand = Add(p, id); Play(g, p, "TLC_888");
        Core2026Proof.Check(hand.Cost == 8, "current-turn Dragon insufficient"); End(g); End(g);
        Core2026Proof.Check(hand.Cost == 6 && hand[GameTag.COST] == 6, "secondary Dragon activates visible hand discount");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[hand.Id], clone.Player2.Hero);
        Core2026Proof.Check(hand.Zone == p.HandZone && p.Opponent.Hero.Damage == 0, "discount and Battlecry clone isolated");
        End(g); End(g); Core2026Proof.Check(hand.Cost == 8, "empty previous turn removes discount");
        PlayEntity(g, p, hand, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && p.Hero.Armor == 10, "hero target receives five; Armor accumulates");
    });
}
