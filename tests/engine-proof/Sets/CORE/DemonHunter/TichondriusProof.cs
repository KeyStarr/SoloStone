using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TichondriusProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CATA_001", (g, p, id) => {
        var demon = Add(p, "CORE_BT_510"); var cheap = Add(p, "EX1_319");
        Effects.SetCost(0).ApplyTo(cheap);
        var ordinary = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var enemyDemon = Add(p.Opponent, "CORE_BT_510");
        var onBoard = Summon(p, "CORE_BT_510");
        var inDeck = Entity.FromCard(p, Cards.FromId("CORE_BT_510")); p.DeckZone.Add(inDeck);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check((int)source.Card.Class == 14 && source.IsRace(Race.DEMON)
            && source.Cost == 9 && source.AttackDamage == 9 && source.Health == 8, "9 mana 9/8 Demon Hunter Demon");
        Immune(p.Hero, true); Immune(p.Opponent.Hero, false);
        Core2026Proof.Check(g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t =>
            t.EntityId == p.Hero.Id && t.Tag == GameTag.IMMUNE && t.Value == 1), "client history publishes IMMUNE on hero entity");
        Core2026Proof.Check(demon.Cost == 0 && demon[GameTag.COST] == 0 && cheap.Cost == 0
            && ordinary.Cost == 4 && spell.Cost == 4 && enemyDemon.Cost == 5
            && onBoard.Cost == 5 && inDeck.Cost == 5, "visible zero cost only for friendly Demons in hand");
        var later = Add(p, "CORE_BT_510"); g.AuraUpdate();
        Core2026Proof.Check(later.Cost == 0 && later[GameTag.COST] == 0, "later draw receives visible discount");
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "CORE_CATA_001e") == 1,
            "exact exported Demonic Summoning enchantment attached to controller");
        p.Hero.Damage = 6; Play(g, p, "CS2_089", p.Hero);
        p.Hero.TakeDamage(p.Opponent.Hero, 5);
        Core2026Proof.Check(p.Hero.Damage == 0, "owner heals own Immune hero; damage prevented");

        foreach (var clone in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var cp = clone.Player1;
            Immune(cp.Hero, true);
            ((Minion)clone.IdEntityDic[source.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
            Immune(cp.Hero, false);
            Core2026Proof.Check(clone.IdEntityDic[demon.Id].Cost == 0, "battlecry survives source Silence");
            PlayEntity(clone, cp, clone.IdEntityDic[demon.Id]);
            Core2026Proof.Check(clone.IdEntityDic[demon.Id].Cost == 5 && clone.IdEntityDic[later.Id].Cost == 5,
                "ordinary Demon played free consumes discount in resumed branch");
            Immune(p.Hero, true);
            Core2026Proof.Check(demon.Cost == 0 && cheap.Zone == p.HandZone, "branch consumption and Silence isolated");
        }

        var expiry = g.CloneExact(history: true);
        End(expiry);
        Core2026Proof.Check(expiry.IdEntityDic[demon.Id].Cost == 5
            && expiry.IdEntityDic[demon.Id][GameTag.COST] == 5
            && !expiry.Player1.AppliedEnchantments.Any(e => e.Card.Id == "CORE_CATA_001e"), "unused discount expires at end of turn");
        Immune(expiry.Player1.Hero, true);
        Add(expiry.Player2, "CS2_029");
        Core2026Proof.Check(!expiry.Player2.Options().Any(t => t.Target?.Id == expiry.Player1.Hero.Id),
            "enemy spell, hero power and attacks cannot target aura-protected hero");
        // Grant during the opponent turn to isolate trigger ownership from normal end-turn expiry.
        var resumedSource = expiry.IdEntityDic[source.Id];
        resumedSource.Card.Power.PowerTask.Process(expiry, expiry.Player1, resumedSource, null);
        expiry.AuraUpdate();
        Play(expiry, expiry.Player2, "EX1_319");
        Core2026Proof.Check(expiry.IdEntityDic[demon.Id].Cost == 0, "opponent Demon never consumes friendly discount");
        End(expiry);
        Core2026Proof.Check(expiry.IdEntityDic[demon.Id].Cost == 5, "off-turn grant still expires this turn");

        Kill(g, source); Immune(p.Hero, false);
        Core2026Proof.Check(demon.Cost == 0, "battlecry survives source death");
        PlayEntity(g, p, ordinary); PlayEntity(g, p, spell, p.Opponent.Hero);
        Summon(p, "EX1_319");
        Core2026Proof.Check(demon.Cost == 0, "non-Demons and summoned Demons do not consume discount");
        PlayEntity(g, p, cheap);
        Core2026Proof.Check(p.UsedMana == 0 && demon.Cost == 5 && later.Cost == 5
            && !p.AppliedEnchantments.Any(e => e.Card.Id == "CORE_CATA_001e"), "first played Demon costs zero and consumes once");
        PlayEntity(g, p, demon);
        Core2026Proof.Check(p.UsedMana == 5, "second Demon pays ordinary cost");

        ClearBoard(g, p);
        var first = Summon(p, id); var second = Summon(p, id);
        first.Silence(); g.DeathProcessingAndAuraUpdate(); Immune(p.Hero, true);
        Kill(g, second); Immune(p.Hero, false);
        var bounced = Summon(p, id); Immune(p.Hero, true);
        End(g); Play(g, p.Opponent, "EX1_581", bounced); Immune(p.Hero, false);
        Core2026Proof.Check(bounced.Zone == p.HandZone, "returning source to hand removes immunity");
    });

    private static void Immune(Hero hero, bool expected)
    {
        Core2026Proof.Check(hero.IsImmune == expected && hero[GameTag.IMMUNE] == (expected ? 1 : 0),
            "hero immunity and client-visible IMMUNE agree");
    }
}
