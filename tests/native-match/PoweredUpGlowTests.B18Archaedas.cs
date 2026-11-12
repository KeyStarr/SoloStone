using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("b18-t-archaedas-native", ProofKind.Native)]
    private static void Unpinned_Archaedas_WeaponSpellNameProjection()
    {
        Check(Cards.All.Any(c => c.Id == "BE_055t" && c.Implemented), "Archaedas native weapon registered");
        foreach (string id in new[] { "CS2_024", "CS2_029" })
        {
            var f = new Fixture(); var selected = f.Draw(id); var spell = selected.Card;
            var weapon = Generic.ChangeEntityBlock(f.Player, selected, Cards.FromId("BE_055t"), true);
            PayloadRules.StoreCard(weapon, 18055, spell, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
            f.Game.AuraUpdate(); f.Flush();
            int frozenReference = PayloadRules.Entries(weapon, 18055).Single().Id;
            AssertArchaedasTags(f, weapon, spell.AssetId);
            var frozen = PowerHistoryBuilder.ShowEntity(weapon).Entity;
            foreach (var next in new[] { f.Game.CloneExact(history: true), SabberStoneCore.Model.Game.LoadSnapshot(f.Game.SaveSnapshot()) })
            {
                var snapshot = PowerHistoryBuilder.ShowEntity(next.IdEntityDic[weapon.Id]).Entity;
                var projected = (PegasusGame.PowerHistoryEntity)typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(f.Match, new object[] { snapshot, true, true, null, false });
                int restoredSpell = PayloadRules.Entries(next.IdEntityDic[weapon.Id], 18055).Single().Id;
                Check(projected.Tags.Single(t => t.Name == 2).Value == restoredSpell
                    && new[] { 1078, 1452 }.All(tag => projected.Tags.Single(t => t.Name == tag).Value == spell.AssetId),
                    "cloned/restored weapon projects the retained spell entity (tag 2) and dbfId (related card)");
            }
            PayloadRules.Clear(weapon, PayloadPolicy.Silence); f.Game.AuraUpdate(); f.Flush();
            AssertArchaedasTags(f, weapon, 0);
            Generic.ChangeEntityBlock(f.Player, weapon, Cards.FromId("CS2_106"), true); f.Game.AuraUpdate(); f.Flush();
            var cleared = ArchaedasWireTags(f, weapon.Id);
            Check(new[] { 1078, 1452 }.All(tag => !cleared.TryGetValue(tag, out int value) || value == 0),
                "replacing weapon identity clears old spell related-card references");
            var historical = (PegasusGame.PowerHistoryEntity)typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(f.Match, new object[] { frozen, true, true, null, false });
            Check(historical.Tags.Single(t => t.Name == 2).Value == frozenReference
                && new[] { 1078, 1452 }.All(tag => historical.Tags.Single(t => t.Name == tag).Value == spell.AssetId),
                "old history keeps frozen spell after later payload removal");
        }
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(g.Player1)), "opponent Archaedas turn");
            var owner = opponent ? g.Player2 : g.Player1; owner.BaseMana = 10; owner.UsedMana = 0;
            var source = Generic.DrawCard(owner, Cards.FromId("BE_055"));
            Check(g.Process(PlayCardTask.Any(owner, source)), "native Archaedas play"); f.Flush();
            var offers = owner.Choice.Choices.ToArray();
            if (opponent) AssertArchaedasHidden(f, offers);
            int selected = offers[1]; int dbfId = g.IdEntityDic[selected].Card.AssetId;
            var decision = g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out _), "native Discover completes"); f.Flush();
            var weapon = owner.HandZone.Single(c => c.Card.Id == "BE_055t");
            var stored = PayloadRules.Entries(weapon, 18055).Single();
            if (!opponent) AssertArchaedasNameBeforeHand(f, weapon.Id, stored);
            var copy = Generic.Copy(owner, source, weapon, SabberStoneCore.Enums.Zone.HAND); g.AuraUpdate(); f.Flush();
            var copyStored = PayloadRules.Entries(copy, 18055).Single();
            if (opponent) AssertArchaedasHidden(f, new[] { stored.Id, copyStored.Id });
            else
            {
                // The owner's client must be able to name the spell: its payload entities are sent named, to nobody else.
                foreach (var entry in new[] { stored, copyStored })
                    Check(f.Entries.Any(e => e.HasFullEntity && e.FullEntity.Entity == entry.Id && e.FullEntity.Name == entry.Card.Id),
                        "owner receives the stored spell entity named");
            }
            if (opponent) AssertArchaedasHidden(f, offers.Concat(new[] { copy.Id }).ToArray());
            else { AssertArchaedasTags(f, weapon, dbfId); AssertArchaedasTags(f, copy, dbfId); }
            owner.UsedMana = 0; Check(g.Process(PlayCardTask.Any(owner, weapon)), "native generated weapon equips"); f.Flush();
            // An opponent's equipped weapon is public, but its spell stays private (its text reads "another card", as it always
            // did: a dbfId in an entity-reference tag never named anything), and so does its related-card panel. Retail is
            // unpinned (rules record STILL-UNPINNED): private is the default assumption, for the owner's ruling.
            AssertArchaedasTags(f, weapon, dbfId, referenceVisible: !opponent);
            PayloadRules.StoreCard(weapon, 18055, Cards.FromId("CS2_182")); f.Flush();
            AssertArchaedasTags(f, weapon, 0);
            var toDeck = Generic.Copy(owner, source, copy, SabberStoneCore.Enums.Zone.DECK); f.Flush();
            AssertArchaedasHidden(f, new[] { toDeck.Id });
            Check(f.Entries.All(e => !e.HasTagChange || !PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),
                "internal payload tags never reach native deltas");
            Check(f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
                .Where(e => e != null).All(e => e.Tags.All(t => !PayloadRules.IsInternalTag((GameTag)t.Name))),
                "internal payload tags never reach native snapshots");
            owner.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(PayloadRules.Entries(weapon).Length == 0, "native removal retires spell payload");
        }
        ArchaedasNativeSwitch();
        Console.WriteLine("PASS Unpinned_Archaedas_WeaponSpellNameProjection: tags 2/1078/1452, copy/equip/load, frozen history, both-seat privacy");
    }

    private static void ArchaedasNativeSwitch()
    {
        var f = new Fixture(); var source = f.Draw("BE_055"); var weapon = f.Draw("BE_055t"); f.Flush();
        var refresh = typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        refresh.Invoke(f.Match, null);
        var options = (PowerAllOptions)typeof(NativeMatch).GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Match);
        bool enabled = NativeMatch.ArchaedasNativeEnabled;
        foreach (var card in new[] { source, weapon })
        {
            Check(options.PowerOptionList.Any(o => o.MainOption?.EntityId == card.Id) == enabled,
                "one native switch gates both parent and weapon options");
            Check(NativeMatch.NativeActionEnabled(PlayCardTask.Any(f.Player, card)) == enabled,
                "native dispatcher enforces same switch");
        }
        var bot = Generic.DrawCard(f.Game.Player2, Cards.FromId("BE_055"));
        Check(NativeMatch.NativeActionEnabled(PlayCardTask.Any(f.Game.Player2, bot)) == enabled,
            "native bot shares parent enablement policy");
        Check(NativeMatch.NativeActionEnabled(EndTurnTask.Any(f.Player)), "switch always leaves end-turn available");
    }

    private static void AssertArchaedasTags(Fixture f, IPlayable weapon, int dbfId, bool referenceVisible = true)
    {
        var tags = ArchaedasWireTags(f, weapon.Id);
        string name = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
            .Last(e => e != null && e.Entity == weapon.Id).Name;
        Check(name == "BE_055t", "weapon native identity remains BE_055t");
        var live = f.Match.SnapshotVisibleTransportTags(weapon);
        // Tag 2 is read by the client as an ENTITY id (CardTextBuilderType 23); the related-card panel reads the dbfId.
        var entries = PayloadRules.Entries(weapon, 18055);
        int reference = dbfId == 0 || entries.Length != 1 || !referenceVisible ? 0 : entries[0].Id;
        foreach (int tag in new[] { 2, 1078, 1452 })
        {
            // A hidden (opposing) spell is hidden in the related-card panel too: the rules record is unpinned, default private.
            int expected = tag == 2 ? reference : referenceVisible ? dbfId : 0;
            Check(tags.TryGetValue(tag, out int value) && value == expected && live.TryGetValue(tag, out int current) && current == expected,
                "wire and live audit tag " + tag + " carry " + (tag == 2 ? "the spell entity id " : "the selected spell dbfId=") + expected
                + " (wire " + (tags.TryGetValue(tag, out int sentValue) ? sentValue.ToString() : "absent")
                + ", live " + (live.TryGetValue(tag, out int liveValue) ? liveValue.ToString() : "absent") + ")");
        }
    }

    // The pinned client builds the weapon text ("cast '{0}'") once, when the card is shown in hand, from the
    // weapon's own tag 2, read as an ENTITY id (GameState.GetEntity). CardTextBuilder.OnTagChange does not rebuild on
    // tag 2, so a later delta keeps "another card". The spell entity must already be named to the owner and referenced
    // by the weapon when it enters the hand.
    private static void AssertArchaedasNameBeforeHand(Fixture f, int id, IPlayable spellEntity)
    {
        int reference = spellEntity.Id;
        bool spellNamed = false;
        int known = 0;
        foreach (var entry in f.Entries)
        {
            var actor = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            bool entersHand = false;
            if (actor != null && actor.Entity == spellEntity.Id && actor.Name == spellEntity.Card.Id) spellNamed = true;
            if (actor != null && actor.Entity == id)
                foreach (var tag in actor.Tags)
                {
                    if (tag.Name == 2) known = tag.Value;
                    if (tag.Name == 49 && tag.Value == 3 && actor.Name == "BE_055t") entersHand = true;
                }
            if (entry.HasTagChange && entry.TagChange.Entity == id)
            {
                if (entry.TagChange.Tag == 2) known = entry.TagChange.Value;
                if (entry.TagChange.Tag == 49 && entry.TagChange.Value == 3) entersHand = true;
            }
            if (entersHand)
            {
                Check(known == reference && spellNamed,
                    "weapon references its spell entity, already named to the owner, before it is shown in hand (got " + known + ")");
                return;
            }
        }
        Check(false, "weapon entered the hand in native history");
    }

    private static Dictionary<int, int> ArchaedasWireTags(Fixture f, int id)
    {
        var tags = new Dictionary<int, int>();
        foreach (var entry in f.Entries)
        {
            var actor = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (actor != null && actor.Entity == id)
                foreach (var tag in actor.Tags) tags[tag.Name] = tag.Value;
            if (entry.HasTagChange && entry.TagChange.Entity == id) tags[entry.TagChange.Tag] = entry.TagChange.Value;
        }
        return tags;
    }

    private static void AssertArchaedasHidden(Fixture f, int[] ids)
    {
        bool PublicTag(int tag) => tag == 50 || tag == 49 || tag == 53 || tag == 263;
        Check(f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
            .Where(e => e != null && ids.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => PublicTag(t.Name))),
            "hidden offers/weapon/payload/deck identities never leak through snapshots");
        Check(f.Entries.Where(e => e.HasTagChange && ids.Contains(e.TagChange.Entity)).All(e => PublicTag(e.TagChange.Tag)),
            "hidden actors leak no spell, cost, text or related-card deltas");
    }

    [GroupProof("powered-up-b18-t-archaedas", ProofKind.PoweredUp)]
    private static void B18ArchaedasGlow()
    {
        var f = new Fixture();
        foreach (string id in new[] { "BE_055", "BE_055t" })
        {
            var card = f.Draw(id); f.Flush(); f.Expected(card, 0, "unconditional Archaedas has no condition glow");
            Check(!PoweredUp.HasCondition(card.Card), "no invented Archaedas condition");
        }
    }
}
