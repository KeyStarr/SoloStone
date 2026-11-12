using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RustrotViperProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_072", (g, p, id) => {
        var friendlyWeapon = Play(g, p, "CS2_106");
        End(g); var enemyWeapon = Play(g, p.Opponent, "CS2_106"); End(g);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.BEAST) && source.AttackDamage == 3 && source.Health == 4, "printed Beast 3/4");
        Core2026Proof.Check(p.Opponent.Hero.Weapon == null && enemyWeapon.Zone == p.Opponent.GraveyardZone
            && p.Hero.Weapon == friendlyWeapon, "battlecry destroys only enemy weapon");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon == friendlyWeapon, "no enemy weapon is harmless");
        End(g); enemyWeapon = Play(g, p.Opponent, "CS2_106"); End(g);
        var traded = Add(p, id); p.UsedMana = 0;
        var replacement = p.DeckZone.TopCard; int deckCount = p.DeckZone.Count, handCount = p.HandZone.Count;
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(clone.Process(TradeCardTask.Any(clone.Player1, clone.IdEntityDic[traded.Id])), "clone trade accepted");
        Core2026Proof.Check(traded.Zone == p.HandZone && p.UsedMana == 0, "clone trade leaves original untouched");
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, traded)), "Tradeable hand action accepted");
        Core2026Proof.Check(traded.Zone == p.DeckZone && replacement.Zone == p.HandZone && p.DeckZone.Count == deckCount
            && p.HandZone.Count == handCount && p.UsedMana == 1 && p.Opponent.Hero.Weapon == enemyWeapon,
            "trade costs one, draws prior top card, shuffles source without battlecry");
        traded = Add(p, id); p.UsedMana = 10;
        Core2026Proof.Check(!TradeCardTask.IsValid(p, traded), "trade requires one available mana");
        p.UsedMana = 0;
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Core2026Proof.Check(!TradeCardTask.IsValid(p, traded), "empty deck forbids trade");
        var summoned = Summon(p, id);
        Core2026Proof.Check(p.Opponent.Hero.Weapon == enemyWeapon && !TradeCardTask.IsValid(p, summoned), "summon skips battlecry and board cannot trade");
    });
}
