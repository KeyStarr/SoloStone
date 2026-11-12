using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SpellweaversBrillianceProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_452", (g, p, id) => {
        var held = Add(p, id);
        Core2026Proof.Check(held.Cost == 10 && held[GameTag.COST] == 10, "no spell damage keeps base visible cost");
        var shield = Summon(p.Opponent, "CS2_231"); shield.HasDivineShield = true;
        Play(g, p, "CS2_008", shield);
        p.Hero.IsImmune = true; Play(g, p, "CS2_008", p.Hero); p.Hero.IsImmune = false;
        p.Hero.TakeDamage((Spell)Entity.FromCard(p, Cards.FromId("CS2_008")), 0);
        var dealer = Summon(p, "CS2_182"); p.Opponent.Hero.TakeDamage(dealer, 1);
        Generic.CastSpell(p.Opponent, g, (Spell)Entity.FromCard(p.Opponent, Cards.FromId("CS2_008")), p.Hero, 0);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 10, "prevented, zero, minion and opponent spell damage excluded");
        p.Opponent.Hero.Armor = 5; Play(g, p, "CS2_008", p.Opponent.Hero);
        Core2026Proof.Check(held.Cost == 9 && held[GameTag.COST] == 9, "armor-only spell damage counts");
        var victim = Summon(p, "CS2_231");
        Play(g, p, "CS2_029", victim);
        var late = Add(p, id);
        Core2026Proof.Check(held.Cost == 3 && late.Cost == 3 && late[GameTag.COST] == 3,
            "friendly one-Health victim contributes all six overkill damage before card creation");
        var clone = g.Clone();
        Play(clone, clone.Player1, "CS2_029", clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 0 && held.Cost == 3,
            "clone-only damage floors discount at zero without affecting original");
        End(g); End(g);
        Core2026Proof.Check(held.Cost == 10 && late.Cost == 10 && held[GameTag.COST] == 10,
            "previous global turn spell damage expires");
        PlayEntity(g, p, held);
        var dragon = p.BoardZone.Single(m => m.Card.Id == "CATA_452t");
        Core2026Proof.Check(dragon.Card.AssetId == 122455 && dragon.AttackDamage == 6 && dragon.Health == 6
            && dragon.IsRace(Race.DRAGON) && dragon.ZonePosition == p.BoardZone.SlotCount - 1,
            "exact Azure Warden token and spell summon at physical right edge");
        while (p.BoardZone.SlotCount < 7) Summon(p, "CS2_182");
        PlayEntity(g, p, late);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_452t") == 1,
            "full board suppresses token");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
        foreach (var minion in p.BoardZone.ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(minion));
        var extreme = Add(p, id);
        var spell = (Spell)Entity.FromCard(p, Cards.FromId("CS2_008"));
        for (int i = 0; i < 2; i++) Summon(p, "CS2_231").TakeDamage(spell, int.MaxValue);
        Core2026Proof.Check(g.HistoryFacts.Query.SpellDamageAmount(p.Id, g.HistoryFacts.Query.ThisTurn) == int.MaxValue,
            "multiple maximum spell hits saturate the turn total");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(extreme.Cost == 0 && extreme[GameTag.COST] == 0,
            "maximum spell damage floors Brilliance cost without overflow");
    });
}
