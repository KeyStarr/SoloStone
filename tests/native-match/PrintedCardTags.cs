using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    private static void PrintedCardTags()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        game.Player1.Hero.AuraEffects = (SabberStoneCore.Enchants.AuraEffects)Activator.CreateInstance(
            typeof(SabberStoneCore.Enchants.AuraEffects), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { CardType.HERO }, null);
        game.PowerHistory.Last.Clear();
        void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
        var fixtures = new List<(string Id, int[] Tags)> {
            ("JAIL_407", new[] { 114, 4354, 1743, 32 }),
            ("EDR_528", new[] { 220 }),
            ("CORE_EX1_005", new[] { 1720, 1743 }),
            ("CORE_BT_801", new[] { 1333, 1635 })
        };
        var forge = Cards.All.FirstOrDefault(card => card[(GameTag)2785] != 0);
        Check(forge != null, "the card registry holds a Forge definition for the printed Forge fixture");
        fixtures.Add((forge.Id, new[] { 2785, 1743, 3074 }));
        foreach (var fixture in fixtures)
        {
            var card = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId(fixture.Id));
            var before = card.NativeTags.ToDictionary(p => p.Key, p => p.Value);
            Flush();
            Check(before.Count == card.NativeTags.Count && before.All(p => card.NativeTags[p.Key] == p.Value),
                "printed projection mutated instance tags");
            var reveal = NativeHistory(capture).Last(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id).ShowEntity;
            foreach (int tag in fixture.Tags)
                Check(card.Card[(GameTag)tag] != 0 && reveal.Tags.Any(t => t.Name == tag && t.Value == card.Card[(GameTag)tag]),
                    "printed reveal " + fixture.Id + " missing " + tag);
            Check(reveal.Tags.Select(t => t.Name).Distinct().Count() == reveal.Tags.Count, "duplicate printed snapshot tags");
        }

        var vanessa = game.Player1.HandZone.Single(c => c.Card.Id == "JAIL_407");
        Check(!vanessa.NativeTags.ContainsKey(GameTag.PREPARE), "Prepare fixture must use printed keyword");
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int prepare = options.Options.FindIndex(o => o.MainOption?.Id == vanessa.Id && o.MainOption.PowerKeyword == 4354);
        Check(prepare >= 0 && options.Options.Any(o => o.MainOption?.Id == vanessa.Id && o.MainOption.PowerKeyword == 0),
            "printed Vanessa needs distinct native play/Prepare options");
        int hand = game.Player1.HandZone.Count, deck = game.Player1.DeckZone.Count, position = vanessa.ZonePosition;
        match.SendOption(options.Id, prepare, 0, -1, 0);
        Check(vanessa.Zone == game.Player1.HandZone && vanessa.ZonePosition == position
            && game.Player1.HandZone.Count == hand && game.Player1.DeckZone.Count == deck
            && game.Player1.RemainingMana == 4 && vanessa.Cost == 0 && vanessa[GameTag.PREPARING] == 1,
            "printed Vanessa Prepare must spend six mana, discount seven and retain hand identity");
        Check(!capture.Last<AllOptions>(14).Options.Any(o => o.MainOption?.Id == vanessa.Id), "prepared Vanessa must lock");
        game.PowerHistory.Last.Clear();

        var combo = game.Player1.HandZone.Single(c => c.Card.Id == "EDR_528");
        combo[GameTag.COMBO] = 0;
        Flush();
        Check(match.SnapshotVisibleTransportTags(combo)[220] == 0, "instance zero must override printed Combo");
        // Snapshot overrides are frozen: later engine state must not rewrite an earlier reveal.
        var frozen = PowerHistoryBuilder.ShowEntity(combo);
        combo[GameTag.COMBO] = 1;
        SendHistoryEntries(match, new List<IPowerHistoryEntry> { frozen });
        Check(NativeHistory(capture).Last(e => e.HasShowEntity && e.ShowEntity.Entity == combo.Id)
            .ShowEntity.Tags.Single(t => t.Name == 220).Value == 0, "snapshot override lost to current instance");
        Flush();
        combo.NativeTags[GameTag.COMBO] = 0;
        Flush();
        Check(NativeHistory(capture).Last(e => e.HasTagChange && e.TagChange.Entity == combo.Id && e.TagChange.Tag == 220)
            .TagChange.Value == 0, "visible printed keyword must reconcile even without engine history");

        int cursor = capture.Packets.Count;
        var hidden = SabberStoneCore.Actions.Generic.DrawCard(game.Player2, Cards.FromId("JAIL_407"));
        Flush();
        var hiddenEntries = NativeHistory(capture, cursor).Where(e => SnapshotEntity(e) == hidden.Id).ToArray();
        Check(hiddenEntries.Length > 0 && hiddenEntries.All(e => SnapshotName(e) == ""), "opponent legendary identity leaked");
        foreach (var entry in hiddenEntries)
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.ShowEntity;
            Check(HasOnlyHiddenTags(snapshot.Tags), "opponent legendary printed tags leaked");
        }
        Check(!NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == hidden.Id
            && new[] { 114, 4354, 1743, 32 }.Contains(e.TagChange.Tag)), "opponent legendary delta leaked");

        // Native HIDE and SHOW merge tags; retain old identity values until the next reveal clears them.
        combo[GameTag.COMBO] = 1;
        Flush();
        SendHistoryEntries(match, new List<IPowerHistoryEntry> { PowerHistoryBuilder.HideEntity(combo) });
        var changed = PowerHistoryBuilder.ShowEntity(combo);
        changed.Entity.Name = "CS2_182";
        changed.Entity.Tags.Remove(GameTag.COMBO);
        SendHistoryEntries(match, new List<IPowerHistoryEntry> { changed });
        Check(NativeHistory(capture).Last(e => e.HasShowEntity && e.ShowEntity.Entity == combo.Id)
            .ShowEntity.Tags.Any(t => t.Name == 220 && t.Value == 0), "reveal after hide must clear old printed Combo");
        capture.SerializeAll();
        PrintedTagsTransform();
        PrintedTagsSilence();
        PrintedTagsAuras();
        Console.WriteLine("PASS printed tags: reveals, printed Vanessa Prepare option/payment/lock, overrides, frozen replay, current values, hidden legendary and hide/reveal clears");
    }

    private static void PrintedTagsSilence()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_022", null);
        var game = match.Game;
        game.PowerHistory.Last.Clear();
        var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CORE_NEW1_023"), zone: game.Player2.BoardZone);
        FlushHistory(match);
        game.PowerHistory.Last.Clear();
        Check(match.SnapshotVisibleTransportTags(target)[1211] == 1, "Faerie Dragon must start Elusive");
        var beforeSilence = PowerHistoryBuilder.ShowEntity(target);
        int cursor = capture.Packets.Count;
        target.Silence();
        Check(!target.CantBeTargetedBySpells && !target.CantBeTargetedByHeroPowers,
            "Silence must remove effective targeting restrictions");
        Check(target[(GameTag)1211] == 1 && !target.NativeTags.ContainsKey((GameTag)1211),
            "regression requires stale printed Elusive without an engine override");
        var afterSilence = PowerHistoryBuilder.ShowEntity(target);
        var fullAfterSilence = PowerHistoryBuilder.FullEntity(target);
        FlushHistory(match);
        game.PowerHistory.Last.Clear();
        Check(NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == target.Id
            && e.TagChange.Tag == 1211 && e.TagChange.Value == 0), "Silence must explicitly clear Elusive");
        Check(match.SnapshotVisibleTransportTags(target)[1211] == 0, "Silence audit must use effective Elusive");

        // Later state must not rewrite either side of the Silence boundary.
        target.CantBeTargetedBySpells = true;
        foreach (var snapshot in new IPowerHistoryEntry[] { beforeSilence, afterSilence, fullAfterSilence })
        {
            int expected = ReferenceEquals(snapshot, beforeSilence) ? 1 : 0;
            SendHistoryEntries(match, new List<IPowerHistoryEntry> { snapshot });
            var entry = NativeHistory(capture).Last(e => SnapshotEntity(e) == target.Id);
            var tags = entry.HasShowEntity ? entry.ShowEntity.Tags : entry.FullEntity.Tags;
            Check(tags.Any(t => t.Name == 1211 && t.Value == expected),
                "Elusive snapshot must retain frozen targeting state");
        }
        target.CantBeTargetedBySpells = false;
        game.PowerHistory.Last.Clear();
        var protection = new SabberStoneCore.Enchants.Effect(GameTag.CANT_BE_TARGETED_BY_SPELLS,
            SabberStoneCore.Enchants.EffectOperator.SET, 1);
        protection.ApplyAuraTo(target);
        var protectedHistories = CaptureAuraSnapshots(target);
        FlushHistory(match);
        var protectedSnapshot = match.SnapshotVisibleTransportTags(target);
        Check(protectedSnapshot[1211] == 1, "aura must grant effective Elusive even after Silence");
        cursor = capture.Packets.Count;
        protection.RemoveAuraFrom(target);
        FlushHistory(match);
        Check(protectedSnapshot[1211] == 1, "captured effective Elusive must stay frozen after aura removal");
        Check(NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == target.Id
            && e.TagChange.Tag == 1211 && e.TagChange.Value == 0), "aura end must explicitly clear Elusive");
        Check(!target.CantBeTargetedBySpells && !target.CantBeTargetedByHeroPowers,
            "aura replay regression requires protection removed before replay");
        foreach (var snapshot in protectedHistories)
        {
            SendHistoryEntries(match, new List<IPowerHistoryEntry> { snapshot });
            var entry = NativeHistory(capture).Last(e => SnapshotEntity(e) == target.Id);
            Check(SnapshotTag(entry, (GameTag)1211) == 1
                && SnapshotTag(entry, GameTag.CANT_BE_TARGETED_BY_SPELLS) == 1
                && SnapshotTag(entry, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS) == 1,
                "aura-active " + snapshot.PowerType + " must freeze Elusive and both targeting restrictions");
        }

        // Rebase the same entity to its printed targeting rule, then transform away from it.
        SabberStoneCore.Actions.Generic.ChangeEntityBlock(game.Player2, target, target.Card, true);
        FlushHistory(match);
        Check(match.SnapshotVisibleTransportTags(target)[1211] == 1, "set-to-base must restore printed Elusive");
        game.PowerHistory.Last.Clear();
        cursor = capture.Packets.Count;
        SabberStoneCore.Actions.Generic.ChangeEntityBlock(game.Player2, target, Cards.FromId("CS2_tk1"), true);
        FlushHistory(match);
        Check(NativeHistory(capture, cursor).Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == target.Id
            && e.ChangeEntity.Tags.Any(t => t.Name == 1211 && t.Value == 0)), "transform must explicitly clear Elusive");
        Check(!target.NativeTags.ContainsKey((GameTag)1211), "projection must not mutate Elusive engine tags");
        capture.SerializeAll();
        Console.WriteLine("PASS printed Elusive: real Silence, explicit zero, frozen SHOW/FULL, aura end, rebase, transform and no tag mutation");
    }

    private static IPowerHistoryEntry[] CaptureAuraSnapshots(IPlayable target)
    {
        string state = target.Game.Hash();
        var raw = target.NativeTags.ToDictionary(p => p.Key, p => p.Value);
        int full = target.Game.PowerHistory.Full.Count, last = target.Game.PowerHistory.Last.Count;
        var targetingCache = typeof(Character).GetField("_modifiedCantBeTargetedBySpells",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object cachedTargeting = target is Character ? targetingCache.GetValue(target) : null;
        var snapshots = new IPowerHistoryEntry[2];
        for (int i = 0; i < snapshots.Length; i++)
        {
            snapshots[i] = i == 0 ? (IPowerHistoryEntry)PowerHistoryBuilder.ShowEntity(target)
                : PowerHistoryBuilder.FullEntity(target);
            Check(!(target is Character) || Equals(cachedTargeting, targetingCache.GetValue(target)),
                snapshots[i].PowerType + " snapshot must not populate or change targeting cache");
        }
        Check(target.Game.Hash() == state && raw.Count == target.NativeTags.Count
            && raw.All(p => target.NativeTags[p.Key] == p.Value)
            && full == target.Game.PowerHistory.Full.Count && last == target.Game.PowerHistory.Last.Count,
            "snapshot builders must not change game state, raw tags, auras or history");
        return snapshots;
    }

    private static void PrintedTagsAuras()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_022", null);
        var game = match.Game;
        // Construct directly: FromCard emits FULL before returning and can hide a snapshot cache write.
        var elusive = new Minion(game.Player2, Cards.FromId("EDR_598"), new Dictionary<GameTag, int>());
        game.IdEntityDic[elusive.Id] = elusive;
        game.Player2.BoardZone.Add(elusive);
        // Existing stat getters cache independently of targeting; prime them before the hash check.
        _ = elusive.AttackDamage;
        _ = elusive.BaseHealth;
        Check(typeof(Character).GetField("_modifiedCantBeTargetedBySpells",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(elusive) == null,
            "fresh printed-Elusive fixture must start with null targeting cache");
        Check(elusive.Zone == game.Player2.BoardZone && !elusive.IsSilenced
            && !(elusive.AuraEffects?.CantBeTargetedBySpells ?? false)
            && elusive.Card[GameTag.CANT_BE_TARGETED_BY_SPELLS] == 0
            && elusive.Card[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] == 0,
            "printed-Elusive fixture must be on board without aura, Silence or printed targeting tags");
        foreach (var snapshot in CaptureAuraSnapshots(elusive))
        {
            var tags = snapshot is PowerHistoryShowEntity show ? show.Entity.Tags : ((PowerHistoryFullEntity)snapshot).Entity.Tags;
            Check(tags[GameTag.CANT_BE_TARGETED_BY_SPELLS] == 1
                && tags[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] == 1,
                "printed-Elusive " + snapshot.PowerType + " must freeze both effective targeting restrictions without aura");
            SendHistoryEntries(match, new List<IPowerHistoryEntry> { snapshot });
            var entry = NativeHistory(capture).Last(e => SnapshotEntity(e) == elusive.Id);
            Check(SnapshotTag(entry, (GameTag)1211) == 1,
                "printed-Elusive " + snapshot.PowerType + " replay must retain native Elusive without aura");
        }
        var minion = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        var spell = game.Player1.HandZone.First(c => c.Card.Id == "CS2_022");
        var weapon = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_106"), zone: game.Player1.HandZone);
        var location = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.All.First(c => c.Type == CardType.LOCATION), zone: game.Player1.HandZone);
        FlushHistory(match);
        game.PowerHistory.Last.Clear();
        var auraTags = new[] { GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
            GameTag.ECHO, GameTag.RUSH, GameTag.IMMUNE, GameTag.HEROPOWER_DAMAGE, GameTag.CANNOT_ATTACK_HEROES,
            GameTag.CANT_PLAY };
        foreach (var target in new[] { minion, spell, weapon, location, game.Player1.Hero, game.Player1.Hero.HeroPower })
            foreach (var snapshot in CaptureAuraSnapshots(target))
            {
                var tags = snapshot is PowerHistoryShowEntity show ? show.Entity.Tags : ((PowerHistoryFullEntity)snapshot).Entity.Tags;
                foreach (var tag in auraTags)
                    Check(tags.ContainsKey(tag) == target.NativeTags.ContainsKey(tag),
                        "no-aura " + snapshot.PowerType + " added absent " + tag + " on " + target.Card.Type);
            }

        foreach (var fixture in new (IPlayable Target, GameTag Tag, int Value)[] {
            (minion, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, 1),
            (minion, GameTag.ECHO, 1), (minion, GameTag.RUSH, 1),
            (spell, GameTag.ECHO, 1), (weapon, GameTag.ECHO, 1), (location, GameTag.ECHO, 1),
            (weapon, GameTag.IMMUNE, 1), (game.Player1.Hero, GameTag.IMMUNE, 1),
            (game.Player1.Hero, GameTag.HEROPOWER_DAMAGE, 3),
            (game.Player1.Hero, GameTag.CANNOT_ATTACK_HEROES, 1)
        })
        {
            var target = fixture.Target;
            Check(target[fixture.Tag] == 0 && target.Card[fixture.Tag] == 0,
                "aura fixture must start without " + fixture.Tag);
            var aura = new SabberStoneCore.Enchants.Effect(fixture.Tag,
                SabberStoneCore.Enchants.EffectOperator.SET, fixture.Value);
            aura.ApplyAuraTo(target);
            Check(target[fixture.Tag] == fixture.Value, "real aura must grant " + fixture.Tag);
            var snapshots = CaptureAuraSnapshots(target);
            aura.RemoveAuraFrom(target);
            Check(target[fixture.Tag] == 0, "aura must end before replaying " + fixture.Tag);
            foreach (var snapshot in snapshots)
            {
                SendHistoryEntries(match, new List<IPowerHistoryEntry> { snapshot });
                var entry = NativeHistory(capture).Last(e => SnapshotEntity(e) == target.Id);
                Check(SnapshotTag(entry, fixture.Tag) == fixture.Value,
                    "aura-active " + snapshot.PowerType + " must freeze " + fixture.Tag + " on " + target.Card.Type);
            }
        }
        // Prepare publishes CANT_PLAY as history only; its indexer derives the value from PREPARING.
        var preparing = new SabberStoneCore.Enchants.Effect(GameTag.PREPARING,
            SabberStoneCore.Enchants.EffectOperator.SET, 1);
        Check(!spell.NativeTags.ContainsKey(GameTag.CANT_PLAY) && spell[GameTag.CANT_PLAY] == 0,
            "Prepare fixture must lack raw CANT_PLAY");
        preparing.ApplyTo(spell);
        var preparedHistories = CaptureAuraSnapshots(spell);
        preparing.RemoveFrom(spell);
        Check(spell[GameTag.CANT_PLAY] == 0, "Prepare effect must end before replay");
        foreach (var snapshot in preparedHistories)
        {
            SendHistoryEntries(match, new List<IPowerHistoryEntry> { snapshot });
            var entry = NativeHistory(capture).Last(e => SnapshotEntity(e) == spell.Id);
            Check(SnapshotTag(entry, GameTag.CANT_PLAY) == 1,
                "prepared " + snapshot.PowerType + " must freeze derived CANT_PLAY");
        }
        capture.SerializeAll();
        Console.WriteLine("PASS aura snapshots: frozen SHOW/FULL replay, targeting, Echo, Rush, Immune, hero-power damage, attack/play restrictions, no-aura shape and pure reads");
    }

    private static void PrintedTagsTransform()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_022", null);
        match.Game.PowerHistory.Last.Clear();
        var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            match.Game.Player2, Cards.FromId("JAIL_407"), zone: match.Game.Player2.BoardZone);
        FlushHistory(match);
        var original = NativeHistory(capture).Last(e => SnapshotEntity(e) == target.Id);
        Check(SnapshotTag(original, GameTag.ELITE) == 1 && SnapshotTag(original, GameTag.PREPARE) == 1,
            "visible opponent board legendary must receive printed tags");
        var source = match.Game.Player1.HandZone.First(card => card.Card.Id == "CS2_022");
        var task = match.Game.Player1.Options().First(option => option.Source?.Id == source.Id && option.Target?.Id == target.Id);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, task);
        FlushHistory(match);
        var change = NativeHistory(capture, cursor).Single(e => e.HasChangeEntity && e.ChangeEntity.Entity == target.Id).ChangeEntity;
        foreach (int tag in new[] { 114, 4354, 1743, 32 })
            Check(change.Tags.Any(t => t.Name == tag && t.Value == 0), "transform must explicitly clear printed tag " + tag);
        capture.SerializeAll();
    }
}
