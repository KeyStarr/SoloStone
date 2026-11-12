using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static LostCityPoolProof;

internal static class DiscoverSummonBoundaryProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (int seed in new[] { 1, 2, 3 })
        {
            Scenario("DINO_426", seed, g => Pyromancer(g, "DINO_426", "CORE_EX1_014", 2), failures);
            Scenario("DINO_424", seed, g => Pyromancer(g, "DINO_424", "CORE_EX1_014", 9), failures);
            Scenario("DINO_415", seed, g => Pyromancer(g, "DINO_415", "CORE_EX1_110", 4), failures);
            Scenario("DINO_415", seed, g => LiveDeathrattle(g, "CORE_EX1_110", 1), failures);
            Scenario("DINO_415", seed, g => LiveDeathrattle(g, "CATA_723", 2), failures);
            Scenario("DINO_415", seed, Replay, failures);
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS Lost City Discover-summon boundaries: Pyromancer; living Cairne/Mongrel; seeds=3; unsupported cards unavailable");
    }

    private static void Scenario(string id, int seed, Action<Game> proof, List<string> failures)
    {
        try
        {
            // Deferred cards must stay unavailable until their actual interactions pass.
            if (!StandardSets.CollectibleIds.Contains(id))
            {
                Core2026Proof.Check(!EffectModules.ByDbfId.Values.Any(c => c == id || c == id + "e") &&
                    !OfflinePoolDeclarations.Cards.Any(c => c.CardId == id), id + " has no playable module or pool declaration");
                return;
            }
            proof(PlayerDecisionProof.Fixture(seed));
        }
        catch (Exception e) { failures.Add(id + " seed=" + seed + ": " + e.Message); }
    }

    // DINO_415 uses the selector decision-spell path: held out of the graveyard until the answer, then finished once.
    private static void Pending(Game g, IPlayable spell)
    {
        if (!(spell.Card.Power?.PowerTask is SabberStoneCore.Tasks.SimpleTasks.DecisionSpellTask)) return;
        var p = spell.Controller;
        Core2026Proof.Check(g.SpellCompletion?.SourceId == spell.Id && !Generic.HasDecisionPlay(p) && spell.Zone != p.GraveyardZone
            && spell.Zone != p.HandZone && p.Choice?.Choices.Count > 0, spell.Card.Id + " pends as a decision spell outside the graveyard");
    }

    private static void Finished(Game g, int spellId)
    {
        var spell = g.IdEntityDic[spellId];
        if (!(spell.Card.Power?.PowerTask is SabberStoneCore.Tasks.SimpleTasks.DecisionSpellTask)) return;
        Core2026Proof.Check(g.SpellCompletion == null && !Generic.HasDecisionPlay(spell.Controller) && spell.Zone == spell.Controller.GraveyardZone
            && spell.Controller.GraveyardZone.Count(e => e.Id == spellId) == 1, spell.Card.Id + " finishes once into the graveyard after the answer");
    }

    private static void Pyromancer(Game g, string id, string selected, int health)
    {
        var p = g.Player1;
        var existing = Summon(p, "CS2_182");
        var pyro = Summon(p, "CORE_NEW1_020");
        IPlayable source = null;
        int prematureDamage = 0, offer = 0;
        WithPool(g, id, new[] { selected }, () => {
            source = Play(g, p, id);
            Pending(g, source);
            prematureDamage = pyro.Damage;
            offer = p.Choice.Choices.Single();
        });
        // Restore the pinned manifest before snapshot/load; the chosen real identity is already fixed.
        PoolPilotProof.Resume(g, next => {
            Answer(next, offer);
            Finished(next, source.Id);
            var made = next.Player1.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Core2026Proof.Check(prematureDamage == 0 && ((Minion)next.IdEntityDic[pyro.Id]).Damage == 1
                && ((Minion)next.IdEntityDic[existing.Id]).Damage == 1 && made.Health == health,
                "Pyromancer waits for Discover and damages summoned minion: pending damage=" + prematureDamage +
                "; summoned health=" + made.Health + "; expected=" + health);
        });
    }

    private static void Replay(Game g)
    {
        var p = g.Player1;
        WithPool(g, "DINO_415", new[] { "CORE_EX1_110" }, () => {
            var spell = (Spell)Entity.FromCard(p, Cards.FromId("DINO_415"));
            Generic.CastSpell(p, g, spell, null, 0);
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null && !Generic.HasDecisionPlay(p)
                && spell.Zone == p.GraveyardZone
                && p.BoardZone.Count(m => m.Card.Id == "CORE_EX1_110") == 1
                && p.BoardZone.Count(m => m.Card.Id == "EX1_110t") == 1,
                "replayed Story resolves its mandatory choice and Deathrattle synchronously");
        });
    }

    private static void LiveDeathrattle(Game g, string selected, int count)
    {
        var p = g.Player1;
        var left = Summon(p, "CS2_182");
        var location = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990"));
        p.BoardZone.Add(location);
        WithPool(g, "DINO_415", new[] { selected }, () => {
            var spell = Play(g, p, "DINO_415");
            Pending(g, spell);
            Answer(g, p.Choice.Choices.Single());
            Finished(g, spell.Id);
            var source = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id);
            var summons = p.BoardZone.Where(m => m.Id != left.Id && m.Id != source.Id).ToArray();
            Core2026Proof.Check(summons.Length == count && left.ZonePosition == 0 && location.ZonePosition == 1 &&
                source.ZonePosition == 2 && summons.Select(m => m.ZonePosition).SequenceEqual(Enumerable.Range(3, count)),
                selected + " living Deathrattle summons beside source after existing minion and location; slots=" +
                string.Join(",", Enumerable.Range(0, p.BoardZone.SlotCount).Select(i => p.BoardZone.GetSlot(i).Card.Id)));
        }, dependencies: selected == "CATA_723" ? new[] { "CORE_EX1_005" } : null);
    }
}
