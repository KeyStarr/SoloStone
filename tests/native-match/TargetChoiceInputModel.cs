using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PegasusGame;
using SabberStoneCore.Enums;

internal static partial class Program
{
    private static void PinnedTargetInputContract()
    {
        using var pe = new PEReader(File.OpenRead("references/game/Assembly-CSharp.dll"));
        var metadata = pe.GetMetadataReader();
        byte[] Body(string typeName, string methodName)
        {
            var type = metadata.GetTypeDefinition(metadata.TypeDefinitions.Single(h =>
                metadata.GetString(metadata.GetTypeDefinition(h).Name) == typeName
                && metadata.GetString(metadata.GetTypeDefinition(h).Namespace) == ""
                && metadata.GetTypeDefinition(h).GetDeclaringType().IsNil));
            var method = metadata.GetMethodDefinition(type.GetMethods().Single(h =>
                metadata.GetString(metadata.GetMethodDefinition(h).Name) == methodName));
            return pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
        }
        List<string> Calls(string type, string method) => IlCalls.Calls(Body(type, method), metadata);
        Check(Calls("GameState", "EnterChoiceMode").Contains("Entity.HasHandTarget")
            && Body("Entity", "HasHandTarget").AsSpan().IndexOf(BitConverter.GetBytes(1508)) >= 0
            && Calls("Entity", "HasHandTarget").Contains("GameState.GetFriendlyEntityChoices")
            && Calls("Entity", "HasHandTarget").Contains("Card.GetZone"),
            "pinned EnterChoiceMode latches HasHandTarget from tag 1508 plus offered hand Cards");
        Check(Calls("InputManager", "HandleClickOnCard").Contains("GameState.CanTargetCardsInHand")
            && Calls("InputManager", "HandleClickOnCard").Contains("InputManager.HandleClickOnCardInBattlefield")
            && Calls("InputManager", "HandleClickOnCardInBattlefield").Contains("InputManager.DoNetworkResponse")
            && Calls("InputManager", "DoNetworkResponse").Contains("InputManager.DoNetworkChoice"),
            "pinned hand click gates battlefield dispatch on CanTargetCardsInHand then sends CHOICE response");
        Check(Calls("InputManager", "DoNetworkChoice").Contains("GameState.IsChoosableEntity")
            && Calls("InputManager", "DoNetworkChoice").Contains("GameState.SendChoices")
            && Calls("GameState", "SendChoices").Contains("Network.SendChoices")
            && Calls("InputManager", "StartPendingChoiceTarget").Contains("TargetReticleManager.CreateFriendlyTargetArrow")
            && Calls("TargetReticleManager", "CreateFriendlyTargetArrow").Contains("Entity.GetCard"),
            "pinned choice validates offered entity, sends through Network, and needs source Card for arrow");
    }

    private static int ChoiceWireTag(Capture capture, int entity, int tag)
    {
        int value = 0;
        foreach (var row in DeckDiscoverWireHistory(capture, 0))
        {
            var snapshot = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity
                : row.HasChangeEntity ? row.ChangeEntity : null;
            if (snapshot?.Entity == entity && snapshot.Tags.FirstOrDefault(t => t.Name == tag) is { } item)
                value = item.Value;
            if (row.HasTagChange && row.TagChange.Entity == entity && row.TagChange.Tag == tag)
                value = row.TagChange.Value;
        }
        return value;
    }

    // Headless model of the audited native hand-click path. Only Network.SendChoices is a test boundary.
    private sealed class TargetChoiceInputModel
    {
        private readonly EntityChoices choices;
        private readonly Dictionary<int, (string Card, int Zone)> wire;
        private readonly Action<int, List<int>> sendChoices;
        private readonly bool canTargetCardsInHand;
        internal int Sent { get; private set; }
        internal TargetChoiceInputModel(Capture capture, Action<int, List<int>> sendChoices)
        {
            choices = capture.Last<EntityChoices>(17);
            wire = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
            this.sendChoices = sendChoices;
            // GameState.EnterChoiceMode -> Entity.HasHandTarget; evaluated when packet arrives.
            canTargetCardsInHand = ChoiceWireTag(capture, choices.Source, 1508) > 0
                && choices.Entities.Any(id => wire.TryGetValue(id, out var card)
                    && !string.IsNullOrEmpty(card.Card) && card.Zone == (int)SabberStoneCore.Enums.Zone.HAND);
        }
        internal bool ClickHand(int id, NativeMatch.OfferPresentation offer)
        {
            // Actor hit test, source arrow Card, HandleClickOnCard's hand gate, then DoNetworkChoice.
            if (Sent != 0 || !wire.TryGetValue(choices.Source, out var source) || string.IsNullOrEmpty(source.Card)
                || source.Zone == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME
                || !wire.TryGetValue(id, out var card) || card.Zone != (int)SabberStoneCore.Enums.Zone.HAND
                || !offer.CardExists || !offer.HasCardDef || !offer.CardActive || !offer.ActorExists
                || !offer.ActorActive || !offer.ActorShown || !offer.ColliderEnabled || offer.Loading || offer.InputBlocked
                || !canTargetCardsInHand || choices.ChoiceType != (int)ChoiceType.TARGET
                || !choices.Entities.Contains(id) || choices.CountMin != 1 || choices.CountMax != 1)
                return false;
            Sent++;
            sendChoices(choices.Id, new List<int> { id });
            return true;
        }
    }
}
