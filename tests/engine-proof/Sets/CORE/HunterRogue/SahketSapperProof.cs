using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SahketSapperProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_280", (g, p, id) => {

        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasDeathrattle && source.IsRace(Race.PIRATE), "printed deathrattle and Pirate");
        var friendly = Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(friendly.Zone == p.BoardZone && p.HandZone.Count == 0, "no enemies leaves friendly minions alone");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.AttackDamage = 8; enemy.BaseHealth = 9; enemy.Damage = 3;
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(enemy.Zone == p.Opponent.HandZone && enemy.Card.Id == "CS2_182" && enemy.AttackDamage == 4 && enemy.Health == 5 && enemy.Damage == 0, "returns exact enemy entity to its owner's hand, resets buffs/damage");
        source = Summon(p, id); var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_200");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(first.Zone == p.Opponent.BoardZone && second.Zone == p.Opponent.BoardZone, "clone bounce isolated");
        Kill(g, source);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && p.Opponent.HandZone.Count == 2 && friendly.Zone == p.BoardZone, "one random enemy only");
        Core2026Proof.Check(first.Zone.Type == clone.IdEntityDic[first.Id].Zone.Type && second.Zone.Type == clone.IdEntityDic[second.Id].Zone.Type, "same RNG state selects same enemy");
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.Opponent.BoardZone.Count == 1, "Silence removes bounce");
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        enemy = p.Opponent.BoardZone.Single(); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.HandZone.Count == 10, "full opposing hand destroys returned minion");
        enemy = Summon(p.Opponent, "EX1_096");
        int deckBefore = p.Opponent.DeckZone.Count;
        int deathsBefore = p.Opponent.NumFriendlyMinionsThatDiedThisTurn;
        int totalDeathsBefore = g.NumMinionsKilledThisTurn;
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.HandZone.Count == 10
            && p.Opponent.DeckZone.Count == deckBefore - 1, "full-hand Loot Hoarder dies and its Deathrattle draws/burns a card");
        Core2026Proof.Check(p.Opponent.NumFriendlyMinionsThatDiedThisTurn == deathsBefore + 1
            && g.NumMinionsKilledThisTurn == totalDeathsBefore + 2, "full-hand bounce counts target and Sapper deaths");
    });
}
