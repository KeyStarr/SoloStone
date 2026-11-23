using System;
using System.Collections.Generic;
using System.Linq;
using NativeHistory = PegasusGame.PowerHistoryData;

// Options > Show Opponents' Pets off (NativeOpponentPetsOption.Gate) starts the match with OpponentPetCard = null. The same scripted
// game with and without the NPC pet: no NPC pet entity, link or reaction when hidden, and the player's pet sees exactly the same
// reactions, likes, treats and XP either way.
internal static partial class Program
{
    private sealed class PetRun
    {
        internal int PetEntities, HumanPet;
        internal bool NpcLinked, OtherReactions;
        internal List<(int Tag, int Value)> HumanPetTags = new List<(int, int)>();
        internal LocalPetXpCounts Xp;
        internal string Fired;
    }

    private static PetRun ScriptedPetRun(string pet, string npcPet)
    {
        using var fixture = new LikeMatchFixture(true, pet);
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "warrior", 1337) { OpponentPetCard = npcPet }; match.Start();
        var initial = NativeHistory(capture);
        var run = new PetRun {
            PetEntities = initial.Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true),
            HumanPet = initial.Single(e => e.HasFullEntity && e.FullEntity.Name == pet).FullEntity.Entity,
            NpcLinked = initial.Single(e => e.HasCreateGame).CreateGame.Players.Single(p => p.Id == 2).Entity.Tags.Any(t => t.Name == 4017 || t.Name == 4037)
        };
        match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
        int cursor = capture.Packets.Count;
        var game = match.Game; var human = game.Player1;
        game.PowerHistory.Last.Clear();
        game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
        game.Player2.Hero.TakeDamage(game.Player1.Hero, 5);
        game.Player2.Hero.TakeHeal(game.Player2.Hero, 4);
        FlushHistory(match);
        human.BaseMana = 10; human.UsedMana = 0;
        var fireball = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
        FlushHistory(match);
        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, fireball, game.Player2.Hero)), "Fireball accepted");
        FlushHistory(match);
        Check(match.ReceiveNativePetTreat(67), "a treat is fed");
        game.PowerHistory.Last.Clear(); game.Turn = 4; FlushHistory(match);
        var history = capture.Packets.Skip(cursor).Where(p => p.Id == 19).SelectMany(p => ((PegasusGame.PowerHistory)p.Message).List).ToList();
        run.HumanPetTags = history.Where(e => e.HasTagChange && e.TagChange.Entity == run.HumanPet).Select(e => (e.TagChange.Tag, e.TagChange.Value)).ToList();
        run.OtherReactions = history.Any(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Entity != run.HumanPet);
        run.Xp = match.PetXpCounts; run.Fired = string.Join(",", match.PetLikes.Fired.OrderBy(like => like, StringComparer.Ordinal));
        return run;
    }

    private static void OpponentPetsHidden()
    {
        string pet = PetFamilyWithNative(9);
        var shown = ScriptedPetRun(pet, "PET_4_3");
        var hidden = ScriptedPetRun(pet, null);
        Check(shown.PetEntities == 2 && shown.NpcLinked && shown.OtherReactions, "control run: the NPC pet exists, is linked and reacts");
        Check(hidden.PetEntities == 1 && !hidden.NpcLinked && !hidden.OtherReactions, "hidden: no NPC pet entity, player link or reaction");
        Check(hidden.HumanPet == shown.HumanPet, "the player's pet keeps its entity id");
        Check(hidden.HumanPetTags.SequenceEqual(shown.HumanPetTags) && hidden.HumanPetTags.Any(t => t.Tag == 4038) && hidden.HumanPetTags.Any(t => t.Tag >= 4076 && t.Tag <= 4078),
            "the player's pet gets the same reactions, treat and XP tags: " + string.Join(";", hidden.HumanPetTags) + " vs " + string.Join(";", shown.HumanPetTags));
        Check(hidden.Fired == shown.Fired && hidden.Fired.Length > 0, "the same likes fire: " + hidden.Fired + " vs " + shown.Fired);
        Check(hidden.Xp.Small == shown.Xp.Small && hidden.Xp.Medium == shown.Xp.Medium && hidden.Xp.Large == shown.Xp.Large && hidden.Xp.Medium > 0,
            "the player's pet XP counts are unchanged");
        Console.WriteLine("PASS opponent pets hidden: no NPC pet entity, link or reaction; the player's pet reactions, likes, treat and XP unchanged");
    }
}
