using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MisplacedPyromancerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_101", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Card.AssetId == 121670 && source.AttackDamage == 4 && source.BaseHealth == 3,
            "3-Mana 4/3 Pyromancer");
        var first = Summon(p.Opponent, "TIME_046"); DormantRules.Awaken(first);
        var second = Summon(p.Opponent, "TIME_046"); DormantRules.Awaken(second);
        var sleeper = Summon(p.Opponent, "TIME_046");
        int events = 0;
        g.TriggerManager.Events.Trace = (e, _, _) => { if (e is ShatterEvent) events++; };
        Add(p, "CS2_182"); Add(p, "CATA_489");
        Core2026Proof.Check(events == 1 && first.Damage == 2 && second.Damage == 2 && sleeper.Damage == 0 && source.Damage == 0 &&
            p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0,
            "Unpinned_ShatterEvent real Arcane Flow split deals exactly 2 to every active enemy minion once");
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { "CATA_489t", "CS2_182", "CATA_489t2" }) &&
            p.HandZone.Where(c => c.Card.Shatter != null).All(c => c.Cost == 4), ShatterRulesProof.Leans);
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var c = next.Player1;
            Add(c, "CATA_489");
            Core2026Proof.Check(((Minion)next.IdEntityDic[first.Id]).Damage == 4 && ((Minion)next.IdEntityDic[second.Id]).Damage == 4,
                "cloned/restored typed split listener deals exactly 2");
        }
        Core2026Proof.Check(first.Damage == 2 && second.Damage == 2, "clone isolation");
        var divider = p.HandZone.Single(c => c.Card.Id == "CS2_182");
        p.SetasideZone.Add(p.HandZone.Remove(divider));
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "CATA_489" && p.HandZone.Single()[ShatterRules.AlreadyCombined] == 1 &&
            events == 1 && first.Damage == 2, "join is not Shatter");
        ShatterRules.GiveCombined(p, "CATA_489", p.Hero);
        Core2026Proof.Check(events == 1 && first.Damage == 2, "combined grant is not Shatter");
        // Space is counted before entry (49c82af4d): a full hand burns the parent; one space admits the left half.
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 10); Add(p, "CATA_489");
        Core2026Proof.Check(events == 1 && first.Damage == 2, "full hand burns the parent; overflow burn emits no event");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9); Add(p, "CATA_489");
        Core2026Proof.Check(events == 2 && first.Damage == 4 && p.HandZone.Count == 10 && p.HandZone[0].Card.Id == "CATA_489t" &&
            !p.HandZone.Any(c => c.Card.Id == "CATA_489t2"), "left-only split still fires once");
        PoolPilotProof.EmptyHand(p);
        Add(p.Opponent, "CATA_489");
        Core2026Proof.Check(first.Damage == 4 && source.Damage == 0, "opponent split does not trigger friendly Pyromancer");
        DormantRules.Make(source, 2, "TIME_058e", p.Hero); Add(p, "CATA_489");
        Core2026Proof.Check(first.Damage == 4, "sleep suppresses own ordinary split trigger");
        DormantRules.Awaken(source); source.Silence();
        PoolPilotProof.EmptyHand(p); Add(p, "CATA_489");
        Core2026Proof.Check(first.Damage == 4, "Silence removes split listener");
        Kill(g, source); PoolPilotProof.EmptyHand(p); Add(p, "CATA_489");
        Core2026Proof.Check(first.Damage == 4, "dead source has no split listener");
        source = Summon(p, id); Generic.ReturnToHandBlock(p, source);
        PoolPilotProof.EmptyHand(p); Add(p, "CATA_489");
        Core2026Proof.Check(first.Damage == 4, "source leaves board; hand copy cannot trigger");
        source = Summon(p, id); var other = Summon(p, id);
        PoolPilotProof.EmptyHand(p); Add(p, "CATA_489");
        Core2026Proof.Check(first.Damage == 8 && second.Damage == 8 && sleeper.Damage == 0,
            "each active Pyromancer fires once; damage not spell damage");
    });
}
