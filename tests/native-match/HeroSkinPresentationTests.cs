using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;

internal static partial class Program
{
    private static void HeroSkinPresentationTests()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior", "mage", 4242) { DebugScenario = DebugScenario.Parse("humanSkin=HERO_01bb") };
        match.Start();
        var tags = match.SnapshotVisibleTransportTags(match.Game.Player1.Hero);
        Check(tags.TryGetValue(3211, out int value) && value == 1, "Ragnaros transport must preserve static attack tag instead of Garrosh zero");
        Check(tags.TryGetValue(3564, out value) && value == 1, "Ragnaros corner presentation belongs to displayed skin");
        Check(match.Game.Player1.Hero[(GameTag)3211] == 0, "skin metadata never changes engine rules");
        match.StopNpc();
        Console.WriteLine("PASS hero skin presentation tags");
        HeroSkinAuditIdentities();
        AllHeroSkinMetadata();
        HeroSkinAttackScenarios();
        HeroSkinPowerScenarios();
        HeroSkinPowerHistory();
        HeroSkinConcedeBoundaries();
        var invalidCapture = new Capture();
        var invalid = new NativeMatch(invalidCapture.Send, "mage", "mage", 4242) {
            DebugScenario = DebugScenario.Parse("humanSkin=HERO_01bb\nnpcSkin=HERO_01bb") };
        invalid.Start();
        Check(invalid.DebugScenario == null && invalid.Game.Player2.Hero.Card.Id == "HERO_08"
            && invalid.EngineStartOpponentHero == "HERO_08", "rejected skin fixture cannot partially override the NPC class");
        invalid.StopNpc();
    }
}

internal static partial class Program
{
    private static void AllHeroSkinMetadata()
    {
        var project = typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", BindingFlags.NonPublic | BindingFlags.Instance);
        var refresh = typeof(NativeMatch).GetMethod("PresentedStats", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var group in HeroSkinData.All.Values.GroupBy(s => s.BaseHero))
        {
            var first = group.First(); var capture = new Capture();
            var match = new NativeMatch(capture.Send, SabberStoneCore.Model.Cards.FromId(group.Key),
                Enumerable.Repeat(SabberStoneCore.Model.Cards.FromId("CS2_231"), 30), 4242) {
                DebugScenario = DebugScenario.Parse("humanSkin=" + first.Id + "\nnpcSkin=" + first.Id) };
            match.Start();
            foreach (var definition in group)
            {
                match.DebugScenario = DebugScenario.Parse("humanSkin=" + definition.Id + "\nnpcSkin=" + definition.Id);
                // This metadata matrix deliberately changes fixture cosmetics between rows. Production captures once at Start.
                typeof(NativeMatch).GetMethod("CaptureCosmeticPowerCards", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(match, null);
                foreach (var player in new[] { match.Game.Player1, match.Game.Player2 })
                {
                    var frozen = PowerHistoryBuilder.ShowEntity(player.Hero).Entity;
                    var wire = (PegasusGame.PowerHistoryEntity)project.Invoke(match, new object[] { frozen, true, true, null, false });
                    Check(wire.Name == definition.Id, "skin identity both seats: " + definition.Id);
                    foreach (int tag in HeroSkinData.Tags)
                        Check((wire.Tags.FirstOrDefault(t => t.Name == tag)?.Value ?? 0) == definition.Tag(tag),
                            "skin definition tag both seats: " + definition.Id + ":" + tag);
                    var refreshed = ((System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<GameTag, int>>)refresh.Invoke(match,
                        new object[] { player.Hero })).GroupBy(p => (int)p.Key).ToDictionary(g => g.Key, g => g.Last().Value);
                    foreach (int tag in HeroSkinData.Tags)
                        Check(refreshed[tag] == definition.Tag(tag), "refresh must not erase skin metadata: " + definition.Id);
                    Check(player.Hero.Card.Id == group.Key && player.Hero[(GameTag)3211] == 0, "skin cannot mutate engine class or presentation flags");
                    var power = player.Hero.HeroPower;
                    if (power != null && definition.Id != group.Key && definition.Power.Length != 0)
                    {
                        var powerSnapshot = PowerHistoryBuilder.ShowEntity(power).Entity;
                        var powerWire = (PegasusGame.PowerHistoryEntity)project.Invoke(match, new object[] { powerSnapshot, true, true, null, false });
                        Check(powerWire.Name == definition.Power, "skin base power both seats " + definition.Id);
                        int upgrade = power.Card[(GameTag)1086];
                        if (upgrade != 0 && definition.UpgradedPower.Length != 0)
                        {
                            powerSnapshot.Name = SabberStoneCore.Model.Cards.FromAssetId(upgrade).Id;
                            powerWire = (PegasusGame.PowerHistoryEntity)project.Invoke(match, new object[] { powerSnapshot, true, true, null, false });
                            Check(powerWire.Name == definition.UpgradedPower, "skin upgraded power both seats " + definition.Id);
                        }
                        powerSnapshot.Name = "EX1_625t"; // Mind Spike is a replacement, never a class skin power.
                        powerWire = (PegasusGame.PowerHistoryEntity)project.Invoke(match, new object[] { powerSnapshot, true, true, null, false });
                        Check(powerWire.Name == NativeMatch.TransportCardId("EX1_625t"), "replacement power keeps its definition");
                    }
                }
            }
            match.StopNpc();
        }
        Console.WriteLine("PASS all " + HeroSkinData.All.Count + " pinned skins: snapshots, refresh, both seats, powers/upgrades/replacements");
    }

    private static void HeroSkinAttackScenarios()
    {
        foreach (string weapon in new[] { "none", "TLC_478" })
        foreach (string secret in new[] { "none", "CORE_EX1_289", "EX1_610" })
        {
            var capture = new Capture();
            var scenario = DebugScenario.Parse("humanSkin=HERO_01bb\nheroAttack=human\nattackWeapon=" + weapon
                + "\nattackSecret=" + secret + "\nattackArmor=2");
            Check(DebugScenario.Parse(scenario.Canonical()).Canonical() == scenario.Canonical(), "attack scenario roundtrip");
            var match = new NativeMatch(capture.Send, "warrior", "mage", 4242) { DebugScenario = scenario };
            match.Start(); match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new System.Collections.Generic.List<int>());
            Check(match.DebugScenarioApplied != null && !match.DebugScenarioApplied.StartsWith("FAILED"), "attack fixture applied atomically");
            var options = capture.Last<PegasusGame.AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == match.Game.Player1.Hero.Id);
            // Use the real transport option: fixture never synthesizes an ATTACK block.
            Check(index >= 0, "fixture offers legal hero attack " + weapon + "/" + secret);
            int cursor = capture.Packets.Count;
            match.SendOption(options.Id, index, match.Game.Player2.Hero.Id, -1, 0);
            var history = capture.HistorySnapshots(cursor).SelectMany(h => h.List).ToArray();
            Check(history.Any(h => h.HasPowerStart && (int)h.PowerStart.Type == (int)BlockType.ATTACK), "real attack history emitted");
            if (secret == "CORE_EX1_289") Check(match.Game.Player2.SecretZone.Count == 0 && match.Game.Player2.Hero.Armor >= 7,
                "Ice Barrier actually triggers before attack damage");
            Check(match.Game.Player1.Hero.Card.Id == "HERO_01", "native skin attack keeps engine hero");
            match.StopNpc();
        }
        foreach (bool lethal in new[] { false, true })
        {
            var capture = new Capture(); double now = 0;
            var match = new NativeMatch(capture.Send, "mage", "warrior", 4242) {
                DebugScenario = DebugScenario.Parse("npcSkin=HERO_01bb\nheroAttack=npc\nattackWeapon=TLC_478\nattackLethal=" + (lethal ? "1" : "0")
                    + (lethal ? "" : "\nattackSecret=CORE_EX1_289")),
                NativeNpcIdle = () => true, NpcClock = () => now };
            match.Start(); match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new System.Collections.Generic.List<int>());
            var options = capture.Last<PegasusGame.AllOptions>(14);
            int cursor = capture.Packets.Count;
            match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
            for (int frame = 0; frame < 200 && match.Game.Player2.Hero.NumAttacksThisTurn == 0 && match.Game.State != State.COMPLETE; frame++)
            { now += 500; match.TickNpc(); }
            Check(capture.HistorySnapshots(cursor).SelectMany(h => h.List).Any(h => h.HasPowerStart
                && (int)h.PowerStart.Type == (int)BlockType.ATTACK && h.PowerStart.Source == match.Game.Player2.Hero.Id), "NPC scenario drives an actual hero attack");
            if (lethal) Check(match.Game.State == State.COMPLETE, "NPC scenario killing blow is real engine terminal");
            else Check(match.Game.Player1.SecretZone.Count == 0 && match.Game.Player1.Hero.Armor >= 6, "NPC Ice Barrier scenario matches original freeze");
            match.StopNpc();
        }
        Console.WriteLine("PASS hero skin attack fixtures: weapon/no weapon, armor, Ice Barrier/Explosive Trap");
    }

    private static void HeroSkinPowerScenarios()
    {
        foreach (string seat in new[] { "human", "npc" })
        foreach (bool upgraded in new[] { false, true })
        {
            var capture = new Capture(); double now = 0;
            var scenario = DebugScenario.Parse(seat + "Skin=HERO_01bb\nheroPower=" + seat
                + (upgraded ? "\nheroPowerUpgrade=" + seat : ""));
            Check(DebugScenario.Parse(scenario.Canonical()).Canonical() == scenario.Canonical(), "power fixture roundtrip");
            var match = new NativeMatch(capture.Send, "warrior", "warrior", 4242) {
                DebugScenario = scenario, NativeNpcIdle = () => true, NpcClock = () => now };
            match.Start(); match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new System.Collections.Generic.List<int>());
            var options = capture.Last<PegasusGame.AllOptions>(14);
            var player = seat == "human" ? match.Game.Player1 : match.Game.Player2;
            if (seat == "human")
            {
                int index = options.Options.FindIndex(o => o.MainOption?.Id == player.Hero.HeroPower.Id);
                Check(index >= 0, "human power fixture offers real native option");
                match.SendOption(options.Id, index, 0, -1, 0);
            }
            else
            {
                match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
                for (int frame = 0; frame < 200 && player.Hero.Armor == 0; frame++) { now += 500; match.TickNpc(); }
            }
            Check(player.Hero.Armor == (upgraded ? 4 : 2), "both seats drive actual base/upgraded warrior power");
            match.StopNpc();
        }
        Console.WriteLine("PASS both seats: real base/upgraded hero-power fixture actions");
    }
}
