using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NerubianSwarmguardProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_RLK_062", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 3 && source.HasTaunt && source.IsRace(Race.UNDEAD),
            "1/3 Undead Taunt");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), p.Hero, source);
        source.HasDivineShield = true;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var copies = p.BoardZone.Where(m => m.Card.Id == id && m != source).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 3 && m.Health == 5
            && m.HasTaunt && m.HasDivineShield && m.IsRace(Race.UNDEAD)), "two buffed copies retain native shield and Taunt without repeating Battlecry");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copies[0].Id, copies[1].Id, right.Id }),
            "copies right of actual source position in creation order");
        copies[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copies[0].AttackDamage == 1 && copies[0].Health == 3 && !copies[0].HasTaunt
            && !copies[0].HasDivineShield && source.AttackDamage == 3 && source.HasDivineShield
            && copies[1].AttackDamage == 3, "copy Silence independent from source and other copy");
        ClearBoard(g, p);
        source = Summon(p, id); source.Damage = 1;
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_011o"), p.Hero, source);
        source.Poisonous = true;
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        copies = p.BoardZone.Where(m => m.Card.Id == id && m != source).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 3 && m.Damage == 1 && m.Poisonous),
            "board copies retain damage, temporary stats and Poisonous");
        End(g);
        Core2026Proof.Check(copies.All(m => m.AttackDamage == 1 && m.Poisonous && m.Damage == 1)
            && source.AttackDamage == 1, "temporary copied stats expire independently");
        End(g);
        ClearBoard(g, p);
        left = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1;
        source = Summon(p, id); right = Summon(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        copies = p.BoardZone.Where(m => m.Card.Id == id && m != source).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copies[0].Id, copies[1].Id, right.Id }),
            "Dormant neighbour counts in source position");
        left[GameTag.DORMANT] = 0;
        ClearBoard(g, p);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        Kill(g, source); source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        copies = p.BoardZone.Where(m => m.Card.Id == id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 1 && m.Health == 3 && m.HasTaunt)
            && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, copies[0].Id, copies[1].Id }),
            "removed source appends copies");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 2, "only one copy with one free slot");
        int entities = g.IdEntityDic.Count;
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && g.IdEntityDic.Count == entities, "full board allocates no copies");
    });
}
