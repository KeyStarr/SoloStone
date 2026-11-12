using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AugmentedPorcupineProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_201", (g, p, id) => {

        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasDeathrattle && source.IsRace(Race.BEAST) && source.IsRace(Race.MECHANICAL) && source.Card.Races.Count == 2, "both printed minion types");
        p.Opponent.Hero.HasDivineShield = true; Kill(g, source);
        Core2026Proof.Check(!p.Opponent.Hero.HasDivineShield && p.Opponent.Hero.Damage == 1, "two separate missiles: shield then one damage");
        source = Summon(p, id); Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_011o"), p.Hero, source);
        Summon(p, "EX1_012"); int before = p.Opponent.Hero.Damage; Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before + 4, "current buffed Attack; no Spell Damage scaling");
        source = Summon(p, id); source.AttackDamage = 0; before = p.Opponent.Hero.Damage; Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before, "zero Attack causes no missiles");
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before, "Silence removes deathrattle");
        source = Summon(p, id); source.AttackDamage = 8;
        var first = Summon(p.Opponent, "CS2_182"); first.BaseHealth = 30;
        var second = Summon(p.Opponent, "CS2_182"); second.BaseHealth = 30;
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(source.Zone == p.BoardZone && first.Damage == 0 && second.Damage == 0 && p.Opponent.Hero.Damage == before, "clone missiles isolated");
        Kill(g, source);
        Core2026Proof.Check(first.Damage + second.Damage + p.Opponent.Hero.Damage - before == 8 && p.Hero.Damage == 0, "eight damage split among enemies only");
        Core2026Proof.Check(first.Damage == ((Minion)clone.IdEntityDic[first.Id]).Damage && second.Damage == ((Minion)clone.IdEntityDic[second.Id]).Damage, "same RNG state gives same split");
        ClearBoard(g, p.Opponent); before = p.Opponent.Hero.Damage;
        first = Summon(p.Opponent, "CS2_171"); p.Opponent.Hero.IsImmune = true;
        source = Summon(p, id); source.AttackDamage = 20; Kill(g, source);
        Core2026Proof.Check(first.Zone == p.Opponent.GraveyardZone && p.Opponent.Hero.Damage == before, "lethal minions excluded from later missiles; Immune absorbs remainder");
        p.Opponent.Hero.IsImmune = false;
        foreach (bool porcupineFirst in new[] { true, false })
        {
            ClearBoard(g, p); ClearBoard(g, p.Opponent); p.Opponent.Hero.Damage = 0;
            Minion bear;
            if (porcupineFirst) { source = Summon(p, id); bear = Summon(p.Opponent, "CORE_AV_337"); }
            else { bear = Summon(p.Opponent, "CORE_AV_337"); source = Summon(p, id); }
            Core2026Proof.Check((source.OrderOfPlay < bear.OrderOfPlay) == porcupineFirst, "fixture has requested play order");
            source.AttackDamage = 12;
            Play(g, p, "EX1_312"); // Twisting Nether kills both in the same death phase.
            var cubs = g.IdEntityDic.Values.OfType<Minion>().Where(m => m.Card.Id == "AV_337t" && m.Id > bear.Id).ToArray();
            Core2026Proof.Check(cubs.Length == 2 && source.Zone == p.GraveyardZone && bear.Zone == p.Opponent.GraveyardZone,
                "simultaneous deaths resolve both Deathrattles");
            if (porcupineFirst)
                Core2026Proof.Check(p.Opponent.Hero.Damage == 12 && cubs.All(m => m.Damage == 0 && m.Zone == p.Opponent.BoardZone),
                    "older Porcupine finishes all missiles before newer Bear creates Cubs");
            else
                Core2026Proof.Check(cubs.Sum(m => m.Damage) > 0 && p.Opponent.Hero.Damage + cubs.Sum(m => m.Damage) == 12,
                    "older Bear creates Cubs before newer Porcupine missiles select targets");
        }
    });
}
