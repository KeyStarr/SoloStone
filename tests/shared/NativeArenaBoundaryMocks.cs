// Isolated client boundaries for the production Arena transport (plugin/NativeArena.cs, extracted by extract_arena.py).
// Shared by native-bindings-logic (stub service) and progression (real ArenaService over a temp LocalProfile).
namespace NativeArenaBoundary
{
using PegasusShared;
using PegasusUtil;

internal static partial class NativeArena
{
    private static void UpdatePresentation() { }
    // Production derives this from the pending/last settled match record (plugin/NativeArenaMatch.cs).
    internal static bool ArenaMatchOwned;
    private static bool OwnsArenaMatch() => ArenaMatchOwned;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Arena: " + message); }
}

internal sealed class ManualLogSource { internal void LogInfo(string value) { } internal void LogWarning(string value) { } internal void LogError(string value) { } }
internal static class Time { internal static int frameCount; internal static float realtimeSinceStartup { get; set; } }
internal static class NativeUIProbe { internal static bool Active; internal static int Left; internal static string LeaveStuckScene(SceneMgr.Mode stuck, SceneMgr.Mode destination) { Left++; return null; } }
internal sealed class SceneMgr
{
    internal enum Mode { HUB, TOURNAMENT, DRAFT, GAMEPLAY }
    internal static SceneMgr Get() => new SceneMgr();
    internal static bool IsInitialized() => true;
    internal Mode GetMode() => Mode.DRAFT;
}
internal sealed class DraftDisplay
{
    internal static DraftDisplay Current = new DraftDisplay();
    internal static DraftDisplay Get() => Current;
    internal bool IsRewardsFlowPending;
}
internal sealed class Widget { internal void TriggerEvent(string value) { } }
internal sealed class ArenaLandingPageManager { internal Widget GetWidget() => new Widget(); }
internal sealed class ArenaModeToggleButton { internal T GetComponent<T>() where T : new() => new T(); }
internal sealed class AlertPopup
{
    internal enum ResponseDisplay { OK }
    internal sealed class PopupInfo { internal string m_id, m_headerText, m_text; internal ResponseDisplay m_responseDisplay; }
}
internal sealed class DialogManager
{
    internal static AlertPopup.PopupInfo Last;
    internal static DialogManager Get() => new DialogManager();
    internal void ShowPopup(AlertPopup.PopupInfo info) { Last = info; }
}
internal static class FairPackRules { internal static int NativeBoosterId(string type) => 1; }
internal static class GameUtils
{
    internal static int TranslateCardIdToDbId(string id) => id[id.Length - 1];
    internal static string GetHeroPowerCardIdFromHero(string hero) => "POWER_" + hero;
}
internal enum DeckType { NORMAL_DECK, DRAFT_DECK }
internal sealed class CollectionDeck { internal enum ChangeSource { Arena } internal long ID; internal DeckType Type; }
internal enum TAG_PREMIUM { NORMAL, GOLDEN }
internal abstract class RewardData { }
internal sealed class GoldRewardData(int amount) : RewardData { internal int Amount = amount; }
internal sealed class ArcaneDustRewardData(int amount) : RewardData { internal int Amount = amount; }
internal sealed class BoosterPackRewardData(int id, int count) : RewardData { internal int Id = id, Count = count; }
internal static class NetCache { internal sealed class CardDefinition { internal string Name; internal TAG_PREMIUM Premium; } }
internal sealed class Network
{
    internal static Action<int> Receive;
    internal static Network Get() => new Network();
    internal bool FakeHandleType(int id) { Receive(id); return true; }
    internal sealed class CardChoice { internal NetCache.CardDefinition CardDef; }
    internal sealed class CardUserData { internal int DbId, Count; internal TAG_PREMIUM Premium; }
    internal sealed class DeckContents { internal long Deck; internal List<CardUserData> Cards = new(); }
    internal sealed class BeginDraft
    {
        internal long DeckID; internal int Wins, MaxSlot; internal DraftSlotType SlotType; internal List<DraftSlotType> UniqueSlotTypesForDraft;
        internal ArenaSession Session; internal List<NetCache.CardDefinition> Heroes = new();
    }
    internal sealed class DraftChoicesAndContents
    {
        internal int Slot, RedraftSlot, MaxSlot, MaxWins, MaxLosses, Wins, Losses; internal DraftSlotType SlotType; internal ArenaSession Session;
        internal RewardChest Chest; internal DeckContents DeckInfo = new(); internal NetCache.CardDefinition Hero = new(), HeroPower = new();
        internal List<CardChoice> Choices = new(); internal List<DraftSlotType> UniqueSlotTypesForDraft = new();
    }
    internal sealed class DraftChosen { internal CardChoice ChosenCard; internal DraftSlotType SlotType; internal List<CardChoice> NextChoices; }
    internal sealed class DraftRetired { internal long Deck; internal RewardChest Chest; }
    internal sealed class RewardChest { internal List<RewardData> Rewards = new(); }
}
}

namespace NativeArenaBoundary.PegasusShared
{
internal enum ArenaSessionState { NO_RUN, DRAFTING, MIDRUN, REWARDS }
internal enum DraftSlotType { DRAFT_SLOT_HERO, DRAFT_SLOT_CARD }
internal enum RuneType { }
internal enum ErrorCode { ERROR_OK }
internal sealed class GameContentSeasonSpec { internal int SeasonId; internal ulong EndSecondsFromNow; }
internal sealed class ArenaSeasonSpec { internal GameContentSeasonSpec GameContentSeason; }
internal sealed class CardDef { internal int Asset, Premium; }
}
namespace NativeArenaBoundary.PegasusUtil
{
using PegasusShared;
internal sealed class ArenaSeasonInfo { internal int CurrentSeasonDisplayId; internal bool IsDualClass; internal ArenaSeasonSpec Season; internal List<ArenaRewardLevel> NormalRewards = new(); }
internal sealed class ArenaRewardLevel { internal int Wins, Losses; internal List<ArenaRewardSlot> Rewards = new(); }
internal sealed class ArenaRewardSlot { internal List<ArenaRewardCurrencySlot> CurrencyRewards = new(); internal List<ArenaRewardSpecificBoosterSlot> SpecificBoosterRewards = new(); }
internal enum ArenaRewardCurrencyType { GOLD, DUST, TAVERN_TICKET }
internal sealed class ArenaRewardCurrencySlot { internal ArenaRewardCurrencyType CurrencyType; internal int MinAmount, MaxAmount; }
internal sealed class ArenaRewardSpecificBoosterSlot { internal int BoosterType, MinBoosterCount, MaxBoosterCount; }
internal sealed class ArenaSession { internal int Wins, Losses; internal bool IsActive, IsInRewards, IsUnderground; internal ArenaSessionState State; internal ArenaSeasonInfo Info; }
internal sealed class ArenaSessionResponse { internal enum PacketID { ID = 351 } internal ErrorCode ErrorCode; internal ArenaSeasonInfo CurrentSeason; internal ArenaSession Session, UndergroundSession; }
internal sealed class ArenaRatingInfoResponse { internal enum PacketID { ID = 719 } internal ArenaPlayerInfo PlayerInfo; }
internal sealed class ArenaPlayerInfo { internal int NormalWins, NormalLosses, NormalMaxWins, NormalMaxLosses, UndergroundMaxWins, UndergroundMaxLosses, Rating; }
internal sealed class SaveArenaDeckResponse { internal enum PacketID { ID = 716 } internal DeckContents Decks; }
internal sealed class DeckContents { internal long DeckId; internal List<DeckCardData> Cards = new(); }
internal sealed class DeckCardData { internal CardDef Def; internal int Qty; }
internal sealed class DraftError { internal enum PacketID { ID = 251 } internal enum ErrorCode { DE_UNKNOWN, DE_NOT_IN_DRAFT, DE_NO_LICENSE } internal ErrorCode ErrorCode_; }
internal sealed class DraftBeginning { internal enum PacketID { ID = 246 } }
internal sealed class DraftRetired { internal enum PacketID { ID = 247 } }
internal sealed class DraftChoicesAndContents { internal enum PacketID { ID = 248 } }
internal sealed class DraftChosen { internal enum PacketID { ID = 249 } }
internal sealed class DraftRewardsAcked { internal enum PacketID { ID = 288 } }
internal sealed class DraftRemovePremiumsResponse { internal enum PacketID { ID = 355 } }
}
