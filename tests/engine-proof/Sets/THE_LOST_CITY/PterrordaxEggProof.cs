using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PterrordaxEggProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_831", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var small = Summon(p.Opponent, "CS2_168");
        friend.Damage = 1; friend.HasDivineShield = true;
        int hero = p.Hero.Health; Kill(g, source); var token = p.BoardZone.Single(m => m.Card.Id == "TLC_831t");
        Core2026Proof.Check(token.Card.Implemented && token.Card.ATK == 3 && token.Card.Health == 3 && token.AttackDamage == 3 && token.Health == 7 && token.IsRace(Race.BEAST), "pinned 3/3 Beast steals four Health");
        Core2026Proof.Check(friend.BaseHealth == 4 && friend.Health == 3 && friend.Damage == 1 && friend.HasDivineShield && right.BaseHealth == 4 && right.Health == 4 && right.Damage == 0 && enemy.BaseHealth == 4 && enemy.Health == 4 && enemy.Damage == 0 && small.Zone.Type == Zone.GRAVEYARD && p.Hero.Health == hero, "both boards lose one maximum Health including lethal, damage and Divine Shield preserved, heroes excluded");
        Core2026Proof.Check(friend.AppliedEnchantments.Any(e => e.Card.Id == "TLC_831e") && token.AppliedEnchantments.Any(e => e.Card.Id == "TLC_831e2") && p.BoardZone[0] == token, "pinned Health enchantments; token takes dying leftmost Egg's slot");
        Play(g, p, "BE_228", token); Core2026Proof.Check(token.Health == 3, "silence removes stolen Health");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id); Kill(g, source); token = p.BoardZone.Single(); Core2026Proof.Check(token.Health == 3, "empty boards steal nothing");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 1, "silence prevents summon and steal");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[0].Card.Id == "TLC_831t" && p.BoardZone[0].Health == 9, "full board uses freed slot and steals from six");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        left[GameTag.DORMANT] = 1;
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        var generated = p.BoardZone.Where(m => new[] { "TLC_831t" }.Contains(m.Card.Id)).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 1 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(generated.Select(m => m.Id)).Append(right.Id)), "live source summons right in creation order with Dormant left neighbour");
        left[GameTag.DORMANT] = 0;
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => new[] { "TLC_831t" }.Contains(m.Card.Id)) == 1, "full board prevents additional summons");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        CheckDeathrattleSlot(p, board, p.BoardZone.Where(m => m.Card.Id == "TLC_831t"), "token fills the dying Egg's physical slot");
        ParkBoard(p);
        End(g);
    });
}
