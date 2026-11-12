using System.Xml.Linq;
using SabberStoneCore.Model;

// Era-specific card proofs retain their original pools; release boundaries are tested separately.
internal static class KnownMetaProofFixture
{
    private static readonly Lazy<OfflinePoolManifest> BlackEmpire = new(() => OfflinePoolPolicy.Parse(BlackEmpireCatalog()).BuildManifest());
    internal static XDocument BlackEmpireCatalog()
    {
        var xml = OfflinePoolFixtures.Catalog();
        var metas = xml.Root.Element("Metas").Elements("Meta").ToArray();
        var selected = metas.Single(m => (string)m.Attribute("id") == "black-empire");
        var sets = selected.Elements("Set").Select(s => (string)s.Attribute("name")).ToHashSet();
        var known = metas.SelectMany(m => m.Elements("Set")).Select(s => (string)s.Attribute("name")).ToHashSet();
        var extras = selected.Elements("ExtraCard").Select(s => (string)s.Attribute("cardId")).ToHashSet();
        var knownExtras = metas.SelectMany(m => m.Elements("ExtraCard")).Select(s => (string)s.Attribute("cardId")).ToHashSet();
        xml.Root.SetAttributeValue("activeMeta", "black-empire");
        xml.Root.Element("Sets").ReplaceNodes(selected.Elements("Set")
            .Select(s => new XElement("Set", new XAttribute("name", (string)s.Attribute("name")))));
        foreach (var card in xml.Root.Element("Members").Elements("Card"))
        {
            string set = (string)card.Attribute("set");
            string id = (string)card.Attribute("cardId");
            card.SetAttributeValue("scope", sets.Contains(set) || extras.Contains(id) ? "Standard" :
                known.Contains(set) || knownExtras.Contains(id) ? "Inactive" : "Past");
        }
        return xml;
    }
    internal static void UseBlackEmpire(Game game) => game.RestoreOfflinePools(BlackEmpire.Value);
    internal static Game BlackEmpireGame(SabberStoneCore.Enums.CardClass hero)
    {
        var game = ImbueProof.Make(hero);
        UseBlackEmpire(game);
        return game;
    }
}
