using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Batch14K7Proof
{
    [GroupProof("b14-k7-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_566", Wriggler);
        Batch14K7CataclysmProof.Run();
        Batch14K7EmeraldDreamProof.Run();
        Batch14K7VioletHoldProof.Run();
        Batch14K7TimeTravelProof.Run();
    }

    private static void Wriggler(Game g, Controller p, string id)
    {
        HiddenShuffle(g, id);
        HiddenShuffle(g, id, buffed: true);
        ShuffleResetsHandEnchantments(g, id);
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "empty hand finishes without choice");
        ParkBoard(p);
        var baseline = g.CloneExact(history: true);
        foreach (int pick in new[] { 0, 1, 2 })
        {
            var next = baseline.CloneExact(history: true); var owner = next.Player1;
            var hand = new[] { Add(owner, "CS2_029"), Add(owner, "GAME_005"), Add(owner, "EX1_561") };
            var enemy = Add(owner.Opponent, "CS2_029");
            var deck = owner.DeckZone.Select(c => c.Id).ToArray();
            var source = (Minion)Play(next, owner, id);
            Core2026Proof.Check(owner.Choice.Choices.SequenceEqual(hand.Select(c => c.Id)), "only remaining friendly hand identities offered");
            int expectedPosition = next.CloneExact(history: true).Random.Next(deck.Length + 1);
            var bytes = next.SaveSnapshot();
            var branches = new[] { next, next.CloneExact(history: true), Game.LoadSnapshot(bytes) };
            foreach (var branch in branches)
            {
                PlayerDecisionProof.Answer(branch, hand[pick].Id);
                var chosen = branch.IdEntityDic[hand[pick].Id]; var player = branch.Player1;
                Core2026Proof.Check(chosen.Zone == player.DeckZone && chosen.Cost == Math.Max(0, chosen.Card.Cost - 1)
                    && player.HandZone.Select(c => c.Id).SequenceEqual(hand.Where(c => c.Id != chosen.Id).Select(c => c.Id))
                    && player.DeckZone.Count == deck.Length + 1 && branch.IdEntityDic[enemy.Id].Cost == 4,
                    "chosen physical card shuffles once and gets permanent one-cost reduction; other hand untouched");
                Core2026Proof.Check(player.DeckZone.Where(c => c.Id != chosen.Id).Select(c => c.Id).SequenceEqual(deck)
                    && player.DeckZone[expectedPosition].Id == chosen.Id, "seeded shuffle uses RNG position inside ordered deck");
                var body = (Minion)branch.IdEntityDic[source.Id]; body.Silence(); Kill(branch, body);
                End(branch); End(branch);
                var drawn = Generic.Draw(player, player.DeckZone.ToList().FindIndex(c => c.Id == chosen.Id));
                Core2026Proof.Check(drawn?.Id == chosen.Id && drawn.Cost == Math.Max(0, chosen.Card.Cost - 1),
                    "discount survives source Silence/death, turn change and real draw");
            }
            Core2026Proof.Check(branches.Skip(1).All(b => Canonical.State(b) == Canonical.State(next)), "parked choice clone/snapshot has identical RNG and future");
        }
        var singleton = (Minion)Add(p, "CS2_171");
        Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_570e"), singleton, singleton);
        CostRules.AddModifier(singleton, EffectOperator.ADD, 4);
        while (!p.DeckZone.IsFull) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); PlayerDecisionProof.Answer(g, singleton.Id);
        Core2026Proof.Check(singleton.Zone == p.HandZone && singleton.Cost == 5 && singleton.AttackDamage == 3
            && singleton.Health == 3 && singleton.AppliedEnchantments.Single().Card.Id == "EDR_570e",
            "full deck retains selected hand card without resetting buffs, discount or loss");
    }

    private static void ShuffleResetsHandEnchantments(Game baseline, string id)
    {
        // Tier A: dark-gift/dg7-backwards-moves.yaml; coordinator ruling: reset before the new discount.
        var game = baseline.CloneExact(history: true); var owner = game.Player1;
        var chosen = (Minion)Add(owner, "CS2_171");
        var retained = (Minion)Add(owner, "CS2_171");
        foreach (var card in new[] { chosen, retained })
        {
            Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_570e"), card, card);
            CostRules.AddModifier(card, EffectOperator.ADD, 4);
            Core2026Proof.Check(card.AttackDamage == 3 && card.Health == 3 && card.Cost == 5
                && card.AppliedEnchantments.Single().Card.Id == "EDR_570e", "buffed 1/1 hand fixture");
        }
        Play(game, owner, id);
        foreach (var branch in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            PlayerDecisionProof.Answer(branch, chosen.Id);
            var shuffled = (Minion)branch.IdEntityDic[chosen.Id];
            var untouched = (Minion)branch.IdEntityDic[retained.Id];
            Core2026Proof.Check(shuffled.Zone == branch.Player1.DeckZone && shuffled.AttackDamage == 1
                && shuffled.Health == 1 && shuffled.Cost == 0 && shuffled.AppliedEnchantments.Count == 0,
                "Wriggler resets hand buffs and attachments before applying only the one-cost discount");
            var drawn = (Minion)Generic.Draw(branch.Player1, branch.Player1.DeckZone.ToList().FindIndex(c => c.Id == chosen.Id));
            Core2026Proof.Check(drawn.Id == chosen.Id && drawn.Card.Id == "CS2_171" && drawn.AttackDamage == 1
                && drawn.Health == 1 && drawn.Cost == 0 && drawn.HasCharge && drawn.AppliedEnchantments.Count == 0,
                "buffed 1/1 redraws as printed card with only Wriggler discount");
            Core2026Proof.Check(untouched.Zone == branch.Player1.HandZone && untouched.AttackDamage == 3
                && untouched.Health == 3 && untouched.Cost == 5 && untouched.AppliedEnchantments.Single().Card.Id == "EDR_570e",
                "unchosen hand card retains its stats, enchantment and cost modifier");
        }
    }

    private static void HiddenShuffle(Game baseline, string id, bool buffed = false)
    {
        var branches = new[] { baseline.CloneExact(history: true), baseline.CloneExact(history: true) };
        var packets = new List<byte[]>[2];
        for (int i = 0; i < branches.Length; i++)
        {
            var game = branches[i]; End(game); var owner = game.Player2;
            var hidden = Add(owner, buffed ? (i == 0 ? "CS2_171" : "EX1_008") : (i == 0 ? "CS2_029" : "EX1_561"));
            if (buffed) Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_570e"), hidden, hidden);
            Add(owner, "CS2_182");
            var source = Add(owner, id);
            int start = game.PowerHistory.Full.Count;
            PlayEntity(game, owner, source);
            PlayerDecisionProof.Answer(game, hidden.Id);
            Core2026Proof.Check(hidden.Zone == owner.DeckZone, "opponent shuffle fixture moved real hidden card");
#if CLIENT_TRANSPORT
            packets[i] = StarsurgeProof.Packets(game, start);
#endif
        }
#if CLIENT_TRANSPORT
        Core2026Proof.Check(packets[0].Count == packets[1].Count && packets[0].Zip(packets[1]).All(p => p.First.SequenceEqual(p.Second)),
            "opponent receives byte-identical packets for different chosen hidden identities");
#endif
    }
}
