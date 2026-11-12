using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SleetStormProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_485", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == friend) && options.Any(t => t.Target == enemy) && options.Any(t => t.Target == p.Hero) && options.Any(t => t.Target == p.Opponent.Hero), "required character target on either side");
        PlayEntity(g, p, spell, friend);
        Core2026Proof.Check(friend.Health == 3 && enemy.Health == 4, "two to chosen character, one to sole random enemy minion");
        ClearBoard(g, p.Opponent); var loot = Summon(p.Opponent, "EX1_556"); loot.Damage = 1;
        Play(g, p, id, loot);
        var slime = p.Opponent.GraveyardZone.OfType<Minion>().Single(m => m.Card.Id == "skele21");
        Core2026Proof.Check(loot.Zone.Type == Zone.GRAVEYARD && slime.Card.Id == "skele21" && slime.AttackDamage == 2 && slime.BaseHealth == 1 && slime.Damage == 1, "first hit processes Harvest Golem Deathrattle before random target");
        ClearBoard(g, p.Opponent); int health = p.Opponent.Hero.Health;
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Health == health - 2, "no random minion safely skips second hit");
        enemy = Summon(p.Opponent, "CS2_182"); Summon(p, "CS2_052"); health = p.Opponent.Hero.Health;
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Health == health - 3 && enemy.Health == 3, "both hits gain spell damage");
        var other = Summon(p.Opponent, "CS2_182"); var clone = g.CloneExact(history: true);
        var originalSpell = Add(p, id); var cloneSpell = Add(clone.Player1, id);
        PlayEntity(g, p, originalSpell, p.Hero); PlayEntity(clone, clone.Player1, cloneSpell, clone.Player1.Hero);
        Core2026Proof.Check(p.Opponent.BoardZone.Select(m => m.Health).SequenceEqual(clone.Player2.BoardZone.Select(m => m.Health)), "random target selection deterministic after clone");
        ClearBoard(g, p.Opponent); var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        Play(g, p, id, p.Hero); Core2026Proof.Check(shield.Health == 1 && !shield.HasDivineShield, "secondary damage obeys shield");
        shield.HasDivineShield = true; Play(g, p, id, shield);
        Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD, "primary hit removes shield; secondary can select and kill same minion");
    });
}
