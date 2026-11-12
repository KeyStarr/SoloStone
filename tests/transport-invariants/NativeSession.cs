using System.Reflection;
using System.Runtime.ExceptionServices;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal sealed class NativeSession(NativeMatch match)
{
    private HistoryShape history;
    internal HistoryShape History => history ??= new(match.Game, match.WireReturnId, match.EngineReturnId);
    internal ClientModel Client = new() { WireId = match.WireReturnId, EngineId = match.EngineReturnId };
    internal readonly List<WirePacket> Packets = new();
    // Every CHANGE_ENTITY the wire carried, in order (a regression witness can name the transform it needs).
    internal readonly List<(int Entity, string Card)> Changed = new();
    private PowerEntityChoices pendingChoice;
    private HashSet<int> rewindKnownDeckOffers, rewindPrivateBurns;
    private readonly HashSet<int> retainedOverdraws = new();
    internal void Receive(int id, IProtoBuf packet)
    {
        using var stream = new MemoryStream();
        packet.Serialize(stream);
        if (stream.Length != packet.GetSerializedSize()) throw new InvalidOperationException("Native packet size mismatch");
        Packets.Add(new(id, Convert.ToBase64String(stream.ToArray())));
        if (packet is EntitiesChosen)
        {
            stream.Position = 0;
            var received = new EntitiesChosen();
            received.Deserialize(stream);
            Client.Apply(received);
        }
        if (packet is PegasusGame.PowerHistory)
        {
            retainedOverdraws.UnionWith(match.Game.PowerHistory.Last.OfType<PowerHistoryRetainedOverdraw>().Select(burn => burn.Entity.Id));
            stream.Position = 0;
            var received = new PegasusGame.PowerHistory();
            received.Deserialize(stream);
            if (stream.Position != stream.Length) throw new InvalidOperationException("Native history did not consume its wire bytes");
            foreach (var entry in received.List) if (entry.HasChangeEntity) Changed.Add((entry.ChangeEntity.Entity, entry.ChangeEntity.Name));
            Client.Apply(received);
            var boardFaults = BoardActorInvariant.Inspect(Client).ToArray();
            SelfChecks.Check(boardFaults.Length == 0, "packet board actors: " + string.Join("; ", boardFaults));
            if (Client.HasRewound)
            {
                var violations = Invariants.Compare(match.Game, Client).ToArray();
                SelfChecks.Check(violations.Length == 0, "post-reset history oracle: " + string.Join("; ", violations.Select(v => v.ToString())));
            }
            // The pinned decoder assigns EffectIndex=0, setting HasEffectIndex even when field 6 was absent.
            // The outgoing protobuf retains the actual wire presence bits; Client still gets the round-trip.
            History.Observe((PegasusGame.PowerHistory)packet);
        }
    }
    internal void Flush() => Invoke("SendHistory", match.Game.PowerHistory.Last);
    // Retail shows the other player's GENERAL prompt as card backs: every non-mulligan EntityChoices is the chooser's
    // GENERAL prompt and lists only entities this client holds nameless.
    internal bool OpposingPromptsHidden(int chooser) => Packets.Where(p => p.Id == 17).Select(p =>
    {
        var choice = new EntityChoices();
        using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
        choice.Deserialize(stream);
        return choice;
    }).Where(c => c.ChoiceType != (int)ChoiceType.MULLIGAN).All(c => c.PlayerId == chooser && c.ChoiceType == (int)ChoiceType.GENERAL
        && c.Entities.All(id => Client.Entities.TryGetValue(id, out var shown) && shown.CardId == ""));
    internal void PublishDecision(Controller actor)
    {
        pendingChoice = null;
        if (actor == match.Game.Player1)
        {
            Invoke(actor.Choice != null ? "SendHumanChoice" : "SendHumanOptions");
            pendingChoice = (PowerEntityChoices)typeof(NativeMatch).GetField("humanChoices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
        }
        else if (actor.Choice?.ChoiceType == ChoiceType.MULLIGAN || actor.Choice?.ChoiceAction == SabberStoneCore.Model.ChoiceAction.REWIND)
        {
            // Start sends the bot mulligan and acknowledgment; other bot choices stay internal.
            pendingChoice = PowerChoicesBuilder.EntityChoices(match.Game, actor.Choice);
            var packet = (IProtoBuf)typeof(NativeMatch).GetMethod("ToNativeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { pendingChoice });
            Receive(17, packet);
        }
    }
    internal void Process(PlayerTask task)
    {
        if ((task is TradeCardTask || task is PrepareCardTask) && task.Controller == match.Game.Player1)
        {
            var options = (PowerAllOptions)typeof(NativeMatch).GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            int index = options.PowerOptionList.FindIndex(o => o.MainOption?.EntityId == task.Source.Id
                && o.MainOption.PowerKeyword == (task is PrepareCardTask ? (int)GameTag.PREPARE : (int)GameTag.TRADEABLE));
            var resolved = (PlayerTask)typeof(NativeMatch).GetMethod("ResolveTask", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { options, index, 0, -1, 0 });
            if (resolved.PlayerTaskType != task.PlayerTaskType || resolved.Source != task.Source)
                throw new InvalidOperationException("Native Trade input did not resolve the legal source");
            task = resolved;
        }
        if (task.Controller == match.Game.Player1 && (task is ActivateLocationTask
            || task is PlayCardTask && (task.Source is BoardObject || task.ActionTarget is BoardObject)))
        {
            var options = (PowerAllOptions)typeof(NativeMatch).GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            int index = options.PowerOptionList.FindIndex(o => o.MainOption?.EntityId == task.Source.Id && o.MainOption.PowerKeyword == 0);
            var resolved = (PlayerTask)typeof(NativeMatch).GetMethod("ResolveTask", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { options, index, task.ActionTarget?.Id ?? 0, task.ChooseOne <= 0 ? -1 : task.ChooseOne - 1,
                    task is PlayCardTask play ? play.ZonePosition + 1 : 0 });
            if (resolved.PlayerTaskType != task.PlayerTaskType || resolved.Source.Id != task.Source.Id
                || resolved.ActionTarget?.Id != task.ActionTarget?.Id
                || resolved is PlayCardTask placed && placed.ZonePosition != ((PlayCardTask)task).ZonePosition)
                throw new InvalidOperationException("Native location input did not resolve the exact legal task");
            task = resolved;
        }
        if (SabberStoneCore.Model.Rewind.RewindRules.WouldBegin(match.Game, task))
        {
            // Freeze pre-attempt knowledge, independent of the reset packet under test.
            rewindPrivateBurns = retainedOverdraws.Where(id => !Client.Entities.TryGetValue(Client.WireId(id), out var shown)
                || shown.CardId.Length == 0).ToHashSet();
            rewindKnownDeckOffers = match.Game.Player1.DeckZone.Where(entity => Client.Entities.TryGetValue(Client.WireId(entity.Id), out var shown) && shown.CardId.Length > 0)
                .Select(entity => entity.Id).ToHashSet();
        }
        if (task is ChooseTask rewindAnswer && match.Game.Rewind is { KeepBoth: false } transaction &&
            rewindAnswer.Choices.Contains(transaction.RewindEntityId))
        {
            var expected = SabberStoneCore.Model.Game.LoadSnapshotWithPools(transaction.Baseline, match.Game.OfflinePools);
            var verify = Client.ResetApplied;
            Client.ResetApplied = model =>
            {
                var violations = Invariants.CompareReset(expected, model, rewindKnownDeckOffers, rewindPrivateBurns).ToArray();
                SelfChecks.Check(violations.Length == 0, "reset oracle: " + string.Join("; ", violations.Select(v => v.ToString())));
                verify?.Invoke(model);
            };
        }
        Invoke("Process", task);
        if (pendingChoice != null && task is ChooseTask choice) Invoke("SendChosen", pendingChoice, choice.Choices);
        pendingChoice = null;
        if (match.Game.Step == SabberStoneCore.Enums.Step.BEGIN_MULLIGAN
            && match.Game.Player1.MulliganState == SabberStoneCore.Enums.Mulligan.DONE
            && match.Game.Player2.MulliganState == SabberStoneCore.Enums.Mulligan.DONE) match.Game.MainBegin();
        Flush();
        Client.ResetApplied = null;
    }
    internal void Invoke(string name, params object[] args)
    {
        try { typeof(NativeMatch).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
}

internal sealed record WirePacket(int Id, string Base64);
