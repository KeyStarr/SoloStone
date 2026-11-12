using System.Collections.Generic;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Zone = SabberStoneCore.Enums.Zone;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Spell = SabberStoneCore.Model.Entities.Spell;

internal static class B14PrivacyProof
{
    internal static void Run(string only = null)
    {
        var failures = new List<string>();
        foreach (int owner in new[] { 1, 2 })
        {
            void Case(string name, Action action)
            {
                if (only != null && only != name) return;
                try { action(); }
                catch (InvalidOperationException error) { failures.Add($"{name}/seat={owner}: {error.Message}"); }
            }
            foreach (string source in new[] { "CORE_EDR_004_2026", "TIME_038", "TIME_038t1" })
                Case("reset", () => Reset(owner, source));
            Case("aura", () => Aura(owner));
            Case("quest", () => Quest(owner));
            Case("quest-rejected", () => Quest(owner, "full"));
            Case("quest-rejected", () => Quest(owner, "occupied"));
            Case("quest-rejected", () => Quest(owner, "full", staged: true));
            Case("quest-rejected", () => Quest(owner, "occupied", staged: true));
        }
        SelfChecks.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS batch 14 privacy: hidden/public SETASIDE reset, active Aura, ordered Treasuregill Quest; both seats");
    }

    private static (NativeMatch Match, NativeSession Session) Attach(SabberStoneCore.Model.Game game)
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        session.Flush(); game.PowerHistory.Last.Clear();
        return (match, session);
    }

    private static void Reset(int owner, string sourceId)
    {
        var game = OfflinePoolTestRecipes.NewGame(owner == 2);
        var p = game.CurrentPlayer;
        var (match, session) = Attach(game);
        var hidden = new[] { "CAP_400t2t", "DINO_433" }
            .Select(id => Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone)).ToArray();
        var known = Entity.FromCard(p, Cards.FromId("DINO_433"), zone: p.GraveyardZone);
        var source = Generic.DrawCard(p, Cards.FromId(sourceId));
        session.Flush(); game.PowerHistory.Last.Clear();
        foreach (var actor in hidden)
        {
            SelfChecks.Check(session.Client.Entities[actor.Id].CardId == "", "deck fixture anonymous");
            Generic.RemoveFromZone(p, actor); p.SetasideZone.Add(actor);
        }
        Generic.RemoveFromZone(p, known); p.SetasideZone.Add(known);
        session.Flush(); game.PowerHistory.Last.Clear();
        p.BaseMana = 10;
        session.Process(PlayCardTask.Any(p, source));
        for (int choices = 0; p.Choice != null && p.Choice.ChoiceAction != ChoiceAction.REWIND && choices < 10; choices++)
            session.Process(ChooseTask.Pick(p, p.Choice.Choices[0]));
        SelfChecks.Check(game.Rewind != null, sourceId + " offers Rewind");
        // Discarded-attempt disclosure must not change the baseline observer's knowledge.
        game.PowerHistory.Last.Clear();
        foreach (var actor in hidden) game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(actor));
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        session.Process(ChooseTask.Pick(p, game.Rewind.RewindEntityId));
        var entries = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => {
            var packet = new PegasusGame.PowerHistory();
            using var stream = new MemoryStream(Convert.FromBase64String(w.Base64));
            packet.Deserialize(stream); return packet.List;
        }).ToArray();
        int reset = Array.FindIndex(entries, e => e.HasResetGame);
        int end = Array.FindIndex(entries, reset, e => e.HasPowerEnd);
        var snapshots = entries.Skip(reset).Take(end - reset).Select(e => e.HasFullEntity ? e.FullEntity
            : e.HasChangeEntity ? e.ChangeEntity : e.HasShowEntity ? e.ShowEntity : null).Where(e => e != null).ToArray();
        foreach (var actor in hidden)
        {
            var restored = snapshots.Where(e => e.Entity == actor.Id).ToArray();
            SelfChecks.Check(restored.Length > 0 && restored.All(e => e.Name == ""
                && e.Tags.All(t => t.Name is (int)GameTag.ENTITY_ID or (int)GameTag.CONTROLLER or (int)GameTag.ZONE or (int)GameTag.ZONE_POSITION)),
                sourceId + " reset must keep formerly anonymous SETASIDE actor anonymous: " + actor.Card.Id);
        }
        SelfChecks.Check(snapshots.Single(e => e.Entity == known.Id).Name == "DINO_433",
            sourceId + " reset preserves public SETASIDE identity");
        SelfChecks.Check(!session.History.Findings.Any(f => f.Rule == "PRIVACY"), sourceId + " reset history privacy");
    }

    private static void Aura(int owner)
    {
        var game = OfflinePoolTestRecipes.NewGame(owner == 2, hero: CardClass.PALADIN);
        var controller = owner == 1 ? game.Player1 : game.Player2;
        var (match, session) = Attach(game);
        var actor = (Spell)Entity.FromCard(controller, Cards.FromId("BE_146"), zone: controller.DeckZone);
        session.Flush();
        SelfChecks.Check(session.Client.Entities[actor.Id].CardId == "", "deck Aura starts anonymous");
        Generic.CastSpell(controller, game, actor, null, 0);
        session.Flush();
        SelfChecks.Check(actor.Zone == controller.SecretZone && session.Client.Entities[actor.Id].CardId == "BE_146"
            && session.Client.Entities[actor.Id].Get(GameTag.COST) == actor.Cost,
            "automatic deck Aura reveals its public identity and cost for either owner");
        var installed = (Spell)Entity.FromCard(controller, Cards.FromId("JAIL_327"), zone: controller.DeckZone);
        session.Flush();
        AuraSpellRules.Install(installed); session.Flush();
        SelfChecks.Check(session.Client.Entities[installed.Id].CardId == "JAIL_327"
            && session.Client.Entities[installed.Id].Get(GameTag.COST) == installed.Cost,
            "direct Aura installation reveals the public actor for either owner");

        foreach (Zone zone in new[] { Zone.SECRET, Zone.HAND, Zone.DECK })
        foreach (CardType type in new[] { CardType.SPELL, CardType.MINION })
        foreach (int active in new[] { 0, 1 })
        {
            var shape = new HistoryShape();
            shape.Observe(new PegasusGame.PowerHistory { List = new() { new() { FullEntity = new() {
                Entity = 10, Name = "EDR_259e1", Tags = new() {
                    new() { Name = (int)GameTag.ZONE, Value = (int)zone },
                    new() { Name = (int)GameTag.CONTROLLER, Value = owner },
                    new() { Name = (int)GameTag.CARDTYPE, Value = (int)type },
                    new() { Name = 3429, Value = active }
                }
            } } } });
            shape.Finish();
            bool isPublic = zone == Zone.SECRET && type == CardType.SPELL && active == 1;
            SelfChecks.Check(shape.Findings.Any(f => f.Rule == "PRIVACY") == (owner == 2 && !isPublic),
                $"active Aura public only for SPELL/SECRET/1: {zone}/{type}/{active}");
        }
        // A real Secret never borrows the Aura exemption: by identity (Counterspell) or by a SECRET tag.
        foreach (var (name, secretTag) in new[] { ("EX1_287", 0), ("EX1_287", 1), ("EDR_259e1", 1) })
        {
            var shape = new HistoryShape();
            var tags = new List<PegasusGame.Tag> {
                new() { Name = (int)GameTag.ZONE, Value = (int)Zone.SECRET },
                new() { Name = (int)GameTag.CONTROLLER, Value = owner },
                new() { Name = (int)GameTag.CARDTYPE, Value = (int)CardType.SPELL },
                new() { Name = 3429, Value = 1 }
            };
            if (secretTag == 1) tags.Add(new() { Name = (int)GameTag.SECRET, Value = 1 });
            shape.Observe(new PegasusGame.PowerHistory { List = new() { new() { FullEntity = new() { Entity = 10, Name = name, Tags = tags } } } });
            shape.Finish();
            SelfChecks.Check(shape.Findings.Any(f => f.Rule == "PRIVACY") == (owner == 2),
                $"stale Aura tag cannot hide Secret {name}/SECRET={secretTag}");
        }
    }

    private static void Quest(int owner, string rejection = null, bool staged = false)
    {
        var game = OfflinePoolTestRecipes.NewGame(owner == 2);
        var p = game.CurrentPlayer;
        foreach (var spell in p.DeckZone.OfType<Spell>().ToArray()) Generic.RemoveFromZone(p, spell);
        if (rejection == "full")
            foreach (string id in new[] { "EX1_287", "EX1_289", "EX1_294", "EX1_295", "tt_010" })
                Entity.FromCard(p, Cards.FromId(id), zone: p.SecretZone);
        Spell active = null;
        if (rejection == "occupied")
        {
            active = (Spell)Entity.FromCard(p, Cards.FromId("TLC_426"), zone: p.DeckZone);
            Generic.CastSpell(p, game, active, null, 0);
        }
        var (match, session) = Attach(game);
        var quest = Entity.FromCard(p, Cards.FromId("TLC_426"), zone: p.DeckZone);
        var treasuregill = Generic.DrawCard(p, Cards.FromId("TLC_438"));
        session.Flush(); game.PowerHistory.Last.Clear(); p.BaseMana = 10;
        SelfChecks.Check(session.Client.Entities[quest.Id].CardId == "", "deck Quest starts anonymous");
        SelfChecks.Check(QuestRules.CanInstall(p, quest.Card) == (rejection == null), "Quest admission fixture; rejection=" + rejection);
        int cursor = session.Packets.Count;
        if (staged)
        {
            p.SetasideZone.Add(p.DeckZone.Remove(quest));
            session.Flush(); game.PowerHistory.Last.Clear();
            SelfChecks.Check(session.Client.Entities[quest.Id].CardId == "", "staged Quest remains anonymous");
            Generic.CastSpell(p, game, (Spell)quest, null, 0);
            session.Flush();
        }
        else session.Process(PlayCardTask.Any(p, treasuregill));
        var entries = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => {
            var packet = new PegasusGame.PowerHistory();
            using var stream = new MemoryStream(Convert.FromBase64String(w.Base64));
            packet.Deserialize(stream); return packet.List;
        }).ToList();
        int reveal = entries.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == quest.Id && e.ShowEntity.Name == "TLC_426");
        int publicEntry = entries.FindIndex(e => e.HasTagChange && e.TagChange.Entity == quest.Id
            && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)(rejection == null ? Zone.SECRET : Zone.GRAVEYARD));
        SelfChecks.Check(reveal >= 0 && (publicEntry < 0 || reveal < publicEntry),
            "Treasuregill Quest reveal precedes public entry; rejection=" + rejection);
        SelfChecks.Check((rejection == null ? quest.Zone == p.SecretZone : quest.Zone == p.GraveyardZone)
            && session.Client.Entities[quest.Id].CardId == "TLC_426", "Treasuregill casts public Quest; rejection=" + rejection);
        if (active != null)
            SelfChecks.Check(active.Zone == p.SecretZone && active.QuestState != null && active.QuestProgress == (staged ? 0 : 1)
                && p.SecretZone.ModernQuests.Single() == active && session.Client.Entities[active.Id].CardId == "TLC_426",
                "rejected Quest preserves already-active Quest");
        SelfChecks.Check(!session.History.Findings.Any(f => f.Rule is "ANON_PUBLIC" or "PRIVACY"),
            "Treasuregill Quest reveal must precede public zone entry: " + string.Join("; ", session.History.Findings));
        // Rejected automatic casts retire inside their own PLAY, after a same-identity DECK reveal.
        SelfChecks.Check(!session.History.Findings.Any(f => f.Rule == "MILL_ORDER"),
            "rejected deck Quest keeps the native mill order; rejection=" + rejection + " staged=" + staged + ": "
            + string.Join("; ", session.History.Findings.Where(f => f.Rule == "MILL_ORDER")));
    }
}
