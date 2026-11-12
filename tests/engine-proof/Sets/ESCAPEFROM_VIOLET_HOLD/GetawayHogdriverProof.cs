using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class GetawayHogdriverProof
{
    // Burn does not satisfy either draw: tier C Card_draw (Details/Overdraw) agrees with an independent
    // tier E community report, 2026-08-08 (two minions, second burned, no Charge):
    // https://hearthstone.wiki.gg/wiki/Card_draw
    // https://us.forums.blizzard.com/en/hearthstone/t/getaway-hogdriver-not-counting-drawn-discarded-cards/164354
    // Getaway_Hogdriver patch history lists only its addition in 36.0.0.245258; no later card change through pin 37.0.

    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in ids) Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
    }

    private static Minion Run(Game g, Controller p, string id, params string[] deck)
    {
        PoolPilotProof.EmptyHand(p);
        Deck(p, deck);
        var source = Add(p, id);
        PlayEntity(g, p, source);
        return (Minion)source;
    }

    /// <summary>Battlecry on a fresh board copy, so a full hand can be prepared before resolution.</summary>
    private static Minion Resolve(Game g, Controller p, string id, int hand, params string[] deck)
    {
        PoolPilotProof.EmptyHand(p);
        while (p.HandZone.Count < hand) Add(p, "CS2_231");
        Deck(p, deck);
        var source = Summon(p, id);
        PoolPilotProof.Resolve(g, source);
        return source;
    }

    internal static void Run() => StandardSetsProof.Case("JAIL_462", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).IsRace(Race.QUILBOAR) && !Cards.FromId(id).Charge, "Quilboar, no printed Charge");
        var enemy = Summon(p.Opponent, "CS2_120");
        int enemyHand = p.Opponent.HandZone.Count;
        var fresh = g.CloneExact(history: true);

        var charged = Run(g, p, id, "CS2_182", "CS2_120");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => c is Minion) && p.DeckZone.Count == 0
            && p.Opponent.HandZone.Count == enemyHand, "draws exactly two of its owner's cards");
        Core2026Proof.Check(charged.HasCharge && charged[GameTag.CHARGE] == 1 && CanAttack(charged, p.Opponent.Hero) && CanAttack(charged, enemy),
            "two minions: gains Charge and attacks anything on its entry turn");
        AttackOptionsProof.Lifecycle(g, charged, enemy, rush: false);
        Play(g, p, "BE_228", charged);
        Core2026Proof.Check(!charged.HasCharge && !CanAttack(charged, p.Opponent.Hero) && !CanAttack(charged, enemy), "Silence removes granted Charge and re-exhausts");

        foreach (var (label, hand, deck) in new (string, int, string[])[] {
            ("minion and spell", 0, new[] { "CS2_025", "CS2_120" }),
            ("spell and minion", 0, new[] { "CS2_120", "CS2_025" }),
            ("two spells", 0, new[] { "CS2_025", "CS2_025" }),
            ("one card then fatigue", 0, new[] { "CS2_182" }),
            ("empty deck", 0, new string[0]),
            ("second minion burned", 9, new[] { "CS2_120", "CS2_182" }),
            ("both burned", 10, new[] { "CS2_120", "CS2_182" }) })
        {
            var branch = fresh.CloneExact(history: true);
            var bp = branch.Player1;
            int health = bp.Hero.Health;
            var minion = Resolve(branch, bp, id, hand, deck);
            Core2026Proof.Check(!minion.HasCharge && !CanAttack(minion, bp.Opponent.Hero)
                && !CanAttack(minion, (ICharacter)branch.IdEntityDic[enemy.Id]), label + ": no Charge, summoning sick");
            Core2026Proof.Check(deck.Length >= 2 || bp.Hero.Health < health, label + ": fatigue applies when the deck runs out");
            Core2026Proof.Check(bp.DeckZone.Count == Math.Max(0, deck.Length - 2) && bp.HandZone.Count == Math.Min(10, hand + Math.Min(2, deck.Length)),
                label + ": two draw attempts");
        }
        var control = fresh.CloneExact(history: true);
        var controlMinion = Resolve(control, control.Player1, id, 8, "CS2_120", "CS2_182");
        Core2026Proof.Check(controlMinion.HasCharge && CanAttack(controlMinion, control.Player1.Opponent.Hero) && control.Player1.HandZone.Count == 10,
            "boundary: hand of 8 keeps both minions and gains Charge");
        var held = fresh.CloneExact(history: true);
        var heldSource = Add(held.Player1, id);
        Deck(held.Player1, "CS2_182", "CS2_120");
        End(held);
        Core2026Proof.Check(heldSource[GameTag.CHARGE] == 0 && held.Player1.DeckZone.Count == 2, "no draw or Charge from hand");
    });
}
