using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TimeAdmralHooktailProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_713", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var enemyLeft = Summon(p.Opponent, "CS2_182");
        var enemyRight = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(p.DragonInHand, "holding-Dragon condition sees Hooktail's secondary Dragon type");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 6 && source.IsRace(Race.DRAGON)
            && source.IsRace(Race.PIRATE) && !source.IsRace(Race.BEAST), "Hooktail stats and both printed types");
        var chest = p.Opponent.BoardZone.Single(m => m.Card.Id == "TIME_713t");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, right.Id })
            && p.Opponent.BoardZone.Select(m => m.Id).SequenceEqual(new[] { enemyLeft.Id, enemyRight.Id, chest.Id }),
            "opponent Chest appends; friendly board order unchanged");
        Core2026Proof.Check(chest.Controller == p.Opponent && chest.Card.AssetId == 120806 && chest.Card.Implemented
            && chest.AttackDamage == 0 && chest.Health == 8 && chest.HasDeathrattle, "exact 0/8 Chest with Deathrattle");
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 0, "no Coins until Chest dies");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[chest.Id]);
        Core2026Proof.Check(clone.Player1.HandZone.Count == 10 && p.HandZone.Count == 0 && chest.Zone.Type == Zone.PLAY,
            "clone Chest death and generated hand isolated");
        var preserved = Add(p, "CS2_182");
        Kill(g, chest);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Contains(preserved) && p.Opponent.HandZone.Count == 0,
            "Chest fills its opponent's remaining hand slots, preserves existing cards");
        var coins = p.HandZone.Where(c => c.Id != preserved.Id).ToArray();
        Core2026Proof.Check(coins.Length == 9 && coins.All(c => c.Card.Id == "GAME_005" && c.Card.AssetId == 1746
            && c.Card.Implemented && c.Cost == 0 && c.Card.Type == CardType.SPELL), "nine exact playable Coins");
        p.UsedMana = 5;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, coins[0])) && p.RemainingMana == 6, "generated Coin grants temporary mana");
        // Full recipient hand: no extra cards, including no burned overflow Coins.
        Add(p, "CS2_182");
        chest = Summon(p.Opponent, "TIME_713t");
        int entities = g.IdEntityDic.Count;
        Kill(g, chest);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic.Count == entities, "full hand creates no excess Coins");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        source.Silence();
        Core2026Proof.Check(source.IsRace(Race.DRAGON) && source.IsRace(Race.PIRATE), "Silence keeps both Hooktail types");
        Kill(g, source);
        chest = Summon(p.Opponent, "TIME_713t");
        chest.Silence(); Kill(g, chest);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Chest Deathrattle");
        chest = Summon(p.Opponent, "TIME_713t");
        Generic.ReturnToHandBlock(p.Opponent, chest);
        Core2026Proof.Check(p.HandZone.Count == 0, "bouncing Chest gives no Coins");
        ClearBoard(g, p.Opponent);
        for (int i = 0; i < 7; i++) Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 7 && !p.Opponent.BoardZone.Any(m => m.Card.Id == "TIME_713t"),
            "full opponent board prevents Chest");
        Kill(g, p.Opponent.BoardZone[0]); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 7 && p.Opponent.BoardZone.Last().Card.Id == "TIME_713t",
            "one free opponent slot accepts exactly one Chest");
        ClearBoard(g, p.Opponent);
        foreach (var owner in new[] { p, p.Opponent })
            foreach (var card in owner.HandZone.ToArray()) { owner.HandZone.Remove(card); owner.SetasideZone.Add(card); }
        chest = Summon(p, "TIME_713t"); Kill(g, chest);
        Core2026Proof.Check(p.Opponent.HandZone.Count == 10 && p.Opponent.HandZone.All(c => c.Card.Id == "GAME_005")
            && p.HandZone.Count == 0, "Chest ownership determines recipient on either side");
    });
}
