using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("b18-t-generated-native", ProofKind.Native)]
    private static void B18GeneratedNative()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (bool potion in new[] { false, true }) {
            var f = new Fixture(); var g = f.Game; var owner = opponent ? g.Player2 : g.Player1;
            var definition = potion
                ? GeneratedDefinitionRules.Potion(Cards.FromId("CFM_621t2"), Cards.FromId("CFM_621t3"))
                : GeneratedDefinitionRules.Creation(Cards.FromId("NEW1_009"), Cards.FromId("EX1_575"));
            owner[PayloadRules.CoinReplacement] = 127154;
            var card = Generic.DrawCard(owner, definition); f.Flush();
            var actors = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
                .Where(e => e != null && e.Entity == card.Id).ToArray();
            bool Hidden(int tag) => tag == 50 || tag == 49 || tag == 53 || tag == 263;
            if (opponent) {
                Check(actors.All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => Hidden(t.Name))),
                    "opponent generated hand hides identity, components, stats and text builder");
                Check(f.Entries.Where(e => e.HasTagChange && e.TagChange.Entity == card.Id)
                    .All(e => Hidden(e.TagChange.Tag)), "hidden generated changes never leak component IDs");
            } else {
                Check(actors.Any(e => e.Name == definition.Id && e.Tags.Any(t => t.Name == 782 && t.Value == (potion ? 3 : 14))
                    && e.Tags.Any(t => t.Name == 471 && t.Value == definition[GameTag.MODULAR_ENTITY_PART_1])
                    && e.Tags.Any(t => t.Name == 472 && t.Value == definition[GameTag.MODULAR_ENTITY_PART_2])),
                    "owner gets pinned builder and ordered component dbfIds in initial definition: "+string.Join(";",actors.Select(e=>e.Name+":"+string.Join(",",e.Tags.Select(t=>t.Name+"="+t.Value)))));
            }
            Check(f.Entries.All(e => !e.HasTagChange || !PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),
                "private controller Coin policy never reaches native history");
            var inDeck = SabberStoneCore.Model.Entities.Entity.FromCard(owner, definition, zone: owner.DeckZone); f.Flush();
            Check(f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
                .Where(e => e != null && e.Entity == inDeck.Id)
                .All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => Hidden(t.Name))),
                "generated-to-deck definition remains private");
        }
        {
            var f=new Fixture();var g=f.Game;
            Check(g.Process(EndTurnTask.Any(g.Player1)),"opponent Creation setup");
            var owner=g.Player2;owner.BaseMana=10;owner.UsedMana=0;
            var victor=Generic.DrawCard(owner,Cards.FromId("CATA_470"));
            Check(g.Process(PlayCardTask.Any(owner,victor)),"opponent Victor play accepted");f.Flush();
            for(int i=0;i<2;i++) {
                var offers=owner.Choice.Choices.ToArray();
                Check(offers.Length>0,"private component prompt actually exists");
                var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity})
                    .Where(e=>e!=null && offers.Contains(e.Entity)).ToArray();
                Check(actors.All(e=>string.IsNullOrEmpty(e.Name) && e.Tags.All(t=>t.Name==50 || t.Name==49 || t.Name==53 || t.Name==263)),
                    "opponent cannot see either component offer");
                var pending=g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(pending.Id,pending.ControllerId,new[]{offers[0]}),out _),"private component answered");f.Flush();
            }
            var creation=owner.HandZone.Single(c=>c.Card.Id=="CATA_470t1");
            Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity})
                .Where(e=>e!=null && e.Entity==creation.Id).All(e=>string.IsNullOrEmpty(e.Name)
                    && e.Tags.All(t=>t.Name==50 || t.Name==49 || t.Name==53 || t.Name==263)),"actual opponent Creation stays hidden");
        }
        Console.WriteLine("PASS generated native definition tags and hidden hand/deck privacy");
    }

    [GroupProof("powered-up-b18-t-generated", ProofKind.PoweredUp)]
    private static void B18GeneratedGlow()
    {
        Scenario("CATA_470", (f, card, met) => { if (met) f.Draw("EX1_043"); },
            (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
                for (int i = 0; i < 2; i++) {
                    var d = f.Game.GetPendingDecisions().Single();
                    Check(f.Game.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { f.Player.Choice.Choices[0] }), out _),
                        "Creation native choice completes"); f.Flush();
                }
                var result = f.Player.HandZone.Single(c => c.Card.Id == "CATA_470t1");
                return result.Cost == Math.Max(0, result.Card.Cost - 3);
            });
        foreach(bool met in new[]{false,true}) {
            var f=new Fixture();
            var creation=Generic.DrawCard(f.Player,GeneratedDefinitionRules.Creation(Cards.FromId("FIR_901"),Cards.FromId("CORE_ULD_723")));
            var held=f.Draw("CS2_182");
            if(met) DarkGifts.Mirror(held,"EDR_100t5",f.Player.Hero);
            f.Flush();f.Expected(creation,met?1:0,"composed conditional component");
            f.Process(PlayCardTask.Any(f.Player,creation));
            Check(f.Player.BoardZone.Count(c=>c.Card.Id=="FIR_901t")== (met?2:0),"composed glow matches inherited summon outcome");
        }
    }
}
