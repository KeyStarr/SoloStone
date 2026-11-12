using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-either-side-play", ProofKind.PoweredUp)]
    private static void RunB17EitherSideGlow()
    {
        foreach (string id in new[] { "CAP_004", "CORE_DAL_575", "JAIL_442", "JAIL_452", "JAIL_455", "JAIL_461" })
        {
            var f = new Fixture(); var card = f.Draw(id); f.Flush();
            Check(card.Card.Implemented, id + " implemented"); f.Expected(card, 0, id + " unconditional");
        }
        Check(Cards.FromId("TLC_522").Combo && !PoweredUp.HasCondition(Cards.FromId("TLC_522")), "Opu uses ordinary native Combo glow");
        foreach (bool active in new[] { false, true })
        {
            var f=new Fixture(); var card=f.Draw("TLC_522"); f.Player.IsComboActive=active;
            f.Process(PlayCardTask.Any(f.Player,card,zonePosition:0));
            Check(f.Player.HandZone.Count==(active?2:1), "native Opu outcome follows Combo state");
        }
    }

    [GroupProof("b17-either-side-play-native", ProofKind.Native)]
    private static void Unpinned_ExecutionerSideOptionEncoding()
    {
        Program.B17SideCommands();
        B17HiddenCards();
        var f = new Fixture(); var p = f.Player; var card = f.Draw("JAIL_461");
        var options = PowerOptionsBuilder.AllOptions(f.Game, p.Options());
        var index = options.PowerOptionList.FindIndex(o => o.MainOption?.EntityId == card.Id);
        var option = options.PowerOptionList[index];
        Check(option.SubOptions.Count == 2, "side suboptions advertised");
        Check(option.SubOptions.Select(o => f.Game.IdEntityDic[o.EntityId].Card.Id)
            .SequenceEqual(new[] { "JAIL_461a", "JAIL_461b" }), "exported side identities ordered friendly/enemy");
        var resolve = typeof(NativeMatch).GetMethod("ResolveTask", BindingFlags.Instance | BindingFlags.NonPublic);
        var task = (PlayCardTask)resolve.Invoke(f.Match, new object[] { options,index,0,1,1 });
        Check(task.ChooseOne == 2 && task.ZonePosition == 0, "native side and one-based position decoded once");
        f.Process(task);
        Check(card.Controller == p.Opponent && card.Zone == p.Opponent.BoardZone, "destination control projected");
        Check(f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.CONTROLLER] == p.Opponent.PlayerId,
            "ordinary CONTROLLER identifies enemy board");
    }
    private static void B17HiddenCards()
    {
        var f = new Fixture(); var enemy = f.Player.Opponent;
        var hidden = Generic.DrawCard(enemy, Cards.FromId("JAIL_442"));
        var ids = hidden.ChooseOnePlayables.Select(c=>c.Id).Append(hidden.Id).ToArray();
        f.Flush();
        foreach (var entry in f.Entries)
        {
            var entity=entry.HasFullEntity?entry.FullEntity:entry.HasShowEntity?entry.ShowEntity:entry.HasChangeEntity?entry.ChangeEntity:null;
            if(entity!=null && ids.Contains(entity.Entity))
                Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t=>t.Name==(int)GameTag.ENTITY_ID || t.Name==(int)GameTag.CONTROLLER
                    || t.Name==(int)GameTag.ZONE || t.Name==(int)GameTag.ZONE_POSITION), "hidden side helpers never reveal parent identity");
        }
        var doctor=(Minion)f.Draw("JAIL_442");
        f.Process(PlayCardTask.Any(f.Player,doctor,zonePosition:0,chooseOne:2));
        doctor.ToBeDestroyed=true; f.Game.DeathProcessingAndAuraUpdate();
        var blights=enemy.DeckZone.Where(c=>c.Card.Id=="JAIL_443t").Select(c=>c.Id).ToArray();
        Check(blights.Length==4,"Doctor generated four hidden Blights"); f.Flush();
        foreach(var entry in f.Entries)
        {
            var entity=entry.HasFullEntity?entry.FullEntity:entry.HasShowEntity?entry.ShowEntity:entry.HasChangeEntity?entry.ChangeEntity:null;
            if(entity!=null && blights.Contains(entity.Entity))
                Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t=>t.Name==(int)GameTag.ENTITY_ID || t.Name==(int)GameTag.CONTROLLER
                    || t.Name==(int)GameTag.ZONE || t.Name==(int)GameTag.ZONE_POSITION), "opponent receives no generated deck identity");
            if(entry.HasTagChange && blights.Contains(entry.TagChange.Entity))
                Check(entry.TagChange.Tag==(int)GameTag.ENTITY_ID || entry.TagChange.Tag==(int)GameTag.CONTROLLER
                    || entry.TagChange.Tag==(int)GameTag.ZONE || entry.TagChange.Tag==(int)GameTag.ZONE_POSITION, "no hidden deck detail tags");
        }
    }

}
