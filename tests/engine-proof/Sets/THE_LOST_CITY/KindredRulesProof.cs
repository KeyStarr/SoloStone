using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Loader;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class KindredRulesProof
{
    private static Card School(int school, CardType type = CardType.SPELL) => new Card("kindred-school-proof", -100002,
        new[] { new Tag(GameTag.CARDTYPE, (int)type), new Tag((GameTag)1635, school) },
        new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>());

    internal static void Run()
    {
        // Lean_SpellSchoolKindred: kindred-school-match.yaml; scalar, never bitwise.
        var spells = new[] { "TLC_236", "TLC_440", "TLC_447", "TLC_519", "TLC_815", "TLC_816" }.Select(Cards.FromId).ToArray();
        foreach (var left in spells)
        foreach (var right in spells)
            Core2026Proof.Check(KindredRules.SharesKindredType(left, right) == (left[(GameTag)1635] == right[(GameTag)1635]), "Lean_: exact school equality across all assigned spells");
        foreach (int school in new[] { 0, 8, 10, 16 })
            Core2026Proof.Check(!KindredRules.SharesKindredType(School(school), School(school)), "Lean_: schoolless and unsupported scalar values never match");
        Core2026Proof.Check(!KindredRules.SharesKindredType(School(3), School(1)) && !KindredRules.SharesKindredType(School(3), School(2)), "Lean_: schools not a bitmask");
        Core2026Proof.Check(!KindredRules.SharesKindredType(School(3), School(3, CardType.MINION))
            && !KindredRules.SharesKindredType(School(3, CardType.MINION), School(3))
            && !KindredRules.SharesKindredType(null, School(3)) && !KindredRules.SharesKindredType(School(3), null), "Lean_: no mixed type or null match");
        Core2026Proof.Check(KindredRules.SharesKindredType(Cards.FromId("CORE_LOE_039"), Cards.FromId("CS2_171"))
            && KindredRules.SharesKindredType(Cards.FromId("DINO_435"), Cards.FromId("CS2_171"))
            && !KindredRules.SharesKindredType(Cards.FromId("CS2_182"), Cards.FromId("CS2_182")), "Lean_: dual types, ALL and typeless minion behaviour retained");
        foreach (bool history in new[] { false, true }) Lean_PreviousOwnTurnOnly(history);
        Console.WriteLine("PASS Lean_SpellSchoolKindred / Lean_PreviousOwnTurnOnly / Lean_CounteredPlayCounts / Lean_IndirectCastDoesNotCount; history=on/off");
    }

    private static void Lean_PreviousOwnTurnOnly(bool history)
    {
        // kindred-turn-window.yaml / kindred-play-versus-cast.yaml; preserve controller snapshot semantics.
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var source = Add(p, "TLC_440");
        var indirect = (Spell)Entity.FromCard(p, Cards.FromId("TLC_440"));
        Generic.CastSpell(p, g, indirect, p.Opponent.Hero, 0);
        End(g); End(g);
        Core2026Proof.Check(!SelfCondition.Kindred.Eval(source) && !g.HistoryFacts.Query.Plays().Any(x => x.EntityId == indirect.Id), "Lean_: effect cast alone never enters previous-turn plays");
        var countered = (Spell)Add(p, "TLC_440");
        TriggerManager.TriggerHandler intercept = entity => { if (entity.Id == countered.Id) ((Spell)entity).IsCountered = true; };
        g.TriggerManager.CastSpellTrigger += intercept;
        int health = p.Opponent.Hero.Health; PlayEntity(g, p, countered, p.Opponent.Hero);
        g.TriggerManager.CastSpellTrigger -= intercept;
        Core2026Proof.Check(p.Opponent.Hero.Health == health && !SelfCondition.Kindred.Eval(source), "Lean_: counter prevents damage; current-turn play insufficient");
        End(g); End(g);
        Core2026Proof.Check(SelfCondition.Kindred.Eval(source)
            && g.HistoryFacts.Query.Plays(g.HistoryFacts.Query.PreviousTurnOf(p.Id)).Any(x => x.EntityId == countered.Id), "Lean_: countered committed play matches previous-own-turn history");
        var clone = g.CloneExact(history: history); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        Core2026Proof.Check(SelfCondition.Kindred.Eval(clone.IdEntityDic[source.Id]) && SelfCondition.Kindred.Eval(loaded.IdEntityDic[source.Id]), "Lean_: clone and snapshot preserve school window");
        p.NumTurnsLeft = 2; End(g);
        Core2026Proof.Check(g.CurrentPlayer == p && !SelfCondition.Kindred.Eval(source)
            && SelfCondition.Kindred.Eval(clone.IdEntityDic[source.Id]), "Lean_: empty extra own turn replaces older match; clone isolated");
        End(g); Play(g, p.Opponent, "TLC_440", p.Hero); End(g);
        Core2026Proof.Check(!SelfCondition.Kindred.Eval(source), "Lean_: opponent school never activates friendly Kindred");
        var copy = Generic.Copy(p, p.Hero, countered, Zone.HAND); PlayEntity(g, p, copy, p.Opponent.Hero);
        End(g); End(g);
        Core2026Proof.Check(SelfCondition.Kindred.Eval(source), "Lean_: copied generated card committed from hand activates normally");
    }
}
