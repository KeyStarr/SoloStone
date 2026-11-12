using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UnstableSpellcasterProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_483", (g, p, id) => {
        var ordinary = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1 && ordinary.SpellPower == 1 && ordinary.IsRace(Race.ELEMENTAL), "no spell damage does not copy; intrinsic spell damage and Elemental");
        ClearBoard(g, p);
        var shield = Summon(p.Opponent, "CS2_231"); shield.HasDivineShield = true;
        Play(g, p, "CS2_008", shield);
        p.Hero.IsImmune = true; Play(g, p, "CS2_008", p.Hero); p.Hero.IsImmune = false;
        p.Opponent.Hero.TakeDamage((Spell)Entity.FromCard(p, Cards.FromId("CS2_008")), 0);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "Divine Shield, Immune and zero spell damage do not qualify");
        ClearBoard(g, p);
        var dealer = Summon(p, "CS2_231"); p.Opponent.Hero.TakeDamage(dealer, 1);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2, "minion-sourced damage does not qualify");
        ClearBoard(g, p);
        Generic.CastSpell(p.Opponent, g, (Spell)Entity.FromCard(p.Opponent, Cards.FromId("CS2_008")), p.Hero, 0);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "opponent spell damage during current global turn does not qualify");
        ClearBoard(g, p);
        Play(g, p, "CS2_008", p.Hero);
        End(g);
        Play(g, p.Opponent, "CS2_008", p.Hero);
        End(g);
        var hand = (Minion)Add(p, id);
        PlayEntity(g, p, hand);
        Core2026Proof.Check(p.BoardZone.Count == 1, "previous-turn own and opponent damage do not qualify");
        ClearBoard(g, p);
        var clone = g.Clone();
        Play(clone, clone.Player1, "CS2_008", clone.Player1.Hero);
        Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 2 && p.BoardZone.Count == 0, "clone-only own spell hitting friendly hero qualifies before creation");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "clone damage cannot qualify original");
        ClearBoard(g, p);
        Play(g, p, "CS2_008", p.Hero);
        var left = Summon(p, "CS2_182");
        var location = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
        var right = Summon(p, "CS2_182");
        hand = (Minion)Add(p, id);
        Play(g, p, "CORE_CFM_753");
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, hand, zonePosition: location.ZonePosition + 1)), "play beside location");
        var copies = p.BoardZone.Where(m => m.Card.Id == id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 3 && m.Health == 6 && m.SpellPower == 1), "copy retains hand buff and Spell Damage");
        Core2026Proof.Check(p.BoardZone.GetSlot(hand.ZonePosition - 1).Id == location.Id
            && p.BoardZone.GetSlot(hand.ZonePosition + 1).Id == copies.Single(m => m.Id != hand.Id).Id
            && p.BoardZone.GetSlot(hand.ZonePosition + 2).Id == right.Id, "copy uses physical slot immediately right of source");
        var copy = copies.Single(m => m.Id != hand.Id); copy.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.AttackDamage == 2 && copy.Health == 5 && copy.SpellPower == 0 && hand.SpellPower == 1, "copy buff and intrinsic keyword silence independently");
        ClearBoard(g, p);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "mixed full board suppresses copy");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
    });
}
