using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

// #190: a minion returned to hand is a fresh copy. Retail "Return to hand": damage, enchantments and Silences are removed
// (Schism's +2/+3 and Elusive, plain stat buffs). Before the fix Minion.Reset kept every plain stat enchantment.
internal static class ReturnToHandResetProof
{
    internal static void Run()
    {
        TwilightMistressBounce();
        PlainBuffsAndStateViaGenericBounce();
        ShuffleIntoDeckResets();
        AuraPresentationsOnBounce();
        Console.WriteLine("PASS return to hand resets enchantments (#190)");
    }

    // Schism's enchantment (CATA_306e: +2/+3, Elusive) and Blessing of Kings' (CS2_092e: +4/+4) attached as the spells do.
    private static void Cast(Controller p, string spellId, Minion target)
    {
        var spell = Entity.FromCard(p, Cards.FromId(spellId), zone: p.SetasideZone);
        Generic.AddEnchantmentBlock(p.Game, Cards.FromId(spellId == "CATA_306" ? "CATA_306e" : "CS2_092e"), spell, target);
    }

    private static void AssertFresh(Minion m, string what)
    {
        L.Check(m.Zone is SabberStoneCore.Model.Zones.HandZone, what + ": in hand");
        L.Check(m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health && m.BaseHealth == m.Card.Health, what + $": stats {m.AttackDamage}/{m.Health} want {m.Card.ATK}/{m.Card.Health}");
        L.Check(!m.CantBeTargetedBySpells && !m.CantBeTargetedByHeroPowers, what + ": no Elusive");
        L.Check(m.AppliedEnchantments == null || m.AppliedEnchantments.Count == 0, what + ": enchantments left " +
            string.Join(",", (m.AppliedEnchantments ?? new List<Enchantment>()).Select(e => e.Card.Id)));
        L.Check(m.Damage == 0 && !m.IsSilenced, what + ": damage/silence cleared");
    }

    private static void TwilightMistressBounce()
    {
        var g = L.Game(); var p1 = g.Player1; var p2 = g.Player2;
        var medivh = L.Minion(p1, "CS2_182");
        Cast(p1, "CATA_306", medivh); // Schism: +2/+3 and Elusive on the target
        L.Check(medivh.AttackDamage == 6 && medivh.Health == 8 && medivh.CantBeTargetedBySpells, "Schism applied");
        L.End(g); p2.BaseMana = 10;
        var mistress = Entity.FromCard(p2, Cards.FromId("CATA_201"), zone: p2.HandZone);
        L.Check(g.Process(PlayCardTask.Minion(p2, mistress)), "Twilight Mistress");
        AssertFresh(medivh, "Mistress bounce");
        L.End(g); p1.BaseMana = 10;
        L.Check(g.Process(PlayCardTask.Minion(p1, medivh)), "replay");
        L.Check(medivh.AttackDamage == 4 && medivh.Health == 5 && !medivh.CantBeTargetedBySpells, "replayed as the base card");
    }

    private static void PlainBuffsAndStateViaGenericBounce()
    {
        foreach (bool history in new[] { true, false })
        {
            var g = L.Game(history: history); var p = g.Player1;
            var m = L.Minion(p, "CS2_182");
            Cast(p, "CS2_092", m);   // Blessing of Kings +4/+4
            Cast(p, "CATA_306", m);  // Schism: Elusive stays active at the bounce
            m.Damage = 3;
            L.Check(m.AttackDamage == 10 && m.CantBeTargetedBySpells, "buffed before bounce");
            int before = history ? g.PowerHistory.Last.Count() : 0;
            L.Check(Generic.ReturnToHandBlock(p, m), "bounce");
            AssertFresh(m, "ReturnToHandBlock history=" + history);
            if (history)
            {
                // The client rebuilds the hand entity from tag changes plus the Show: Elusive must be emitted as cleared, because
                // the Show lists a targeting tag only when it differs from the printed card.
                var tags = g.PowerHistory.Last.Skip(before).OfType<PowerHistoryTagChange>().Where(t => t.EntityId == m.Id).ToList();
                L.Check(tags.Any(t => t.Tag == GameTag.CANT_BE_TARGETED_BY_SPELLS && t.Value == 0),
                    "history clears Elusive: " + string.Join(",", tags.Select(t => t.Tag + "=" + t.Value)));
            }
        }
        // Silenced and re-buffed minion returns as the plain card.
        var s = L.Game(); var sp = s.Player1;
        var silenced = L.Minion(sp, "CS2_182");
        Cast(sp, "CATA_306", silenced); silenced.Silence();
        L.Check(silenced.IsSilenced, "silenced");
        Cast(sp, "CS2_092", silenced);
        L.Check(Generic.ReturnToHandBlock(sp, silenced), "bounce silenced");
        AssertFresh(silenced, "silenced + buffed");
    }

    private static void ShuffleIntoDeckResets()
    {
        var g = L.Game(); var p = g.Player1;
        var m = L.Minion(p, "CS2_182");
        Cast(p, "CATA_306", m);
        var task = new SabberStoneCore.Tasks.SimpleTasks.MoveToDeck(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET);
        L.Check(m.AppliedEnchantments.Count > 0, "has enchantment");
        task.Process(g, p, p.Hero, m, null);
        L.Check(m.Zone is SabberStoneCore.Model.Zones.DeckZone && m.AttackDamage == 4 && m.Health == 5 && !m.CantBeTargetedBySpells
            && (m.AppliedEnchantments == null || m.AppliedEnchantments.Count == 0), "shuffled minion is fresh");
    }

    // Only aura presentations survive Reset: Crystal Core keeps working in hand; a board aura's (Stormwind Champion) is dropped.
    private static void AuraPresentationsOnBounce()
    {
        var g = L.Game(); var p = g.Player1;
        var core = Entity.FromCard(p, Cards.FromId("UNG_067t1"), zone: p.SetasideZone);
        Generic.AddEnchantmentBlock(g, Cards.FromId("UNG_067t1e"), core, p);
        g.DeathProcessingAndAuraUpdate();
        var m = L.Minion(p, "CS2_182");
        g.DeathProcessingAndAuraUpdate();
        L.Check(m.AttackDamage == 5 && m.Health == 5, "Crystal Core on board 5/5");
        L.Check(Generic.ReturnToHandBlock(p, m), "bounce Crystal Core minion");
        g.DeathProcessingAndAuraUpdate();
        L.Check(m.AttackDamage == 5 && m.Health == 5, $"Crystal Core minion stays 5/5 in hand ({m.AttackDamage}/{m.Health})");
        L.Check(m.AppliedEnchantments != null && m.AppliedEnchantments.Any(e => e.Card.Id == "UNG_067t1e2"), "UNG_067t1e2 presentation kept");

        var g2 = L.Game(); var q = g2.Player1;
        L.Minion(q, "CS2_222"); // Stormwind Champion: +1/+1 to other friendly minions
        var buffed = L.Minion(q, "CS2_182");
        g2.DeathProcessingAndAuraUpdate();
        L.Check(buffed.AttackDamage == 5 && buffed.Health == 6, "Champion aura on board");
        L.Check(Generic.ReturnToHandBlock(q, buffed), "bounce Champion-buffed minion");
        g2.DeathProcessingAndAuraUpdate();
        L.Check(buffed.AttackDamage == 4 && buffed.Health == 5 && (buffed.AppliedEnchantments == null || buffed.AppliedEnchantments.Count == 0),
            $"Champion buff dropped in hand ({buffed.AttackDamage}/{buffed.Health}, enchantments {buffed.AppliedEnchantments?.Count})");
    }
}
