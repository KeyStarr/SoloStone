using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Eligibility/export oracle; prospective extras use admitted identities, with scopes/metadata derived from policy/export.
internal static class OfflinePoolExpectations
{
    private static readonly Lazy<(JsonElement Card, bool Standard)[]> Export = new(() => {
        using var records = JsonDocument.Parse(File.ReadAllText("data/pools/pool-eligibility-v1.json"));
        using var overrides = JsonDocument.Parse(File.ReadAllText("data/carddb/37.0.0.254477/set-overrides.json"));
        using var policy = JsonDocument.Parse(File.ReadAllText("data/pools/offline-standard-v1.json"));
        var metas = policy.RootElement.GetProperty("metas").EnumerateArray().ToArray();
        var active = metas.Single(m => m.GetProperty("id").GetString() == policy.RootElement.GetProperty("activeMeta").GetString());
        var sets = active.GetProperty("sets").EnumerateArray().Select(s => s.GetString()).ToHashSet();
        var extras = active.GetProperty("extraCards").EnumerateArray().Select(s => s.GetString()).ToHashSet();
        var knownExtras = metas.SelectMany(m => m.GetProperty("extraCards").EnumerateArray()).Select(s => s.GetString()).ToHashSet();
        var inactive = metas.SelectMany(m => m.GetProperty("sets").EnumerateArray()).Select(s => s.GetString()).Except(sets).ToHashSet();
        var byId = ClientExport.RequireCards();
        var identities = records.RootElement.GetProperty("records").EnumerateArray().Select(r =>
            (Id: r.GetProperty("cardId").GetString(), DbfId: r.GetProperty("dbfId").GetInt32()))
            .Concat(OfflinePoolPolicy.Load().Members.Where(m => knownExtras.Contains(m.CardId)).Select(m => (Id: m.CardId, DbfId: m.DbfId))).Distinct();
        return identities.Select(r => {
            var card = byId[r.Id];
            if (card.GetProperty("dbfId").GetInt32() != r.DbfId) throw new Exception("eligibility/export identity mismatch");
            string set = card.GetProperty("cardSet").GetProperty("name").GetString();
            if (set == "INVALID") set = overrides.RootElement.GetProperty("overrides").GetProperty(card.GetProperty("dbfId").ToString()).GetProperty("set").GetString();
            bool standard = sets.Contains(set) || extras.Contains(r.Id);
            return (Card: card, Standard: standard, Inactive: !standard && (inactive.Contains(set) || knownExtras.Contains(r.Id)));
        }).Where(c => !c.Inactive).Select(c => (c.Card, c.Standard)).ToArray();
    });

    // Derive collectible reprint groups from the export. Hero skins can share names without sharing identity.
    private static readonly Lazy<Dictionary<int, int[]>> ReprintGroups = new(() => {
        var groups = ClientExport.RequireCards().Values
            .Where(c => c.GetProperty("collectible").GetBoolean() && c.GetProperty("cardType").GetString() != "HERO")
            .GroupBy(c => (c.GetProperty("name").GetString(), c.GetProperty("cardType").GetString()));
        var byDbf = new Dictionary<int, int[]>();
        foreach (var group in groups)
        {
            var printings = group.Select(c => Number(c, "dbfId")).ToArray();
            if (printings.Length > 1)
                foreach (int dbf in printings) byDbf.Add(dbf, printings);
        }
        return byDbf;
    });

    internal static string[] Ids(PoolScope scope) => Export.Value.Where(c => scope == PoolScope.Wild || c.Standard == (scope == PoolScope.Standard))
        .OrderBy(c => c.Card.GetProperty("dbfId").GetInt32()).Select(c => c.Card.GetProperty("cardId").GetString()).ToArray();

    internal static int Count(OfflinePoolFilter filter, string hero, int source, PoolDestination destination = PoolDestination.Hand)
        => Ids(filter, hero, source, destination).Length;

    internal static string[] Ids(OfflinePoolFilter filter, string hero, int source, PoolDestination destination = PoolDestination.Hand)
    {
        var printings = source == 0 ? null : ReprintGroups.Value.GetValueOrDefault(source);
        var rows = Export.Value.Where(c => filter.Scope == PoolScope.Wild || c.Standard == (filter.Scope == PoolScope.Standard)).Select(c => c.Card)
            .Where(c => {
                int dbf = Number(c, "dbfId");
                return dbf != source && printings?.Contains(dbf) != true;
            });
        // Printed Colossals cannot enter play directly; Herald Colossals cannot be generated off-board either.
        rows = rows.Where(c => !c.GetProperty("tags").TryGetProperty("2247", out var colossal) || colossal.GetInt32() <= 0
            || (destination == PoolDestination.Hand || destination == PoolDestination.Deck)
                && (!c.GetProperty("tags").TryGetProperty("4538", out var herald) || herald.GetInt32() <= 0));
        // Exported Old Gods are available only to explicit Old God/curated recipes.
        rows = rows.Where(c => !c.GetProperty("tags").TryGetProperty("4754", out var god) || god.GetInt32() == 0
            || filter.Keyword == (GameTag)4754 || filter.CardIds?.Contains(c.GetProperty("cardId").GetString()) == true);
        if (filter.CardIds != null) rows = rows.Where(c => filter.CardIds.Contains(c.GetProperty("cardId").GetString()));
        if (filter.AnyOf.Count != 0)
        {
            var union = filter.AnyOf.SelectMany(branch => Ids(branch, hero, source, destination)).ToHashSet(StringComparer.Ordinal);
            rows = rows.Where(c => union.Contains(c.GetProperty("cardId").GetString()));
        }
        if (filter.ExactAttack.HasValue) rows = rows.Where(c => Number(c, "atk") == filter.ExactAttack.Value);
        if (filter.AttackParity.HasValue) rows = rows.Where(c => Number(c, "atk") % 2 == filter.AttackParity.Value);
        if (filter.CostParity.HasValue) rows = rows.Where(c => Number(c, "cost") % 2 == filter.CostParity.Value);
        if (filter.Type.HasValue) rows = rows.Where(c => c.GetProperty("cardType").GetString() == filter.Type.Value.ToString());
        if (filter.MinCost.HasValue) rows = rows.Where(c => Number(c, "cost") >= filter.MinCost.Value);
        if (filter.MaxCost.HasValue) rows = rows.Where(c => Number(c, "cost") <= filter.MaxCost.Value);
        if (filter.Rarity.HasValue) rows = rows.Where(c => c.GetProperty("rarity").GetString() == filter.Rarity.Value.ToString());
        if (filter.SpellSchool.HasValue) rows = rows.Where(c => Array.IndexOf(new[] { "NONE", "ARCANE", "FIRE", "FROST", "NATURE", "HOLY", "SHADOW", "FEL" },
            c.TryGetProperty("spellSchool", out var school) ? school.GetString() : "NONE") == filter.SpellSchool.Value);
        if (filter.Keyword.HasValue) rows = rows.Where(c => c.TryGetProperty("tags", out var tags)
            && tags.TryGetProperty(((int)filter.Keyword.Value).ToString(), out var value) && value.GetInt32() > 0);
        if (filter.Race.HasValue) rows = rows.Where(c => Strings(c, "races").Contains(filter.Race.Value.ToString())
            || filter.Race != Race.ALL && filter.Race != Race.INVALID && Strings(c, "races").Contains("ALL"));
        if (filter.MultipleMinionTypes) rows = rows.Where(c => Strings(c, "races").Length > 1 || Strings(c, "races").Contains("ALL"));
        return rows.Where(c => filter.ClassMode switch {
            PoolClassMode.Any => true,
            PoolClassMode.Exact => Strings(c, "cardClasses").Contains(filter.ExactClass.Value.ToString()),
            PoolClassMode.ControllerAndNeutral => Strings(c, "cardClasses").Intersect(new[] { hero, "NEUTRAL" }).Any(),
            PoolClassMode.OtherClass => !Strings(c, "cardClasses").Intersect(new[] { hero, "NEUTRAL" }).Any(),
            _ => throw new Exception("unsupported class mode")
        }).OrderBy(c => c.GetProperty("dbfId").GetInt32()).Select(c => c.GetProperty("cardId").GetString()).ToArray();
    }
    private static int Number(JsonElement card, string key) => card.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
    private static string[] Strings(JsonElement card, string key) => card.TryGetProperty(key, out var value)
        ? value.EnumerateArray().Select(s => s.GetString()).ToArray() : Array.Empty<string>();
}
