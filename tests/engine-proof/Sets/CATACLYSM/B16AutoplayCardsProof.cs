using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16AutoplayCardsProof
{
    [GroupProof("b16-autoplay-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_477", Chamber);
        StandardSetsProof.Case("CATA_527", Unpinned_Nespirah);
        B16AutoplayEmeraldProof.Run();
        B16AutoplayPrisonProof.Run();
        B16AutoplayTerrorProof.Run();
        B16AutoplayConfluxProof.Run();
    }
    private static void Chamber(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        Core2026Proof.Check(!LocationRules.CanActivate(p, location, null), "Chamber empty hand has no activation");
        var minion = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        LocationProofs.Use(location);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { minion.Id }) && location.Durability == 2, "Chamber frozen minion-only offer before charge");
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            B16AutoplayProof.Answer(next, minion.Id);
            var held = (Minion)next.IdEntityDic[minion.Id];
            var loc = (BoardObject)next.IdEntityDic[location.Id];
            Core2026Proof.Check(held.AttackDamage == held.Card.ATK + 2 && held.Health == held.Card.Health + 2 && loc.Durability == 1, "Chamber resume grants one +2/+2");
            B16AutoplayProof.Ready(loc); LocationProofs.Use(loc);
            Core2026Proof.Check(loc.Zone == loc.Controller.BoardZone, "non-summoning final stays in play during decision");
            B16AutoplayProof.Answer(next, minion.Id);
            Core2026Proof.Check(loc.Zone.Type == Zone.GRAVEYARD && held.AttackDamage == held.Card.ATK + 4, "final continuation buffs before retirement");
            held.Silence(); Core2026Proof.Check(held.AttackDamage == held.Card.ATK, "ordinary buff removable");
        }
    }
    private static void Unpinned_Nespirah(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.CantBeTargetedBySpells = true;
        Core2026Proof.Check(LocationRules.CanActivate(p, location, enemy) && LocationRules.CanActivate(p, location, p.Opponent.Hero), "Nespirah targets Elusive and heroes");
        enemy.HasStealth = true; Core2026Proof.Check(!LocationRules.CanActivate(p, location, enemy), "Nespirah excludes enemy Stealth");
        enemy.HasStealth = false; enemy.IsImmune = true; Core2026Proof.Check(!LocationRules.CanActivate(p, location, enemy), "Nespirah excludes enemy Immune");
        enemy.IsImmune = false;
        LocationProofs.Use(location, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 1 && location.Durability == 4, "one damage one charge");
        Play(game, p, "CORE_BT_035");
        Core2026Proof.Check(!location.IsExhausted && location.Durability == 4, "friendly Fel reopens without restoring charge");
        LocationProofs.Use(location, p.Opponent.Hero);
        End(game); Play(game, p.Opponent, "CORE_BT_035");
        Core2026Proof.Check(location.IsExhausted, "opponent Fel does not reopen location");
        End(game);
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            var owner = next.Player1; var loc = (BoardObject)next.IdEntityDic[location.Id];
            Play(next, owner, "CORE_EX1_312"); // Pinned Fel Twisting Nether also destroys the location.
            Core2026Proof.Check(loc.Zone == owner.GraveyardZone && owner.BoardZone.Any(m => m.Card.Id == "CATA_527t2"), "location destruction frees Nespirah");
            var freed = owner.BoardZone.Single(m => m.Card.Id == "CATA_527t2");
            var prior = owner.HandZone.Select(c => c.Id).ToHashSet();
            Play(next, owner, "CORE_BT_035");
            var generated = owner.HandZone.Single(c => !prior.Contains(c.Id) && c[GameTag.DISPLAYED_CREATOR] == freed.Id);
            Core2026Proof.Check(generated.Card.IsRace(Race.NAGA) && generated.Cost == 1 &&
                (generated.Card.ColossalAppendageIds == null || generated.Card.ColossalAppendageIds.Count == 0), "freed Nespirah generates non-Colossal Naga costing one: " + generated.Card.Id + " cost=" + generated.Cost + " races=" + string.Join(",", generated.Card.Races));
            freed.Silence(); prior = owner.HandZone.Select(c => c.Id).ToHashSet();
            Play(next, owner, "CORE_BT_035");
            Core2026Proof.Check(!owner.HandZone.Any(c => !prior.Contains(c.Id) && c[GameTag.DISPLAYED_CREATOR] == freed.Id), "silence removes Naga generation");
        }
    }
}
