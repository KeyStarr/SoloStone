using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Issue #83: a Death Knight with Twig of the World Tree (LOOT_392) equipped can play another weapon over it. Twig is destroyed
// and its Deathrattle gains 10 full Mana Crystals (to the cap of 10), so after a 2-Cost weapon at 4 crystals 8 Mana is left.
internal static class TwigReplaceProof
{
    [GroupProof("twig-replace", ProofKind.Cards)]
    internal static void Run()
    {
        var weapons = Cards.All.Where(c => c.Type == CardType.WEAPON && c.Collectible && c.Implemented
            && (c.Class == CardClass.DEATHKNIGHT || c.Class == CardClass.NEUTRAL)).Select(c => c.Id).Append("LOOT_392").ToArray();
        Core2026Proof.Check(weapons.Length > 3, "Death Knight and neutral weapons found");
        foreach (string id in weapons)
        {
            var game = DeathKnightC1Proof.NewGame(5); var p = game.Player1;
            // Generated: Twig arrives in hand from an effect, not from the deck.
            var twig = (Weapon)Core2026Proof.Add(p, "LOOT_392");
            Core2026Proof.PlayEntity(game, p, twig);
            Core2026Proof.Check(p.Hero.Weapon == twig, "Twig equipped");
            var next = Core2026Proof.Add(p, id);
            p.BaseMana = Math.Max(4, next.Cost); p.UsedMana = 0; p.TemporaryMana = 0;
            var play = p.Options().FirstOrDefault(o => o.PlayerTaskType == PlayerTaskType.PLAY_CARD && o.Source?.Id == next.Id);
            Core2026Proof.Check(play != null, id + " can be played over Twig");
            Core2026Proof.Check(game.Process(play), id + " play accepted");
            Core2026Proof.Check(p.Hero.Weapon?.Id == next.Id && twig.Zone == p.GraveyardZone, id + " replaces Twig, which is destroyed");
            Core2026Proof.Check(p.BaseMana == 10 && p.RemainingMana == 10 - next.Cost, id + ": Twig's Deathrattle gives full crystals up to 10");
        }
    }
}
