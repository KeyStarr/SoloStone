using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AessinaProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_430", (g, p, id) => {
        HiddenPermutation();
        p.Opponent.Hero.Armor = 100;
        for (int i = 0; i < 18; i++) Kill(g, Summon(p, "CS2_231"));
        for (int i = 0; i < 2; i++) Kill(g, Summon(p.Opponent, "CS2_231"));
        Generic.DiscardBlock(p, Add(p, "CS2_182"));
        End(g); End(g);
        Kill(g, Summon(p, "CS2_231"));
        var source = Add(p, id);
        Core2026Proof.Check(source.Card.ConsumesHistory && g.HistoryFacts.HasConsumers, "declared death-history consumer");
        var clone = g.Clone();
        Kill(clone, Summon(clone.Player1, "CS2_231"));
        PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player2.Hero.Armor == 80 && p.Opponent.Hero.Armor == 100, "20th friendly death enables 20 missiles only in clone");
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 100, "19 deaths; enemy deaths and discard do not qualify");
        Kill(g, (Minion)source);
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 100;
        int armor = p.Opponent.Hero.Armor;
        Play(g, p, id);
        Core2026Proof.Check(armor - p.Opponent.Hero.Armor + enemy.Damage == 20, "20 random damage split across enemy minion and hero");
    });

    private static void HiddenPermutation()
    {
        foreach (bool inDeck in new[] { false, true })
        {
            Game Fixture(bool withCard)
            {
                var game = HistoryFactsProof.Fixture();
                var owner = game.Player2;
                var hidden = Entity.FromCard(owner, Cards.FromId(withCard ? "EDR_430" : "CS2_182"));
                if (inDeck) owner.DeckZone.Add(hidden); else owner.HandZone.Add(hidden);
                var victim = Summon(owner, "CS2_231");
                game.PowerHistory.Last.Clear();
                Kill(game, victim);
                game.AuraUpdate();
                return game;
            }
            var a = Fixture(true); var b = Fixture(false);
            Core2026Proof.Check(a.PowerHistory.Print(false) == b.PowerHistory.Print(false), "hidden Aessina causes no extra blocks or tags on death");
            var fa = FairBotState.Create(a, a.Player1.PlayerId, 42, DeckStyle.Control);
            var fb = FairBotState.Create(b, b.Player1.PlayerId, 42, DeckStyle.Control);
            Core2026Proof.Check(fa.Evaluate() == fb.Evaluate() && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())), "hidden permutation leaves projected evaluation and legal actions equal");
        }
    }
}
