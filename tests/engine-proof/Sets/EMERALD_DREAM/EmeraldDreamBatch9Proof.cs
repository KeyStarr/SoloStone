using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EmeraldDreamBatch9Proof
{
    internal static IPlayable Choose(Game g, Controller p, string id, int branch, ICharacter target = null, bool resume = true)
    {
        var source = Add(p, id);
        p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        Core2026Proof.Check(source.ChooseOnePlayables.Select(c => c.Card.Id).SequenceEqual(new[] { id + "a", id + "b" })
            && source.ChooseOnePlayables.All(c => c.Card.Implemented), "exact registered export options");
        int option = branch == 0 ? -1 : branch;
        var task = p.Options().FirstOrDefault(t => t.Source?.Id == source.Id && t.ChooseOne == option && t.Target?.Id == target?.Id);
        Core2026Proof.Check(task != null, "bot enumerates selected branch and target");
        var view = g.GetPendingDecisions().Single();
        Core2026Proof.Check(view.Options.Any(o => o.EntityId == source.Id && o.BranchIndex == option && o.TargetId == (target?.Id ?? 0)),
            "decision primitive exposes same branch");
        void PlayChoice(Game next) {
            var owner = next.ControllerById(p.Id);
            var selected = owner.Options().First(t => t.Source?.Id == source.Id && t.ChooseOne == option && t.Target?.Id == target?.Id);
            Core2026Proof.Check(next.Process(selected), "bot branch accepted after clone/snapshot");
        }
        if (resume) PoolPilotProof.Resume(g, PlayChoice);
        else PlayChoice(g);
        Core2026Proof.Check(p.UsedMana == source.Cost, "choice pays once");
        return source;
    }

    internal static void GeneratedBoard(Game g, IPlayable source, IEnumerable<Minion> output, int cost, int count, string recipe = null)
    {
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == (recipe ?? source.Card.Id));
        var filter = declaration.Pools.Single().Filter;
        var pool = OfflineCardPools.Candidates(g, source.Controller.HeroClass, filter, source.Card.AssetId);
        var cards = output.ToArray();
        Core2026Proof.Check(cards.Length == count && cards.Select(c => c.Id).Distinct().Count() == count, "summon count and unique entity ids");
        foreach (var card in cards)
        {
            Core2026Proof.Check(pool.Any(c => c.Id == card.Card.Id) && card.Card.Id != source.Card.Id && card.Card.Cost == cost
                && card.Card.Type == CardType.MINION, "declared any-class printed-cost minion pool and source exclusion");
            var member = g.OfflinePools.Standard.Single(m => m.CardId == card.Card.Id);
            Core2026Proof.Check(card.Card.ATK == member.Attack && card.Card.Health == member.Health && card.Health == card.Card.Health,
                "pinned summoned definition and Health");
            int attack = card.Card.ATK + (card.Card.Id == "JAIL_311" && card.Controller.DeckZone.Count >= 25 ? 5 : 0);
            Core2026Proof.Check(card.AttackDamage == attack, "summoned effective Attack");
            Core2026Proof.Check(card.Controller == source.Controller && card.Zone == source.Controller.BoardZone && g.IdEntityDic[card.Id] == card
                && card[GameTag.DISPLAYED_CREATOR] == source.Id && card.ZonePosition >= 0, "summoned owner/zone/entity/creator/physical position");
        }
    }
}
