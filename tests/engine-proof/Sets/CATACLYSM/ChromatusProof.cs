using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ChromatusProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_432", (g, p, id) => {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.HasTaunt && body.HasLifeSteal && body.CantBeTargetedBySpells && body.CantBeTargetedByHeroPowers && body.HasDivineShield, "all four body keywords");
        var heads = ColossalRules.AppendagesOf(body).ToDictionary(h => h.Card.Id);
        Core2026Proof.Check(heads.Count == 4 && heads["CATA_432t1"].HasTaunt && heads["CATA_432t2"].HasLifeSteal && heads["CATA_432t3"].CantBeTargetedBySpells && heads["CATA_432t4"].HasDivineShield, "each head owns keyword");
        for (int i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                StandardSetsProof.ParkBoard(p); body = Summon(p, id);
                heads = ColossalRules.AppendagesOf(body).ToDictionary(h => h.Card.Id);
            }
            var unrelated = Summon(p, id);
            Kill(g, heads[new[] { "CATA_432t1", "CATA_432t2", "CATA_432t3", "CATA_432t4" }[i]]);
            Core2026Proof.Check(body.HasTaunt == (i != 0) && body.HasLifeSteal == (i != 1) && body.CantBeTargetedBySpells == (i != 2)
                && body.CantBeTargetedByHeroPowers == (i != 2) && body.HasDivineShield == (i != 3), "head removes only own keyword; others stay");
            Core2026Proof.Check(unrelated.HasTaunt && unrelated.HasLifeSteal && unrelated.CantBeTargetedBySpells && unrelated.HasDivineShield, "own linked body only");
        }
        StandardSetsProof.ParkBoard(p); body = Summon(p, id);
        body.HasDivineShield = false;
        Kill(g, ColossalRules.AppendagesOf(body).Single(h => h.Card.Id == "CATA_432t4"));
        Core2026Proof.Check(!body.HasDivineShield && body.HasTaunt && body.HasLifeSteal && body.CantBeTargetedBySpells, "bronze head removing an already spent Divine Shield is a no-op");
        StandardSetsProof.ParkBoard(p); body = Summon(p, id); heads = ColossalRules.AppendagesOf(body).ToDictionary(h => h.Card.Id);
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        foreach (var h in heads.Values) Kill(g, h);
        Core2026Proof.Check(body.IsSilenced && !body.HasTaunt && !body.HasLifeSteal && !body.CantBeTargetedBySpells && !body.HasDivineShield, "silenced body has nothing to remove");
        StandardSetsProof.ParkBoard(p); body = Summon(p, id); heads = ColossalRules.AppendagesOf(body).ToDictionary(h => h.Card.Id);
        heads["CATA_432t1"].Silence(); Kill(g, heads["CATA_432t1"]); Core2026Proof.Check(body.HasTaunt, "silenced head loses Deathrattle");
        Kill(g, body); foreach (var h in heads.Values.Where(h => h.Card.Id != "CATA_432t1")) Kill(g, h);
        Core2026Proof.Check(p.BoardZone.Count == 0, "heads dying after body safely no-op");
        body = Summon(p, id);
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        body.HasTaunt = true; body.HasDivineShield = true;
        Kill(g, ColossalRules.AppendagesOf(body).Single(h => h.Card.Id == "CATA_432t1"));
        Core2026Proof.Check(!body.HasTaunt && body.HasDivineShield, "green head removes Taunt granted after body Silence; shield stays");
        Kill(g, ColossalRules.AppendagesOf(body).Single(h => h.Card.Id == "CATA_432t4"));
        Core2026Proof.Check(!body.HasTaunt && !body.HasDivineShield, "bronze head removes Divine Shield granted after body Silence");
    });
}
