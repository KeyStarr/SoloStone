using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PlatysaurProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_603", (g, p, id) => {
        int deck = p.DeckZone.Count; var source = (Minion)Play(g, p, id); var drawn = p.HandZone.Single(); var other = Add(p, "CS2_182");
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "Battlecry draws exactly one"); var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.IdEntityDic[drawn.Id].Zone.Type == Zone.GRAVEYARD && drawn.Zone == p.HandZone, "clone remembers drawn entity without mutating original");
        Kill(g, source); Core2026Proof.Check(drawn.Zone == p.GraveyardZone && other.Zone == p.HandZone, "Deathrattle discards only drawn entity");
        source = (Minion)Play(g, p, id); drawn = p.HandZone.Last(); PlayEntity(g, p, drawn); Kill(g, source); Core2026Proof.Check(drawn.Zone == p.BoardZone, "already played card is not discarded");
        source = (Minion)Play(g, p, id); drawn = p.HandZone.Last(); Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(drawn.Zone == p.HandZone, "Silence removes discard");
        ClearBoard(g, p); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source = Summon(p, id); source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 10, "burned draw cannot discard another card");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); int damage = p.Hero.Damage; source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage > damage && p.HandZone.Count == 10, "fatigue records no card");
    });
}
