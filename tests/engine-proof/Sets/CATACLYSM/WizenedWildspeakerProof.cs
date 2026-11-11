using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WizenedWildspeakerProof
{
    private static void Resolve(Game g, Controller p, string id, int used)
    {
        var card = Add(p, id); p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card)), "Wildspeaker play accepted");
        Core2026Proof.Check(p.UsedMana == used && ((Minion)card).HasTaunt, "previous own-turn minion predicate and refresh exactly three");
        Kill(g, (Minion)card);
    }

    internal static void Run() => StandardSetsProof.Case("MEND_041", (g, p, id) => {
        Resolve(g, p, id, 2);
        Resolve(g, p, id, 2);
        p.NumTurnsLeft = 2; End(g);
        Resolve(g, p, id, 5);
        End(g);
        Play(g, p.Opponent, "CS2_231");
        End(g);
        Resolve(g, p, id, 5);
        End(g); End(g);
        Summon(p, "CS2_231"); Play(g, p, "GAME_005");
        End(g); Play(g, p.Opponent, "CS2_231"); End(g);
        var clone = g.Clone();
        Resolve(clone, clone.Player1, id, 2);
        clone.Player1.NumTurnsLeft = 2; End(clone);
        Resolve(clone, clone.Player1, id, 5);
        Resolve(g, p, id, 2);
        Core2026Proof.Check(g.CurrentPlayer == p && g.Turn + 1 == clone.Turn, "clone minion plays and extra-turn chronology isolated");
        ClearBoard(g, p);
        var card = Add(p, id); card.Cost = 0; p.UsedMana = 1;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card)) && p.UsedMana == 0, "refresh floors used mana at zero");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
    });
}
