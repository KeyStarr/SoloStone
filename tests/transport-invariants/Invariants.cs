using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;

namespace TransportInvariants;
using Card = SabberStoneCore.Model.Card;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;
using Character = SabberStoneCore.Model.Entities.Character;
using Minion = SabberStoneCore.Model.Entities.Minion;
using Weapon = SabberStoneCore.Model.Entities.Weapon;

internal sealed record Violation(string Card, int Entity, string Tag, string Expected, string Shown);
internal static class Invariants
{
    // Independent controller-aura inventory; raw storage excludes active aura contributions.
    internal static readonly GameTag[] ControllerAuraTags = { GameTag.TIMEOUT, GameTag.SPELLPOWER_DOUBLE, GameTag.SPELL_HEALING_DOUBLE,
        GameTag.HERO_POWER_DOUBLE, GameTag.HEALING_DOES_DAMAGE, GameTag.CHOOSE_BOTH, GameTag.SPELLS_COST_HEALTH,
        GameTag.EXTRA_BATTLECRIES_BASE, GameTag.EXTRA_END_TURN_EFFECT, GameTag.HERO_POWER_DISABLED,
        GameTag.ALL_HEALING_DOUBLE, GameTag.EXTRA_MINION_BATTLECRIES_BASE, GameTag.SPELLPOWER };
    // Heroes, hero powers and equipped weapons use a ZONE tag without a zone container.
    private static Zone Location(IPlayable entity) => entity.Zone?.Type ?? (Zone)entity[GameTag.ZONE];
    internal static bool Visible(IPlayable entity) => entity is not Enchantment &&
        (Location(entity) == Zone.PLAY || entity.Controller?.PlayerId == 1 && Location(entity) is Zone.HAND or Zone.SECRET);

    internal static IEnumerable<Violation> CompareReset(Game game, ClientModel client, IReadOnlySet<int> knownDeckOffers = null,
        IReadOnlySet<int> privateBurns = null)
    {
        foreach (var violation in Compare(game, client, true).Concat(CompareMana(game, client))) yield return violation;
        int max = game.IdEntityDic.Max(pair => pair.Key);
        foreach (int id in client.Entities.Keys.Where(id => client.EngineId(id) > max))
            yield return new("", id, "RESET_CUTOFF", "absent", "present");
        // Current aliases do not replace the privacy obligation on the retired original actors.
        foreach (var pair in client.Entities.Where(pair => client.WireId(client.EngineId(pair.Key)) != pair.Key))
            if (pair.Value.CardId.Length > 0)
                yield return new(pair.Value.CardId, pair.Key, "RESET_PRIVACY", "hidden retired actor", pair.Value.CardId);
        foreach (var entity in game.IdEntityDic.Values)
        {
            if (!client.Entities.TryGetValue(client.WireId(entity.Id), out var shown))
            {
                yield return new(entity.Card.Id, entity.Id, "RESET_ENTITY", "present", "absent");
                continue;
            }
            // Immolate's definition defaults describe Smoldering cards; only instance tags are live loop state.
            foreach (int tag in new[] { 2505, 2600 })
            {
                int expected = shown.CardId.Length > 0 && entity.NativeTags.TryGetValue((GameTag)tag, out int value) ? value : 0;
                if (shown.Get((GameTag)tag) != expected)
                    yield return new(entity.Card.Id, entity.Id, "RESET_ACTOR_STATE", expected.ToString(), shown.Get((GameTag)tag).ToString());
            }
            if (Visible(entity) && (entity.Card[SabberStoneCore.Actions.HeraldRules.Keyword] > 0 || SabberStoneCore.Actions.HeraldRules.IsSoldier(entity.Card)))
            {
                // Contract: the entity carries its own source-aware resolution (0 = no Soldier class, unpinned branch d).
                // The CONTROLLER tag (the only one the native text builder reads) is checked live in HeraldChecks.CrossClass:
                // a reset snapshot restores entity tags in FULL_ENTITY; AppendHeraldState re-sends the controller tag after the reset packet.
                int expected = (int)SabberStoneCore.Actions.HeraldRules.Class(entity.Controller, entity.Card);
                int value = shown.Get(SabberStoneCore.Actions.HeraldRules.Profile);
                if (value != expected) yield return new(entity.Card.Id, entity.Id, "HERALD_PROFILE", expected.ToString(), value.ToString());
            }
            if (Visible(entity))
                foreach (var tag in new[] { GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                {
                    // Grimlynx text uses restored effective stats, not its printed script defaults.
                    int expected = entity.Card.Id == "BE_850t" && entity is Minion grimlynx
                        ? tag == GameTag.TAG_SCRIPT_DATA_NUM_1 ? grimlynx.Health : grimlynx.AttackDamage
                        : entity[tag];
                    // Entity-reference text (client builders 19/20/23): the viewer is sent the stored entity's id only when it may name it,
                    // and 0 for every other host of that builder family (same rule as NativeMatch.ProjectReference, read from the restored game).
                    if (tag == GameTag.TAG_SCRIPT_DATA_NUM_1 && expected != 0)
                    {
                        var card = entity.Card;
                        if (SabberStoneCore.Actions.PayloadRules.MasksReference(card)) expected = 0;
                        else if (SabberStoneCore.Actions.PayloadRules.ReferencesEntity(card, tag)
                            && (!game.IdEntityDic.TryGetValue(expected, out var referenced)
                                || SabberStoneCore.Actions.PayloadRules.IsStored(referenced) && !SabberStoneCore.Actions.PayloadRules.SharedWith(referenced, 1)))
                            expected = 0;
                    }
                    if (shown.Get(tag) != expected)
                        yield return new(entity.Card.Id, entity.Id, tag.ToString(), expected.ToString(), shown.Get(tag).ToString());
                }
            bool hidden = Location(entity) == Zone.DECK || entity.Controller == game.Player2 &&
                (Location(entity) == Zone.HAND || Location(entity) == Zone.SECRET && entity.Card.IsSecret);
            if (hidden && !(entity.Controller == game.Player1 && knownDeckOffers?.Contains(entity.Id) == true) && shown.CardId != "") yield return new(entity.Card.Id, entity.Id, "RESET_PRIVACY", "hidden", shown.CardId);
        }
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var tag in owner.NativeTags.Keys.Except(ControllerAuraTags))
            {
                // LAST_CARD_DISCARDED is engine-only pet evidence (ae49c26f): NativeMatch never publishes it.
                if (IsPrivateRuleTag(tag) || (int)tag == 4327 || tag == GameTag.LAST_CARD_DISCARDED) continue;
                int value = client.Entities[client.WireId(owner.Id)].Get(tag), expected = tag == GameTag.RESOURCES_USED ? owner.UsedMana + owner.OverloadLocked : owner[tag];
                // Historical card references use public aliases; private retained burns must remain unlinkable.
                if (tag is GameTag.LAST_CARD_DRAWN or GameTag.LAST_CARD_PLAYED)
                    expected = privateBurns?.Contains(expected) == true ? 0 : client.WireId(expected);
                if (value != expected) yield return new("", owner.Id, tag.ToString(), expected.ToString(), value.ToString());
            }
        }
    }

    // Rule storage is private on both incremental packets and rewind snapshots.
    private static bool IsPrivateRuleTag(GameTag tag) =>
        NativeTailRules.IsInternalTag(tag) || ObjectiveRules.IsInternalTag(tag) || DeathContextRules.IsInternalTag(tag)
        || StartOfGameRules.IsInternalTag(tag) || CombatRules.IsInternalTag(tag) || BasicHeroPowerRules.IsInternalTag(tag)
        || QuestRules.IsInternalTag(tag) || CostRules.IsInternalTag(tag) || Generic.IsDecisionInternalTag(tag)
        || LeylineRules.IsInternalTag(tag) || StatGainRules.IsInternalTag(tag) || SwapRules.IsInternalTag(tag)
        || CompanionRules.IsInternalTag(tag) || tag == ColossalRules.RealizedFactor
        || DormantRules.IsPrivateTag(tag) || TemporaryRules.IsPrivateTag(tag) || PlayLockRules.IsPrivateTag(tag)
        || PayloadRules.IsInternalTag(tag) || AuraSpellRules.IsInternalTag(tag) || HealingRules.IsInternalTag(tag)
        || ShatterRules.IsInternalTag(tag) || HandProgressRules.IsInternalTag(tag) || CthunRules.IsInternalTag(tag)
        || OldGodRules.IsInternalTag(tag) || AutonomousTurnRules.IsInternalTag(tag) || DiscoverRealRules.IsInternalTag(tag);

    // The native actor is chosen when the client processes the zone move; a nameless card moved into a public zone
    // stays a Card_Hidden placeholder (bodyless, untargetable) even after a later SHOW names it. GRAVEYARD renders no
    // zone actor and may hold cards retail never shows (destroyed Secrets, rejected deck offers): LATE_IDENTITY still
    // catches a graveyard move whose name arrives later in the same list.
    internal static IEnumerable<Violation> ActorIdentity(Game game, ClientModel client)
    {
        foreach (var pair in client.Entities.OrderBy(p => p.Key))
        {
            // EntityList indexes an array: presentation-only client ids (choice proxies, pets) lie beyond it.
            int engineId = client.EngineId(pair.Key);
            if (engineId <= 0 || !game.IdEntityDic.ContainsKey(engineId) || client.WireId(engineId) != pair.Key) continue;
            var entity = game.IdEntityDic[engineId];
            if (entity is Enchantment) continue;
            var shown = pair.Value;
            int zone = shown.CardZone, controller = shown.CardController;
            bool required = zone == (int)Zone.PLAY
                || controller == 1 && zone is (int)Zone.HAND or (int)Zone.SECRET
                || zone == (int)Zone.SECRET && entity.Card[GameTag.SECRET] != 1;
            if (zone == (int)Zone.PLAY && shown.Get(GameTag.CARDTYPE) is 4 or 39 && shown.HiddenByNullZone)
                yield return new(entity.Card.Id, entity.Id, "HIDDEN_BOARD_ACTOR", "shown", "hidden by SETASIDE transit");
            if (required && shown.ActorCardId.Length == 0)
                yield return new(entity.Card.Id, entity.Id, "ANON_ACTOR", "bound", ((Zone)zone).ToString());
        }
        // Enchantments have no zone actor; their attachment, not their ZONE, decides what the client shows.
        foreach (var late in client.LateIdentities.Where(l => !(l.Entity > 0 && game.IdEntityDic.ContainsKey(client.EngineId(l.Entity))
            && game.IdEntityDic[client.EngineId(l.Entity)] is Enchantment)))
            yield return new(late.Card, late.Entity, "LATE_IDENTITY", "named before " + (Zone)late.To, "after " + (Zone)late.From + "->" + (Zone)late.To);
    }

    internal static IEnumerable<Violation> CompareMana(Game game, ClientModel client)
    {
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            if (!client.Entities.TryGetValue(client.WireId(owner.Id), out var player)) continue;
            // Pinned Player.GetNumAvailableResources: locks are included in RESOURCES_USED.
            int available = Math.Max(0, player.Get(GameTag.RESOURCES) + player.Get(GameTag.TEMP_RESOURCES) - player.Get(GameTag.RESOURCES_USED));
            int expected = Math.Max(0, owner.RemainingMana);
            if (available != expected) yield return new("", owner.Id, "AVAILABLE_MANA", expected.ToString(), available.ToString());
        }
    }

    internal static IEnumerable<Violation> Compare(Game game, ClientModel client, bool reset = false)
    {
        foreach (var entity in client.Entities)
            foreach (int tag in entity.Value.Tags.Keys.Where(tag => IsPrivateRuleTag((GameTag)tag)))
                yield return new(entity.Value.CardId, entity.Key, "PRIVATE_RULE_TAG", "absent", tag.ToString());
        if (reset || client.HasRewound)
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                client.Entities.TryGetValue(client.WireId(owner.Id), out var shown);
                foreach (var tag in ControllerAuraTags)
                {
                    int value = shown?.Get(tag) ?? 0, expected = owner[tag];
                    if (value != expected) yield return new("", owner.Id, tag.ToString(), expected.ToString(), value.ToString());
                }
                int spellpower = shown?.Tags.GetValueOrDefault(291) ?? 0;
                if (spellpower != owner.CurrentSpellPower) yield return new("", owner.Id, "CURRENT_SPELLPOWER_BASE", owner.CurrentSpellPower.ToString(), spellpower.ToString());
            }
        foreach (var violation in CardStateChecks.Compare(game, client)) yield return violation;
        foreach (var entity in game.IdEntityDic.Values.OrderBy(e => e.Id))
        {
            // Independent oracle: publication/cache state must not define the expected hand glow.
            if ((entity.Controller == game.Player1 || Visible(entity)) && entity.Card[(GameTag)1333] == 1
                && client.Entities.TryGetValue(client.WireId(entity.Id), out var outcast))
            {
                int highlight = game.State == State.RUNNING && game.CurrentPlayer == game.Player1
                    && entity.Controller == game.Player1 && Location(entity) == Zone.HAND
                    && (entity.ZonePosition == 0 || entity.ZonePosition == game.Player1.HandZone.Count - 1
                        || entity.Card.Id == "END_005" && SabberStoneCore.Actions.CorpseRules.Available(entity.Controller) >= 4) ? 1 : 0;
                int value = outcast.Get(GameTag.POWERED_UP);
                if (value != highlight)
                    yield return new(entity.Card.Id, entity.Id, "POWERED_UP", highlight.ToString(), value.ToString());
            }
            bool secretBack = Location(entity) == Zone.SECRET && entity.Controller?.PlayerId == 2;
            if (!Visible(entity) && !secretBack) continue;
            if (!client.Entities.TryGetValue(client.WireId(entity.Id), out var shown))
            {
                yield return new(entity.Card.Id, entity.Id, "ENTITY", "visible", "missing");
                continue;
            }
            string identity = entity.Card.Id == "JAIL_EVENT_100t" && entity.Card[GameTag.MODULAR_ENTITY_PART_1] != 0
                ? Cards.FromAssetId(entity.Card[GameTag.MODULAR_ENTITY_PART_1]).Id : entity.Card.Id;
            if (!secretBack && shown.CardId != NativeMatch.TransportCardId(identity))
                yield return new(entity.Card.Id, entity.Id, "CARD_ID", NativeMatch.TransportCardId(identity), shown.CardId);
            var expected = new Dictionary<GameTag, int> {
                [GameTag.ZONE] = (int)Location(entity),
                // Indexed zones expose zero-based positions; native tags are one-based.
                [GameTag.ZONE_POSITION] = entity.Zone != null ? entity.ZonePosition + 1 : entity[GameTag.ZONE_POSITION],
                [GameTag.CONTROLLER] = entity.Controller?.PlayerId ?? 0
            };
            if (!secretBack)
            {
                expected[GameTag.COST] = entity.Cost;
                expected[GameTag.CARD_ALTERNATE_COST] = (int)SabberStoneCore.Actions.CostRules.PaymentKind(entity);
                expected[GameTag.EXHAUSTED] = entity.IsExhausted ? 1 : 0;
                if (entity.Card.HandProgress != null || entity.Card.Id == "JAIL_801t")
                    expected[GameTag.TAG_SCRIPT_DATA_NUM_1] = entity[GameTag.TAG_SCRIPT_DATA_NUM_1];
                if (entity.Card.Shatter != null)
                    foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401 }) expected[tag] = entity[tag];
                if (entity is BoardObject location)
                {
                    expected[GameTag.HEALTH] = location[GameTag.HEALTH];
                    expected[GameTag.DAMAGE] = location[GameTag.DAMAGE];
                    expected[GameTag.LOCATION_ACTION_COOLDOWN] = location[GameTag.LOCATION_ACTION_COOLDOWN];
                    expected[GameTag.CARDTYPE] = 39;
                }
                if (entity is Character character)
                {
                    expected[GameTag.ATK] = character.AttackDamage;
                    expected[GameTag.HEALTH] = character.BaseHealth;
                    expected[GameTag.DAMAGE] = character.Damage;
                    expected[GameTag.ARMOR] = character.Armor;
                    expected[GameTag.TAUNT] = character.HasTaunt ? 1 : 0;
                    expected[GameTag.DIVINE_SHIELD] = character.HasDivineShield ? 1 : 0;
                    expected[GameTag.STEALTH] = character.HasStealth ? 1 : 0;
                    expected[GameTag.FROZEN] = character.IsFrozen ? 1 : 0;
                    expected[GameTag.WINDFURY] = character.HasWindfury ? 1 : 0;
                    expected[GameTag.SILENCED] = character.IsSilenced ? 1 : 0;
                }
                if (entity is HeroPower && entity.Card.Id == SabberStoneCore.Actions.ImbueRules.Blessing(entity.Card.Class))
                {
                    expected[GameTag.TAG_SCRIPT_DATA_NUM_1] = entity[GameTag.TAG_SCRIPT_DATA_NUM_1];
                    if (entity.Card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
                        expected[GameTag.TAG_SCRIPT_DATA_NUM_2] = entity[GameTag.TAG_SCRIPT_DATA_NUM_2];
                }
                if (entity is Minion minion)
                {
                    expected[GameTag.CARDRACE] = (int)minion.Card.GetRawRace();
                    foreach (GameTag tag in Card.AdditionalRaceTags.Keys) expected[tag] = minion.Card[tag];
                    expected[GameTag.CHARGE] = minion.HasCharge ? 1 : 0;
                    expected[GameTag.SPELLPOWER] = minion.SpellPower;
                }
                if (entity is Weapon weapon)
                {
                    expected[GameTag.ATK] = weapon.AttackDamage;
                    // NativeAuditMappings maps DURABILITY to HEALTH; UI subtracts DAMAGE.
                    int durability = shown.Get(GameTag.HEALTH) - shown.Get(GameTag.DAMAGE);
                    if (weapon.Durability != durability)
                        yield return new(entity.Card.Id, entity.Id, "DURABILITY", weapon.Durability.ToString(), durability.ToString());
                }
            }
            foreach (var pair in expected)
            {
                int value = shown.Get(pair.Key);
                // Numeric UI values: an omitted tag and explicit zero both display zero (TagMap.GetTag).
                if (!NativeUIProjection.TransportTagMatches(true, value, true, pair.Value))
                    yield return new(entity.Card.Id, entity.Id, pair.Key.ToString(), pair.Value.ToString(), value.ToString());
            }
        }
        foreach (var owner in new[] { game.Player1, game.Player2 })
            if (owner.NativeTags.ContainsKey(GameTag.IMBUE))
            {
                int expected = SabberStoneCore.Actions.ImbueRules.Count(owner);
                int shown = client.Entities.TryGetValue(client.WireId(owner.Id), out var player) ? player.Get(GameTag.IMBUE) : 0;
                if (expected != shown) yield return new("", owner.Id, "IMBUE", expected.ToString(), shown.ToString());
            }
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            client.Entities.TryGetValue(client.WireId(owner.Id), out var player);
            foreach (var tag in new[] { GameTag.CORPSES, GameTag.CORPSES_SPENT_THIS_GAME })
            {
                int expected = owner[tag], shown = player?.Get(tag) ?? 0;
                if (expected != shown) yield return new("", owner.Id, tag.ToString(), expected.ToString(), shown.ToString());
            }
            int available = Math.Max(0, (player?.Get(GameTag.CORPSES) ?? 0) - (player?.Get(GameTag.CORPSES_SPENT_THIS_GAME) ?? 0));
            int expectedAvailable = SabberStoneCore.Actions.CorpseRules.Available(owner);
            if (expectedAvailable != available)
                yield return new("", owner.Id, "CORPSES_AVAILABLE", expectedAvailable.ToString(), available.ToString());
        }
        foreach (var violation in ActorIdentity(game, client)) yield return violation;
        // An engine-only iteration misses client ghosts left in PLAY/HAND after an unsent move.
        foreach (var pair in client.Entities)
        {
            var shown = pair.Value;
            bool clientVisible = shown.Get(GameTag.ZONE) == (int)Zone.PLAY
                || shown.Get(GameTag.CONTROLLER) == 1 && shown.Get(GameTag.ZONE) is (int)Zone.HAND or (int)Zone.SECRET;
            if (!clientVisible || shown.CardId.Length == 0) continue;
            int engineId = client.EngineId(pair.Key);
            var entity = engineId > 0 && game.IdEntityDic.ContainsKey(engineId) && client.WireId(engineId) == pair.Key
                ? game.IdEntityDic[engineId] : null;
            if (entity == null)
            {
                if (shown.Get(GameTag.CARDTYPE) != (int)CardType.ENCHANTMENT)
                    yield return new(shown.CardId, pair.Key, "ENTITY", "absent", "visible");
            }
            else if (entity is not Enchantment && !Visible(entity))
                yield return new(entity.Card.Id, entity.Id, "ZONE", ((int)Location(entity)).ToString(), shown.Get(GameTag.ZONE).ToString());
        }
    }
}
