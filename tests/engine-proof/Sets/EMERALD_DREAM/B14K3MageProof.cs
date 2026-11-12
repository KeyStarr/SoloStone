using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static B14K3BEProof;

internal static class B14K3EMERALDDREAMProof
{
    internal static void CardsProof()
    {
        StandardSetsProof.Case("EDR_520", Unpinned_ForbiddenShrine);
        StandardSetsProof.Case("EDR_874", StellarBalance);
    }
    // auto-cast/random-{cast-target-fizzle,secret-admission}.yaml, C/E analogy, approved assumption.
    private static void Unpinned_ForbiddenShrine(Game g, Controller p, string id)
    {
        CheckPoolBuckets(g, id);
        var filter = Filter(id);
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            var location = (BoardObject)Play(branch, owner, id);
            owner.BaseMana = seed; owner.UsedMana = 0; owner.TemporaryMana = 1; owner.OverloadLocked = 1;
            branch.SetRandomSeed(seed); var expectedGame = branch.CloneExact(history: true);
            var selected = OfflineCardPools.Pick(expectedGame, filter.WithCost(seed), owner.HeroClass, Cards.FromId(id).AssetId);
            var before = branch.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            int spent = owner.TotalManaSpentThisGame;
            LocationProofs.Use(location);
            var generated = branch.IdEntityDic.Values.Where(c => !before.Contains(c.Id) && c.Card.Type == CardType.SPELL && c[GameTag.CREATOR] == location.Id).ToArray();
            Core2026Proof.Check(owner.RemainingMana == 0 && owner.TemporaryMana == 0 && owner.TotalManaSpentThisGame == spent + seed && location.Durability == 2 && generated.Length == (selected == null ? 0 : 1)
                && generated.All(c => c.Card.Id == selected.Id && c.Card.Cost == seed), "spend remaining mana including temporary/overload; exact seeded spell cast once");
            Core2026Proof.Check(!owner.Options().Any(t => t is ActivateLocationTask a && a.Source.Id == location.Id), "location cooldown prevents repeated use");
        }
        Pool(g, new[] { "CORE_EX1_169" }, () => {
            var shrine = (BoardObject)Play(g, p, id); p.BaseMana = 0; p.UsedMana = 0;
            int count = g.IdEntityDic.Count;
            LocationProofs.Use(shrine);
            Core2026Proof.Check(shrine.Durability == 2 && p.TemporaryMana == 1 && g.IdEntityDic.Count > count,
                "zero remaining mana casts the exact zero-cost Innervate bucket");
        });
        ParkBoard(p); p.TemporaryMana = 0;
        Pool(g, new[] { "CORE_EX1_287" }, () => {
            var shrine = (BoardObject)Play(g, p, id); p.BaseMana = 3; p.UsedMana = 0;
            LocationProofs.Use(shrine);
            Core2026Proof.Check(p.SecretZone.Any(s => s.Card.Id == "CORE_EX1_287"), "random Secret installs without firing");
            End(g); End(g); p.BaseMana = 3; p.UsedMana = 0;
            LocationProofs.Use(shrine);
            Core2026Proof.Check(p.SecretZone.Count(s => s.Card.Id == "CORE_EX1_287") == 1, "duplicate automatic Secret fizzles");
        });
        ParkBoard(p);
        Pool(g, new[] { "CORE_EX1_246" }, () => {
            var shrine = (BoardObject)Play(g, p, id); p.BaseMana = 3; p.UsedMana = 0;
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0, "required-target fixture empty");
            LocationProofs.Use(shrine);
            Core2026Proof.Check(p.GraveyardZone.Any(c => c.Card.Id == "CORE_EX1_246") && p.BoardZone.Count == 0, "required target absent fizzles without reroll");
        });
        ParkBoard(p);
        Pool(g, Array.Empty<string>(), () => {
            var shrine = (BoardObject)Play(g, p, id); p.BaseMana = 10; p.UsedMana = 0;
            var random = g.Random.CaptureState(); int count = g.IdEntityDic.Count;
            LocationProofs.Use(shrine);
            Core2026Proof.Check(p.RemainingMana == 0 && shrine.Durability == 2 && g.IdEntityDic.Count == count && g.Random.CaptureState().Equals(random), "empty exact bucket spends all mana/durability and yields nothing");
        });
    }
    // fabled-spelldamage/spell-damage-{copy,stacking}.yaml, PINNED-RULE-ONLY (C).
    private static void StellarBalance(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var spells = p.HandZone.ToArray();
        Core2026Proof.Check(spells.Select(c => c.Card.Id).SequenceEqual(new[] { "CS2_008", "EX1_173" }) && spells.All(c => c[GameTag.SPELLPOWER] == 1
            && c.AppliedEnchantments.Single().Card.Id == "EDR_874e") && p.CurrentSpellPower == 0, "exact Moonfire/Starfire each has independent +1 Spell Damage");
        var copy = Generic.Copy(p, p.Hero, spells[0], Zone.HAND);
        Core2026Proof.Check(copy[GameTag.SPELLPOWER] == 1 && copy.AppliedEnchantments.Single().Target == copy, "forward hand copy retains independent local grant");
        var back = Generic.Copy(p, p.Hero, spells[0], Zone.DECK);
        Core2026Proof.Check(back[GameTag.SPELLPOWER] == 0, "backward copy loses grant");
        Future(g, next => {
            var owner = next.Player1; Summon(owner, "CORE_EX1_012");
            var moon = next.IdEntityDic[spells[0].Id];
            int damage = owner.Opponent.Hero.Damage;
            PlayEntity(next, owner, moon, owner.Opponent.Hero);
            Core2026Proof.Check(owner.Opponent.Hero.Damage == damage + 3, "spell-local and controller bonuses add once");
            var star = next.IdEntityDic[spells[1].Id]; damage = owner.Opponent.Hero.Damage;
            int deck = owner.DeckZone.Count; PlayEntity(next, owner, star, owner.Opponent.Hero);
            Core2026Proof.Check(owner.Opponent.Hero.Damage == damage + 7 && owner.DeckZone.Count == deck - 1, "Starfire deals enhanced damage and draws");
        });
        PoolPilotProof.EmptyHand(p); FillHand(p, 9); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Last().Card.Id == "CS2_008" && p.HandZone.Last()[GameTag.SPELLPOWER] == 1
            && p.GraveyardZone.Any(c => c.Card.Id == "EX1_173"), "one space gives Moonfire, Starfire burns");
    }

}
