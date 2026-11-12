using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class CharityProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_805", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory && Cards.FromId("MEND_805e").AssetId == 130679
            && Cards.FromId("MEND_805e").Implemented && Cards.FromId("MEND_805e").Text == "+3/+3.", "Charity consumer and exact enchantment");
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "no deaths yields no cards");
        var previous = Summon(p, "CS2_231"); Kill(g, previous); End(g);
        Kill(g, Summon(p.Opponent, "CS2_200"));
        var stolen = Summon(p.Opponent, "CS2_120");
        new ControlTask(EntityType.TARGET).Process(g, p, p.Hero, stolen); g.DeathProcessingAndAuraUpdate(); Kill(g, stolen);
        // Friendly means controller at death, even on the opponent's active turn.
        var automatic = (Spell)Entity.FromCard(p, Cards.FromId(id));
        Generic.CastSpell(p, g, automatic, null, 0); g.DeathProcessingAndAuraUpdate();
        var copy = (Minion)p.HandZone.Single();
        Core2026Proof.Check(copy.Card.Id == "CS2_120" && copy.AttackDamage == 5 && copy.Health == 6,
            "current global turn, controller at death, prior turn and opponent deaths excluded");
        p.HandZone.Remove(copy); p.SetasideZone.Add(copy); End(g);
        Play(g, p, id);
        Core2026Proof.Check(!p.HandZone.Any(m => m.Card.Id == "CS2_120"), "opponent-turn friendly death expires next global turn");
        foreach (var drawn in p.HandZone.ToArray()) { p.HandZone.Remove(drawn); p.SetasideZone.Add(drawn); }

        var first = Summon(p, "JAIL_974");
        Generic.AddEnchantmentBlock(g, Cards.FromId("MEND_805e"), first, first);
        first.Damage = 1; Kill(g, first);
        Kill(g, Summon(p, "CS2_231")); Kill(g, Summon(p, "CS2_231"));
        Kill(g, Summon(p.Opponent, "CS2_200"));
        var discarded = Add(p, "CS2_120"); Generic.DiscardBlock(p, discarded); g.DeathProcessingAndAuraUpdate();
        // Removing a graveyard entity must not erase its recorded death.
        p.GraveyardZone.Remove(first); p.SetasideZone.Add(first);
        var late = Add(p, id);
        var fork = g.Clone(); var owner = fork.Player1;
        Kill(fork, Summon(owner, "CS2_200"));
        PlayEntity(fork, owner, fork.IdEntityDic[late.Id]);
        Core2026Proof.Check(owner.HandZone.Select(m => m.Card.Id).SequenceEqual(new[] { "JAIL_974", "CS2_231", "CS2_231", "CS2_200" })
            && p.HandZone.Count == 1 && g.HistoryFacts.Query.Deaths(g.HistoryFacts.Query.ThisTurn).Count(f => f.ControllerId == p.Id) == 3,
            "clone death suffix and generated cards independent of original: " + string.Join(",", owner.HandZone.Select(m => m.Card.Id))
                + $" original hand={p.HandZone.Count} deaths={g.HistoryFacts.Query.Deaths(g.HistoryFacts.Query.ThisTurn).Count(f => f.ControllerId == p.Id)}");
        for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
        var board = p.BoardZone.Select(m => m.Id).ToArray();
        PlayEntity(g, p, late);
        Core2026Proof.Check(p.HandZone.Select(m => m.Card.Id).SequenceEqual(new[] { "JAIL_974", "CS2_231", "CS2_231" })
            && p.BoardZone.Select(m => m.Id).SequenceEqual(board), "late-created Charity sees ordered duplicate deaths even with full board");
        foreach (Minion minion in p.HandZone)
            Core2026Proof.Check(minion.AttackDamage == Cards.FromId(minion.Card.Id).ATK + 3
                && minion.Health == Cards.FromId(minion.Card.Id).Health + 3 && minion.Damage == 0
                && minion.AppliedEnchantments.Count(e => e.Card.Id == "MEND_805e") == 1,
                "fresh exact death-form stats plus one buff, prior damage/enchantments not retained");
        Core2026Proof.Check(p.HandZone[0].Card.Power.Trigger != null, "materialized death definition retains its registered trigger recipe");

        var overflow = g.Clone(); owner = overflow.Player1;
        while (owner.HandZone.Count < 9) Add(owner, "GAME_005");
        var spell = Add(owner, id); var existing = owner.HandZone.Select(m => m.Id).ToHashSet();
        PlayEntity(overflow, owner, spell);
        var admitted = owner.HandZone.Where(m => !existing.Contains(m.Id)).ToArray();
        Core2026Proof.Check(owner.HandZone.Count == 10 && admitted.Length == 1 && admitted[0].Card.Id == "JAIL_974"
            && ((Minion)admitted[0]).AttackDamage == Cards.FromId("JAIL_974").ATK + 3,
            "vacated hand slot admits first death only; overflow respects ten-card hand");
        var playedCopy = (Minion)owner.HandZone.First(m => m.Card.Id == "CS2_231");
        Kill(overflow, owner.BoardZone[0]); PlayEntity(overflow, owner, playedCopy);
        Core2026Proof.Check(playedCopy.AttackDamage == 4 && playedCopy.Health == 4, "Charity buff survives hand-to-board move");
    });
}
