using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class RaptorHeraldProof
{
    internal static void Run()
    {
        foreach (string identity in new[] { "CORE_EDR_004", "CORE_EDR_004_2026" })
            StandardSetsProof.Case(identity, (g, p, id) => {
                var filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.BEAST);
                GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.ROGUE }, filter);
                foreach (bool kindred in new[] { false, true })
                foreach (string gift in new[] { "EDR_100t1", "EDR_100t2", "EDR_100t8" })
                {
                    var game = GiftedDiscoverProof.New(g.Seed, CardClass.HUNTER); var owner = game.Player1;
                    if (kindred) { Play(game, owner, "CS2_171"); End(game); End(game); }
                    else Play(game, owner, "CS2_171");
                    // Arrow Retriever is an admitted 2-cost 3/1 Beast with no cost aura; all three gifts are eligible.
                    var xml = OfflinePoolFixtures.Catalog();
                    var rows = xml.Root.Element("Members");
                    rows.ReplaceNodes(rows.Elements("Card").Single(r => (string)r.Attribute("cardId") == "TIME_601"));
                    xml.Root.Element("Dependencies").RemoveNodes();
                    var installed = game.OfflinePools;
                    var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
                    var source = (Minion)Add(owner, id); owner.BaseMana = 10; owner.UsedMana = 0;
                    Core2026Proof.Check(SabberStoneCore.Model.Rewind.RewindRules.TryBegin(game,
                        SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, source)), "fixture begins before transient catalog");
                    try {
                        property.SetValue(game, OfflinePoolFixtures.Manifest(xml));
                        PlayEntity(game, owner, source);
                    }
                    finally { property.SetValue(game, installed); }
                    Core2026Proof.Check(source.AttackDamage == 4 && source.BaseHealth == 2 && source.IsRace(Race.BEAST), "pinned 4/2 Beast");
                    var offers = GiftedDiscoverProof.Offers(game, id, filter);
                    int index = 0;
                    Core2026Proof.Check(offers.Length == 1 && offers[0].Card.Id == "TIME_601", "fixed admitted Beast cost fixture");
                    GiftedDiscoverProof.Pair(game, offers[index], gift);
                    int chosenId = offers[index].Id, baseCost = offers[index].Card.Cost;
                    PoolPilotProof.Resume(game, next => {
                        GiftedDiscoverProof.Choose(next, offers, index);
                        var chosen = next.IdEntityDic[chosenId];
                        int expected = Math.Max(0, baseCost - (gift == "EDR_100t2" ? 2 : 0) - (kindred ? 1 : 0));
                        Core2026Proof.Check(chosen.Cost == expected && chosen[GameTag.COST] == expected && DarkGifts.Has(chosen),
                            $"Kindred={kindred}, gift={gift}, card={chosen.Card.Id}, expected={expected}, cost={chosen.Cost}, tag={chosen[GameTag.COST]}, hasGift={DarkGifts.Has(chosen)}");
                        Core2026Proof.Check(chosen.Zone.Type == (gift == "EDR_100t8" ? Zone.DECK : Zone.HAND), "Sweet Dreams relocation retains discount");
                    });
                    RewindSliceProof.CheckCopies(game, next => Core2026Proof.Check(next.IdEntityDic[chosenId].Cost ==
                        Math.Max(0, baseCost - (gift == "EDR_100t2" ? 2 : 0) - (kindred ? 1 : 0)), "discount survives clone/load"));
                }
            });
    }
}
