using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class TyphoonProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_232", (g, p, id) => {
        var friendly = Summon(p, "EDR_485"); var enemy = Summon(p.Opponent, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), p.Hero, friendly); enemy.Damage = 3;
        // Seed 2 keeps both owners; seed 11 sends each minion to the opposite player.
        foreach (int seed in new[] { 2, 11 }) {
            var seeded = g.CloneExact(history: true); seeded.SetRandomSeed(seed);
            var caster = seeded.Player1; var opponent = seeded.Player2;
            int casterDeck = caster.DeckZone.Count, opponentDeck = opponent.DeckZone.Count;
            Play(seeded, caster, id);
            Core2026Proof.Check(caster.DeckZone.Count == casterDeck + 1 && opponent.DeckZone.Count == opponentDeck + 1,
                "seeded shuffle sends exactly one minion to each deck");
            foreach (var original in new[] { friendly, enemy }) {
                var shuffled = (Minion)seeded.IdEntityDic[original.Id];
                var expected = original == friendly ? caster : opponent;
                if (seed == 11) expected = expected.Opponent;
                Core2026Proof.Check(shuffled.Controller == expected && shuffled[GameTag.CONTROLLER] == expected.PlayerId
                    && shuffled.Zone == expected.DeckZone && expected.DeckZone.Contains(shuffled),
                    "seeded shuffle preserves or transfers both controllers and destination membership");
            }
        }
        int deck = p.DeckZone.Count + p.Opponent.DeckZone.Count; int hand = p.HandZone.Count;
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 0 && clone.Player2.BoardZone.Count == 0
            && p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1, "cloned random shuffle isolated");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0
            && p.DeckZone.Count + p.Opponent.DeckZone.Count == deck + 2 && p.HandZone.Count == hand,
            "all minions shuffled into decks without Deathrattles");
        foreach (var minion in new[] { friendly, enemy })
            Core2026Proof.Check(minion.Zone == minion.Controller.DeckZone && minion.Damage == 0
                && minion.AttackDamage == minion.Card.ATK && (minion.AppliedEnchantments?.Count ?? 0) == 0,
                "shuffled entity changes owner if needed and resets stats/damage/enchantments");
        Core2026Proof.Check(friendly.Controller.PlayerId == clone.IdEntityDic[friendly.Id].Controller.PlayerId
            && enemy.Controller.PlayerId == clone.IdEntityDic[enemy.Id].Controller.PlayerId, "same-seed RNG destinations reproduce");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 0, "empty board legal");
        var rager = Summon(p, "EDR_598"); rager.Silence();
        Core2026Proof.Check(!rager.CantBeTargetedBySpells, "Silence removes printed Elusive before shuffle");
        Play(g, p, id); var ragerOwner = rager.Controller;
        Core2026Proof.Check(rager.Zone == ragerOwner.DeckZone, "silenced Dream Rager shuffled into chosen deck");
        Core2026Proof.Check(Generic.Draw(ragerOwner, rager) == rager && rager.Zone == ragerOwner.HandZone, "same Dream Rager redrawn");
        if (g.CurrentPlayer != ragerOwner) End(g);
        PlayEntity(g, ragerOwner, rager);
        Core2026Proof.Check(!rager.IsSilenced && rager.CantBeTargetedBySpells && rager.CantBeTargetedByHeroPowers
            && rager[GameTag.CANT_BE_TARGETED_BY_SPELLS] == 1 && rager[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] == 1,
            "redrawn and replayed Dream Rager restores printed Elusive and targeting tags");
        End(g); var observer = ragerOwner.Opponent; var spell = Add(observer, "CS2_029");
        observer.BaseMana = 10; observer.UsedMana = 0; var ordinaryTarget = Summon(ragerOwner, "CS2_182");
        Core2026Proof.Check(observer.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == ordinaryTarget.Id)
            && !observer.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == rager.Id),
            "replayed Dream Rager rejects spell targeting while ordinary minion remains targetable");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        if (g.CurrentPlayer != p) End(g);
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        while (p.Opponent.BoardZone.Count < 7) Summon(p.Opponent, "CS2_182");
        deck = p.DeckZone.Count + p.Opponent.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count + p.Opponent.BoardZone.Count == 0
            && p.DeckZone.Count + p.Opponent.DeckZone.Count == deck + 14, "full boards all shuffled exactly once");
        var dormant = Summon(p, "EDR_469"); var ordinary = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(dormant.IsDormant && p.BoardZone.Single() == dormant && ordinary.Zone == ordinary.Controller.DeckZone,
            "Dormant minion excluded from all-minion effect");
        foreach (var owner in new[] { p, p.Opponent })
            while (!owner.DeckZone.IsFull) InDeck(owner, "CS2_182");
        friendly = Summon(p, "EDR_485"); enemy = Summon(p.Opponent, "CS2_182"); hand = p.HandZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(friendly.Zone == friendly.Controller.GraveyardZone && enemy.Zone == enemy.Controller.GraveyardZone
            && !friendly.ToBeDestroyed && p.HandZone.Count == hand && p.DeckZone.IsFull && p.Opponent.DeckZone.IsFull,
            "full destination burns without death event or Deathrattle; both decks capped");
    });
}
