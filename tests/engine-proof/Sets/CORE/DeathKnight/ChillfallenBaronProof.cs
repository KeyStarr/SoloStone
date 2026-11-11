using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ChillfallenBaronProof
{
    internal static void Run() => StandardSetsProof.Case("RLK_708", (g, p, id) => {
        DeathTriggerPlayOrder(g);
        int deck = p.DeckZone.Count;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2 && source.HasDeathrattle
            && source.IsRace(Race.UNDEAD) && source.IsRace(Race.DRAENEI), "2/2 dual-type Baron with Deathrattle");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1
            && p.HandZone[0].Card.Id == "CS2_182", "Battlecry draws one actual deck card");
        Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 2, "death draws one more card");
        source = (Minion)Play(g, p, id);
        int hand = p.HandZone.Count; deck = p.DeckZone.Count;
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == hand && p.DeckZone.Count == deck, "Silence removes Deathrattle");
        source = Summon(p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck, "summoning does not trigger Battlecry");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand burns Deathrattle draw");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        int damage = p.Hero.Damage;
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == damage + 1 && p.HandZone.Count == 0, "empty-deck Battlecry fatigues");
        Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == damage + 3 && p.HandZone.Count == 0, "empty-deck Deathrattle advances fatigue");
    });

    private static void DeathTriggerPlayOrder(Game game)
    {
        // Re-enabling Acolyte requires death triggers and Deathrattles to share play-order resolution.
        if (!StandardSets.CollectibleIds.Contains("CORE_RLK_121"))
        {
            Core2026Proof.Check(!EffectModules.ByDbfId.ContainsKey(111318)
                && !Cards.All.Any(card => card.Id == "CORE_RLK_121"),
                "Acolyte excluded until death-trigger play order is supported");
            return;
        }
        var results = new List<string>();
        bool correct = true;
        foreach (var row in new[] { (spiritFirst: true, friendlyDamage: 1, enemyDamage: 3),
            (spiritFirst: false, friendlyDamage: 0, enemyDamage: 2) })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            foreach (var card in owner.DeckZone.ToArray()) { owner.DeckZone.Remove(card); owner.SetasideZone.Add(card); }
            owner.Hero.Damage = 0; owner.Hero.Fatigue = 0;
            Minion spirit;
            if (row.spiritFirst)
            {
                spirit = (Minion)Play(branch, owner, "CAP_803");
                Play(branch, owner, "CORE_RLK_121");
            }
            else
            {
                Play(branch, owner, "CORE_RLK_121");
                spirit = (Minion)Play(branch, owner, "CAP_803");
            }
            Kill(branch, spirit);
            correct &= owner.Hero.Damage == row.friendlyDamage && owner.Opponent.Hero.Damage == row.enemyDamage
                && owner.Hero.Fatigue == 1 && owner.HandZone.Count == 0;
            results.Add($"{(row.spiritFirst ? "Spirit first" : "Acolyte first")}: friendly damage={owner.Hero.Damage}, enemy damage={owner.Opponent.Hero.Damage}, fatigue={owner.Hero.Fatigue}");
        }
        Core2026Proof.Check(correct, "death-trigger play order: " + string.Join("; ", results));
    }
}
