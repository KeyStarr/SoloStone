using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using System.Xml.Linq;
internal static class BEPoolProof
{
    internal static OfflinePoolFilter Declared(string id) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;
    internal static Minion[] Children(Controller p, IPlayable source) => p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
    internal static void Outputs(Game g, Controller owner, IPlayable source, int count, int cost, int attackBonus = 0)
    {
        var children = Children(owner, source);
        Core2026Proof.Check(children.Length == count, "exact summon count");
        var pool = OfflineCardPools.Candidates(g, source.Controller.HeroClass, Declared(source.Card.Id).WithCost(cost), source.Card.AssetId);
        foreach (var m in children)
            Core2026Proof.Check(m.Id != source.Id && m.Card.Id != source.Card.Id && pool.Any(c => c.Id == m.Card.Id) && m.Card.Type == CardType.MINION && m.Card.Cost == cost
                && m.Controller == owner && m.Zone == owner.BoardZone && owner.BoardZone.GetSlot(m.ZonePosition) == m && m.ZonePosition == owner.BoardZone.SlotCount - children.Length + Array.IndexOf(children, m)
                && m.AttackDamage == m.Card.ATK + attackBonus && m.Health == m.Card.Health, "summoned identity/filter/owner/zone/physical position/stats");
    }
    internal static void Restricted(Game g, string id, int count, Action action, int? cost = null)
    {
        var filter = cost.HasValue ? Declared(id).WithCost(cost.Value) : Declared(id);
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        if (count == 0)
        {
            selected = rows.Elements("Card").Take(1).ToArray();
            selected[0].SetAttributeValue("scope", "Standard"); selected[0].SetAttributeValue("set", "CORE");
            selected[0].SetAttributeValue("type", "WEAPON"); selected[0].SetAttributeValue("cost", 999);
        }
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId).Count == count, "fixture size");
        var installed = g.OfflinePools;
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }
    internal static void Location(Controller p)
    {
        var location = Entity.FromCard(p, Cards.FromId("CORE_REV_990"));
        p.BoardZone.Add((BoardObject)location, p.BoardZone.SlotCount);
        p.Game.DeathProcessingAndAuraUpdate();
    }
}
