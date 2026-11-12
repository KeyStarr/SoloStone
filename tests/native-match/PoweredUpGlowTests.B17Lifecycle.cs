using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-lifecycle", ProofKind.PoweredUp)]
    private static void RunB17LifecyclePoweredUp()
    {
        var f = new Fixture();
        foreach (string id in new[] { "BE_143", "CAP_800", "CATA_307", "MEND_900", "MEND_801", "TIME_042", "TIME_618", "TIME_620" })
        {
            f.ClearHand();
            var card = f.Draw(id); f.Flush();
            Check(!PoweredUp.HasCondition(card.Card), id + " has no conditional pre-play bonus");
            f.Expected(card, 0, id + " uses ordinary native keywords");
        }
        Console.WriteLine("PASS lifecycle unconditional cards have no PoweredUp glow");
    }

    [GroupProof("native-b17-lifecycle", ProofKind.Native)]
    private static void RunB17LifecycleNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var f=new Fixture(); if(opponent) f.Process(EndTurnTask.Any(f.Player));
            var p=opponent?f.Game.Player2:f.Player;
            foreach(var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
            SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CORE_CS2_093"),zone:p.DeckZone);
            var source=Generic.DrawCard(p,Cards.FromId("BE_143"));
            p.BaseMana=10;p.UsedMana=0;f.Process(PlayCardTask.Any(p,source));
            var copy=p.DeckZone.Single(c=>c[GameTag.CASTSWHENDRAWN]>0);
            f.Flush();
            var hidden=p.DeckZone.Select(c=>c.Id).Concat(p.DeckZone.SelectMany(c=>c.AppliedEnchantments == null ? Enumerable.Empty<Enchantment>() : c.AppliedEnchantments).Select(e=>e.Id)).ToArray();
            var entries=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null && hidden.Contains(e.Entity)).ToArray();
            Check(entries.All(e=>string.IsNullOrEmpty(e.Name)),"deck copies and their grants never reveal hidden identities");
            Generic.AddHandPhase(p,p.DeckZone.Remove(copy));f.Flush();
            if(opponent) Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity})
                .Where(e=>e!=null && e.Entity==copy.Id).All(e=>string.IsNullOrEmpty(e.Name)),"opponent put-in-hand identity remains private");
            else Check(f.Match.SnapshotVisibleTransportTags(copy).TryGetValue((int)GameTag.CASTSWHENDRAWN,out int keyword) && keyword==1,
                "owner receives granted Cast When Drawn keyword");
        }
        var recruit=new Fixture(); var owner=recruit.Player;
        var savior=recruit.Draw("MEND_801");recruit.Process(PlayCardTask.Any(owner,savior));
        var ping=recruit.Draw("CS2_008"); recruit.Process(PlayCardTask.Any(owner,ping,(Minion)savior));
        recruit.Flush();
        Check(recruit.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==owner.Id && e.TagChange.Tag==4806 && e.TagChange.Value==1),
            "retail SilverHandCardTextBuilder receives controller HP buff 4806");
        var team=recruit.Draw("MEND_900");recruit.Process(PlayCardTask.Any(owner,team));
        Check(owner.BoardZone.Where(m=>m.Card.Id=="CS2_101t").All(m=>recruit.Match.SnapshotVisibleTransportTags(m)[(int)GameTag.HEALTH]==2),"Recruit stats projected through actual buff layer");
        var transferred = owner.BoardZone.First(m=>m.Card.Id=="CS2_101t");
        recruit.Process(EndTurnTask.Any(owner));
        owner.Opponent.BaseMana=10; owner.Opponent.UsedMana=0;
        var enemySavior = (Minion)Generic.DrawCard(owner.Opponent, Cards.FromId("MEND_801"));
        recruit.Process(PlayCardTask.Any(owner.Opponent, enemySavior));
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET)
            .Process(recruit.Game, owner.Opponent, enemySavior, transferred);
        var enemyPing = Generic.DrawCard(owner.Opponent, Cards.FromId("CS2_008"));
        recruit.Process(PlayCardTask.Any(owner.Opponent, enemyPing, enemySavior));
        Check(recruit.Match.SnapshotVisibleTransportTags(transferred)[(int)GameTag.HEALTH]==3,
            "transferred Recruit projects new owner's independent Health increment");
        Check(recruit.Match.SnapshotVisibleTransportTags(transferred).Keys.All(t=>t<31287 || t>31289),
            "both controller application counts omitted from live snapshot");
        Check(recruit.Entries.Where(e=>e.HasTagChange).All(e=>e.TagChange.Tag<31287 || e.TagChange.Tag>31289)
            && recruit.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null)
                .SelectMany(e=>e.Tags).All(t=>t.Name<31287 || t.Name>31289),"Recruit bookkeeping never projected");
        foreach (string id in new[] { "MEND_900", "TIME_620", "TIME_042" })
        {
            var privateHand = new Fixture(); privateHand.Process(EndTurnTask.Any(privateHand.Player));
            var enemy = privateHand.Game.Player2;
            foreach (var held in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(held));
            enemy.BaseMana=10; enemy.UsedMana=0;
            var card=Generic.DrawCard(enemy,Cards.FromId(id));
            privateHand.Process(PlayCardTask.Any(enemy,card));
            var hiddenIds=enemy.HandZone.Concat(enemy.SecretZone.Cast<IPlayable>()).Select(c=>c.Id).ToArray();
            Check(hiddenIds.Length>0,"privacy fixture must create a hidden entity: "+id);
            Check(privateHand.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity})
                .Where(e=>e!=null && hiddenIds.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),
                "opponent generated Recruits/Banana and installed Secret stay private");
        }
        var death = new Fixture();
        var abomination=(Minion)death.Draw("EX1_097");death.Process(PlayCardTask.Any(death.Player,abomination));
        var secret=death.Draw("TIME_620");death.Process(PlayCardTask.Any(death.Player,secret));
        death.Process(EndTurnTask.Any(death.Player));
        var caster=death.Game.Player2;caster.BaseMana=10;caster.UsedMana=0;
        var fireball=Generic.DrawCard(caster,Cards.FromId("CS2_029"));
        death.Process(PlayCardTask.Any(caster,fireball,abomination));
        Check(death.Player.SecretZone.Count==0 && death.Player.BoardZone.Single().Health==4,
            "native death continuation follows old Deathrattle and leaves fresh 4/4");
        Check(death.Entries.Where(e=>e.HasTagChange).All(e=>e.TagChange.Tag!=31288)
            && death.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null)
                .SelectMany(e=>e.Tags).All(t=>t.Name!=31288),"pending Secret subject is internal");
        Console.WriteLine("PASS native b17 lifecycle privacy and visible state");
    }
}
