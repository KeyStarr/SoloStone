using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineSpell = SabberStoneCore.Model.Entities.Spell;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Before 2026-10-24 every Choose One play was sent with sub-option 0, so the client played no per-option effect,
    // the history tile always showed option 1, and Timereaver (END_010), which has no effect of its own, changed stats
    // silently. PLAY and the parent POWER block now carry the chosen option (Choose Both keeps 0 on PLAY, and each of
    // its POWER activations carries its own index). The prefab id below is the retail one for Timereaver's group heal
    // (applied to all enchanted minions); Secret Ingredient's Attack option has its own in SecretIngredientSubOptionHistory.
    private static void ChooseOneSubOptionHistory()
    {
        const string prefab = "ReuseFX_Holy_AE_HealingWave_Centered_BothSides_Super:c468a26646b575b45ae00505fea3c00f";
        foreach (bool bot in new[] { false, true })
        foreach (int branch in new[] { 1, 2, 0 })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            if (branch == 0) EngineEntity.FromCard(owner, Cards.FromId("OG_044"), zone: owner.BoardZone);
            var victims = new[] { owner, owner.Opponent }.SelectMany(player => new[] {
                (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_200"), zone: player.BoardZone),
                (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_101t"), zone: player.BoardZone)
            }).ToArray();
            var allBefore = owner.BoardZone.Concat(owner.Opponent.BoardZone).ToArray();
            var expected = allBefore.Select(m => m.Id).ToArray();
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId("END_010"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && (branch == 0 || t.ChooseOne == branch)));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            var play = data.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 7);
            Check(play.PowerStart.SubOption == (branch == 0 ? 0 : branch - 1), "Choose One PLAY carries selected branch, both keeps legacy zero");
            var powers = data.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3).ToArray();
            Check(powers.Select(e => e.PowerStart.SubOption).SequenceEqual(branch == 0 ? new[] { 0, 1 } : new[] { branch - 1 }),
                "Timereaver parent POWER carries each activated branch, including Choose Both");
            if (bot)
            {
                foreach (var sub in source.ChooseOnePlayables)
                    Check(capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PegasusGame.PowerHistory)p.Message).List)
                        .TakeWhile(e => !ReferenceEquals(e, play)).Any(e => SnapshotEntity(e) == sub.Id && SnapshotName(e) == sub.Card.Id),
                        "NPC branch actor identity known before PLAY selects sub-card");
                int reveal = data.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == source.Id && e.ShowEntity.Name == "END_010");
                Check(reveal >= 0 && reveal < data.IndexOf(play), "NPC parent identity revealed before PLAY");
            }
            Check(data.Count(e => e.HasSubSpellStart) == powers.Length && data.Count(e => e.HasSubSpellEnd) == powers.Length,
                $"one group scope per branch (bot={bot}, branch={branch})");
            foreach (var power in powers)
            {
                int powerStart = data.IndexOf(power), powerEnd = powerStart + 1, depth = 1;
                for (; powerEnd < data.Count; powerEnd++)
                {
                    if (data[powerEnd].HasPowerStart) depth++;
                    if (data[powerEnd].HasPowerEnd && --depth == 0) break;
                }
                var body = data.Skip(powerStart + 1).Take(powerEnd - powerStart - 1).ToList();
                int startIndex = body.FindIndex(e => e.HasSubSpellStart), endIndex = body.FindIndex(e => e.HasSubSpellEnd);
                Check(body.Count(e => e.HasSubSpellStart) == 1 && body.Count(e => e.HasSubSpellEnd) == 1
                    && startIndex > 0 && endIndex > startIndex && powerEnd < data.Count, "complete group inside POWER");
                var start = body[startIndex].SubSpellStart;
                Check(start.SpellPrefabGuid == prefab && start.SourceEntityId == source.Id
                    && start.TargetEntityIds.SequenceEqual(expected), "effect source and all recipients, including unchanged ones, in board order");
                var meta = body[startIndex - 1];
                Check(meta.HasMetaData && (int)meta.MetaData.Type == 0 && meta.MetaData.Data == 0
                    && meta.MetaData.Info.SequenceEqual(expected), "TARGET Data=0 immediately before group with same list");
                string enchantment = power.PowerStart.SubOption == 0 ? "END_010ae" : "END_010be";
                var attachments = body.Select((e, i) => (Entry: e, Index: i))
                    .Where(row => row.Entry.HasShowEntity && row.Entry.ShowEntity.Name == enchantment).ToArray();
                Check(attachments.Length == expected.Length && attachments.All(row => row.Index > startIndex && row.Index < endIndex)
                    && attachments.Select(row => row.Entry.ShowEntity.Tags.Single(t => t.Name == (int)GameTag.ATTACHED).Value)
                        .SequenceEqual(expected), "every enchantment SHOW enclosed in application order");
                var creations = attachments.Select(row => body.FindIndex(e => e.HasFullEntity
                    && e.FullEntity.Entity == row.Entry.ShowEntity.Entity)).ToArray();
                Check(creations[0] == startIndex + 1 && creations.Zip(attachments, (creation, show) =>
                    creation > startIndex && creation < show.Index).All(enclosed => enclosed),
                    "group starts immediately before first anonymous FULL; every enchantment FULL enclosed before SHOW");
                var stat = power.PowerStart.SubOption == 0 ? GameTag.ATK : GameTag.HEALTH;
                var changes = body.Select((e, i) => (Entry: e, Index: i)).Where(row => row.Entry.HasTagChange
                    && expected.Contains(row.Entry.TagChange.Entity)
                    && (row.Entry.TagChange.Tag == (int)GameTag.ATK || row.Entry.TagChange.Tag == (int)GameTag.HEALTH)).ToArray();
                Check(changes.Length >= expected.Length && changes.All(row => row.Index > startIndex && row.Index < endIndex
                    ), "all recipient stat publications enclosed, including no-op target");
                Check(changes.Where(row => row.Entry.TagChange.Tag == (int)stat).All(row => row.Entry.TagChange.Value == 1),
                    "chosen stat publications set to one");
            }
            Check(victims.All(m => (branch == 2 || m.AttackDamage == 1) && (branch == 1 || m.Health == 1)),
                "chosen gameplay stats correct");
            Check(source.AttackDamage == source.Card.ATK && source.Health == source.Card.Health, "Timereaver excludes itself");
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        foreach (var scenario in new[] { (Card: "EX1_160", Branch: 1), (Card: "EX1_160", Branch: 2),
            (Card: "CS2_117", Branch: 0), (Card: "CS2_029", Branch: 0) })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(scenario.Card), zone: owner.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && (scenario.Branch == 0 || t.ChooseOne == scenario.Branch)
                && (t.Target == null || t.Target.Id == owner.Opponent.Hero.Id)));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(data.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 7)
                .PowerStart.SubOption == (scenario.Branch == 0 ? 0 : scenario.Branch - 1), "legacy/control PLAY selector");
            var powers = data.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3).ToArray();
            Check(powers.Length > 0 && powers.All(e => e.PowerStart.SubOption == scenario.Branch - 1), "legacy/control POWER selector");
            Check(!data.Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), "legacy/control has no substitute recipe");
            capture.SerializeAll();
        }
        SecretIngredientSubOptionHistory();
        ChooseOneDecisionSubOptionHistory();
        ChooseOneGroupBoundaries();
        Console.WriteLine("PASS Choose One sub-option wire, Timereaver group/all recipients, Secret Ingredient hero Attack, Choose Both, legacy and non-Choose-One controls; both seats");
    }
    private static void SecretIngredientSubOptionHistory()
    {
        const string prefab = "ReuseFX_Beast_Impact_FangBite_Gold_Super:f25e8e68c2da89040b1477b8c4d989c6";
        foreach (bool bot in new[] { false, true })
        foreach (int branch in new[] { 1, 2 })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("JAIL_201"), zone: owner.HandZone);
            int attackBefore = owner.Hero.AttackDamage, handBefore = owner.HandZone.Count;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && t.ChooseOne == branch));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(data.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 7)
                .PowerStart.SubOption == branch - 1, "Secret Ingredient PLAY selects chosen sub-card");
            var power = data.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3);
            Check(power.PowerStart.SubOption == branch - 1, "Secret Ingredient parent POWER selects chosen branch");
            if (branch == 2)
            {
                Check(!data.Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), "random Druid card branch emits no sub-spell");
                Check(owner.Hero.AttackDamage == attackBefore && owner.HandZone.Count == handBefore,
                    "random Druid card branch replaces played card without hero Attack");
            }
            else
            {
                int powerStart = data.IndexOf(power), powerEnd = powerStart + 1, depth = 1;
                for (; powerEnd < data.Count; powerEnd++)
                {
                    if (data[powerEnd].HasPowerStart) depth++;
                    if (data[powerEnd].HasPowerEnd && --depth == 0) break;
                }
                Check(powerEnd < data.Count, "Secret Ingredient POWER closes");
                var body = data.Skip(powerStart + 1).Take(powerEnd - powerStart - 1).ToList();
                int startIndex = body.FindIndex(e => e.HasSubSpellStart), endIndex = body.FindIndex(e => e.HasSubSpellEnd);
                Check(data.Count(e => e.HasSubSpellStart) == 1 && data.Count(e => e.HasSubSpellEnd) == 1
                    && startIndex >= 0 && endIndex > startIndex, $"one complete Secret Ingredient scope inside parent POWER (bot={bot})");
                var start = body[startIndex].SubSpellStart;
                Check(start.SpellPrefabGuid == prefab && start.SourceEntityId == owner.Hero.Id
                    && start.TargetEntityIds.Count == 0,
                    "Secret Ingredient retains retail prefab, public hero origin and zero targets");
                var enchantment = body.Single(e => e.HasShowEntity && e.ShowEntity.Name == "JAIL_201ae1");
                int creation = body.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == enchantment.ShowEntity.Entity);
                Check(creation > startIndex && creation < body.IndexOf(enchantment) && body.IndexOf(enchantment) < endIndex,
                    "Secret Ingredient enchantment FULL and SHOW enclosed");
                Check(body.Skip(startIndex + 1).Take(endIndex - startIndex - 1).Any(e => e.HasTagChange
                    && e.TagChange.Entity == owner.Hero.Id && e.TagChange.Tag == (int)GameTag.ATK
                    && e.TagChange.Value == attackBefore + 2) && owner.Hero.AttackDamage == attackBefore + 2,
                    "Secret Ingredient scope wraps actual hero Attack gain");
            }
            Check(!NativeAdmission.ValidatePackets(capture.HistorySnapshots()
                .Select(p => (System.Collections.Generic.IReadOnlyList<PegasusGame.PowerHistoryData>)p.List)).Any(f => f.Rule == "R1"),
                "Secret Ingredient respects effect-specific target admission");
            capture.SerializeAll();
        }
    }
    private static void ChooseOneDecisionSubOptionHistory()
    {
        var definition = Cards.FromId("EX1_160");
        var original = definition.Power;
        try
        {
            definition.Power = new SabberStoneCore.Enchants.Power {
                PowerTask = new SabberStoneCore.Tasks.SimpleTasks.DecisionSpellTask("proof/native/choose-one/v1",
                    new SabberStoneCore.Tasks.SimpleTasks.ArmorTask(1))
            };
            foreach (bool bot in new[] { false, true })
            foreach (bool automatic in new[] { false, true })
            {
                var (match, capture) = AnimationMatch(botTurn: bot);
                var owner = match.Game.CurrentPlayer;
                var source = (EngineSpell)EngineEntity.FromCard(owner, definition, zone: owner.HandZone);
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                if (automatic) SabberStoneCore.Actions.Generic.CastSpell(owner, match.Game, source, null, 2);
                else InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                    && t.Source?.Id == source.Id && t.ChooseOne == 2));
                FlushFieldHistory(match);
                var powers = History(capture, cursor).Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                    && (int)e.PowerStart.Type == 3).ToArray();
                Check(powers.Length == 1 && powers[0].PowerStart.SubOption == 1 && owner.Hero.Armor == 1,
                    "direct decision recipe and immediate Execute keep chosen parent sub-option");
                capture.SerializeAll();
            }
        }
        finally { definition.Power = original; }
    }

    private static void ChooseOneGroupBoundaries()
    {
        foreach (string boundary in new[] { "complete", "empty", "block", "reset", "flush" })
        {
            var (match, _) = AnimationMatch();
            var game = match.Game; var owner = game.CurrentPlayer; var history = game.PowerHistory;
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId("END_010"), zone: owner.BoardZone);
            var victim = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_101t"), zone: owner.BoardZone);
            history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", 0, 0));
            int cursor = history.Full.Count;
            var visual = typeof(SabberStoneCore.Kettle.PowerHistory).GetMethod("Visual",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var phase = Enum.Parse(typeof(SabberStoneCore.Model.Game).Assembly.GetType("SabberStoneCore.Presentation.VisualPhase"), "BoardBuff");
            using (var scope = (IDisposable)visual.Invoke(history, new object[] { game, phase, source, null }))
            {
                if (boundary != "empty")
                    SabberStoneCore.Actions.Generic.AddEnchantmentBlock(game, Cards.FromId("END_010ae"), source, victim);
                if (boundary == "block")
                {
                    history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
                    history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
                }
                if (boundary == "reset") history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(game));
                if (boundary == "flush")
                {
                    FlushFieldHistory(match);
                    // Regrow Last beyond its old count: count-only flush detection would miss this.
                    for (int i = 0; i < cursor + 10; i++)
                        history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(victim.Id, GameTag.ATK, 1));
                }
            }
            history.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            Check(history.Full.Skip(cursor).Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart)
                == (boundary == "complete" ? 1 : 0), "group suppresses unsafe or empty span: " + boundary);
            if (boundary == "complete")
                Check(history.Last.SequenceEqual(history.Full.Skip(history.Full.Count - history.Last.Count)),
                    "group insertion matches Full and Last");
        }
    }

}
