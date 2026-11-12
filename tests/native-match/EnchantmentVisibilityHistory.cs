using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // Live 2026-11-05/06: TestControlVisibleDiff threw "Transport snapshot requires an already-visible entity" for an enchantment
    // (EX1_619e on a minion that just died, TIME_003e on a dying Sahket Sapper, TIME_005t8e at a choice). The host forgets an
    // enchantment's reveal when the history that hides it is BUILT; the client forgets it when it APPLIES that history. This
    // pins both halves: after the whole batch the wire has hidden the enchantment (client zone off PLAY, so the audit skips it),
    // and until the batch is applied the client still shows it while the host no longer lists it, which is the window the audit
    // must treat as pending rather than as a failed read.
    private static void EnchantmentVisibilityHistory()
    {
        foreach (string scenario in new[] { "equality-death", "sapper-return-to-hand", "rafaam-choice" })
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.PALADIN);
            var game = match.Game; var human = game.Player1; var npc = game.Player2;
            SabberStoneCore.Model.Entities.Minion Summon(SabberStoneCore.Model.Entities.Controller owner, string id)
            {
                var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id));
                Generic.SummonBlock(game, minion, -1, null);
                return minion;
            }
            bool rafaam = scenario == "rafaam-choice";
            var victim = rafaam ? null : Summon(npc, scenario == "equality-death" ? "CS2_189" : "CORE_ULD_280");
            var returned = scenario == "sapper-return-to-hand" ? Summon(human, "CS2_182") : null;
            SabberStoneCore.Model.Entities.IPlayable secondRafaam = null;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            SabberStoneCore.Model.Entities.IPlayable ench;
            if (scenario == "equality-death")
            {
                var equality = Generic.DrawCard(human, Cards.FromId("EX1_619"));
                FlushFieldHistory(match);
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(equality.Id) && o.MainOption.PowerKeyword == 0);
                Check(play >= 0, "Equality native play option");
                cursor = capture.Packets.Count;
                match.SendOption(options.Id, play, 0, -1, 0);
                ench = victim.AppliedEnchantments.Single(e => e.Card.Id == "EX1_619e");
            }
            else if (rafaam)
            {
                // Live f2b3bbb6: the NPC's Murloc Rafaam leaves "next Rafaam costs 3 less" on its player; the next Rafaam consumes it in the
                // same play, so the client sees the enchantment until that batch is applied (live, a choice was open at that moment; the engine here
                // resolves the second Rafaam's choice without one, which does not change the host/client ordering under test).
                game.Process(EndTurnTask.Any(human)); FlushFieldHistory(match);
                npc.BaseMana = 10; npc.UsedMana = 0;
                var first = Generic.DrawCard(npc, Cards.FromId("TIME_005t8"));
                secondRafaam = Generic.DrawCard(npc, Cards.FromId("TIME_005t3"));
                FlushFieldHistory(match);
                Check(game.Process(PlayCardTask.Minion(npc, (SabberStoneCore.Model.Entities.Minion)first)), "NPC plays Murloc Rafaam");
                game.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match);
                ench = game.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>().Single(e => e.Card.Id == "TIME_005t8e");
            }
            else
            {
                ench = SabberStoneCore.Model.Entities.Enchantment.GetInstance(npc, victim, victim, Cards.FromId("TIME_003e"));
                FlushFieldHistory(match);
            }
            (string Name, int Zone) Client(int upTo)
            {
                var all = capture.Packets.Take(upTo).Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
                var tags = ReplayNativeEntityTags(all, ench.Id);
                string name = all.Where(e => e.HasFullEntity && e.FullEntity.Entity == ench.Id || e.HasShowEntity && e.ShowEntity.Entity == ench.Id)
                    .Select(e => e.HasFullEntity ? e.FullEntity.Name : e.ShowEntity.Name).LastOrDefault() ?? "";
                if (all.Any(e => e.HasHideEntity && e.HideEntity.Entity == ench.Id) && tags.GetValueOrDefault((int)GameTag.ZONE) != (int)EngineZone.PLAY) name = "";
                return (name, tags.GetValueOrDefault((int)GameTag.ZONE));
            }
            bool Revealed()
            {
                try { match.SnapshotVisibleTransportTags(ench); return true; }
                catch (TransportEntityNotVisibleException error) { Check(error.Message.Contains("revealed=false"), "unrevealed read names the entity"); return false; }
            }
            Check(Revealed() && Client(capture.Packets.Count) == (ench.Card.Id, (int)EngineZone.PLAY), scenario + ": enchantment is revealed and shown while attached");
            int before = capture.Packets.Count;
            if (rafaam)
            {
                Check(game.Process(PlayCardTask.Minion(npc, (SabberStoneCore.Model.Entities.Minion)secondRafaam)), "NPC plays the second Rafaam");
            }
            else
            {
                victim.Destroy();
                game.DeathProcessingAndAuraUpdate();
            }
            // Host side: history built, reveal forgotten. Client side: nothing applied yet.
            var sendHistory = typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = game.PowerHistory.Last.ToList();
            sendHistory.Invoke(match, new object[] { entries }); game.PowerHistory.Last.Clear();
            bool hostHid = !Revealed();
            if (returned != null) Check(returned.Zone?.Type == EngineZone.HAND, scenario + ": Sahket Sapper's deathrattle returned the enemy minion to hand");
            var applied = Client(capture.Packets.Count);
            var pending = Client(before);
            Check(hostHid, scenario + ": host forgot the enchantment's reveal when the death history was built");
            Check(pending.Name == ench.Card.Id && pending.Zone == (int)EngineZone.PLAY,
                scenario + ": until that history is applied the client still shows the enchantment in PLAY (the pending window)");
            Check(applied.Name.Length == 0 || applied.Zone != (int)EngineZone.PLAY,
                scenario + ": once applied the client no longer shows it in PLAY (zone " + applied.Zone + ", card '" + applied.Name + "'), so the audit skips it");
            Console.WriteLine("CHECK enchantment visibility " + scenario + ": ench " + ench.Card.Id + "#" + ench.Id + " pending=" + pending + " applied=" + applied);
        }
    }
}

internal static partial class Program
{
    // The host side of entity-drag --position: a board slot is accepted for a minion or location and refused for every other source.
    // The test-control validator turns the user's --position into the position it expects the native send to carry
    // (TestControlRules.OptionPosition, keyed by the client card type), so that value must be accepted for every kind of card.
    private static void BoardPositionContract()
    {
        Check((int)CardType.MINION == 4 && (int)CardType.LOCATION == 39 && (int)CardType.HERO == 3 && (int)CardType.WEAPON == 7 && (int)CardType.SPELL == 5,
            "engine card types carry the client TAG_CARDTYPE numbers OptionPosition keys on");
        foreach (var (cardId, takesSlot) in new[] { ("CS2_182", true), ("JAIL_877", true), ("CS2_106", false), ("CS2_023", false), ("CORE_EX1_323", false), ("EX1_323", true) })
        {
            var card = Cards.FromId(cardId);
            Check(takesSlot == (card.Type == CardType.MINION || card.Type == CardType.LOCATION), cardId + " is a " + card.Type);
            foreach (var (label, position) in new[] { ("none", 0), ("raw --position 1", 1), ("validator", TestControlRules.OptionPosition((int)card.Type, 1)) })
            {
                var (capture, match) = ChoiceRetirementMatch(card.Class == CardClass.NEUTRAL ? CardClass.MAGE : card.Class);
                var owner = match.Game.Player1;
                var source = Generic.DrawCard(owner, card);
                FlushFieldHistory(match);
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(source.Id) && o.MainOption.PowerKeyword == 0);
                Check(play >= 0, cardId + " native play option");
                bool accepted;
                try { match.SendOption(options.Id, play, 0, -1, position); accepted = true; }
                catch (ArgumentException) { accepted = false; }
                Check(accepted == (label == "validator" || position != 0 == takesSlot), cardId + " (" + card.Type + ") with " + label + " position " + position
                    + (accepted ? " accepted" : " refused"));
            }
        }
        Console.WriteLine("PASS board position contract: minion and location need a slot; spell, weapon and hero cards refuse one; the validator's position always fits");
    }
}
