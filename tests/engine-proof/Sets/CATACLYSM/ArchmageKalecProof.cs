using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArchmageKalecProof
{
    [GroupProof("archmage-kalec", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("CATA_458", ArchmageKalec);
    private static void ArchmageKalec(Game g, Controller p, string id)
    {
        var fire = Add(p, "CS2_029"); var heal = Add(p, "CS1_130"); var minion = Add(p, "CS2_182");
        var deckSpell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var enemy = Add(p.Opponent, "CS2_029");
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(new[] { fire, heal, deckSpell }.All(c => c[GameTag.SPELLPOWER] == 1
            && c.AppliedEnchantments.Single().Card.Id == "CATA_458e") && minion[GameTag.SPELLPOWER] == 0
            && enemy[GameTag.SPELLPOWER] == 0 && p.CurrentSpellPower == 0, "only existing own hand/deck spells gain local bonus");
        body.Silence(); Kill(g, body);
        Play(g, p, id);
        Core2026Proof.Check(fire[GameTag.SPELLPOWER] == 2 && Add(p, "CS2_029")[GameTag.SPELLPOWER] == 0, "grants stack; future spells excluded");
        var copy = Generic.Copy(p.Opponent, p.Hero, fire, Zone.HAND);
        var backward = Generic.Copy(p, p.Hero, fire, Zone.DECK);
        Core2026Proof.Check(copy[GameTag.SPELLPOWER] == 2 && copy.AppliedEnchantments.Count == 2
            && copy.AppliedEnchantments.All(e => e.Target == copy && e.Controller == p.Opponent)
            && backward[GameTag.SPELLPOWER] == 0, "hand copy keeps independent local payload; backward copy drops it");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var spell = (IPlayable)next.IdEntityDic[fire.Id];
            int before = next.Player2.Hero.Damage;
            Generic.DamageCharFunc(spell, next.Player2.Hero, 6, true);
            Core2026Proof.Check(next.Player2.Hero.Damage == before + 8, "local grant remains after source Silence/death and clone/snapshot");
        }
        var changed = Generic.ChangeEntityBlock(p, fire, Cards.FromId("CS2_024"), true);
        Core2026Proof.Check(changed[GameTag.SPELLPOWER] == 0, "replacing a spell clears its removed local grants");
    }
}
