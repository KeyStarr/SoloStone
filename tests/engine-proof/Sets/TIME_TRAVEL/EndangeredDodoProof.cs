using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EndangeredDodoProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_703", (g, p, id) => {
        p.Hero.Damage = 19; var none = (Minion)Play(g, p, id); Core2026Proof.Check(none.AttackDamage == 5 && none.Health == 5 && p.BoardZone.Count == 1 && none.HasTaunt, "11 Health does not activate");
        ClearBoard(g, p); p.Hero.Damage = 20; p.Hero.Armor = 50;
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var copy = p.BoardZone.Single(m => m.Card.Id == id && m.Id != source.Id);
        Core2026Proof.Check(source.AttackDamage == 10 && source.Health == 10 && copy.AttackDamage == 10 && copy.Health == 10 && copy.HasTaunt && copy.AppliedEnchantments.Any(e => e.Card.Id == "TIME_703e"), "10 Health activates despite Armor; copy retains buff");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copy.Id, right.Id }), "copy immediately right of source");
        copy.Silence(); Core2026Proof.Check(copy.AttackDamage == 5 && copy.Health == 5 && source.AttackDamage == 10, "copy buff independent under Silence");
        ClearBoard(g, p); p.Hero.Damage = 29; for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 10 && source.Health == 10 && p.BoardZone.Count == 7, "below threshold buffs even when copy cannot fit");
    });
}
