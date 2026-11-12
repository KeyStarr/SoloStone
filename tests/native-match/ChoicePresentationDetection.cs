using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using Tag = PegasusGame.Tag;

internal static partial class Program
{
    private static NativeMatch.OfferPresentation ReadyChoiceOffer(bool inHand = false) => new NativeMatch.OfferPresentation {
        CardExists = true, HasCardDef = true, ActorExists = true, CardActive = true,
        ActorActive = true, ActorShown = true, ColliderEnabled = true, InHand = inHand
    };
    private static NativeMatch.ChoicePresentation MissingChoicePresentation(int id) => new NativeMatch.ChoicePresentation {
        ChoiceId = id, Type = ChoiceType.GENERAL, Settled = true, SourceExists = true,
        Offers = new[] { default(NativeMatch.OfferPresentation) }
    };

    // Table of states for the predicate behind the choice-stall watchdog (ChoicePresentation.Renderable), added after
    // the Cultist Map freeze (see AgentCoinChoiceHistory). Renderable must say "broken" only when a settled choice
    // really cannot be clicked, because a false positive makes the game answer for the player. Hence the cases for
    // Hide/Show, rerolls, alternate pickers, TARGET and HERO choices and still-loading actors.
    private static void ChoicePresentationDetection()
    {
        var view = MissingChoicePresentation(17);
        Check(view.Renderable(17) == false, "settled missing picker is detected");
        view.Offers[0] = ReadyChoiceOffer();
        Check(view.Renderable(17) == true, "shown default picker is usable");
        view.Offers = new[] { ReadyChoiceOffer(), default(NativeMatch.OfferPresentation) };
        Check(view.Renderable(17) == true, "one clickable offer keeps mixed ready/missing single-pick choice human-owned");
        view.SourceExists = false;
        Check(view.Renderable(17) == true, "clickable answer suppresses recovery before missing-source failure");
        view.CountMin = 2;
        Check(view.Renderable(17) == false, "two-pick choice needs two usable offers");
        view.Offers = new[] { ReadyChoiceOffer(), ReadyChoiceOffer(), default(NativeMatch.OfferPresentation) };
        Check(view.Renderable(17) == true, "enough clickable offers suppress recovery with missing source and spare offer");
        view.SourceExists = true; view.CountMin = 1;
        view.Offers = new[] { ReadyChoiceOffer() };
        // Actor.Hide disables its visual child, not necessarily its GameObject or collider.
        view.StateChoiceId = 17; view.StateRevealed = true; view.ToggleButtonExists = true;
        view.ChoicesShown = false; view.Offers[0].ActorShown = false;
        Check(view.Offers[0].ColliderEnabled && view.Renderable(17) == true, "Hide with enabled collider keeps valid picker");
        view.Offers[0].ColliderEnabled = false;
        Check(view.Renderable(17) == true, "deliberately hidden picker does not require collider");
        view.Offers[0].ActorExists = false;
        Check(view.Renderable(17) == false, "Hide never masks missing actor");
        view.ChoicesShown = true; view.Offers[0] = ReadyChoiceOffer();
        Check(view.Renderable(17) == true, "Show restores valid picker");
        view.Offers[0].ActorShown = false;
        Check(view.Renderable(17) == false, "unrequested hidden visual is missing even with live collider");
        view.Offers[0] = ReadyChoiceOffer(); view.Offers[0].Loading = true; view.Offers[0].ActorExists = false;
        Check(view.Renderable(17) == null, "native actor loading is progress");
        view.Offers[0].Loading = false;
        Check(view.Renderable(17) == false, "actor absent after loading is failure");
        view.Offers = new[] { ReadyChoiceOffer(), new NativeMatch.OfferPresentation { CardId = "JAIL_319t" } };
        Check(view.Renderable(17) == true, "reroll button needs no card actor");
        view.Offers[0].ActorExists = false;
        Check(view.Renderable(17) == false, "reroll button does not mask missing ordinary offer");
        foreach (var display in new[] { ChoiceCardMgr.ChoiceState.ChoiceDisplayType.Rewind,
            ChoiceCardMgr.ChoiceState.ChoiceDisplayType.Assemble, ChoiceCardMgr.ChoiceState.ChoiceDisplayType.Shop })
        {
            view.DisplayType = (int)display;
            Check(view.Renderable(17) == null, "alternate display owns separate controls: " + display);
        }
        view.DisplayType = 0; view.ShopChoice = true;
        Check(view.Renderable(17) == null, "shop flag owns separate controls");
        view.ShopChoice = false; view.MagicItemDiscover = true;
        Check(view.Renderable(17) == null, "magic item discover owns separate controls");
        view.MagicItemDiscover = false; view.StateChoiceId = -1;
        view.Type = ChoiceType.TARGET; view.Offers = new[] { ReadyChoiceOffer() };
        Check(view.Renderable(17) == true, "TARGET uses existing card actors without GENERAL state");
        view.Offers[0].ColliderEnabled = false;
        Check(view.Renderable(17) == false, "TARGET missing input is detected");
        view.Offers[0] = ReadyChoiceOffer(); view.Offers[0].InHand = true; view.CanTargetCardsInHand = false;
        Check(view.Renderable(17, sourceSubstituted: true) == false,
            "visible hand actor without native hand-target latch is not usable");
        view.CanTargetCardsInHand = true;
        Check(view.Renderable(17, sourceSubstituted: true) == true, "native hand-target latch enables visible hand actor");
        view.Offers[0].InputBlocked = true;
        Check(view.Renderable(17, sourceSubstituted: true) == false, "native disabled entity input is not usable");
        view.CountMin = 2; view.Offers = new[] { ReadyChoiceOffer(), default(NativeMatch.OfferPresentation) };
        Check(view.Renderable(17, sourceSubstituted: true) == true,
            "substituted TARGET never recovers while any usable target exists, even below CountMin");
        view.Offers[0] = default; view.Offers[0].Loading = true; view.SourceExists = false;
        Check(view.Renderable(17, sourceSubstituted: true) == null, "substituted TARGET loading cannot age recovery");
        view.CountMin = 1; view.SourceExists = true;
        view.Type = ChoiceType.GENERAL; view.Offers[0] = ReadyChoiceOffer();
        Check(typeof(HeroChoiceActor).IsSubclassOf(typeof(Actor))
            && typeof(HeroChoiceActor).GetMethod(nameof(Actor.IsShown)).DeclaringType == typeof(Actor)
            && typeof(HeroChoiceActor).GetMethod(nameof(Actor.IsColliderEnabled)).DeclaringType == typeof(Actor)
            && view.Renderable(17) == true, "pinned HERO actor inherits tested Actor visibility/collider contract");
        view.SourceExists = false;
        Check(view.Renderable(17) == true, "missing source cannot override clickable offers");
        view.Offers[0].ActorExists = false;
        Check(view.Renderable(17) == false, "missing source and no clickable answer is failure");
        view.SourceExists = true; view.Settled = false;
        Check(view.Renderable(17) == null, "native power work/transition is progress");
        view.Settled = true;
        Check(view.Renderable(18) == null, "different native choice cannot age engine choice");
        Console.WriteLine("PASS production choice predicate: Hide/Show, missing, loading, reroll, alternate, TARGET/HERO");
    }

    // Same freeze class as the Cultist Map case, found in review: if the choice's printed creator (here Geddon, banished
    // by Osk) is gone from the client, the source must fall back to the enchantment that still exists, and only then to
    // the owning player. Engine state is never changed, only what is shown. Both seats, both Osk variants.
    private static void BanishedChoiceCreator()
    {
        foreach (bool opposing in new[] { false, true })
        foreach (string oskId in new[] { "TLC_452t21", "TLC_452t29" })
        {
            var (match, capture) = AnimationMatch(deck: "showcase-rogue-imbue");
            var g = match.Game; var owner = opposing ? g.Player2 : g.Player1;
            var banisher = owner.Opponent;
            void Process(PlayerTask task) { InvokeProcess(match, task); FlushFieldHistory(match); }
            if (opposing) Process(EndTurnTask.Any(g.Player1));
            owner.BaseMana = banisher.BaseMana = 10;
            owner.UsedMana = 0;
            var geddon = EngineEntity.FromCard(owner, Cards.FromId("CATA_591"), zone: owner.HandZone);
            FlushFieldHistory(match); Process(PlayCardTask.Any(owner, geddon));
            Process(EndTurnTask.Any(owner));
            banisher.UsedMana = 0;
            var osk = EngineEntity.FromCard(banisher, Cards.FromId(oskId), zone: banisher.HandZone);
            FlushFieldHistory(match); Process(PlayCardTask.Any(banisher, osk, oskId == "TLC_452t21" ? (ICharacter)geddon : null));
            while (banisher.Choice != null) Process(ChooseTask.Pick(banisher, banisher.Choice.Choices[0]));
            Check(geddon[GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME, "Osk banishes Geddon creator for either seat");
            Process(EndTurnTask.Any(banisher));
            var choice = owner.Choice;
            Check(choice != null && g.IdEntityDic[choice.SourceId].Card.Id == "CATA_591e", "banished Geddon retains draw choice");
            if (!opposing) typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var native = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message)
                .Last(p => p.PlayerId == owner.PlayerId && p.ChoiceType == (int)ChoiceType.GENERAL);
            Check(native.Source == choice.SourceId, "native retains surviving enchantment when printed creator is removed");
            var wire = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
            Check(wire.ContainsKey(native.Source) && wire[native.Source].Zone != (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME,
                "both seats name source still present on native wire");
            // Exercise fallback after both recipe and creator have left the client, without changing engine decision.
            var remove = new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                Entity = choice.SourceId, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME } };
            typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { new PegasusGame.PowerHistory { List = new List<PowerHistoryData> { remove } } });
            var projected = (EntityChoices)typeof(NativeMatch).GetMethod("ToNativeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { PowerChoicesBuilder.EntityChoices(g, choice) });
            Check(projected.Source == owner.Id && projected.Entities.SequenceEqual(choice.Choices),
                "missing entire source chain falls back to public owning player without changing offers");
            Check(choice.SourceId != owner.Id && g.IdEntityDic[choice.SourceId].Card.Id == "CATA_591e", "presentation never mutates engine source");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS banished Geddon: Osk t21/t29, both seats, surviving enchantment and public player fallback");
    }

    private static void TargetChoiceSourceFallback()
    {
        PinnedTargetInputContract();
        foreach (string finish in new[] { "click", "watchdog", "concede" })
        {
            var (match, capture) = AnimationMatch(deck: "showcase-rogue-imbue");
            var g = match.Game; var owner = g.Player1;
            Check(!match.PlayerTurnTimerEnabled, "substituted TARGET fixtures use default disabled turn timer");
            var target = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var agent = EngineEntity.FromCard(owner, Cards.FromId("CATA_200"), zone: owner.HandZone);
            owner.BaseMana = 10; owner.UsedMana = 0;
            FlushFieldHistory(match); InvokeProcess(match, PlayCardTask.Any(owner, agent)); FlushFieldHistory(match);
            void Publish() => typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            void History(PowerHistoryData entry)
            {
                typeof(NativeMatch).GetMethod("TrackVisibility", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(match, new object[] { entry });
                typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(match, new object[] { new PegasusGame.PowerHistory { List = new List<PowerHistoryData> { entry } } });
            }
            Publish();
            var choice = owner.Choice;
            Check(choice.ChoiceType == ChoiceType.TARGET && capture.Last<EntityChoices>(17).Source == agent.Id,
                "TARGET retains original Agent with native hand-target tag");
            Check(DeckDiscoverWireHistory(capture, 0).Any(entry => entry.HasFullEntity && entry.FullEntity.Entity == agent.Id
                && entry.FullEntity.Tags.Any(tag => tag.Name == 1508 && tag.Value != 0)), "Agent source publishes CAN_TARGET_CARDS_IN_HAND");

            // A live recipe enchantment's targeting tags must not be replaced by its printed creator's tags.
            var effect = SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, agent, owner, Cards.FromId("CS2_092e"));
            effect.NativeTags[(GameTag)1508] = 1;
            History(new PowerHistoryData { FullEntity = new PegasusGame.PowerHistoryEntity { Entity = effect.Id, Name = effect.Card.Id,
                Tags = new List<Tag> { new Tag { Name = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.PLAY },
                    new Tag { Name = 1508, Value = 1 } } } });
            var packet = new PowerEntityChoices(999, ChoiceType.TARGET, new List<int> { target.Id }) {
                SourceId = effect.Id, PlayerId = owner.PlayerId, CountMin = 1, CountMax = 1 };
            var projected = (EntityChoices)typeof(NativeMatch).GetMethod("ToNativeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            Check(projected.Source == effect.Id, "TARGET preserves surviving enchantment input tags even when printed creator survives");

            // Keep an existing capability value intact after this choice; never mutate engine hero tags.
            int originalTag = finish == "concede" ? 3 : 0;
            int engineTag = owner.Hero[(GameTag)1508];
            History(new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                Entity = owner.Hero.Id, Tag = 1508, Value = originalTag } });
            History(new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                Entity = agent.Id, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME } });
            int before = capture.Packets.Count;
            Publish();
            var native = capture.Last<EntityChoices>(17);
            Check(native.Source == owner.Hero.Id && native.Entities.SequenceEqual(choice.Choices),
                "substituted TARGET uses public hero Card/arrow origin with exact offers");
            var changes = DeckDiscoverWireHistory(capture, before);
            Check(capture.Packets.Count == before + 2 && changes.Count == 1 && changes[0].HasTagChange
                && changes[0].TagChange.Entity == owner.Hero.Id && changes[0].TagChange.Tag == 1508
                && changes[0].TagChange.Value == 1 && ChoiceWireTag(capture, owner.Hero.Id, 1508) == 1,
                "only scoped capability precedes TARGET packet; no identity/history recreation");
            Check(owner.Hero[(GameTag)1508] == engineTag && choice.SourceId == agent.Id,
                "replacement capability leaves engine source and hero unchanged");
            // Opponent projection must also use a public actor, not the no-Card player entity.
            packet.SourceId = agent.Id; packet.PlayerId = owner.Opponent.PlayerId;
            projected = (EntityChoices)typeof(NativeMatch).GetMethod("ToNativeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            Check(projected.Source == owner.Opponent.Hero.Id, "opposing substituted TARGET also names its public hero");

            double now = 0;
            var view = MissingChoicePresentation(native.Id); view.Type = ChoiceType.TARGET;
            view.Offers = native.Entities.Select(_ => default(NativeMatch.OfferPresentation)).ToArray();
            var traces = new List<string>(); match.BotTrace = traces.Add;
            match.NativeHumanChoicePresentation = () => view; match.NpcClock = () => now;
            void Hold(double end) { for (; now <= end; now += 500) match.TickChoiceWatchdog(); }
            var input = new TargetChoiceInputModel(capture, match.SendChoices);
            Check(!input.ClickHand(target.Id, default), "native input model rejects absent hand actor");
            // Removing only the projected capability reproduces the old native hand-click failure.
            History(new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                Entity = owner.Hero.Id, Tag = 1508, Value = 0 } });
            var untagged = new TargetChoiceInputModel(capture, match.SendChoices);
            Check(!untagged.ClickHand(target.Id, ReadyChoiceOffer()) && owner.Choice == choice,
                "clickable hand actor cannot bypass InputManager CanTargetCardsInHand gate");
            History(new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                Entity = owner.Hero.Id, Tag = 1508, Value = 1 } });

            if (finish == "watchdog")
            {
                view.Settled = false; Hold(20000);
                Check(owner.Choice == choice, "unsettled substituted TARGET cannot recover");
                view.Settled = true; view.Offers[0].Loading = true; Hold(40000);
                Check(owner.Choice == choice, "loading substituted TARGET cannot recover");
                view.Offers[0] = ReadyChoiceOffer(inHand: true); view.CountMin = 2; Hold(60000);
                Check(owner.Choice == choice, "any usable substituted TARGET suppresses recovery beyond grace");
                view.Offers[0] = default; view.CountMin = 1;
                Hold(75000);
                Check(owner.Choice == choice, "zero usable substituted TARGET requires fresh full grace");
                Hold(77000);
                Check(owner.Choice == null && g.IdEntityDic[native.Entities[0]].Card.Id == "GAME_005",
                    "zero usable substituted TARGET completes through watchdog with timer off");
                Check(input.Sent == 0 && traces.Count(t => t.StartsWith("CHOICE_STALL_RECOVERY")) == 1,
                    "substituted TARGET recovers exactly once without direct answer submission");
            }
            else
            {
                view.Offers[Array.IndexOf(native.Entities.ToArray(), target.Id)] = ReadyChoiceOffer(inHand: true);
                Hold(20000);
                Check(owner.Choice == choice && traces.All(t => !t.StartsWith("CHOICE_STALL_RECOVERY")),
                    "clickable hand target stays human-owned beyond recovery grace");
                if (finish == "click")
                    Check(input.ClickHand(target.Id, ReadyChoiceOffer()) && input.Sent == 1 && owner.Choice == null
                        && g.IdEntityDic[target.Id].Card.Id == "GAME_005",
                        "native input model completes substituted TARGET through hand gate and Network boundary");
                else
                {
                    match.Concede();
                    Check(g.State == State.COMPLETE && owner.Choice == null, "Concede clears substituted TARGET");
                }
            }
            Check(ChoiceWireTag(capture, owner.Hero.Id, 1508) == originalTag && owner.Hero[(GameTag)1508] == engineTag,
                "choice completion/recovery/Concede restores prior hero capability without engine mutation: " + finish
                    + " wire=" + ChoiceWireTag(capture, owner.Hero.Id, 1508) + " expected=" + originalTag
                    + " engine=" + owner.Hero[(GameTag)1508] + " expectedEngine=" + engineTag);
            capture.SerializeAll();
        }
        Console.WriteLine("PASS TARGET source: public hero capability, gated native hand click, once-only recovery, cleanup");
    }
}
