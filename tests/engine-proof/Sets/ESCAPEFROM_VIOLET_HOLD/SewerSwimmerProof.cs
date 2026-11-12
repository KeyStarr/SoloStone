using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SewerSwimmerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_395", (g, p, id) => {

        var egg = Summon(p, "FP1_007"); var enemy = Summon(p.Opponent, "FP1_007");
        var source = Add(p, id);
        var options = p.Options().Where(t => t.Source == source && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == egg) && options.All(t => t.Target != enemy && t.Target != p.Hero), "friendly minion target only");
        PlayEntity(g, p, source, egg);
        Core2026Proof.Check(egg.Zone == p.BoardZone && p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 1, "triggers without killing target");
        egg.Silence(); int count = p.BoardZone.Count;
        var next = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(p.Options().Where(t => t.Source == next && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).All(t => t.Target == null), "silenced and non-Deathrattle minions excluded from target options");
        PlayEntity(g, p, next);
        Core2026Proof.Check(p.BoardZone.Count == count + 1, "silenced deathrattle does not trigger");
        ClearBoard(g, p);
        egg = Summon(p, "FP1_007"); egg.Silence();
        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_906e"), egg, egg);
        Play(g, p, id, egg);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "JAIL_906") == 1 && !p.BoardZone.Any(m => m.Card.Id == "FP1_007t"), "silenced target activates newly granted Deathrattle only");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p); source = Play(g, p, id); Core2026Proof.Check(source.Zone == p.BoardZone, "no friendly target still playable");
    });
}
