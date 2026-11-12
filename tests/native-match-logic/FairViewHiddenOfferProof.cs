using EngineUnknown = SabberStoneCore.Model.Entities.Unknown;
using EngineGame = SabberStoneCore.Model.Game;
using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static QuestNativeProof;
using SabberStoneCore.Actions;

// Fair search over own-deck offers (Dark Gift deck Discover, Tracking) and effects that read hidden cards.
internal static partial class FairViewHiddenOfferProof
{
    internal static void CanonicalPrivacyAssertions()
    {
        var source = Fixture();
        var original = Add(source.Player2, "NEW1_008");
        var view = SearchGame(FairBotState.Create(source, 1, 77, DeckStyle.Value));
        AssertHiddenActors(source, view, 2, original.Id);
        var slot = view.Player2.HandZone[original.ZonePosition];
        Check(slot.Id != original.Id, "privacy assertion fixture must remap original hand ID");
        void MustReject(Action mutate, Action restore)
        {
            mutate();
            bool rejected = false;
            try { AssertHiddenActors(source, view, 2, original.Id); }
            catch (Exception) { rejected = true; }
            finally { restore(); }
            Check(rejected, "privacy proof accepted a leak under canonical ID");
        }
        var unknown = slot.Card;
        MustReject(() => slot.Card = original.Card, () => slot.Card = unknown);
        MustReject(() => slot.ChooseOnePlayables = original.ChooseOnePlayables, () => slot.ChooseOnePlayables = null);
        MustReject(() => slot[GameTag.LINKED_ENTITY] = original.Id, () => slot.NativeTags.Remove(GameTag.LINKED_ENTITY));
        MustReject(() => view.Player2.HandZone.Remove(slot), () => view.Player2.HandZone.Add(slot));
    }
}
