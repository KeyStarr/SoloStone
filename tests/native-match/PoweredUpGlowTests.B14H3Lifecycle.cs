using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-h3-lifecycle", ProofKind.PoweredUp)]
    private static void H3Glow()
    {
        var f = new Fixture();
        foreach (string id in new[] {"TIME_049","TIME_055","DINO_430","TIME_103","CATA_190h","END_006","EDR_804"})
        {
            f.ClearHand(); var card = f.Draw(id);
            Check(!PoweredUp.IsMet(card),"H3 unconditional/scaling/target-limited card has no glow: "+id);
        }
    }
    [GroupProof("native-b14-h3-lifecycle", ProofKind.Native)]
    private static void H3Privacy()
    {
        foreach (bool opponent in new[] {false,true})
        foreach (string id in new[] {"DINO_430","CATA_190h","TIME_103","EDR_804","END_006"})
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(f.Player)),"H3 opponent turn");
            var owner = opponent ? g.Player2 : g.Player1;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var hidden = Generic.DrawCard(owner,Cards.FromId("CS2_029"));
            var source = Generic.DrawCard(owner,Cards.FromId(id));
            if (id == "EDR_804") f.Summon("EDR_851t",owner);
            f.Flush(); owner.BaseMana = 10; owner.UsedMana = 0;
            var target = id == "EDR_804" ? owner.BoardZone.Single() : null;
            Check(g.Process(PlayCardTask.Any(owner,source,target)),"H3 native play "+id);
            var offers = owner.Choice?.Choices.ToArray() ?? Array.Empty<int>(); f.Flush();
            if (opponent)
                Check(f.Entries.SelectMany(e => new[] {e.FullEntity,e.ShowEntity,e.ChangeEntity})
                    .Where(e => e != null && offers.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name)),"H3 opponent offers private");
            if (offers.Length != 0)
            {
                var d = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[] {offers[0]}),out _),"H3 native choice"); f.Flush();
            }
            if (id == "DINO_430")
            {
                var specialist = Generic.DrawCard(owner,Cards.FromId("CAP_804")); owner.UsedMana = 0;
                Check(g.Process(PlayCardTask.Any(owner,specialist,(Minion)source)),"H3 native Reborn grant");
                ((Minion)source).Destroy(); g.DeathProcessingAndAuraUpdate(); f.Flush();
                var returned = owner.BoardZone.Single(m => m.Card.Id == id);
                Check(PayloadRules.Entries(returned,1).Length == 1,"H3 native Reborn retains memory");
                foreach (var body in owner.BoardZone.ToArray()) owner.SetasideZone.Add(owner.BoardZone.Remove(body));
                f.Summon("EDR_892",owner).Destroy(); g.DeathProcessingAndAuraUpdate(); f.Flush();
                Check(owner.BoardZone.Count(m => m.Card.Id == id && PayloadRules.Entries(m,1).Length == 1) == 2,
                    "H3 native Felbat copies retain memory");
            }
            var payload = g.IdEntityDic.Values.Where(PayloadRules.IsStored).Select(x => x.Id).ToArray();
            var snapshots = f.Entries.SelectMany(e => new[] {e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e => e != null).ToArray();
            Check(snapshots.Where(e => payload.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name)),"H3 stored definitions never revealed");
            if (opponent) Check(snapshots.Where(e => e.Entity == hidden.Id).All(e => string.IsNullOrEmpty(e.Name)),"H3 unrelated hidden card private");
            bool Private(int tag) => PayloadRules.IsInternalTag((GameTag)tag) || Generic.IsDecisionInternalTag((GameTag)tag);
            Check(snapshots.All(e => e.Tags.All(t => !Private(t.Name))) && f.Entries.Where(e => e.HasTagChange).All(e => !Private(e.TagChange.Tag)),
                "H3 private payload/continuation tags filtered");
        }
        Console.WriteLine("PASS H3 native: both seats, choices, payloads, hidden hand, private tags");
    }
}
