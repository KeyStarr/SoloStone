using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class StorageScuffleProof
{
    private static void Discover(Game g, Controller p)
    {
        new EffectContext(g, p.Id, p.Hero.Id).Discover(new[] { Cards.FromId("GAME_005") });
        Generic.ChoicePick(p, g, p.Choice.Choices[0]); g.DeathProcessingAndAuraUpdate();
    }

    internal static void Run() => StandardSetsProof.Case("TLC_365", (g, p, id) => {
        var held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Card.ConsumesHistory && held.Cost == 3 && held.Card[GameTag.SPELL_SCHOOL] == 1, "Scuffle base cost/Arcane/consumer");
        var ctx = new EffectContext(g, p.Id, p.Hero.Id);
        Core2026Proof.Check(!ctx.Discover(System.Array.Empty<Card>()) && held.Cost == 3, "empty Discover does not complete");
        Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, ChoiceAction.HAND, new[] { Cards.FromId("GAME_005") }, null);
        Generic.ChoicePick(p, g, p.Choice.Choices[0]); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 3, "fixed non-Discover choice excluded");
        End(g); Discover(g, p.Opponent); Core2026Proof.Check(held.Cost == 3, "opponent Discover excluded"); End(g);
        ctx.Discover(new[] { Cards.FromId("GAME_005") }); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 3, "pending Discover not completed");
        var clone = g.Clone(); Generic.ChoicePick(clone.Player1, clone, clone.Player1.Choice.Choices[0]); clone.AuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 0 && held.Cost == 3, "clone Discover independent");
        Generic.ChoicePick(p, g, p.Choice.Choices[0]); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 0 && held[GameTag.COST] == 0, "completed own singleton Discover sets visible COST zero");
        var late = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(late.Cost == 0, "Discover before card creation counts");
        var victim = Summon(p.Opponent, "CS2_200"); PlayEntity(g, p, held, victim);
        Core2026Proof.Check(victim.Damage == 3, "normal spell deals three");
        End(g); Core2026Proof.Check(late.Cost == 3, "prior-turn Discover expires on opponent turn"); End(g);
        Core2026Proof.Check(late.Cost == 3, "previous own-turn Discover not current turn");
        var burn = g.Clone(); var owner = burn.Player1;
        while (!owner.HandZone.IsFull) Add(owner, "CS2_231");
        int count = owner.HandZone.Count; Discover(burn, owner);
        Core2026Proof.Check(owner.HandZone.Count == count && burn.IdEntityDic[late.Id].Cost == 0 && late.Cost == 3,
            "burned chosen reward still completes Discover, clone isolated");
        StandardSetsProof.ClearBoard(g, p); StandardSetsProof.ClearBoard(g, p.Opponent);
        var elusive = Summon(p.Opponent, "EDR_598");
        var stealth = Summon(p.Opponent, "CS2_231"); stealth.HasStealth = true;
        var immune = Summon(p.Opponent, "CS2_231"); immune.IsImmune = true;
        var legal = Summon(p.Opponent, "CS2_200");
        var spell = Entity.FromCard(p, Cards.FromId(id));
        var automatic = new EffectContext(g, p.Id, p.Hero.Id);
        automatic.CastSpell(spell.Id, HistoryCastKind.Effect); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(legal.Damage == 3 && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0,
            "automatic no-target cast selects legal minion with game RNG");
        spell = Entity.FromCard(p, Cards.FromId(id));
        new PlayTask(PlayType.SPELL, EntityType.TARGET).Process(g, p, p.Hero, elusive,
            new TaskStack { Playables = new System.Collections.Generic.List<IPlayable> { spell } });
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(legal.Damage == 6 && elusive.Damage == 0, "automatic producer's illegal Elusive target cannot be damaged");
        Kill(g, legal); var rng = g.Random.Clone();
        spell = Entity.FromCard(p, Cards.FromId(id)); automatic.CastSpell(spell.Id, HistoryCastKind.Repeat); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(g.Random.Next() == rng.Next() && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0,
            "no legal target fizzles without target RNG");
    });
}
