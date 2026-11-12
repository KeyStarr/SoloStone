using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DeathrotMawProof
{
    private static readonly string[] Beasts = { "TLC_446t2", "TLC_446t3", "TLC_446t4" };
    private static void Beast(Minion m)
    {
        Core2026Proof.Check(Beasts.Contains(m.Card.Id) && m.IsRace(Race.DEMON) && m.IsRace(Race.BEAST) && m.Card.Cost == 5, "exact Fel Beast family");
        int dbf = m.Card.Id switch { "TLC_446t2" => 118228, "TLC_446t3" => 120037, _ => 120038 };
        Core2026Proof.Check(m.Card.AssetId == dbf && m.Card.Implemented, "registered exact token identity");
        var (attack, health) = m.Card.Id switch { "TLC_446t2" => (5, 3), "TLC_446t3" => (4, 4), _ => (3, 5) };
        Core2026Proof.Check(m.AttackDamage == attack && m.Health == health, "exact Fel Beast stats");
        Core2026Proof.Check(m.IsRush == (m.Card.Id == "TLC_446t2") && m.HasLifeSteal == (m.Card.Id == "TLC_446t2") &&
            m.HasCharge == (m.Card.Id == "TLC_446t3") && m.CantBeTargetedBySpells == (m.Card.Id == "TLC_446t3") &&
            m.HasTaunt == (m.Card.Id == "TLC_446t4") && (m[GameTag.REBORN] > 0) == (m.Card.Id == "TLC_446t4"), "exact Fel Beast keywords");
    }
    internal static void Run() => StandardSetsProof.Case("TLC_479", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Maw play");
        var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && p.BoardZone.SlotCount == 3, "printed Taunt with no Battlecry summon");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id); Beast(output);
        Core2026Proof.Check(output.Controller == p && output.ZonePosition == 1 && output.Id > source.Id && p.BoardZone.GetSlot(0) == left && p.BoardZone.GetSlot(2) == right, "new entity with owner, creator, former source slot");
        foreach (string beast in Beasts)
        {
            Batch9PoolProof.CleanBoard(g, p); var m = Summon(p, beast); Beast(m); m.Silence();
            Core2026Proof.Check(!m.HasCharge && !m.IsRush && !m.HasTaunt && !(m[GameTag.REBORN] > 0) && !m.HasLifeSteal && !m.CantBeTargetedBySpells, "Silence clears token keywords");
        }
        Batch9PoolProof.CleanBoard(g, p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes random Fel Beast Deathrattle");
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        PoolPilotProof.Resolve(g, source, death: true);
        output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id); Beast(output);
        Core2026Proof.Check(output.ZonePosition == source.ZonePosition + 1 && p.BoardZone.GetSlot(output.ZonePosition + 1) == right,
            "copied Deathrattle summons right of live source");
        Batch9PoolProof.CleanBoard(g, p);
        LostCityPoolProof.FullBoard(p); source = (Minion)Add(p, id);
        PoolPilotProof.Resolve(g, source, death: true); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full mixed board blocks fixed summon");
        Kill(g, p.BoardZone.Last()); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "freed source slot permits one summon"); Beast(p.BoardZone.Last());
        End(g);
    });
}
