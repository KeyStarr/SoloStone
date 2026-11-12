using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class CremateProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_900", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        replay.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Core2026Proof.Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay finishes its recipe synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess replay completes cast once");
            var gifted = next.Player1.HandZone.Concat(next.Player1.DeckZone).Where(DarkGifts.Has).ToArray();
            Core2026Proof.Check(gifted.Length == 1, "Tess Cremate grants one gifted minion");
            Core2026Proof.End(next);
        });

        DecisionSpellOrderingProof.Run(g.Seed, id, (next, chosen) =>
            Core2026Proof.Check(DarkGifts.Has(chosen), "Cremate gift resolved before after-cast"));

        AdaptiveCostDiscount(g.Seed, id);
        // DK has no playable hero here; stolen-card execution uses the controller's class.
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.ROGUE },
            OfflinePoolFilter.Discover(type: CardType.MINION), discount: true);
        foreach (var gift in new[] { "EDR_100t5", "EDR_100t2", "EDR_100t8" })
        {
            var game = GiftedDiscoverProof.New(g.Seed, CardClass.ROGUE);
            GiftedDiscoverProof.Open(game, id);
            var offers = GiftedDiscoverProof.Offers(game, id, OfflinePoolFilter.Discover(type: CardType.MINION));
            var chosen = offers[0]; chosen.Cost = gift == "EDR_100t2" ? 6 : 1;
            if (gift == "EDR_100t2") ((SabberStoneCore.Model.Entities.Minion)chosen).AttackDamage = 3;
            GiftedDiscoverProof.Pair(game, chosen, gift);
            // dg11b-sweet-dreams-sequence: backwards move resets the plain preexisting cost,
            // then Cremate discounts in the resolved zone. Ordinary later draw keeps that discount.
            PoolPilotProof.Resume(game, next => {
                var selected = next.IdEntityDic[chosen.Id]; GiftedDiscoverProof.Choose(next, offers);
                int expected = gift == "EDR_100t8" ? Math.Max(0, selected.Card.Cost - 2) : gift == "EDR_100t2" ? 2 : 0;
                Core2026Proof.Check(selected.Cost == expected && selected[GameTag.COST] == expected, "floor/Short Claws stacking/Sweet Dreams cost order");
                if (gift == "EDR_100t8")
                {
                    Core2026Proof.Check(next.Player1.DeckZone.TopCard.Id == selected.Id, "Sweet Dreams chosen on top");
                    Generic.Draw(next.Player1, selected);
                    Core2026Proof.Check(selected.Zone == next.Player1.HandZone && selected.Cost == expected && DarkGifts.Has(selected), "later draw keeps gift and Cremate reduction");
                }
            });
        }
    });

    private static void AdaptiveCostDiscount(long seed, string id)
    {
        var game = GiftedDiscoverProof.New(seed, CardClass.ROGUE);
        var enemy = Summon(game.Player2, "CS2_182");
        var installed = game.OfflinePools;
        var xml = KnownMetaProofFixture.BlackEmpireCatalog();
        var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(r => (string)r.Attribute("cardId") == "BE_733").ToArray());
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try
        {
            property.SetValue(game, OfflinePoolFixtures.Manifest(xml));
            GiftedDiscoverProof.Open(game, id);
        }
        finally { property.SetValue(game, installed); }
        var offers = game.Player1.Choice.Choices.Select(i => game.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length == 1 && offers[0].Card.Id == "BE_733", "adaptive-cost fixture offers Titanic Thrasher");
        GiftedDiscoverProof.Pair(game, offers[0], "EDR_100t5");
        PoolPilotProof.Resume(game, next => {
            GiftedDiscoverProof.Choose(next, offers);
            var chosen = next.IdEntityDic[offers[0].Id];
            next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(chosen.Cost == 5 && chosen[GameTag.COST] == 5, "Cremate subtracts two once from 8 minus one enemy");
            var second = Summon(next.Player2, "CS2_182");
            Core2026Proof.Check(chosen.Cost == 4, "Cremate discount stays separate as enemy board grows");
            Kill(next, second);
            Kill(next, (SabberStoneCore.Model.Entities.Minion)next.IdEntityDic[enemy.Id]);
            Core2026Proof.Check(chosen.Cost == 6, "Cremate retains only its two-cost discount after enemies leave");
        });
    }
}
