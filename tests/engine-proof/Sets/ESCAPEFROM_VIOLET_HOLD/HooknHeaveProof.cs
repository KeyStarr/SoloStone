using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class HooknHeaveProof
{
    private static Minion[] Cannoneers(Game g, int source) => g.Player1.BoardZone
        .Where(m => m[GameTag.DISPLAYED_CREATOR] == source).OrderBy(m => m.Id).ToArray();

    private static void Tokens(Game g, int source, int count, int position)
    {
        // Tier D: pinned 37.0.0.254477 export, CAP_105 tag 1452 = CAP_107t/dbfId 127012 (Cannoneer).
        var tokens = Cannoneers(g, source);
        Core2026Proof.Check(tokens.Length == count && tokens.All(m => m.Card.Id == "CAP_107t" && m.Card.AssetId == 127012
            && m.AttackDamage == 1 && m.Health == 1 && m.Cost == 1 && m.IsRace(Race.PIRATE)
            && m[GameTag.RUSH] == 0 && m.Controller == g.Player1 && m.Zone == g.Player1.BoardZone), "exact exported Cannoneers, no Cutthroat stand-in");
        for (int i = 0; i < tokens.Length; i++)
            Core2026Proof.Check(tokens[i].ZonePosition == position + i && g.Player1.BoardZone.GetSlot(position + i).Id == tokens[i].Id,
                "spell summons append in creation order, counting locations");
    }

    internal static void Run() => StandardSetsProof.Case("CAP_105", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.ROGUE);
        replay.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Core2026Proof.Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay finishes its recipe synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess replay completes cast once");
            Core2026Proof.Check(next.Player1.HandZone.Single().Card.IsRace(Race.PIRATE), "Tess Hook grants selected Pirate");
            Tokens(next, next.Player1.GraveyardZone.Single(c => c.Card.Id == id).Id, 2, 1);
            Core2026Proof.End(next);
        });

        DecisionSpellOrderingProof.Run(g.Seed, id, (next, chosen) =>
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone
                && next.Player1.GraveyardZone.Count(c => c.Card.Id == "CAP_107t") == 2,
                "Hook grants Pirate and summons both Cannoneers before Pyromancer kills them"));

        LocationProofs.Location(p); Summon(p, "CS2_182");
        var expected = OfflineCardPools.SampleDistinct(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId);
        var source = Play(g, p, id);
        Offers(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 2 && p.HandZone.Count == 0
            && p.Choice.Choices.Select(n => g.IdEntityDic[n].Card.Id).SequenceEqual(expected.Select(c => c.Id)), "seeded Discover suspends before summons");
        EachAnswer(g, (branch, selected) => {
            var result = branch.IdEntityDic[selected];
            Member(branch, branch.IdEntityDic[source.Id], result, VioletHoldPoolProof.Filter(id));
            Core2026Proof.Check(result.Zone == branch.Player1.HandZone && result.ZonePosition == 0, "each selected Pirate enters hand with same entity id");
            Tokens(branch, source.Id, 2, 2);
        });
        Tokens(g, source.Id, 2, 2);
        int damage = p.Opponent.Hero.Damage; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 2, "generated Cannoneers fire at own turn end");
        End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 2, "enemy turn end does not fire Cannoneers");
        foreach (var token in Cannoneers(g, source.Id)) token.Silence();
        End(g); End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 2, "Silence removes generated token triggers");
        foreach (var token in Cannoneers(g, source.Id)) Kill(g, token);
        End(g); End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 2, "departed tokens cannot fire");
        foreach (int count in new[] { 0, 1, 2 }) WithPool(g, id, count, () => {
            StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
            var rng = g.Random.DrawCount; var spell = Play(g, p, id);
            Core2026Proof.Check(g.Random.DrawCount == rng, "tiny Discover no RNG");
            if (count == 0) Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "empty Discover still continues summon effect");
            else { Offers(g, spell, count); Choose(g); Core2026Proof.Check(p.HandZone.Count == 1, "tiny Discover adds one Pirate"); }
            Tokens(g, spell.Id, 2, 1); // The existing location still occupies slot zero.
            foreach (var token in Cannoneers(g, spell.Id)) token.Silence();
        });
        foreach (int slots in new[] { 6, 7 })
        {
            StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
            while (p.BoardZone.SlotCount < slots) Summon(p, "CS2_182");
            var spell = Play(g, p, id); FillHand(p); int selected = p.Choice.Choices.First(); Choose(g);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[selected].Zone == p.GraveyardZone, "full hand burns Pirate but summon continues");
            Tokens(g, spell.Id, 7 - slots, slots);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7, "seven physical slots respected");
            foreach (var token in Cannoneers(g, spell.Id)) token.Silence();
        }
    });
}
