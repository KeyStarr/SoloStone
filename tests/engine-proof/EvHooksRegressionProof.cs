using System.Reflection;
using System.Text;
using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EvHooksRegressionProof
{
    private static void Check(bool ok, string why) => Core2026Proof.Check(ok, why);
    [GroupProof("ev-hooks-regressions")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { TotemEncoding, ChooseOneDefinition, ZombeastTypes,
            RootDeathsBeforeGreen, FairSuspicion, UniformMutationTypes, FairFutureSuspicion,
            FairNestedMetadata, FairWholeViewPrivacy, FairHiddenDescendants, FairOpeningDeckPrivacy, FairQueuedCraftPrivacy, FairOwnCraftRewardVisible, ModifiedDecisionBattlecry, ModifiedAffordableSpellBattlecry, ModifiedPoolRecipes })
        {
            try { test(); Console.WriteLine("PASS " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.Name + ": " + e); Console.WriteLine("FAIL " + test.Method.Name + ": " + e.Message); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
    }
    private static void ModifiedDecisionBattlecry()
    {
        var game=QuestLifecycleProof.Fixture();
        B15PoolTest.WithMembers(game,new[]{"EDR_856"},()=>{
            Play(game,game.Player1,"JAIL_EVENT_100");
            int modified=game.Player1.Choice.Choices.Single();
            EvHooksCardsProof.Pick(game,modified);
            PlayEntity(game,game.Player1,game.IdEntityDic[modified]);
            Check(game.Player1.Choice!=null,"modified Xavius opens its inherited Battlecry decision");
            foreach(var next in new[]{game.CloneExact(history:true),Game.LoadSnapshotWithPools(game.SaveSnapshot(),game.OfflinePools),game}) {
                B15PoolTest.AnswerAll(next);
                Check(next.Player1.Choice==null && !next.Resolution.HasSuspendedEffect
                    && next.IdEntityDic[modified].Card.Id=="JAIL_EVENT_100t",
                    "modified Battlecry resumes its captured definition after clone/snapshot");
            }
        });
    }
    private static void ModifiedAffordableSpellBattlecry()
    {
        var game=QuestLifecycleProof.Fixture(); int modified=0;
        B15PoolTest.WithMembers(game,new[]{"JAIL_986"},()=>{
            Play(game,game.Player1,"JAIL_EVENT_100");
            modified=game.Player1.Choice.Choices.Single();
            EvHooksCardsProof.Pick(game,modified);
        });
        foreach(var next in new[]{game.CloneExact(history:true),Game.LoadSnapshotWithPools(game.SaveSnapshot(),game.OfflinePools),game}) {
            PlayEntity(next,next.Player1,next.IdEntityDic[modified]);
            var spell=next.Player1.HandZone.Single();
            Check(spell.Card.Type==CardType.SPELL && TemporaryRules.IsTemporary(spell)
                && spell.Cost<=next.Player1.RemainingMana,
                "modified Forger uses its original declared affordable-spell recipe after clone/snapshot");
        }
    }
    private static void ModifiedPoolRecipes()
    {
        var failures=new List<string>();
        foreach(string original in new[]{"CATA_EVENT_000","CORE_RLK_116","BE_997","DINO_422","EDR_781"}) {
            try {
                var game=QuestLifecycleProof.Fixture();var installed=game.OfflinePools;
                var hero=Cards.FromId(original).Class;
                if(hero!=CardClass.NEUTRAL) game.Player1.Hero[GameTag.CLASS]=(int)hero;
                var catalog=KnownMetaProofFixture.BlackEmpireCatalog();
                catalog.Root.Element("Members").Elements("Card")
                    .Where(c=>(string)c.Attribute("cardId")!=original && (string)c.Attribute("scope")!="Past").Remove();
                game.RestoreOfflinePools(OfflinePoolFixtures.Manifest(catalog));
                Play(game,game.Player1,"JAIL_EVENT_100");
                Check(game.Player1.Choice!=null,"copied-recipe fixture admits "+original);
                int modified=game.Player1.Choice.Choices.Single();
                game.RestoreOfflinePools(installed);EvHooksCardsProof.Pick(game,modified);
                if(original=="CORE_RLK_116") Kill(game,Summon(game.Player1,"RLK_503"));
                foreach(var next in new[]{game.CloneExact(history:true),Game.LoadSnapshotWithPools(game.SaveSnapshot(),game.OfflinePools),game}) {
                    var p=next.Player1;PlayEntity(next,p,next.IdEntityDic[modified]);
                    if(original=="CATA_EVENT_000") Check(p.HandZone.Single().Card[(GameTag)2247]>0,"modified Lord generates a Past Colossal");
                    else if(original=="CORE_RLK_116") {
                        Check(p.Choice!=null && p.Choice.Choices.All(i=>next.IdEntityDic[i].Card[GameTag.COST_UNHOLY]>0),"modified Mortician uses its Unholy declaration");
                        B15PoolTest.AnswerAll(next);
                    }
                    else if(original=="BE_997") {
                        Add(p,"CS2_231");Check(p.HandZone.Count==2 && p.Opponent.HandZone.Count==1,"modified Proto shares one spell roll");
                    }
                    else if(original=="DINO_422") {
                        int before=p.BoardZone.Count;Kill(next,(Minion)next.IdEntityDic[modified]);
                        Check(p.BoardZone.Count==before+1,"modified Ankylodon summons two surviving Beasts");
                    }
                    else {
                        int before=p.BoardZone.Count;Generic.ReturnToHandBlock(p,(Minion)next.IdEntityDic[modified]);next.DeathProcessingAndAuraUpdate();
                        Check(p.BoardZone.Count==before+1,"modified Harbinger bounce summons two minions");
                    }
                }
                Console.WriteLine("PASS modified pool recipe "+original);
            } catch(Exception error) { failures.Add(original+": "+error.Message);Console.WriteLine("FAIL modified pool recipe "+original+": "+error); }
        }
        Check(failures.Count==0,string.Join("\n",failures));
    }
    private static void TotemEncoding()
    {
        bool found = false;
        foreach (int seed in new[] { 7 }.Concat(Enumerable.Range(1, 400)))
        {
            var g = QuestLifecycleProof.Fixture(); g.SetRandomSeed(seed);
            var offers = Watfin.Offers(g, g.Player1, g.Player1.Hero, new());
            var custom = offers.Single(c => c.Id == "JAIL_EVENT_100t");
            var original = Cards.FromAssetId(custom[GameTag.MODULAR_ENTITY_PART_1]);
            if (custom.IsRace(Race.TOTEM) && !original.IsRace(Race.TOTEM) && original.Races.Count > 0)
            {
                Check(original.Races.All(custom.IsRace), "adding Totem preserves every existing type");
                var actor = Entity.FromCard(g.Player1, custom, zone:g.Player1.HandZone);
                var saved = Game.LoadSnapshot(g.SaveSnapshot()).IdEntityDic[actor.Id].Card;
                Check(saved.IsRace(Race.TOTEM) && original.Races.All(saved.IsRace), "Totem encoding survives snapshot");
                found = true; break;
            }
        }
        Check(found, "seeded typed minion gains Totem");
    }
    private static void ChooseOneDefinition()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var catalog=KnownMetaProofFixture.BlackEmpireCatalog();
        catalog.Root.Element("Members").Elements("Card").Where(c=>(string)c.Attribute("set")=="EVENT").Remove();
        g.RestoreOfflinePools(OfflinePoolFixtures.Manifest(catalog));
        p.Hero[GameTag.CLASS] = (int)CardClass.DRUID; g.SetRandomSeed(53);
        var source = (Minion)Play(g, p, "JAIL_EVENT_100");
        var custom = p.Choice.Choices.Select(i => g.IdEntityDic[i]).Single(e => e.Card.Id == "JAIL_EVENT_100t");
        Check(custom.Card[GameTag.MODULAR_ENTITY_PART_1] == Cards.FromId("END_010").AssetId, "Druid 53 offers suspicious Twilight Timereaver");
        foreach (var next in new[] { g.CloneExact(history:true), Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools), g })
        {
            EvHooksCardsProof.Pick(next, custom.Id);
            var made = next.IdEntityDic[custom.Id];
            Check(made.ChooseOnePlayables.Select(e => e.Card.Id).SequenceEqual(new[] { "END_010a", "END_010b" }), "original Choose One definitions retained");
            var witness = Summon(next.Player1, "CS2_200"); next.Player1.UsedMana = 0;
            Check(next.Process(PlayCardTask.Any(next.Player1, made, chooseOne:1)), "suspicious Choose One plays");
            Check(witness.AttackDamage == 1 && witness.Health == 7, "original Attack option actually resolves");
            Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 1, "chosen option also sets Watfin Attack");
        }
    }
    private static void ZombeastTypes()
    {
        foreach (bool first in new[] { false, true })
        {
            var ooze = Cards.FromId("BE_734"); var beast = Cards.FromId("CS2_172");
            var made = Card.CreateZombeastCard(first ? ooze : beast, first ? beast : ooze, true, true);
            Check(made.Races.OrderBy(r => r).SequenceEqual(new[] { Race.UNDEAD, Race.BEAST }.OrderBy(r => r)), "All component produces exactly Undead Beast");
            var g = QuestLifecycleProof.Fixture(); var actor = (Minion)Entity.FromCard(g.Player1, made, zone:g.Player1.HandZone);
            foreach (var next in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
                Check(next.IdEntityDic[actor.Id].Card.Races.SequenceEqual(made.Races), "composite exact types survive clone/snapshot");
        }
    }
    private static void RootDeathsBeforeGreen()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        for (int i = 0; i < 7; i++) Summon(p, "CS2_231");
        Summon(p.Opponent, "GVG_076"); // Explosive Sheep.
        var storm = Add(p, "CATA_EVENT_110t7"); Add(p, "CATA_EVENT_110t6");
        foreach (var next in new[] { g.CloneExact(history:true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            Check(next.Process(PlayCardTask.Any(next.Player1, next.IdEntityDic[storm.Id])), "Storm root plays");
            Check(next.Player1.BoardZone.Single().Card.Id == "CATA_EVENT_110t6t", "Sheep and Wisps die before adjoining Green summons");
            Check(next.Player1.BoardZone.Single().Health == 8, "Dragon not hit by earlier Sheep deathrattle");
        }
    }
    private static void FairSuspicion()
    {
        foreach (bool second in new[] { false, true })
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer; var source = (Minion)Play(g, p, "JAIL_EVENT_100");
            var ids = p.Choice.Choices.ToArray(); var before = Canonical.State(g); var rng = g.Random.CaptureState();
            foreach (int owner in new[] { p.PlayerId, p.Opponent.PlayerId })
            {
                var fair = FairBotState.Create(g, owner, 71, DeckStyle.Value);
                var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
                Check(!view.IdEntityDic.Values.Any(e => e.Card.Id == "JAIL_EVENT_100t"), "neither fair seat retains suspicious token designation");
                if (owner != p.PlayerId) {
                    Check(!view.IdEntityDic.Values.Any(e => e.Controller.PlayerId == p.PlayerId && e.Zone?.Type == Zone.SETASIDE
                        && ids.Select(i => g.IdEntityDic[i].Card.Id).Contains(e.Card.Id)), "opponent offers hidden after canonical actor renumbering");
                    continue;
                }
                foreach (int id in ids)
                {
                    var shown = view.IdEntityDic[id]; var real = g.IdEntityDic[id];
                    Check(shown.Card.Cost == real.Card.Cost && shown.Card.ATK == real.Card.ATK && shown.Card.Health == real.Card.Health
                        && shown.Card.Name == real.Card.Name && shown.Card.Races.SequenceEqual(real.Card.Races), "owner retains every visible mutation");
                    Check(shown.Card[GameTag.MODULAR_ENTITY_PART_1] == 0, "no special recipe marker in fair offers");
                    Check(shown.Card.Collectible && !ReferenceEquals(shown.Card,Cards.FromId(shown.Card.Id)), "collectibility and definition allocation do not designate suspicious offer");
                    var simulation = view.CloneExact(); EvHooksCardsProof.Pick(simulation, id);
                    Check(((Minion)simulation.IdEntityDic[source.Id]).AttackDamage == 3, "simulation has no correctness oracle for any selection");
                }
            }
            Check(before == Canonical.State(g) && rng.Equals(g.Random.CaptureState()), "fair projection leaves authoritative state and RNG unchanged");
            EvHooksCardsProof.Pick(g, ids.Single(i => g.IdEntityDic[i].Card.Id == "JAIL_EVENT_100t"));
            Check(source.AttackDamage == 4, "authoritative correct pick still earns bonus");
        }
    }
    private static void UniformMutationTypes()
    {
        var fixture = QuestLifecycleProof.Fixture();
        KnownMetaProofFixture.UseBlackEmpire(fixture);
        fixture.Player1.Hero[GameTag.CLASS] = (int)CardClass.SHAMAN;
        var pool = OfflineCardPools.Candidates(fixture,fixture.Player1.HeroClass,Watfin.Pool(),129959);
        string[] edgeIds = { "BE_734", pool.First(c=>c.Cost==0).Id, pool.First(c=>c.Cost>0 && c.Races.Count>0 && !c.IsRace(Race.ALL)).Id };
        var xml = KnownMetaProofFixture.BlackEmpireCatalog(); var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(r=>edgeIds.Contains((string)r.Attribute("cardId"))).ToArray());
        var edgePool = OfflinePoolFixtures.Manifest(xml);
        bool replacedAll = false, replacedZero = false;
        // Candidate-first weighting would drop type mutation on All, or cost on a zero-Cost card.
        foreach (bool tiny in new[] {false,true})
        foreach (int seed in new[] { 179 }.Concat(Enumerable.Range(1, 250)))
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
            if (tiny) {
                p.Hero[GameTag.CLASS] = (int)CardClass.SHAMAN;
                typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g,edgePool);
            }
            var originals = OfflineCardPools.SampleDistinct(g, Watfin.Pool(), p.HeroClass, 129959);
            int slot = g.Random.Next(originals.Length), kind = g.Random.Next(5);
            g.SetRandomSeed(seed); var offers = Watfin.Offers(g, p, p.Hero, new());
            var custom = offers.Single(c => c.Id == "JAIL_EVENT_100t");
            var original = Cards.FromAssetId(custom[GameTag.MODULAR_ENTITY_PART_1]);
            int actual = custom.Cost != original.Cost ? 0 : custom.ATK != original.ATK ? 1 : custom.Health != original.Health ? 2
                : custom.Name != original.Name ? 4 : 3;
            if (tiny && kind==3 && originals[slot].IsRace(Race.ALL)) replacedAll = true;
            if (tiny && kind==0 && originals[slot].Cost==0) replacedZero = true;
            Check(actual == kind, $"seed {seed}: uniform five-way type {kind}, got {actual}; candidate {originals[slot].Id}");
            Check(kind != 0 || custom.Cost == original.Cost - 1 && original.Cost > 0, "cost mutation visible without clamp");
            Check(kind != 3 || custom.Races.Except(original.Races).Any(), "type mutation selects compatible candidate");
            Check(offers.Where(c => c != custom).All(c => c.Id != original.Id), "replacement preserves distinct original options");
        }
        Check(replacedAll && replacedZero,"seeded short pool replaces All/type and zero/Cost candidates without reweighting");
    }
    private static void FairFutureSuspicion()
    {
        foreach (bool second in new[] { false, true })
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer; var source = Add(p,"JAIL_EVENT_100");
            var before = FairBotState.Create(g,p.PlayerId,53,DeckStyle.Value);
            var offered = before.Apply(before.Actions.First(a => a.Source == source.Id));
            var view = (Game)typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(offered);
            Check(view.CurrentPlayer.Choice != null && !view.IdEntityDic.Values.Any(e => e.Card.Id == "JAIL_EVENT_100t"),
                "future simulated Battlecry redacts suspicious designation");
            foreach (var action in offered.Actions)
            {
                var result = offered.Apply(action);
                var after = (Game)typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(result);
                Check(((Minion)after.IdEntityDic[source.Id]).AttackDamage == 3, "future simulated choice has no correctness oracle");
            }
        }
    }
    private static Game View(FairBotState fair) => (Game)typeof(FairBotState)
        .GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);

    private static void DetachedMetadata(Game view, IEnumerable<int> ids)
    {
        foreach (int id in ids)
        {
            var card = view.IdEntityDic[id].Card;
            var canonical = Cards.FromId(card.Id);
            foreach (var pair in new (string Name, object Visible, object Canonical)[] {
                ("PlayRequirements",card.PlayRequirements,canonical.PlayRequirements),
                ("Races",card.Races,canonical.Races), ("Entourage",card.Entourage,canonical.Entourage),
                ("Tags",card.Tags,canonical.Tags), ("RefTags",card.RefTags,canonical.RefTags) })
                Check(!ReferenceEquals(pair.Visible,pair.Canonical), $"{card.Id}: {pair.Name} allocation cannot identify suspicious option");
            Check(card.Tags.Keys.SequenceEqual(card.Tags.Keys.OrderBy(tag=>(int)tag)),
                "definition tag enumeration cannot reveal reconstruction order");
        }
    }
    private static void FairNestedMetadata()
    {
        foreach (bool second in new[] { false,true })
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer; p.Hero[GameTag.CLASS] = (int)CardClass.DRUID; g.SetRandomSeed(53);
            Play(g,p,"JAIL_EVENT_100");
            var generated = p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card).Single(card=>card.Id=="JAIL_EVENT_100t");
            var reordered = generated.Tags.Reverse().ToArray();
            generated.Tags.Clear(); foreach (var tag in reordered) generated.Tags.Add(tag.Key,tag.Value);
            foreach (var saved in new[] { g,g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot()) })
                DetachedMetadata(View(FairBotState.Create(saved,p.PlayerId,53,DeckStyle.Value)),p.Choice.Choices);
            var future = QuestLifecycleProof.Fixture(); if (second) End(future);
            var owner = future.CurrentPlayer; owner.Hero[GameTag.CLASS] = (int)CardClass.DRUID;
            var source = Add(owner,"JAIL_EVENT_100");
            var fair = FairBotState.Create(future,owner.PlayerId,53,DeckStyle.Value);
            var offered = View(fair.Apply(fair.Actions.First(a=>a.Source==source.Id)));
            DetachedMetadata(offered,offered.CurrentPlayer.Choice.Choices);
        }
    }
    private static void NoPrivateIdentity(FairBotState fair, IEnumerable<string> forbidden, string context)
    {
        // Walk the whole reference graph, not a selection of fields; Unknown has no snapshot definition.
        var text = new StringBuilder(); var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Write(object value, string path = "fair")
        {
            if (value == null) return;
            var type = value.GetType();
            if (value is string word) { text.Append(path).Append(':').Append(System.Text.Json.JsonSerializer.Serialize(word)); return; }
            // The public immutable candidate catalog is a runtime service, identified by hash in snapshots too.
            if (value is OfflinePoolManifest manifest) { text.Append(manifest.Hash); return; }
            if (type.IsPrimitive || type.IsEnum || value is decimal) { text.Append(' ').Append(value).Append(';'); return; }
            if (!type.IsValueType && !seen.Add(value)) return;
            if (value is MemberInfo member) { text.Append(member.Name); return; }
            if (value is Delegate callback) { Write(callback.Method,path); Write(callback.Target,path); return; }
            if (value is System.Collections.IEnumerable sequence && (type.IsArray || type.Namespace?.StartsWith("System.Collections",StringComparison.Ordinal)==true))
            { int index=0; foreach (object item in sequence) Write(item,path+"["+index+++"]"); return; }
            for (var current = type; current != null; current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                Write(field.GetValue(value),path+"."+field.Name);
        }
        Write(fair);
        string whole = text.ToString();
        foreach (string id in forbidden.Concat(new[] {"JAIL_EVENT_100t","133446"}))
        {
            int at = whole.IndexOf(id,StringComparison.Ordinal);
            Check(at<0, $"{context}: whole fair view leaks {id}: " + (at<0 ? "" : whole.Substring(Math.Max(0,at-220),Math.Min(250,whole.Length-Math.Max(0,at-220)))));
        }
    }
    private static void FairWholeViewPrivacy()
    {
        foreach (bool second in new[] { false,true })
        foreach (int seed in new[] { 50,53 })
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer; p.Hero[GameTag.CLASS] = (int)CardClass.DRUID; g.SetRandomSeed(seed);
            Play(g,p,"JAIL_EVENT_100");
            var offers = p.Choice.Choices.Select(id=>g.IdEntityDic[id]).ToArray();
            var hidden = offers.SelectMany(e=>(e.ChooseOnePlayables??Array.Empty<IPlayable>()).Prepend(e))
                .Select(e=>e.Card.Id).Distinct().ToArray();
            NoPrivateIdentity(FairBotState.Create(g,p.Opponent.PlayerId,71,DeckStyle.Value),hidden,"opponent pending offers");
            var owned = FairBotState.Create(g,p.PlayerId,53,DeckStyle.Value);
            NoPrivateIdentity(owned,Array.Empty<string>(),"owner pending offers");
            int selected = offers.Single(e=>e.Card.Id=="JAIL_EVENT_100t").Id;
            EvHooksCardsProof.Pick(g,selected); End(g);
            NoPrivateIdentity(FairBotState.Create(g,p.Opponent.PlayerId,71,DeckStyle.Value),hidden,"opponent next turn");

            var future = QuestLifecycleProof.Fixture(); if (second) End(future);
            var owner = future.CurrentPlayer; owner.Hero[GameTag.CLASS] = (int)CardClass.DRUID;
            var source = Add(owner,"JAIL_EVENT_100");
            var fair = FairBotState.Create(future,owner.PlayerId,seed,DeckStyle.Value);
            var simulated = fair.Apply(fair.Actions.First(a=>a.Source==source.Id));
            NoPrivateIdentity(simulated,Array.Empty<string>(),"owner simulated future offers");
            foreach (var action in simulated.Actions)
                NoPrivateIdentity(simulated.Apply(action),Array.Empty<string>(),"owner simulated future selection");
        }
    }
    private static void FairHiddenDescendants()
    {
        foreach (bool second in new[] { false,true })
        foreach (string id in new[] { "END_010","TRL_343","CORE_EX1_154" })
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer;
            var hidden = new[] {
                Entity.FromCard(p,Cards.FromId(id),zone:p.DeckZone),
                Entity.FromCard(p.Opponent,Cards.FromId(id),zone:p.Opponent.HandZone),
                Entity.FromCard(p.Opponent,Cards.FromId(id),zone:p.Opponent.DeckZone) };
            var identities = hidden.SelectMany(e=>e.ChooseOnePlayables.Prepend(e)).Select(e=>e.Card.Id).Distinct().ToArray();
            NoPrivateIdentity(FairBotState.Create(g,p.PlayerId,71,DeckStyle.Value),identities,"ordinary hidden hand/deck descendants");
        }
    }
    private static void FairOpeningDeckPrivacy()
    {
        foreach (bool second in new[] {false,true})
        {
            var g = new Game(new SabberStoneCore.Config.GameConfig {
                RandomSeed=19, StartPlayer=1, Shuffle=false, SkipMulligan=true, History=true,
                Player1HeroClass=CardClass.MAGE, Player2HeroClass=CardClass.PALADIN,
                Player1Deck=Enumerable.Repeat(Cards.FromId(second?"CS2_182":"END_010"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId(second?"END_010":"CS2_182"),30).ToList() });
            g.StartGame();
            var viewer = second ? g.Player1 : g.Player2;
            var before = g.SaveSnapshot();
            NoPrivateIdentity(FairBotState.Create(g,viewer.PlayerId,71,DeckStyle.Value),new[]{"END_010","END_010a","END_010b"},
                "opponent original opening deck");
            Check(before.SequenceEqual(g.SaveSnapshot()),"setup redaction does not modify authoritative shared deck lists");
        }
    }
    private static void FairQueuedCraftPrivacy()
    {
        foreach (bool second in new[] {false,true})
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer;
            var quest = (Spell)Play(g,p,"TLC_EVENT_400"); QuestRules.Advance(quest,3);
            var queued = p.Choice.NextChoice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).ToArray();
            var fair = FairBotState.Create(g,p.PlayerId,71,DeckStyle.Value);
            NoPrivateIdentity(fair,queued,"owner cannot see queued crafting offers");
            Check(fair.CanExpand,"visible first crafting choice can be simulated");
            var next = fair.Apply(fair.Actions[0]);
            NoPrivateIdentity(next,queued,"queued crafting offers remain unknown after simulated first choice");
            Check(!next.CanExpand,"fair search replans when the next hidden choice becomes actionable");
            EvHooksCardsProof.Pick(g); EvHooksCardsProof.Pick(g);
            Check(p.HandZone.Count(card=>card.Card.Id=="ICC_828t")==1,"authoritative crafting still completes");
            var opponent = FairBotState.Create(g,p.Opponent.PlayerId,71,DeckStyle.Value);
            NoPrivateIdentity(opponent,new[]{"ICC_828t"},"opponent crafted hidden composite");
            Check(opponent.Facts.Opponent.OpaqueQuestRewards.Values.SequenceEqual(new[]{"TLC_EVENT_400"}),
                "opaque reward retains only the publicly completed quest");
            NoPrivateIdentity(opponent.WithArtificialSeed(99).ForkForSearch(null),new[]{"ICC_828t"},"forked opponent crafted hidden composite");
        }
    }
    private static void FairOwnCraftRewardVisible()
    {
        foreach (bool second in new[] {false,true})
        {
            var g = QuestLifecycleProof.Fixture(); if (second) End(g);
            var p = g.CurrentPlayer;
            var quest = (Spell)Play(g,p,"TLC_EVENT_400"); QuestRules.Advance(quest,3);
            EvHooksCardsProof.Pick(g); EvHooksCardsProof.Pick(g);
            var reward = p.HandZone.Single(card=>card.Card.Id=="ICC_828t");
            foreach (var own in new[] { FairBotState.Create(g,p.PlayerId,71,DeckStyle.Value),
                FairBotState.Create(g,p.PlayerId,71,DeckStyle.Value).ForkForSearch(null) })
            {
                var visible = own.Facts.Own.Hand.Single(card=>card.EntityId==reward.Id);
                Check(!visible.Unknown && visible.CardId==reward.Card.Id && visible.Cost==reward.Cost
                    && visible.Attack==((Minion)reward).AttackDamage && visible.Health==((Minion)reward).Health,
                    "owner retains crafted reward identity and chosen stats");
                var eval = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
                double held = .8 * eval.RewardPotential(visible.CardId,own.Facts.Own)
                    / (1 + .13 * Math.Max(0,visible.Cost-own.Facts.Own.BaseMana));
                Check(held>0 && eval.Hand(new[]{visible}) + eval.QuestAssets(own.Facts.Own,own.Facts.Own.Hand,true)>=held,
                    "owner retains reward value including ordinary hand credit");
                Check(own.Facts.Own.OpaqueQuestRewards.Count==0,"own known reward needs no opaque prior");
            }
        }
    }
}
