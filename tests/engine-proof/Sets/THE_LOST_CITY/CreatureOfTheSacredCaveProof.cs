using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CreatureOfTheSacredCaveProof
{
    private static SpellCastFact[] Recasts(Game g, int controller) => g.HistoryFacts.Query.Casts()
        .Where(f => f.ControllerId == controller && f.CasterKind == HistoryCastKind.Repeat && f.Outcome == HistoryCastOutcome.Completed).ToArray();

    internal static void Run() => StandardSetsProof.Case("TLC_430", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 5 && source.Card.IsRace(Race.BEAST), "Cave printed stats/Beast");
        End(g); Core2026Proof.Check(Recasts(g, p.Id).Length == 0, "empty own cast list no recast");
        Play(g, p.Opponent, "CORE_CS2_093"); End(g);
        GladesongSirenProof.Cast(g, p, "CORE_CS2_093", HistoryCastKind.Effect);
        Play(g, p, "GAME_005");
        End(g); Core2026Proof.Check(Recasts(g, p.Id).Length == 0, "opponent, previous-turn and non-Holy/automatic casts excluded"); End(g);
        TriggerManager.TriggerHandler counter = entity => ((Spell)entity).IsCountered = true;
        g.TriggerManager.CastSpellTrigger += counter; Play(g, p, "CORE_CS2_093"); g.TriggerManager.CastSpellTrigger -= counter;
        End(g); Core2026Proof.Check(Recasts(g, p.Id).Length == 0, "countered Holy excluded"); End(g);
        var victim = Summon(p.Opponent, "CS2_182"); victim[GameTag.HEALTH] = 30;
        Play(g, p, "CORE_CS1_130", victim); Play(g, p, "CORE_CS1_130", victim);
        Play(g, p, "CORE_CS2_004", victim);
        var casts = g.HistoryFacts.Query.Casts(g.HistoryFacts.Query.ThisTurn).Where(f => f.ControllerId == p.Id
            && f.CasterKind == HistoryCastKind.Player && f.Outcome == HistoryCastOutcome.Completed && f.SpellSchool == 5).ToArray();
        Core2026Proof.Check(casts.Length == 3 && casts.Count(f => f.Definition.Materialize().Id == "CORE_CS1_130") == 2, "duplicate casts retain occurrence weighting");
        var preview = g.CloneExact(); int selected = preview.Random.Next(casts.Length);
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[source.Id]).Silence(); End(clone);
        Core2026Proof.Check(Recasts(clone, p.Id).Length == 0 && source.ActivatedTrigger != null, "clone silence removes own trigger only");
        int sourceHealth = source.Health;
        End(g); var recast = Recasts(g, p.Id).Single();
        Core2026Proof.Check(recast.Definition.DbfId == casts[selected].DbfId, "seeded occurrence selection preserves duplicates");
        Core2026Proof.Check(source.Health == sourceHealth + (recast.Definition.Materialize().Id == "CORE_CS1_130" ? -3 : 2), "recast targets source when legal");
        End(g); End(g); Core2026Proof.Check(Recasts(g, p.Id).Length == 1, "previous-turn casts and own Repeat are not eligible"); End(g);
        Kill(g, source);
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        Play(g, p, "CORE_CS2_004", victim);
        source = (Minion)Play(g, p, id);
        End(g); Core2026Proof.Check(source.Health == 7, "Holy cast before minion created still recasts"); End(g);
        source.CantBeTargetedBySpells = true;
        Play(g, p, "CORE_CS1_130", victim); int damage = victim.Damage;
        End(g); Core2026Proof.Check(victim.Damage == damage + 3 && source.Health == 7, "unavailable self uses legal random target"); End(g);
        Play(g, p, "CORE_CS1_130", victim); Kill(g, victim);
        int beforeNoTarget = Recasts(g, p.Id).Length;
        End(g); Core2026Proof.Check(Recasts(g, p.Id).Length == beforeNoTarget, "required target unavailable skips recast"); End(g);
        Kill(g, source); int count = Recasts(g, p.Id).Length;
        Play(g, p, "CORE_CS2_093"); End(g);
        Core2026Proof.Check(Recasts(g, p.Id).Length == count, "source leaving board removes trigger");
    });
}
