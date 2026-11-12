using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class KronaKeeperOfEonsProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_705", (g, p, id) => {
        foreach (int count in new[] { 0, 3, 8 }) {
            var trial = g.CloneExact(history: true); var owner = trial.Player1; ClassDeck(owner);
            for (int i = 0; i < count; i++) owner.DeckZone.Add(Entity.FromCard(owner, Cards.FromId("CS2_182")));
            var deck = owner.DeckZone.ToArray(); var order = deck.Select(c => c.Id).ToArray();
            if (count > 0) Effects.AddCost(2).ApplyTo(deck.First());
            var source = (Minion)Play(trial, owner, id);
            Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 4 && source.Health == 7, "4/7 Taunt");
            Core2026Proof.Check(order.SequenceEqual(owner.DeckZone.Select(c => c.Id)), "bottom SET preserves deck order/entities");
            for (int i = 0; i < count; i++) Visible(deck[i], i < 5 ? 1 : 4);
            var later = Entity.FromCard(owner, Cards.FromId("CS2_182")); owner.DeckZone.Add(later); Visible(later, 4);
            IPlayable copy = count > 0 ? Generic.Copy(owner.Opponent, source, deck.First(), Zone.HAND) : null;
            if (copy != null) Visible(copy, 1);
            var addedDraw = Generic.Draw(owner); Visible(addedDraw, 4);
            Core2026Proof.Check(addedDraw.Id == later.Id, "ordinary draw starts at last deck index");
            for (int i = count - 1; i >= 0; i--) {
                var drawn = Generic.Draw(owner);
                Core2026Proof.Check(drawn.Id == order[i], "ordinary draws preserve original deck order");
                Visible(drawn, i < 5 ? 1 : 4);
                if (i == 0) { Effects.AddCost(2).ApplyTo(drawn); Visible(drawn, 3); Visible(copy, 1); }
            }
            Resume(trial, End);
        }
        var quiet = g.CloneExact(history: false); var original = quiet.Player1.DeckZone.ToArray();
        Play(quiet, quiet.Player1, id);
        Core2026Proof.Check(original.Take(5).All(c => c.Cost == 1) && original.Skip(5).All(c => c.Cost == 4), "History=false bottom five");
        var summoned = Summon(p, id); Core2026Proof.Check(p.DeckZone.All(c => c.Cost == 4), "summon does not execute Battlecry");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var held = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t.Source == held), "full board rejects Krona");
    });
}
