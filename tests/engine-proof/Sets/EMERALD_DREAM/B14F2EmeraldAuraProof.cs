using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2EmeraldAuraProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_256", Dreamwarden);
        StandardSetsProof.Case("EDR_259", Unpinned_Ursol_CapturedSpellState);
    }
    private static void Dreamwarden(Game g, Controller p, string id)
    {
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.AttackDamage == 3 && first.Health == 4, "original-only deck misses condition");
        var inserted = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(inserted.Zone == p.HandZone && second.AttackDamage == 5 && second.Health == 6,
            "draw exact nonoriginal entity then +2/+2");
        var added = Entity.FromCard(p, Cards.FromId("CS2_168"), zone: p.DeckZone);
        var other = Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        NonFirstPick(g, 2);
        var rng = g.Random.Clone(); var expected = new[] { added, other }[rng.Next(2)].Id;
        Play(g, p, id);
        Core2026Proof.Check(g.IdEntityDic[expected].Zone == p.HandZone && g.Random.CaptureState().Equals(rng.CaptureState()),
            "nonoriginal draw uses seeded candidate selection");
    }
    private static void Unpinned_Ursol_CapturedSpellState(Game g, Controller p, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var tieGame = HeroDivineShieldProof.NewGame(true); var owner = tieGame.Player1;
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            var names = new[] { "CS2_029", "CS2_032" };
            foreach (string name in reverse ? names.Reverse() : names) Add(owner, name);
            // Fireball (4) and Flamestrike (7) become a tied maximum without changing definitions.
            foreach (var spell in owner.HandZone) SabberStoneCore.Enchants.Cost.Effect(SabberStoneCore.Enchants.EffectOperator.SET, 7).ApplyTo((Playable)spell);
            NonFirstPick(tieGame, 2);
            var candidates = owner.HandZone.ToArray(); var rng = tieGame.Random.Clone();
            int expected = candidates[rng.Next(2)].Card.AssetId;
            Play(tieGame, owner, id);
            var capturedSpell = PayloadRules.Entries(Active(owner).Single(), 1).Single();
            Core2026Proof.Check(capturedSpell.Card.AssetId == expected && Active(owner).Single()[GameTag.TAG_SCRIPT_DATA_NUM_1] == capturedSpell.Id
                && tieGame.Random.CaptureState().Equals(rng.CaptureState()), "Ursol chooses seeded highest-Cost tie after hand reorder");
        }
        var holy = (Spell)Add(p, "CS2_029"); holy[GameTag.SPELLPOWER] = 2;
        PayloadRules.StoreValue(holy, 77, 11, PayloadPolicy.Copy);
        var ursol = (Minion)Play(g, p, id);
        Core2026Proof.Check(holy.Zone != p.HandZone && Active(p).Single().Card.Id == "EDR_259e1", "selected hand spell becomes wrapper capture");
        ursol.Silence(); Kill(g, ursol);
        holy[GameTag.SPELLPOWER] = 99;
        PayloadRules.Entries(holy, 77).Single()[PayloadRules.Value] = 99;
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var owner = branch.Player1;
            for (int tick = 0; tick < 3; tick++)
            {
                // Only the opposing hero is a legal Fireball target after immunity masks the owner.
                owner.Hero.CantBeTargetedBySpells = true;
                owner.ControllerAuraEffects[GameTag.SPELLPOWER] = tick;
                int before = owner.Opponent.Hero.Damage;
                End(branch);
                Core2026Proof.Check(owner.Opponent.Hero.Damage == before + 8 + tick,
                    "each tick retains individual spell damage and evaluates live external spell power");
                var working = branch.IdEntityDic.Values.Where(e => e.Card.Id == "CS2_029" && !PayloadRules.IsStored(e)
                    && e.Zone == owner.GraveyardZone).OrderBy(e => e.Id).Last();
                var entry = PayloadRules.Entries(working, 77).Single();
                Core2026Proof.Check(entry[PayloadRules.Value] == 11, "each working copy gets independent frozen card payload");
                entry[PayloadRules.Value] = 99;
                End(branch);
            }
            Core2026Proof.Check(Active(owner).Length == 0, "Ursol expires after three owner ends");
        }
        var nested = HeroDivineShieldProof.NewGame(true); var n = nested.Player1;
        foreach (var held in n.HandZone.ToArray()) n.SetasideZone.Add(n.HandZone.Remove(held));
        Add(n, "TIME_700"); Play(nested, n, id);
        for (int expected = 0; expected < 3; expected++)
        {
            End(nested);
            Core2026Proof.Check(n.BoardZone.Count(m => m.Card.Id == "TIME_700t") == expected * (expected + 1) / 2,
                "Pinned_UrsolWrapsExistingAura: children miss installing end event");
            End(nested);
        }
    }
}
