// Stand-ins for the client types that the recipe picker model (plugin/NativeRecipePickerModel.cs, NativeDeckRunes.cs)
// maps recipes onto: the template deck list entry, the deck being edited, the format, class and rune enums.
// Only the members the model and these tests touch. The pinned client tag value is checked in Program.cs when a client is present.
namespace PegasusShared
{
    public enum FormatType { FT_UNKNOWN, FT_WILD, FT_STANDARD }
}

public enum TAG_CLASS { INVALID, DEATHKNIGHT, DEMONHUNTER, DRUID, HUNTER, MAGE, PALADIN, PRIEST, ROGUE, SHAMAN, WARLOCK, WARRIOR }

public enum RuneType { RT_NONE, RT_BLOOD, RT_FROST, RT_UNHOLY }

public class CollectionDeck
{
    private readonly Dictionary<string, object> sideboards = new();
    public Dictionary<string, object> GetAllSideboards() => sideboards;
    public void ClearSideboards() => sideboards.Clear();
}

public class CollectionManager
{
    public class TemplateDeck
    {
        public int m_id, m_deckTemplateId;
        public TAG_CLASS m_class;
        public string m_title, m_description;
        public PegasusShared.FormatType m_formatType;
        public bool m_isStarterDeck;
        public RuneType m_rune1, m_rune2, m_rune3;
        public Dictionary<string, int> m_cardIds = new();
        public Dictionary<string, List<string>> m_sideboardCardIds = new();
    }
}
