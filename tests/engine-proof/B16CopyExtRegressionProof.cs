using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16CopyExtRegressionProof
{
    internal static Game NewGame(params string[] opening)
        => NewGame(opening, null);

    private static Game NewGame(string[] opening, OfflinePoolManifest pools)
    {
        var g = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.PRIEST, Player2HeroClass = CardClass.MAGE, ReplayPools = pools,
            Player1Deck = opening.Select(Cards.FromId).Concat(Enumerable.Repeat(Cards.FromId("CS2_231"), 30 - opening.Length)).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 }) { PoolPilotProof.EmptyHand(p); p.BaseMana = 10; }
        return g;
    }

    [GroupProof("b16-copy-ext-opt-in")]
    internal static void OptIn()
    {
        var g = NewGame(); var p = g.Player1;
        var copy = Generic.Copy(p, p.Hero, Add(p.Opponent, "CS2_231"), Zone.HAND);
        Core2026Proof.Check(copy[PayloadRules.CopySourceController] == 0, "legacy match must not acquire provenance tags");
        PlayEntity(g, p, copy);
        Core2026Proof.Check(CopyPlayHistory.SourceControllerAt(p, p.PlayHistory.Count - 1) == 0, "legacy play must not acquire provenance witness");
        var donor = Summon(p, "CATA_210"); donor[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5;
        Kill(g, donor);
        Core2026Proof.Check(PayloadRules.Entries(p).Length == 0, "legacy match must not allocate death ledger bodies");

        foreach (string consumer in new[] { "EDR_491", "JAIL_434" })
        foreach (bool intermediate in new[] { false, true })
        {
            var pools = ConsumerPool(consumer, intermediate);
            var generated = NewGame(new[] { intermediate ? "CORE_FP1_011" : "TLC_469" }, pools); var owner = generated.Player1;
            Core2026Proof.Check(generated[PayloadRules.Tracking] == (consumer == "EDR_491" ? 1 : 2), "enable only reachable consumer's tracking kind");
            // Events precede the consumer's creation, not merely its play.
            var earlier = Generic.Copy(owner, owner.Hero, Add(owner.Opponent, "CS2_182"), Zone.HAND);
            var egg = Summon(owner, "CATA_210");
            egg[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; egg[GameTag.TAG_SCRIPT_DATA_NUM_2] = 6;
            Kill(generated, egg);
            generated.DeathProcessingAndAuraUpdate();
            foreach (var branch in new[] { generated.CloneExact(true), Game.LoadSnapshotWithPools(generated.SaveSnapshot(), pools), generated })
            {
                var c = branch.Player1;
                if (intermediate)
                {
                    Kill(branch, Summon(c, "CORE_FP1_011"));
                    var tunnel = c.HandZone.Single(card => card.Card.Id == "TLC_469");
                    PlayEntity(branch, c, tunnel); Kill(branch, (Minion)tunnel);
                }
                else Kill(branch, Summon(c, "TLC_469"));
                var held = c.HandZone.First(card => card.Card.Id == consumer);
                PlayEntity(branch, c, held); Kill(branch, (Minion)held);
                if (consumer == "JAIL_434")
                    Core2026Proof.Check(branch.IdEntityDic[earlier.Id].Cost == 3, "generated Shade sees copies made before its generation");
                else
                    Core2026Proof.Check(c.BoardZone.Count(m => m.Card.Id == "CATA_210t" && m.AttackDamage == 5 && m.Health == 6) == 2,
                        "generated Archdruid retains deaths before its generation across clone/load");
            }
            var unreachable = NewGame(Array.Empty<string>(), pools);
            Core2026Proof.Check(!unreachable.NativeTags.ContainsKey(PayloadRules.Tracking), "admission alone does not enable tracking without reachable generator");
        }
    }

    private static OfflinePoolManifest ConsumerPool(string id, bool intermediate)
    {
        // Isolated two-Cost bucket; no shared admission/catalog edits.
        var xml = OfflinePoolFixtures.Fixture(1); var members = xml.Root.Element("Members");
        var row = new System.Xml.Linq.XElement(members.Elements("Card").First());
        var card = Cards.FromId(id);
        foreach (var pair in new Dictionary<string, object> { ["cardId"] = id, ["dbfId"] = card.AssetId,
            ["set"] = id == "EDR_491" ? "EMERALD_DREAM" : "ESCAPEFROM_VIOLET_HOLD", ["type"] = "MINION",
            ["cost"] = 2, ["attack"] = card.ATK, ["health"] = card.Health, ["rarity"] = card.Rarity.ToString(),
            ["registrationKind"] = "standard-module", ["scope"] = "Standard", ["spellSchool"] = "NONE" })
            row.SetAttributeValue(pair.Key, pair.Value);
        row.RemoveNodes(); row.Add(new System.Xml.Linq.XElement("Class", new System.Xml.Linq.XAttribute("name", card.Class.ToString())));
        members.RemoveNodes(); members.Add(row);
        if (intermediate)
        {
            var tunnel = new System.Xml.Linq.XElement(OfflinePoolFixtures.Catalog().Root.Element("Members").Elements("Card")
                .Single(c => (string)c.Attribute("cardId") == "TLC_469"));
            tunnel.Elements("Dependency").Remove(); members.Add(tunnel);
        }
        return OfflinePoolFixtures.Manifest(xml);
    }

    [GroupProof("b16-copy-ext-death-context")]
    internal static void Unpinned_DeathContext()
    {
        foreach (bool ledger in new[] { false, true })
        {
            var g = NewGame("EDR_491"); var p = g.Player1;
            var donor = Summon(p, "CATA_185");
            var rock = (Minion)Play(g, p, "BE_731");
            if (ledger) { Kill(g, donor); Kill(g, rock); rock = (Minion)Play(g, p, "EDR_491"); }
            var killer = Summon(p.Opponent, "CS2_200");
            var silenced = g.CloneExact(true); var muted = (Minion)silenced.IdEntityDic[rock.Id]; muted.Silence();
            Core2026Proof.Check(muted[DeathContextRules.CaptureDeath] == 0, "Silence removes captured death capability");
            var bounced = g.CloneExact(true); var returned = (Minion)bounced.IdEntityDic[rock.Id];
            Generic.ReturnToHandBlock(bounced.Player1, returned);
            Core2026Proof.Check(returned[DeathContextRules.CaptureDeath] == 0, "leave-play removes captured death capability");
            foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                Attack(branch, (Minion)branch.IdEntityDic[rock.Id], (Minion)branch.IdEntityDic[killer.Id]);
                Core2026Proof.Check(branch.IdEntityDic[killer.Id].Card.Id == "CATA_185", "captured Faceless Deathrattle retains recipient killer through grants/ledger/clone/load");
            }
        }
        {
            var g = NewGame(); var p = g.Player1;
            Summon(p, "EDR_818"); var rock = (Minion)Play(g, p, "BE_731");
            Summon(p, "CS2_222"); int attack = rock.AttackDamage;
            Kill(g, rock);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EDR_818t") == attack, "copied Nythendra uses recipient pre-cleanup aura Attack");
        }
        {
            var g = NewGame(); var p = g.Player1;
            var left = Summon(p, "BE_151"); var rock = Summon(p, "BE_731");
            PayloadRules.GainDeathrattles(rock, left, "BE_731e");
            var right = Summon(p, "CS2_231"); Kill(g, rock);
            Core2026Proof.Check(left.AttackDamage == left.Card.ATK + 2 && right.AttackDamage == right.Card.ATK + 2,
                "copied Vaultkeeper retains recipient death position");
        }
    }

    [GroupProof("b16-copy-ext-discover-lineage")]
    internal static void Unpinned_DiscoverLineage()
    {
        foreach (bool combo in new[] { false, true })
        {
            var g = NewGame("JAIL_432", "JAIL_433"); var p = g.Player1;
            var mind = Add(p, "JAIL_432"); var soul = Add(p, "JAIL_433");
            if (combo) Play(g, p, "GAME_005");
            Play(g, p, "EDR_528");
            var offers = p.Choice.Choices.ToArray();
            Core2026Proof.Check(offers.All(id => PayloadRules.CopiedFromOpponent(g.IdEntityDic[id])), "both Nightmare Fuel offer paths record opponent source");
            foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = branch.Player1; var decision = branch.GetPendingDecisions().Single();
                int selected = offers[0];
                Core2026Proof.Check(branch.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out _), "copy choice accepted");
                var chosen = branch.IdEntityDic[selected];
                // Sweet Dreams may top-deck the selected copy; draw it normally before playing.
                if (chosen.Zone == owner.DeckZone) Generic.Draw(owner);
                PlayEntity(branch, owner, chosen);
                Core2026Proof.Check(PayloadRules.HeldOpponentCopy(branch.IdEntityDic[mind.Id]) && branch.IdEntityDic[soul.Id].Cost == 1,
                    "Nightmare Fuel copy play qualifies holding cards");
                Core2026Proof.Check(CopyPlayHistory.SourceControllerAt(owner, owner.PlayHistory.Count - 1) == owner.Opponent.PlayerId,
                    "definition-based copy retains immutable play witness");
            }
        }
        foreach (string id in new[] { "BOT_435", "CFM_605" })
        {
            var g = NewGame("JAIL_432"); var p = g.Player1; var held = Add(p, "JAIL_432");
            Add(p, "EX1_572"); Play(g, p, id);
            var decision = g.GetPendingDecisions().Single(); int selected = p.Choice.Choices.First();
            Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out _), "legacy enemy-deck Discover accepted");
            PlayEntity(g, p, g.IdEntityDic[selected]);
            Core2026Proof.Check(PayloadRules.HeldOpponentCopy(held), "legacy definition-based Discover copy qualifies: " + id);
        }
    }
}
