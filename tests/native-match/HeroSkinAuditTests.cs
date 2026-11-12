using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    // The plugin's test-control visible-state audit used its own copy of the hero-skin mapping and ignored scenario skins and
    // per-seat skin powers, so an NPC with a scenario skin stalled mulligan settlement on "identity:6/7". The audit must
    // accept exactly what NativeMatch projects; these cases keep the two from drifting apart.
    private static void CheckSkinAuditIdentity(NativeMatch match, Capture capture, IPlayable entity, string expected, string label)
    {
        var projected = capture.HistorySnapshots().SelectMany(history => history.List)
            .Select(row => row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.HasChangeEntity ? row.ChangeEntity : null)
            .Last(row => row?.Entity == entity.Id);
        Check(projected.Name == expected, label + ": projected identity is " + expected);
        // Exact identity expectation used by TestControlVisibleDiff, extracted from production.
        Check(TestControlPresentationCardId(match, entity) == projected.Name,
            label + ": audit must accept the projected identity instead of reporting identity:" + entity.Id);
    }

    private static void HeroSkinAuditIdentities()
    {
        foreach (string seat in new[] { "npc", "human" })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "warrior", "warrior", 4242) {
                DebugScenario = DebugScenario.Parse(seat + "Skin=HERO_01bb\nheroAttack=" + seat) };
            try
            {
                match.Start();
                foreach (bool afterMulligan in new[] { false, true })
                {
                    if (afterMulligan) match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
                    foreach (var player in new[] { match.Game.Player1, match.Game.Player2 })
                    {
                        bool skinned = (player == match.Game.Player1) == (seat == "human");
                        string label = seat + " scenario, seat " + player.PlayerId + ", afterMulligan=" + afterMulligan;
                        CheckSkinAuditIdentity(match, capture, player.Hero, skinned ? "HERO_01bb" : "HERO_01", label + " hero");
                        CheckSkinAuditIdentity(match, capture, player.Hero.HeroPower,
                            skinned ? "HERO_01bbhp" : NativeMatch.TransportCardId(player.Hero.HeroPower.Card.Id), label + " power");
                    }
                }
            }
            finally { match.StopNpc(); }
        }

        var npc = NpcCatalog.All.First(profile => profile.DeckId == "npc-dragon-warrior");
        var npcCapture = new Capture();
        var npcMatch = new NativeMatch(npcCapture.Send, "mage", npc.DeckId, 4242) {
            PresentationSettings = new NpcPresentationSettings { Skin = true } };
        try
        {
            npcMatch.Start();
            npcMatch.SendChoices(npcCapture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            CheckSkinAuditIdentity(npcMatch, npcCapture, npcMatch.Game.Player2.Hero, npc.HeroSkin, "NPC profile hero");
            CheckSkinAuditIdentity(npcMatch, npcCapture, npcMatch.Game.Player2.Hero.HeroPower, npc.HeroSkinPower, "NPC profile power");
        }
        finally { npcMatch.StopNpc(); }

        using var fixture = new CosmeticsFixture();
        string path = Path.Combine(Path.GetTempPath(), "hero-skin-audit-" + Guid.NewGuid() + ".xml");
        var previousProfile = NativeMatch.CosmeticsProfile;
        try
        {
            using var profile = new LocalProfile(path, new ProfileOptions { AllCosmetics = true });
            profile.SetFavoriteHeroSkin("HERO_08", "HERO_08a");
            NativeMatch.CosmeticsProfile = () => profile;
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 4242);
            try
            {
                match.Start();
                match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
                CheckSkinAuditIdentity(match, capture, match.Game.Player1.Hero, "HERO_08a", "human favourite hero");
                CheckSkinAuditIdentity(match, capture, match.Game.Player1.Hero.HeroPower, HeroSkinData.All["HERO_08a"].Power, "human favourite power");
            }
            finally { match.StopNpc(); }
        }
        finally
        {
            NativeMatch.CosmeticsProfile = previousProfile;
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + "*")) File.Delete(file);
        }
        Console.WriteLine("PASS real skin audit identities: scenario NPC/human hero + power, NPC profile, human favourite");
    }
}
