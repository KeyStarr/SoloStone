using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BrollBearmantleProof
{
    private static void Companion(Game g, Controller p, Minion source, Minion token)
    {
        Core2026Proof.Check(token.Card.Id is "NEW1_032" or "NEW1_033" or "NEW1_034", "exact Animal Companion family");
        Core2026Proof.Check(g.IdEntityDic[token.Id] == token && token.Controller == p && token.Zone == p.BoardZone
            && token[GameTag.DISPLAYED_CREATOR] == source.Id && token.ZonePosition == source.ZonePosition + 1, "fresh companion owner/creator/physical position");
        Core2026Proof.Check(token.Card.Cost == 3 && token.Card.IsRace(Race.BEAST), "companion cost and Beast type");
        if (token.Card.Id == "NEW1_032") Core2026Proof.Check(token.Card.AssetId == 959 && token.AttackDamage == 4 && token.Health == 4 && token.HasTaunt, "exact Misha");
        if (token.Card.Id == "NEW1_034") Core2026Proof.Check(token.Card.AssetId == 100 && token.AttackDamage == 4 && token.Health == 2 && token.HasCharge && !token.IsExhausted, "exact Huffer ready with Charge");
        if (token.Card.Id == "NEW1_033") Core2026Proof.Check(token.Card.AssetId == 226 && token.AttackDamage == 2 && token.Health == 4 && source.AttackDamage == 4, "exact Leokk buffs other friendly minions");
    }

    internal static void Run() => StandardSetsProof.Case("EDR_853", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Broll play");
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_171");
        LocationProofs.Location(p, position: 0);
        Core2026Proof.Check(p.BoardZone.SlotCount == 5, "playing minion does not trigger");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Core2026Proof.Check(p.BoardZone.SlotCount == 5, "opponent spell does not trigger");
        PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, "GAME_005"));
        var token = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
        Companion(g, p, source, token);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) is BoardObject && p.BoardZone.GetSlot(source.ZonePosition - 1) == left
            && p.BoardZone.GetSlot(token.ZonePosition + 1) == right, "location consumes physical slot before source");
        p.SetasideZone.Add(p.BoardZone.Remove(token)); g.DeathProcessingAndAuraUpdate();
        var seen = new HashSet<string> { token.Card.Id };
        for (int i = 0; i < 40 && seen.Count < 3; i++)
        {
            Play(g, p, "GAME_005"); token = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Companion(g, p, source, token); seen.Add(token.Card.Id);
            Core2026Proof.Check(enemy.AttackDamage == 4 && (token.Card.Id != "NEW1_033" || left.AttackDamage == 5), "Leokk side and source exclusions");
            if (token.Card.Id == "NEW1_033")
            {
                var cloned = g.CloneExact(); ((Minion)cloned.IdEntityDic[token.Id]).Silence(); cloned.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(((Minion)cloned.IdEntityDic[source.Id]).AttackDamage == 3 && source.AttackDamage == 4, "Leokk Silence and clone isolation");
            }
            p.SetasideZone.Add(p.BoardZone.Remove(token)); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(source.AttackDamage == 3 && left.AttackDamage == 4, "Leokk aura stops on departure");
        }
        Core2026Proof.Check(seen.Count == 3, "all three exact companions exercised at each seed");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "GAME_005"); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full physical board blocks summon");
        Kill(g, p.BoardZone.Last()); source.Silence(); g.DeathProcessingAndAuraUpdate(); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.BoardZone.SlotCount == 6 && source.ActivatedTrigger == null, "Silence removes Broll trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.BoardZone.SlotCount == 5, "removed source cannot trigger");
        source = (Minion)Add(p, id); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.BoardZone.SlotCount == 5, "hand source cannot trigger");
        PlayEntity(g, p, source); source.Damage = source.Health - 1; Play(g, p, "CS2_029", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 5, "spell killing source prevents after-cast summon");
        source.Card.Power.Trigger.SingleTask.Process(g, p, source, null); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        token = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
        Core2026Proof.Check(token.ZonePosition == p.BoardZone.SlotCount - 1, "already-committed summon appends when source is gone");
    });
}
