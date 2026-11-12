using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LoGoshsLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_610", (g, p, id) => {
        // Seeds 1, 2, 3 select the first, second, third eligible hand minion respectively.
        foreach (var fixture in new[] { (Seed: 1, Index: 0), (Seed: 2, Index: 1), (Seed: 3, Index: 2) })
        {
            var randomGame = g.CloneExact(history: true); var owner = randomGame.Player1;
            var bearer = Summon(owner, "CS2_182");
            var excludedSpell = Add(owner, "CS2_029");
            var candidates = new[] { Add(owner, "CS2_182"), Add(owner, "CATA_304"), Add(owner, "EX1_008") };
            var excludedEnemy = Add(owner.Opponent, "CS2_182");
            Play(randomGame, owner, id, bearer); randomGame.SetRandomSeed(fixture.Seed);
            Kill(randomGame, bearer);
            var chosen = (Minion)candidates[fixture.Index];
            Core2026Proof.Check(owner.BoardZone.Count == 1 && owner.BoardZone[0].Id == chosen.Id
                && candidates.Where((_, index) => index != fixture.Index).All(m => m.Zone == owner.HandZone)
                && excludedSpell.Zone == owner.HandZone && excludedEnemy.Zone == owner.Opponent.HandZone,
                "seeded selection spans every eligible minion and excludes spells/opponent hand");
            Core2026Proof.Check(chosen.Damage == 0 && chosen.Health == chosen.Card.Health, "summoning never activates selected minion Battlecry");
        }
        var source = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, source, false);
        var hand = Add(p, "CATA_304"); var spell = Add(p, "CS2_029"); Play(g, p, id, source);
        Core2026Proof.Check(source.HasDeathrattle, "grants Deathrattle");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.BoardZone[0].Id == hand.Id && hand.Zone.Type == Zone.HAND, "clone Deathrattle pulls actual hand entity only");
        Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { hand.Id, right.Id }), "departed source's slot takes summoned hand minion");
        Core2026Proof.Check(hand.Zone.Type == Zone.PLAY && ((Minion)hand).AttackDamage == 3 && ((Minion)hand).Health == 8 && ((Minion)hand).Damage == 0 && ((Minion)hand).HasLifeSteal && spell.Zone.Type == Zone.HAND, "random hand minion keeps identity/stats/keywords, no battlecry");
        source = Summon(p, "CS2_182"); Play(g, p, id, source); source.Silence(); g.DeathProcessingAndAuraUpdate(); var spared = Add(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(spared.Zone.Type == Zone.HAND, "Silence removes granted Deathrattle");
        p.HandZone.Remove(spared); p.SetasideZone.Add(spared);
        source = Summon(p, "CS2_182"); Play(g, p, id, source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 2, "no hand minion does nothing");
        source = Summon(p, "CS2_182"); Play(g, p, id, source); spared = Add(p, "CS2_182"); while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        // Enchantment task is also exercised directly at the board-full boundary.
        source.AppliedEnchantments.Single(e => e.Card.Id == "CATA_610e").Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && spared.Zone.Type == Zone.HAND, "full board does not remove hand minion");
        var enemyHand = Add(p.Opponent, "CS2_182"); Play(g, p, id, enemy); Kill(g, enemy);
        Core2026Proof.Check(enemyHand.Zone == p.Opponent.BoardZone && spared.Zone == p.HandZone, "enemy target pulls from its own controller hand");
        foreach (var minion in p.HandZone.ToArray()) { p.HandZone.Remove(minion); p.SetasideZone.Add(minion); }
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, "CS2_182"));
        var pulled = Add(p, "CATA_304"); Play(g, p, id, source); Kill(g, source);
        CheckDeathrattleSlot(p, board, new[] { pulled }, "granted Deathrattle fills the dying bearer's physical slot");
        ParkBoard(p);
        End(g); // Complete a native action after the direct death/summon fixture.

    });
}
