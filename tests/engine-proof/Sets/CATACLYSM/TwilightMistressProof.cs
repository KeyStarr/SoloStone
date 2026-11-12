using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwilightMistressProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_201", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_120"); var other = Summon(p.Opponent, "CS2_182");
        enemy.AttackDamage = 9; enemy.Damage = 1; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.Opponent.HandZone.Any(m => m.Id == enemy.Id) && p.Opponent.HandZone.Any(m => m.Id == other.Id), "all enemies returned to own hand");
        Core2026Proof.Check(enemy.AttackDamage == 2 && enemy.Health == 3 && friend.Zone.Type == Zone.PLAY, "bounce resets buffs/damage, friendly unaffected");
        while (p.Opponent.HandZone.Count < 10) Add(p.Opponent, "CS2_182"); enemy = Summon(p.Opponent, "BE_572");
        int armor = p.Opponent.Hero.Armor;
        Play(g, p, id); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == 10 && p.Opponent.Hero.Armor == armor + 3, "full hand destroys Vanguard through normal Deathrattle processing");
        var removed = p.Opponent.HandZone[0]; p.Opponent.HandZone.Remove(removed); p.Opponent.SetasideZone.Add(removed);
        var oldest = Summon(p.Opponent, "CS2_120");
        var newest = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
        Generic.SummonBlock(g, newest, 0, null); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(newest.ZonePosition < oldest.ZonePosition && newest.OrderOfPlay > oldest.OrderOfPlay, "newest enemy is leftmost, opposite play order");
        Play(g, p, id);
        Core2026Proof.Check(oldest.Zone.Type == Zone.HAND && newest.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == 10, "oldest enemy takes last hand slot regardless of board position");
        Play(g, p, id); Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "empty enemy board safe");
    });
}
