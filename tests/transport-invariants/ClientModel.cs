using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using EngineZone = SabberStoneCore.Enums.Zone;

namespace TransportInvariants;

internal sealed class ClientEntity
{
    internal string CardId = "";
    // Card id the native actor was loaded for: ZoneMgr binds it on a zone/controller move (or a FULL creating the
    // entity); ZoneChangeList updates actors on SHOW; CHANGE_ENTITY rebinds only when CARDTYPE or PREMIUM changes.
    internal string ActorCardId = "";
    internal bool HiddenByNullZone;
    internal int LastActorZone;
    internal int DeckEntries, DeckSummons, VisibilityRepairs;
    internal readonly Dictionary<int, int> Tags = new();
    internal int Get(GameTag tag) => Tags.GetValueOrDefault((int)tag);
    internal int CardZone, CardController, CardPosition;
}

internal sealed class ClientModel
{
    // Packet state stays in wire IDs; only engine-oracle joins cross this identity boundary.
    internal Func<int, int> WireId = id => id;
    internal Func<int, int> EngineId = id => id;

    // RewindGameSpellController.GetPriorityTagsList, pinned client 37.0.0.254477.
    private readonly HashSet<int> rewindPriorityTags = new() {
        49, // ZONE,
        263, // ZONE_POSITION,
        44, // DAMAGE,
        45, // HEALTH,
        47, // ATK,
        188, // SILENCED,
        240, // IMMUNE,
        194, // DIVINE_SHIELD,
        190, // TAUNT,
        1085, // REBORN,
        217, // DEATHRATTLE,
        685, // LIFESTEAL,
        32, // TRIGGER_VISUAL,
        363, // POISONOUS,
        218, // BATTLECRY,
        192, // SPELLPOWER,
        791, // RUSH,
        189, // WINDFURY,
        1211, // ELUSIVE,
        332, // CANT_BE_TARGETED_BY_HERO_POWERS,
        114, // ELITE,
        191, // STEALTH,
        291, // CURRENT_SPELLPOWER_BASE,
        1945, // SPELLPOWER_ARCANE,
        1946, // SPELLPOWER_FIRE,
        1947, // SPELLPOWER_FROST,
        1948, // SPELLPOWER_NATURE,
        1949, // SPELLPOWER_HOLY,
        1950, // SPELLPOWER_SHADOW,
        1951, // SPELLPOWER_FEL,
        1952, // SPELLPOWER_PHYSICAL,
        386, // POWERED_UP,
        433, // IGNORE_TAUNT
    };
    internal ClientModel(IEnumerable<int> tagVisuals = null)
    {
        // shortcut: visual assets are unavailable headlessly; supply captured m_TagVisuals for native qualification.
        if (tagVisuals != null) rewindPriorityTags.UnionWith(tagVisuals);
    }
    internal bool RepairNullToPlayActors = true;
    internal Action<ClientModel> EntryApplied;
    internal Action<ClientModel> ResetApplied;
    internal bool RemoveTombstonedEntities;
    internal bool HasRewound { get; private set; }
    internal bool InputEnabled { get; private set; } = true;
    internal int InputRestoreCount { get; private set; }
    private int? clickedRewindChoice;
    internal void ClickRewindChoice(int choiceId, int entityId)
    {
        if (!InputEnabled || !Entities.TryGetValue(entityId, out var entity) ||
            entity.CardId != "TIME_000ta" && entity.CardId != "TIME_000tb")
            throw new InvalidOperationException("Rewind click requires enabled input and an offered timeline");
        // RewindUIManager.HandleClickedOnRewindChoice; Keep never disables input.
        if (entity.CardId == "TIME_000tb")
        {
            clickedRewindChoice = choiceId;
            InputEnabled = false;
        }
    }
    internal void Apply(EntitiesChosen packet)
    {
        if (packet.PlayerId != 1 || packet.ChoiceType != (int)ChoiceType.GENERAL ||
            clickedRewindChoice != packet.ChooseEntities.Id) return;
        // HideChoices: only an executing Rewind acknowledged as Keep restarts input.
        if (packet.ChooseEntities.Entities.Count > 0 &&
            Entities.TryGetValue(packet.ChooseEntities.Entities[0], out var entity) && entity.CardId == "TIME_000ta")
            StartWatchingForInput();
        clickedRewindChoice = null;
    }
    private void StartWatchingForInput()
    {
        InputEnabled = true;
        InputRestoreCount++;
    }
    internal readonly Dictionary<int, ClientEntity> Entities = new();
    // shortcut: ordered server moves only; add prediction/auto-correction before using as a general board oracle.
    // Zone.CardSortComparison sorts actor positions first, then entity positions.
    internal int[] BoardOrder(int owner) => Entities.Where(p => p.Value.CardZone == (int)EngineZone.PLAY &&
            p.Value.CardController == owner && p.Value.Get(GameTag.CARDTYPE) is 4 or 39)
        .OrderBy(p => p.Value.CardPosition).ThenBy(p => p.Value.Get(GameTag.ZONE_POSITION)).Select(p => p.Key).ToArray();
    internal readonly Dictionary<string, int> EntryCounts = new();
    // A nameless public move followed by the entity's named snapshot later in the same list (entry-order defect).
    internal readonly List<(int Entity, string Card, int From, int To)> LateIdentities = new();
    // ZoneMgr HAND -> PLAY prediction: zero for spells, one-based position for minions/locations.
    // CheckAndIgnoreServerChangeList only acknowledges PLAY/DECK_ACTION, not nested POWER or GAME_RESET.
    private int localSpell;
    private int localPosition;
    private readonly Stack<(int Type, List<(int Entity, int? Zone, int? Position)> Changes)> localBlocks = new();
    internal bool HasUnresolvedLocalChange => localSpell != 0;
    // ZoneMgr.CheckAndIgnoreServerChangeList suppresses every card zone change in the matched PLAY list.
    internal readonly HashSet<int> IgnoredCardZoneChanges = new();
    internal void PredictLocalSpellPlay(int id) => PredictLocalPlay(id, 0);
    internal void PredictLocalPlay(int id, int position)
    {
        if (localSpell != 0 || Entities[id].Get(GameTag.ZONE) != (int)EngineZone.HAND)
            throw new InvalidOperationException("local spell prediction requires an idle hand card");
        localSpell = id;
        localPosition = position;
    }
    private void ObserveLocalChange(PowerHistoryData entry)
    {
        void Match()
        {
            if (localSpell == 0 || localBlocks.Count == 0) return;
            var (type, changes) = localBlocks.Peek();
            if (type != (int)BlockType.PLAY && type != (int)BlockType.DECK_ACTION) return;
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].Entity != localSpell || changes[i].Zone != (int)EngineZone.PLAY) continue;
                // ZoneMgr.FindNextDstPosChange stops at the next zone change or another entity's position.
                int position = Entities[localSpell].Get(GameTag.ZONE_POSITION);
                for (int j = i; j < changes.Count; j++)
                {
                    if (j != i && changes[j].Zone.HasValue) break;
                    if (!changes[j].Position.HasValue) continue;
                    if (changes[j].Entity == localSpell) position = changes[j].Position.Value;
                    break;
                }
                if (position == localPosition)
                {
                    IgnoredCardZoneChanges.UnionWith(changes.Where(c => c.Zone.HasValue).Select(c => c.Entity));
                    localSpell = 0;
                    return;
                }
            }
            changes.Clear();
        }
        if (entry.HasPowerStart)
        {
            Match();
            localBlocks.Push(((int)entry.PowerStart.Type, new()));
        }
        else if (entry.HasPowerEnd)
        {
            Match();
            if (localBlocks.Count > 0) localBlocks.Pop();
        }
        else if (localBlocks.Count > 0)
        {
            var changes = localBlocks.Peek().Changes;
            if (entry.HasTagChange)
            {
                var tag = entry.TagChange;
                changes.Add((tag.Entity, tag.Tag == (int)GameTag.ZONE ? tag.Value : null,
                    tag.Tag == (int)GameTag.ZONE_POSITION ? tag.Value : null));
            }
            else if (entry.HasFullEntity || entry.HasShowEntity || entry.HasChangeEntity)
            {
                var full = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.ChangeEntity;
                int? Tag(GameTag tag) => full.Tags.FirstOrDefault(t => t.Name == (int)tag)?.Value;
                changes.Add((full.Entity, Tag(GameTag.ZONE), Tag(GameTag.ZONE_POSITION)));
            }
        }
    }
    internal ClientModel Copy()
    {
        var copy = new ClientModel(rewindPriorityTags) { WireId = WireId, EngineId = EngineId, HasRewound = HasRewound,
            InputEnabled = InputEnabled, InputRestoreCount = InputRestoreCount, clickedRewindChoice = clickedRewindChoice,
            RemoveTombstonedEntities = RemoveTombstonedEntities, RepairNullToPlayActors = RepairNullToPlayActors };
        foreach (var pair in Entities)
        {
            var entity = new ClientEntity { DeckEntries = pair.Value.DeckEntries, DeckSummons = pair.Value.DeckSummons, VisibilityRepairs = pair.Value.VisibilityRepairs, CardId = pair.Value.CardId, ActorCardId = pair.Value.ActorCardId, HiddenByNullZone = pair.Value.HiddenByNullZone, LastActorZone = pair.Value.LastActorZone, CardZone = pair.Value.CardZone,
                CardController = pair.Value.CardController, CardPosition = pair.Value.CardPosition };
            foreach (var tag in pair.Value.Tags) entity.Tags.Add(tag.Key, tag.Value);
            copy.Entities.Add(pair.Key, entity);
        }
        foreach (var pair in EntryCounts) copy.EntryCounts.Add(pair.Key, pair.Value);
        copy.IgnoredCardZoneChanges.UnionWith(IgnoredCardZoneChanges);
        copy.LateIdentities.AddRange(LateIdentities);
        return copy;
    }
    private ClientEntity Entity(int id)
    {
        if (!Entities.TryGetValue(id, out var value)) Entities.Add(id, value = new());
        return value;
    }

    private void Merge(int id, string card, IEnumerable<Tag> tags, bool full = false, bool change = false)
    {
        bool created = !Entities.ContainsKey(id);
        var entity = Entity(id);
        int type = entity.Get(GameTag.CARDTYPE), premium = entity.Get(GameTag.PREMIUM);
        if (card != null) entity.CardId = card;
        foreach (var tag in tags) entity.Tags[tag.Name] = tag.Value;
        bool moved = MoveCard(entity, entity.Get(GameTag.ZONE), entity.Get(GameTag.CONTROLLER), entity.Get(GameTag.ZONE_POSITION));
        if (moved || full && created || !full && !change || change && (type != entity.Get(GameTag.CARDTYPE) || premium != entity.Get(GameTag.PREMIUM)))
            entity.ActorCardId = entity.CardId;
    }

    private bool MoveCard(ClientEntity entity, int? zone = null, int? owner = null, int? position = null)
    {
        // ZoneChangeList.ProcessChanges resets Card position on a zone/controller transition;
        // a pure zero position does not move it. Rewind's direct SetTag calls never move Card.
        int previousZone = entity.CardZone;
        bool moved = zone.HasValue && zone != entity.CardZone || owner.HasValue && owner != entity.CardController;
        if (zone.HasValue) entity.CardZone = zone.Value;
        if (owner.HasValue) entity.CardController = owner.Value;
        if (moved || position > 0) entity.CardPosition = position ?? 0;
        // Card.TransitionToZone(null) hides the existing actor. A new PLAY actor from null only
        // spawns when the old actor was absent or already the same PLAY actor; otherwise the native
        // OnActorChanged fallback preserves m_shown=false (even with a correctly named entity).
        if (moved)
        {
            if (entity.CardZone == (int)EngineZone.DECK) entity.DeckEntries++;
            if (previousZone == (int)EngineZone.DECK && entity.CardZone == (int)EngineZone.PLAY) entity.DeckSummons++;
            if (entity.CardZone == (int)EngineZone.SETASIDE)
                entity.HiddenByNullZone = entity.LastActorZone != 0;
            else
            {
                entity.HiddenByNullZone = entity.CardZone == (int)EngineZone.PLAY && entity.HiddenByNullZone
                    && entity.LastActorZone != (int)EngineZone.PLAY;
                // ClientBridge prefix retires only a hidden, different old actor and uses native null->PLAY spawn.
                if (RepairNullToPlayActors && previousZone == (int)EngineZone.SETASIDE && entity.HiddenByNullZone
                    && entity.Get(GameTag.CARDTYPE) is 4 or 39 && entity.CardId.Length > 0)
                {
                    entity.HiddenByNullZone = false;
                    entity.VisibilityRepairs++;
                }
                entity.LastActorZone = entity.CardZone;
                entity.ActorCardId = entity.CardId;
            }
        }
        return moved;
    }

    private static bool PublicZone(int zone, int controller) => zone is (int)EngineZone.PLAY or (int)EngineZone.GRAVEYARD
        || controller == 1 && zone is (int)EngineZone.HAND or (int)EngineZone.SECRET;

    private void ObserveLateIdentity(PegasusGame.PowerHistory packet, int index, ClientEntity entity, int id, int from)
    {
        if (entity.CardId.Length > 0 || !PublicZone(entity.CardZone, entity.CardController)) return;
        for (int ahead = index + 1; ahead < packet.List.Count; ahead++)
        {
            var next = packet.List[ahead];
            if (next.HasHideEntity && next.HideEntity.Entity == id) return;
            var snapshot = next.HasFullEntity ? next.FullEntity : next.HasShowEntity ? next.ShowEntity : next.HasChangeEntity ? next.ChangeEntity : null;
            if (snapshot == null || snapshot.Entity != id) continue;
            if (snapshot.Name.Length > 0) LateIdentities.Add((id, snapshot.Name, from, entity.CardZone));
            return;
        }
    }

    internal void Apply(PegasusGame.PowerHistory packet)
    {
        int resetEnd = -1;
        HashSet<int> resetExisting = new();
        var generated = new List<PowerHistoryData>();
        var queuedRemovals = new HashSet<int>();
        for (int index = 0; index < packet.List.Count; index++)
        {
            var entry = packet.List[index];
            ObserveLocalChange(entry);
            if (entry.HasPowerStart && (int)entry.PowerStart.Type == (int)BlockType.GAME_RESET &&
                index + 1 < packet.List.Count && packet.List[index + 1].HasResetGame && packet.List[index + 1].ResetGame.SourceIsRewind)
            {
                foreach (int id in queuedRemovals) Entities.Remove(id);
                queuedRemovals.Clear();
                // Audit tombstones may remain in this model; native GameState removed them before the reset FULLs.
                foreach (int id in Entities.Where(pair => pair.Value.CardZone == (int)EngineZone.REMOVEDFROMGAME).Select(pair => pair.Key).ToArray())
                    Entities.Remove(id);
                HasRewound = true;
                InputEnabled = false;
                resetEnd = packet.List.FindIndex(index + 2, e => e.HasPowerEnd);
                if (resetEnd < 0) throw new InvalidOperationException("Rewind reset block must not span packets");
                var fulls = packet.List.Skip(index + 2).Take(resetEnd - index - 2).Where(e => e.HasFullEntity)
                    .Select(e => e.FullEntity).ToDictionary(e => e.Entity);
                int max = fulls.Keys.DefaultIfEmpty(0).Max();
                foreach (int id in Entities.Keys.Where(id => !fulls.ContainsKey(id) && id > max).ToArray()) Entities.Remove(id);
                resetExisting = fulls.Keys.Where(Entities.ContainsKey).ToHashSet();
                generated.Clear();
                foreach (int id in resetExisting)
                {
                    var full = fulls[id];
                    NativeMatch.ValidateHistoryEntityIdentity(full);
                    var current = Entities[id];
                    var tags = full.Tags.ToDictionary(t => t.Name, t => t.Value);
                    bool Differs(int tag) => tags.ContainsKey(tag) != current.Tags.ContainsKey(tag) ||
                        tags.GetValueOrDefault(tag) != current.Tags.GetValueOrDefault(tag);
                    var changed = rewindPriorityTags.Where(Differs).ToArray();
                    bool transform = Differs(2321); // TIMES_BEEN_TRANSFORMED
                    if (transform && full.Name != "TIME_038") generated.Add(new() { ChangeEntity = full });
                    foreach (int tag in changed) generated.Add(new() { TagChange = new() {
                        Entity = id, Tag = tag, Value = tags.GetValueOrDefault(tag) } });
                    if (transform || changed.Length != 0)
                        foreach (var tag in new[] { GameTag.ZONE, GameTag.ZONE_POSITION, GameTag.CONTROLLER })
                            if (tags.TryGetValue((int)tag, out int value)) current.Tags[(int)tag] = value;
                }
            }
            string kind;
            if (entry.HasCreateGame)
            {
                kind = "CREATE_GAME";
                var create = entry.CreateGame;
                Merge(create.GameEntity.Id, null, create.GameEntity.Tags);
                foreach (var player in create.Players) Merge(player.Entity.Id, null, player.Entity.Tags);
            }
            else if (entry.HasResetGame)
            {
                kind = "RESET_GAME";
                if (!entry.ResetGame.SourceIsRewind)
                {
                    var create = entry.ResetGame.CreateGame;
                    Merge(create.GameEntity.Id, null, create.GameEntity.Tags);
                    foreach (var player in create.Players) Merge(player.Entity.Id, null, player.Entity.Tags);
                }
            }
            else if (entry.HasFullEntity || entry.HasShowEntity || entry.HasChangeEntity)
            {
                kind = entry.HasFullEntity ? "FULL_ENTITY" : entry.HasShowEntity ? "SHOW_ENTITY" : "CHANGE_ENTITY";
                var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.ChangeEntity;
                NativeMatch.ValidateHistoryEntityIdentity(snapshot);
                // Existing reset FULLs are removed; transforms/priority updates run after original tasks.
                bool filtered = index < resetEnd && (entry.HasFullEntity && resetExisting.Contains(snapshot.Entity) ||
                    entry.HasChangeEntity && snapshot.Name == "TIME_038");
                if (!filtered)
                {
                    // GameState.OnRealTimeFullEntity/AddEntity registers by ENTITY_ID, not the packet's outer ID.
                    // A malformed FULL therefore occupies ID 0; subsequent tasks addressed to its outer ID miss.
                    int registered = entry.HasFullEntity
                        ? snapshot.Tags.FirstOrDefault(t => t.Name == (int)GameTag.ENTITY_ID)?.Value ?? 0 : snapshot.Entity;
                    if (entry.HasFullEntity && !Entities.ContainsKey(registered))
                        Merge(registered, snapshot.Name, snapshot.Tags, full: true);
                    else if (Entities.ContainsKey(snapshot.Entity))
                        Merge(snapshot.Entity, snapshot.Name, snapshot.Tags, entry.HasFullEntity, entry.HasChangeEntity);
                }
            }
            else if (entry.HasTagChange)
            {
                kind = "TAG_CHANGE";
                var tag = entry.TagChange;
                if (!Entities.ContainsKey(tag.Entity)) continue;
                Entity(tag.Entity).Tags[tag.Tag] = tag.Value;
                int from = Entity(tag.Entity).CardZone;
                if (MoveCard(Entity(tag.Entity), tag.Tag == (int)GameTag.ZONE ? tag.Value : null,
                    tag.Tag == (int)GameTag.CONTROLLER ? tag.Value : null,
                    tag.Tag == (int)GameTag.ZONE_POSITION ? tag.Value : null) && index > resetEnd)
                    ObserveLateIdentity(packet, index, Entity(tag.Entity), tag.Entity, from);
                if (RemoveTombstonedEntities && tag.Tag == (int)GameTag.ZONE && tag.Value == (int)EngineZone.REMOVEDFROMGAME)
                    queuedRemovals.Add(tag.Entity);
            }
            else if (entry.HasHideEntity)
            {
                kind = "HIDE_ENTITY";
                var hide = entry.HideEntity;
                if (!Entities.ContainsKey(hide.Entity)) continue;
                var entity = Entity(hide.Entity);
                // Entity.OnHideEntity resets printed stats before clearing its card ID; other tags survive.
                foreach (var tag in new[] { GameTag.ATK, GameTag.HEALTH, GameTag.COST })
                {
                    NativeUIProjection.TryGetAuditDefinitionTag(true, entity.CardId, (int)tag, out int printed);
                    entity.Tags[(int)tag] = printed;
                }
                entity.Tags[(int)GameTag.ZONE] = hide.Zone;
                entity.CardId = "";
                MoveCard(entity, hide.Zone);
                entity.Tags[(int)GameTag.DAMAGE] = 0;
            }
            else if (entry.HasMetaData) kind = "META_DATA"; // Visual/history annotations; actual stat changes arrive as tags.
            else if (entry.HasPowerStart) kind = "POWER_START";
            else if (entry.HasPowerEnd) kind = "POWER_END";
            else if (entry.HasSubSpellStart) kind = "SUB_SPELL_START";
            else if (entry.HasSubSpellEnd) kind = "SUB_SPELL_END";
            else if (entry.HasShuffleDeck) kind = "SHUFFLE_DECK";
            else throw new NotSupportedException("Unknown native history entry");
            EntryCounts[kind] = EntryCounts.GetValueOrDefault(kind) + 1;
            EntryApplied?.Invoke(this);
            if (index == resetEnd)
            {
                Apply(new PegasusGame.PowerHistory { List = generated.ToList() });
                ResetApplied?.Invoke(this);
                // RewindGameSpellController/<DoEffectsWithTiming>.MoveNext, after reset tasks complete.
                StartWatchingForInput();
            }
        }
        foreach (int id in queuedRemovals) Entities.Remove(id);
    }
}
