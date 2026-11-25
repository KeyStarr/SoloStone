using System.Collections.Generic;
using System.Linq;

// Read the loaded offline client's constructed collection before the save picker or any deck repairs.
internal static class NativeRetailCollection
{
    internal static RetailCollection Read(IReadOnlyDictionary<string, EntityDef> definitions)
    {
        var collection = new RetailCollection();
        var baseHeroes = LocalDecks.PlayerHeroes.ToDictionary(id => definitions[id].GetClass());
        foreach (var hero in baseHeroes.Values) collection.AddVersion(hero, 1, 1);
        var index = GameDbf.GetIndex();
        var qualities = GameDbf.CardTag.GetRecords()
            .Where(row => row.TagValue != 0 && (row.TagId == (int)GAME_TAG.HAS_DIAMOND_QUALITY
                || row.TagId == (int)GAME_TAG.HAS_SIGNATURE_QUALITY))
            .ToLookup(row => row.CardId, row => (GAME_TAG)row.TagId);
        foreach (var id in index.GetCollectibleCardIds())
        {
            var def = definitions[id];
            baseHeroes.TryGetValue(def.GetClass(), out var hero);
            if (id == hero) continue;
            if (def.IsHeroSkin())
            {
                var skin = GameUtils.GetCardHeroRecordForCardId(index.GetCardRecord(id).ID);
                if (hero != null && skin.HeroType != Assets.CardHero.HeroType.BATTLEGROUNDS_HERO
                    && skin.HeroType != Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE)
                    collection.HeroClasses.Add(id, hero);
                continue;
            }
            switch (def.GetCardType())
            {
                case TAG_CARDTYPE.MINION:
                case TAG_CARDTYPE.SPELL:
                case TAG_CARDTYPE.WEAPON:
                case TAG_CARDTYPE.LOCATION:
                case TAG_CARDTYPE.HERO:
                    break;
                default:
                    continue;
            }
            int copies = def.GetTag(GAME_TAG.RARITY) == (int)TAG_RARITY.LEGENDARY ? 1 : 2;
            collection.AddCard(id, copies);
            var tags = qualities[index.GetCardRecord(id).ID];
            if (tags.Contains(GAME_TAG.HAS_DIAMOND_QUALITY)) collection.AddVersion(id, 2, copies);
            if (tags.Contains(GAME_TAG.HAS_SIGNATURE_QUALITY)) collection.AddVersion(id, 3, copies);
        }
        return collection;
    }
}
