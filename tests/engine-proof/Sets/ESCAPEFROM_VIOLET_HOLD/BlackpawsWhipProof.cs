using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BlackpawsWhipProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_503", (g, p, id) => {
        var held = Add(p, id); Core2026Proof.Check(held.Cost == 3, "no Coins costs three");
        Add(p.Opponent, "GAME_005"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 3 && held[GameTag.COST] == 3, "opposing Coin alone cannot reduce visible cost");
        var fake = Add(p, "CFM_630"); Core2026Proof.Check(held.Cost == 3, "Counterfeit Coin is not tagged Coin in pinned export");
        var coin = Add(p, "GAME_005"); var cosmetic = Add(p, "TB_BaconShop_HP_008a"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 1 && held[GameTag.COST] == 1, "two distinct pinned Coins reduce visible COST");
        var clone = g.Clone(); clone.Player1.HandZone.Remove(clone.IdEntityDic[coin.Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.COST] == 2 && held.Cost == 1, "clone hand changes independent");
        p.HandZone.Remove(coin); p.SetasideZone.Add(coin); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 2, "Coin removal raises cost");
        Add(p, "GAME_005"); Add(p, "GAME_005"); Add(p, "GAME_005"); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 0, "four Coins floor at zero");
        End(g); Add(p.Opponent, "GAME_005"); End(g); Core2026Proof.Check(held.Cost == 0, "own hand alone determines cost across turns");
        PlayEntity(g, p, held); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 2, "printed 3/2 weapon");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        var attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack) && p.HandZone.Count == 0, "first swing does not draw"); End(g); End(g);
        int before = p.HandZone.Count; attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack) && p.Hero.Weapon == null && p.HandZone.Count == before + 1, "breaking swing draws exactly one");
        Play(g, p, id); before = p.HandZone.Count; Play(g, p, "CS2_082");
        Core2026Proof.Check(p.HandZone.Count == before + 1, "replacement draws exactly one");
        Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        int deck = p.DeckZone.Count; p.Hero.Weapon.Damage = 1; p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
        attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack), "full-hand breaking swing accepted");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand burns Deathrattle draw");
    });
}
