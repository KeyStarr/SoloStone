using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // #118 (Ido's Call of the Threshfleet), #82/#116 (Tyrax), Twinspell twins. Live, with a REAL mouse drag only: the card created
    // into the hand while the dragged card is played never gets an actor, and the queue can stay busy. A semantic play makes no
    // local prediction, so only a model of the drag shows it. The model is what the client does (from its IL):
    //   InputManager.DropHeldMinionLikeCard / DropHeldSpellLikeCard: the dropped card moves HAND -> PLAY locally
    //     (ZoneMgr.AddPredictedLocalZoneChange / AddLocalZoneChange);
    //   ZoneMgr.CheckAndIgnoreServerChangeList: a PLAY block's server task list that moves that card to PLAY (same destination zone,
    //     predicted position equal to the next ZONE_POSITION or 0) is paired with the prediction, and SetIgnoreCardZoneChanges(true)
    //     makes ZoneChangeList.ShouldIgnoreZoneChange true for EVERY card in that list: the entity gets its tags, never a zone move;
    //   a task list is a block's own rows between its nested blocks and sub-spells (PowerProcessor starts a new one at each, and for
    //     the rows after a nested block ends), so the list that holds the card's own move can come after a nested block.
    private sealed class DragClient
    {
        internal readonly Dictionary<int, int> EntityZone = new(), CardZone = new(), Controller = new();
        internal int IgnoredLists;
        private readonly HashSet<int> stale = new(); // revealed in place inside an ignored list: the actor refresh was skipped
        private readonly int played, oldHero;
        private readonly bool predicted;
        private readonly Dictionary<int, int> cardTypes = new(), powerSlots = new();
        internal DragClient(SabberStoneCore.Model.Entities.Controller owner, int played, bool predicted = true)
        {
            this.played = played;
            this.predicted = predicted;
            oldHero = owner.Game.IdEntityDic[played].Card.Type == CardType.HERO ? owner.Hero.Id : 0;
            foreach (var card in owner.HeroPowers.Cast<IPlayable>().Append(owner.Hero))
            {
                EntityZone[card.Id] = CardZone[card.Id] = (int)EngineZone.PLAY;
                Controller[card.Id] = owner.PlayerId;
                cardTypes[card.Id] = (int)card.Card.Type;
                powerSlots[card.Id] = card[BasicHeroPowerRules.SecondaryIndex];
            }
            foreach (var card in owner.HandZone) { EntityZone[card.Id] = CardZone[card.Id] = (int)EngineZone.HAND; Controller[card.Id] = owner.PlayerId; }
            if (predicted) CardZone[played] = (int)EngineZone.PLAY; // the local prediction at the drop
        }

        internal void Apply(IEnumerable<PowerHistory> packets)
        {
            foreach (var packet in packets) Apply(packet.List);
        }

        private readonly Stack<bool> blocks = new(); // per open block: is it the dragged card's own PLAY block

        private void Apply(List<PowerHistoryData> rows)
        {
            // Task lists: a block's own rows between its nested blocks and sub-spells (PowerProcessor starts a new list at each,
            // and again for the rows after a nested block ends, still under the same block), and the packet's end.
            var lists = new List<(bool OwnPlay, List<PowerHistoryData> Rows)>();
            List<PowerHistoryData> open = null;
            foreach (var row in rows)
            {
                if (row.HasPowerStart)
                {
                    open = null;
                    blocks.Push((int)row.PowerStart.Type == (int)BlockType.PLAY && row.PowerStart.Source == played);
                    continue;
                }
                if (row.HasPowerEnd) { open = null; if (blocks.Count > 0) blocks.Pop(); continue; }
                if (row.HasSubSpellStart || row.HasSubSpellEnd) { open = null; continue; }
                if (open == null) { open = new List<PowerHistoryData>(); lists.Add((blocks.Count > 0 && blocks.Peek(), open)); }
                open.Add(row);
                if (row.HasMetaData && (int)row.MetaData.Type == 21) open = null;
            }
            foreach (var (isPlay, list) in lists)
            {
                bool movesPlayedToPlay = predicted && isPlay && list.Any(r => r.HasTagChange && r.TagChange.Entity == played && r.TagChange.Tag == (int)GameTag.ZONE
                        && r.TagChange.Value == (int)EngineZone.PLAY
                    || (r.HasShowEntity || r.HasChangeEntity) && Snapshot(r).Entity == played
                        && Snapshot(r).Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)EngineZone.PLAY));
                if (movesPlayedToPlay) IgnoredLists++;
                foreach (var row in list) Row(row, ignoreCardZones: movesPlayedToPlay);
            }
        }

        private static PegasusGame.PowerHistoryEntity Snapshot(PowerHistoryData row)
            => row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.ChangeEntity;

        private void Move(int id, int zone, bool ignore)
        {
            EntityZone[id] = zone;
            if (!ignore || id == oldHero) { CardZone[id] = zone; stale.Remove(id); } // The old hero is exempt from the predicted list; other cards need a processed list (which loads a fresh actor).
        }

        private void Row(PowerHistoryData row, bool ignoreCardZones)
        {
            if (row.HasFullEntity || row.HasShowEntity || row.HasChangeEntity)
            {
                var snapshot = Snapshot(row);
                if (row.HasChangeEntity) stale.Remove(snapshot.Entity); // a transform refreshes its own actor, in any list
                else if (row.HasShowEntity && snapshot.Entity != played)
                {
                    int now = EntityZone.GetValueOrDefault(snapshot.Entity, -1);
                    int to = snapshot.Tags.Where(t => t.Name == (int)GameTag.ZONE).Select(t => t.Value).DefaultIfEmpty(now).Last();
                    if (!ignoreCardZones) stale.Remove(snapshot.Entity);
                    else if (to == now && now is (int)EngineZone.HAND or (int)EngineZone.PLAY or (int)EngineZone.SECRET) stale.Add(snapshot.Entity);
                }
                foreach (var tag in snapshot.Tags)
                {
                    if (tag.Name == (int)GameTag.CARDTYPE) cardTypes[snapshot.Entity] = tag.Value;
                    if (tag.Name == (int)BasicHeroPowerRules.SecondaryIndex) powerSlots[snapshot.Entity] = tag.Value;
                    if (tag.Name == (int)GameTag.ZONE) Move(snapshot.Entity, tag.Value, ignoreCardZones);
                    else if (tag.Name == (int)GameTag.CONTROLLER) Controller[snapshot.Entity] = tag.Value;
                }
            }
            else if (row.HasHideEntity) Move(row.HideEntity.Entity, row.HideEntity.Zone, ignoreCardZones);
            else if (row.HasTagChange && row.TagChange.Tag == (int)GameTag.ZONE) Move(row.TagChange.Entity, row.TagChange.Value, ignoreCardZones);
            else if (row.HasTagChange && row.TagChange.Tag == (int)GameTag.CONTROLLER) Controller[row.TagChange.Entity] = row.TagChange.Value;
            else if (row.HasTagChange && row.TagChange.Tag == (int)BasicHeroPowerRules.SecondaryIndex) powerSlots[row.TagChange.Entity] = row.TagChange.Value;
        }

        internal int[] PowersInSlot(int owner, int slot) => CardZone.Where(p => p.Value == (int)EngineZone.PLAY
            && Controller.GetValueOrDefault(p.Key) == owner && cardTypes.GetValueOrDefault(p.Key) == (int)CardType.HERO_POWER
            && powerSlots.GetValueOrDefault(p.Key) == slot).Select(p => p.Key).Order().ToArray();

        // Cards the client shows in the human's hand (it has an actor there) against the engine's hand.
        internal string Compare(SabberStoneCore.Model.Entities.Controller owner)
        {
            var shown = CardZone.Where(p => p.Value == (int)EngineZone.HAND && Controller.GetValueOrDefault(p.Key) == owner.PlayerId).Select(p => p.Key).ToHashSet();
            var engine = owner.HandZone.Select(c => c.Id).ToHashSet();
            var missing = engine.Except(shown).ToArray(); var ghosts = shown.Except(engine).ToArray();
            var old = shown.Intersect(stale).ToArray();
            return missing.Length == 0 && ghosts.Length == 0 && old.Length == 0 ? null
                : $"hand actor missing for [{string.Join(",", missing.Select(id => id + ":" + owner.Game.IdEntityDic[id].Card.Id))}], extra [{string.Join(",", ghosts)}]"
                    + (old.Length > 0 ? $", hand actor stale for [{string.Join(",", old)}]" : "");
        }
    }

    private static void DragPlay(string label, string cardId, int hand, int index,
        Action<NativeMatch, SabberStoneCore.Model.Entities.Controller> setup, bool expectCreation,
        Action<SabberStoneCore.Model.Entities.Controller> verify = null, Action<List<PowerHistoryData>> rows = null)
    {
        var (match, capture) = AnimationMatch();
        var game = match.Game; var owner = game.CurrentPlayer;
        setup?.Invoke(match, owner);
        while (owner.HandZone.Count > 0) owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
        // The card sits at `index` of `hand` cards (the client sorts the hand by ZONE_POSITION, left to right).
        IPlayable source = null;
        for (int slot = 0; slot < hand; slot++)
        {
            var card = EngineEntity.FromCard(owner, Cards.FromId(slot == index ? cardId : "CS2_182"), zone: owner.HandZone);
            if (slot == index) source = (IPlayable)card;
        }
        owner.BaseMana = 10; owner.UsedMana = 0;
        FlushFieldHistory(match);
        var drag = new DragClient(owner, source.Id);
        int before = owner.HandZone.Count, cursor = capture.Packets.Count;
        var task = owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
        InvokeProcess(match, task);
        FlushFieldHistory(match);
        for (int decisions = 0; owner.Choice != null && decisions < 10; decisions++)
        {
            InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices[0]));
            FlushFieldHistory(match);
        }
        Check(!owner.HandZone.Contains(source), label + ": the engine played the card");
        // The played card left the hand; a card created into it makes the hand as large as before.
        if (expectCreation) Check(owner.HandZone.Count >= before, label + ": the play puts a card into the hand (hand " + owner.HandZone.Count + " of " + before + ")");
        verify?.Invoke(owner);
        rows?.Invoke(History(capture, cursor));
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HS_DRAW_HISTORY_OUTPUT"))) DumpDrawHistory(label.Replace(' ', '-').Replace("'", ""), capture, cursor);
        drag.Apply(capture.Packets.Skip(cursor).Where(p => p.Id == 19).Select(p => (PowerHistory)p.Message));
        Check(drag.IgnoredLists > 0, label + ": the model paired the drop prediction with the PLAY list (else it tests nothing)");
        string problem = drag.Compare(owner);
        if (problem != null) hiddenHandFailures.Add(label + ": " + problem);
        capture.SerializeAll();
    }

    private static readonly List<string> hiddenHandFailures = new();

    private static void HiddenHandCardHistory()
    {
        int cases = 0;
        hiddenHandFailures.Clear();
        // Ido's linked spell is created by the aura update inside the PLAY block. Every hand size, first/middle/last slot.
        foreach (int hand in new[] { 1, 3, 5, 7, 9 })
        foreach (int index in new[] { 0, hand / 2, hand - 1 }.Distinct())
        {
            DragPlay($"Ido hand {hand} slot {index}", "TLC_241", hand, index, null, expectCreation: true);
            cases++;
        }
        // Twinspell: the twin card is created in the PLAY block before the effect (a spell is dropped the same way).
        foreach (int hand in new[] { 1, 4, 7, 9 })
        {
            DragPlay($"Forest's Aid hand {hand}", "DAL_256", hand, 0, null, expectCreation: true);
            cases++;
        }
        // #82: a Corpse-cost minion completes the quest; Tyrax is created while it is paid for, in the quest's own TRIGGER blocks (already a
        // separate task list, so the packet order is not the Ido shape; kept as a control).
        foreach (int hand in new[] { 1, 5, 9 })
        {
            DragPlay($"Pterrordax finishes the quest, hand {hand}", "TLC_436", hand, hand / 2, (match, owner) =>
            {
                owner.BaseMana = 10; owner.UsedMana = 0;
                var quest = EngineEntity.FromCard(owner, Cards.FromId("TLC_433"), zone: owner.HandZone);
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == quest.Id));
                FlushFieldHistory(match);
                CorpseRules.Gain(owner, 15);
                Check(CorpseRules.TrySpend(owner, 10), "ten Corpses spent before the Pterrordax");
                FlushFieldHistory(match);
            }, expectCreation: true, verify: owner => Check(owner.HandZone.Any(c => c.Card.Id == "TLC_433t"), "Tyrax is in the engine hand"));
            cases++;
        }
        // #116: Paleomancy keeps all three Undead offers in hand (with 5 Corpses) from its own nested effect, at the hand limit and below it.
        foreach (int hand in new[] { 2, 6, 7, 9 })
        {
            DragPlay($"Paleomancy keeps all, hand {hand}", "TLC_434", hand, 0, (match, owner) => CorpseRules.Gain(owner, 5), expectCreation: false);
            cases++;
        }
        // A weapon replacing a weapon: the old one leaves in the PLAY list itself, as it does live today (#83); not wrapped.
        DragPlay("Axe over Twig", "CS2_106", 2, 1, (match, owner) =>
        {
            owner.BaseMana = 10; owner.UsedMana = 0;
            var twig = EngineEntity.FromCard(owner, Cards.FromId("LOOT_392"), zone: owner.HandZone);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == twig.Id));
            FlushFieldHistory(match);
        }, expectCreation: false, verify: owner => { }, rows: rows =>
        {
            int play = rows.FindIndex(r => r.HasPowerStart && (int)r.PowerStart.Type == (int)BlockType.PLAY);
            int old = rows.FindIndex(r => r.HasTagChange && r.TagChange.Tag == (int)GameTag.ZONE && r.TagChange.Value == (int)EngineZone.GRAVEYARD);
            Check(play >= 0 && old > play, "the replaced weapon leaves the board");
            int depth = 0;
            for (int at = play; at < old; at++) { if (rows[at].HasPowerStart) depth++; if (rows[at].HasPowerEnd) depth--; }
            Check(depth == 1, "the replaced weapon leaves inside the PLAY block's own list, as it does today (depth " + depth + ")");
        });
        cases++;
        // Controls that already worked: cards created or discovered by their own nested Battlecry, and a plain minion and spell.
        foreach (string id in new[] { "KAR_009", "TRL_348", "ICC_069", "LOE_003", "CS2_182", "CS2_029" })
        foreach (int hand in new[] { 2, 8 })
        {
            DragPlay($"{id} hand {hand}", id, hand, hand - 1, null, expectCreation: false);
            cases++;
        }
        foreach (string failure in hiddenHandFailures) Console.WriteLine("FAIL " + failure);
        Check(hiddenHandFailures.Count == 0, $"{hiddenHandFailures.Count} of {cases} dragged plays leave a hand card without an actor");
        Console.WriteLine($"PASS dragged play keeps every card created into the hand visible (#118, #82, #116): {cases} cases against the client's drop-prediction model");
    }
}
