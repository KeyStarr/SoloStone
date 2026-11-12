using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LongneckEggProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_130", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Kill(g, source);
        var beast = p.BoardZone.Single(m => m.Card.Id == "DINO_130t");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { friend.Id, beast.Id, right.Id }), "Beast replaces dead middle minion");
        Core2026Proof.Check(beast.AttackDamage == 4 && beast.Health == 4 && beast.IsRace(Race.BEAST) && beast.AppliedEnchantments.Any(e => e.Card.Id == "DINO_130e"), "pinned 3/3 Beast summoned then buffed to 4/4");
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6 && held.AttackDamage == 4 && held.Health == 5 && enemy.AttackDamage == 4 && enemy.Health == 5, "only current friendly board receives +1/+1");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == "BE_228" && p.BoardZone.Single().AttackDamage == 1, "silence removes summon and buff");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Single(m => m.Card.Id == "DINO_130t").Health == 4 && p.BoardZone.Where(m => m.Card.Id == "CS2_182").All(m => m.AttackDamage == 5 && m.Health == 6), "full board uses freed slot; buffs Beast and all six survivors");
        End(g);
    });
}
