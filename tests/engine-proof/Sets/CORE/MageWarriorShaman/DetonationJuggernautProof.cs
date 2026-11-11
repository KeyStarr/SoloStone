using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DetonationJuggernautProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WW_329", (g, p, id) => {
        var taunt = (Minion)Add(p, "CS2_179"); var ordinary = (Minion)Add(p, "CS2_182");
        var granted = (Minion)Add(p, "CS2_182"); granted.HasTaunt = true;
        var spell = Add(p, "CS2_029"); var board = Summon(p, "CS2_179");
        var enemy = (Minion)Add(p.Opponent, "CS2_179"); var deck = p.DeckZone[0];
        var clone = g.Clone(); Play(clone, clone.Player1, id);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[taunt.Id]).AttackDamage == 5 && taunt.AttackDamage == 3, "clone hand buff isolated");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 3 && source.Health == 4, "printed Taunt 3/4");
        Core2026Proof.Check(taunt.AttackDamage == 5 && taunt.Health == 7 && granted.AttackDamage == 6 && granted.Health == 7, "all current Taunt minions in own hand gain +2/+2");
        Core2026Proof.Check(ordinary.AttackDamage == 4 && ordinary.Health == 5 && board.AttackDamage == 3 && enemy.AttackDamage == 3 && deck[GameTag.ATK] == 4 && spell.Cost == 4, "non-Taunt, board, opponent, deck and spells excluded");
        PlayEntity(g, p, taunt); taunt.Damage = 3; taunt.Silence();
        Core2026Proof.Check(taunt.AttackDamage == 3 && taunt.BaseHealth == 5 && taunt.Damage == 1 && taunt.Health == 4, "hand enchantment persists on play; Silence removes it and preserves current Health below printed maximum");
        ClearBoard(g, p); foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "empty hand Battlecry legal");
    });
}
