using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class MemoriamManifestProof
{
    // data/rules-evidence/tie-break-cards/w02-{memoriam-cost-basis,resurrect-death-weight,resurrect-max-ties,resurrect-enchantment-reset}.yaml
    // C/D; owner assumption: printed death-form Cost, death-event weighting, uniform ties, fresh resurrection.
    internal static void Run() => StandardSetsProof.Case("TIME_616", Unpinned_MemoriamManifest);
    private static void Unpinned_MemoriamManifest(Game g, Controller p, string id)
    {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.IsEmpty, "no deaths fizzles");
        Kill(g, Summon(p, "CS2_200")); // Higher-Cost non-Undead.
        Kill(g, Summon(p.Opponent, "RLK_958"));
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.IsEmpty, "non-Undead and opposing deaths excluded");
        var low = Summon(p, "RLK_958"); Kill(g, low);
        var high = Summon(p, "RLK_708"); high.Cost = 0; high.AttackDamage = 20; high.Damage = 1; high.Silence(); Kill(g, high);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        var risen = p.BoardZone.Last();
        Core2026Proof.Check(risen.Card.Id == "RLK_708" && risen.AttackDamage == 2 && risen.Health == 2 && !risen.IsSilenced
            && (risen.AppliedEnchantments?.Count ?? 0) == 0 && risen.ZonePosition == 2, "highest printed Cost, fresh stats, appended after physical slots");
        StandardSetsProof.ParkBoard(p); DeathKnightC1Proof.FullBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full mixed board admits no resurrection");
        StandardSetsProof.ParkBoard(p);
        var tied = Summon(p, "EDR_815"); Kill(g, tied);
        var clone = g.CloneExact(); Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == clone.Player1.BoardZone.Single().Card.Id
            && new[] { "RLK_708", "EDR_815" }.Contains(p.BoardZone.Single().Card.Id), "maximum-Cost ties use deterministic game RNG");
        foreach (bool reverse in new[] { false, true })
        {
            var weighted = DeathKnightC1Proof.NewGame(g.Seed); var owner = weighted.Player1;
            var entries = new[] { "RLK_708", "RLK_708", "EDR_815" };
            if (reverse) Array.Reverse(entries);
            foreach (string cardId in entries) Kill(weighted, Summon(owner, cardId));
            Core2026Proof.Check(weighted.HistoryFacts.Query.Deaths().Select(f => f.Definition.ReadDefinition().Id).SequenceEqual(entries),
                "three separate equal-Cost Undead deaths, including two of the same CardId");
            // RNG v1 selects entry 0/1/2 at seeds 1/2/3; reversing A,A,B also rejects CardId deduplication.
            weighted.Random.SetSeed(g.Seed);
            PoolPilotProof.Resume(weighted, next => Play(next, next.Player1, id));
            Core2026Proof.Check(owner.BoardZone.Single().Card.Id == entries[(int)g.Seed - 1],
                $"seed {g.Seed}, reversed={reverse}: resurrection weights death entries, with no first/last preference");
        }
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
        var all = DeathKnightC1Proof.NewGame(g.Seed); Kill(all, Summon(all.Player1, "GIL_681"));
        Play(all, all.Player1, id);
        Core2026Proof.Check(all.Player1.BoardZone.Single().Card.Id == "GIL_681", "All minion types includes Undead");
    }
}
