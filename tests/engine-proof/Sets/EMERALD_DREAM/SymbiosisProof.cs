using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class SymbiosisProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_273", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_06"));
        var filter = Filter(id);
        Core2026Proof.Check(filter.ClassMode == PoolClassMode.OtherClass && filter.Keyword == GameTag.CHOOSE_ONE, "printed other-class Choose One pool");
        foreach (int count in new[] { 0, 1, 2 })
        {
            EmptyHand(p);
            Pool(g, id, count, () => {
                var rng = g.Random.CaptureState(); Play(g, p, id);
                if (count == 0) Core2026Proof.Check(p.Choice == null && g.Random.CaptureState().Equals(rng), "empty pool no choice/RNG");
                else { var offers = Offers(g, id, filter, count); Answer(g, offers[0].Id); Core2026Proof.Check(p.HandZone.Single().Id == offers[0].Id, "small pool grants chosen entity"); }
            });
        }
        foreach (bool full in new[] { false, true })
        {
            EmptyHand(p);
            if (full) { LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); }
            var before = g.CloneExact(true);
            var source = Play(g, p, id); Play(before, before.Player1, id);
            var offers = Offers(g, id, filter, 3);
            Core2026Proof.Check(Vector(g) == Vector(before) && g.Random.CaptureState().Equals(before.Random.CaptureState()), "same seed same distinct offers/RNG");
            if (full) FillHand(p);
            int slots = p.BoardZone.SlotCount;
            Resume(g, next => {
                var owner = next.Player1; var chosen = next.IdEntityDic[offers[0].Id];
                Answer(next, chosen.Id);
                Core2026Proof.Check(chosen.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND)
                    && owner.HandZone.Count == (full ? 10 : 1) && owner.BoardZone.SlotCount == slots, "chosen grant/burn and board capacity");
                Core2026Proof.Check(offers.Skip(1).All(o => next.IdEntityDic[o.Id].Zone == owner.SetasideZone), "unchosen stay set aside");
                if (!full) Core2026Proof.Check(chosen.ChooseOnePlayables.Length == 2 && chosen.ChooseOnePlayables.All(x => x.Card.Implemented), "generated Choose One retains executable options");
            });
        }
    });

    internal static string Vector(Game g) => string.Join(",", g.Player1.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id));

    internal static void Answer(Game g, int entityId)
    {
        var decision = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { entityId }), out var error), "Discover answer accepted: " + error);
    }

    internal static IPlayable[] Offers(Game g, string sourceId, OfflinePoolFilter filter, int count)
    {
        var p = g.Player1;
        Core2026Proof.Check(p.Choice != null, "Discover opens: " + sourceId);
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(sourceId).AssetId);
        Core2026Proof.Check(offers.Length == count && count <= 3 && offers.Select(o => o.Card.Id).Distinct().Count() == count, "distinct <=3 offers");
        foreach (var offer in offers)
        {
            var member = g.OfflinePools.Standard.Single(m => m.CardId == offer.Card.Id);
            Core2026Proof.Check(offer.Card.Id != sourceId && candidates.Any(c => c.AssetId == offer.Card.AssetId), "candidate identity/source excluded");
            Core2026Proof.Check(offer.Card.Cost == member.Cost && offer.Card.ATK == member.Attack && offer.Card.Health == member.Health, "pinned offer stats");
            Core2026Proof.Check(!filter.Type.HasValue || offer.Card.Type == filter.Type, "printed card type");
            Core2026Proof.Check(!filter.SpellSchool.HasValue || offer.Card[GameTag.SPELL_SCHOOL] == filter.SpellSchool, "printed spell school");
            Core2026Proof.Check(!filter.Keyword.HasValue || offer.Card[filter.Keyword.Value] > 0, "printed keyword");
            Core2026Proof.Check(filter.ClassMode switch {
                PoolClassMode.Exact => member.Classes.Contains(filter.ExactClass.Value.ToString()),
                PoolClassMode.OtherClass => !member.Classes.Contains(p.HeroClass.ToString()) && !member.Classes.Contains("NEUTRAL"),
                _ => member.Classes.Contains(p.HeroClass.ToString()) || member.Classes.Contains("NEUTRAL")
            }, "printed class domain");
            Core2026Proof.Check(offer.Controller == p && offer.Zone == p.SetasideZone, "private offer owner/zone");
        }
        return offers;
    }
}
