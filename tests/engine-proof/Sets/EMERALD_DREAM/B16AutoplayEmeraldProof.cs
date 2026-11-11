using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16AutoplayEmeraldProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_031", Unpinned_Ohnahra);
        Unpinned_OhnahraCastsCrossClassHeraldSpell();
        StandardSetsProof.Case("FIR_907", Amirdrassil);
    }
    private static void Unpinned_OhnahraCastsCrossClassHeraldSpell()
    {
        var game = LocationProofs.Game(); var p = game.Player1;
        Summon(p, "EDR_031");
        B16AutoplayProof.EmptyDeck(p);
        var fourth = Entity.FromCard(p, Cards.FromId("CS2_179"), zone: p.DeckZone);
        var third = Entity.FromCard(p, Cards.FromId("CS2_120"), zone: p.DeckZone);
        var second = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var rejected = (Spell)Entity.FromCard(p, Cards.FromId("CATA_561"), zone: p.DeckZone);
        // Herald cards are playable by every class: a Hunter's Ritual of Power resolves, with the card's own Shaman Soldier.
        Core2026Proof.Check(p.HeroClass == CardClass.HUNTER && HeraldRules.CanCast(p, rejected, null), "Hunter may cast Ritual of Power");
        int hand = p.HandZone.Count;
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            End(next); var owner = next.Player1;
            Core2026Proof.Check(next.IdEntityDic[rejected.Id].Zone == owner.GraveyardZone
                && next.IdEntityDic[second.Id].Zone == owner.BoardZone
                && next.IdEntityDic[third.Id].Zone == owner.BoardZone
                && owner.DeckZone.Single().Id == fourth.Id,
                "Ohnahra casts the cross-class Herald spell once, then advances through remaining two attempts");
            Core2026Proof.Check(HeraldRules.Count(owner) == 1 && owner.HandZone.Count == hand + 2
                && owner.BoardZone.Any(m => m.Card.Id == "CATA_565t") && owner[HeraldRules.Profile] == 0,
                "Ritual resolves for a Hunter: Shaman Soldier, two Breezlings, no profile lock-in");
        }
    }
    private static void Unpinned_Ohnahra(Game game, Controller p, string id)
    {
        var source = Summon(p, id);
        B16AutoplayProof.EmptyDeck(p);
        var weapon = Entity.FromCard(p, Cards.FromId("CS2_106"), zone: p.DeckZone);
        var location = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.DeckZone);
        var archer = Entity.FromCard(p, Cards.FromId("CS2_189"), zone: p.DeckZone);
        p.UsedMana = 10;
        End(game);
        Core2026Proof.Check(archer.Zone == p.BoardZone && location.Zone == p.BoardZone && p.Hero.Weapon == weapon
            && p.Opponent.Hero.Damage == 0 && p.DeckZone.IsEmpty, "Ohnahra mixed top-three no Battlecry, free weapon and location");
        End(game);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var a = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var b = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.DeckZone);
        var c = Entity.FromCard(p, Cards.FromId("CS2_189"), zone: p.DeckZone);
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            End(next);
            Core2026Proof.Check(new[] { a.Id, b.Id, c.Id }.All(i => next.IdEntityDic[i].Zone == next.Player1.HandZone), "full board draws minions and locations without stalling");
        }
    }
    private static void Amirdrassil(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        for (int n = 1; n <= 3; n++)
        {
            p.UsedMana = 10;
            int deck = p.DeckZone.Count, armor = p.Hero.Armor, board = p.BoardZone.Count;
            B16AutoplayProof.Ready(location); LocationProofs.Use(location);
            Core2026Proof.Check(p.Hero.Armor == armor + n && p.DeckZone.Count == deck - n && p.RemainingMana == n,
                "Amirdrassil matching armor/draw/refresh magnitude " + n);
            Core2026Proof.Check(p.BoardZone.Count == board + 1 && p.BoardZone.Any(m => m.Card.Cost == n), "Amirdrassil exact-cost summon " + n);
            if (n == 1) B16AutoplayProof.AssertSaved(game);
        }
        Core2026Proof.Check(location.Zone == p.GraveyardZone && location.Location.UsesCompleted == 3, "three completed Amirdrassil uses");
    }
}
