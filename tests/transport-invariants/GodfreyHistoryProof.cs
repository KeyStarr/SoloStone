using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace TransportInvariants;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

internal static class GodfreyHistoryProof
{
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var filler = Enumerable.Repeat(Cards.FromId("CS2_182"), 29).ToList();
            var godfrey = filler.Append(Cards.FromId("JAIL_509")).ToList();
            var game = new Game(new GameConfig { RandomSeed = 23, StartPlayer = 1, Shuffle = false,
                SkipMulligan = true, History = true, Logging = false,
                Player1Deck = seat == 1 ? godfrey : filler, Player2Deck = seat == 2 ? godfrey : filler });
            game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            void Flush()
            {
                session.Flush(); game.PowerHistory.Last.Clear();
                var zones = ZoneStateOracle.Compare(game, session.Client, match.WireReturnId).ToArray();
                SelfChecks.Check(zones.Length == 0, "Godfrey zones: " + string.Join("; ", zones));
            }
            PegasusGame.PowerHistoryData[] Rows() => session.Packets.Where(p => p.Id == 19).SelectMany(p => {
                var history = new PegasusGame.PowerHistory();
                using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
                history.Deserialize(stream);
                return history.List;
            }).ToArray();
            Flush();
            var owner = seat == 1 ? game.Player1 : game.Player2;
            var effect = owner.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_509e");
            SelfChecks.Check(session.Client.Entities[owner.Hero.Id].Get((GameTag)4717) == 1,
                "Godfrey hero owns persistent portal for both viewers");
            SelfChecks.Check(session.Client.Entities[effect.Id].CardId == "JAIL_509e", "Godfrey public player enchantment");
            while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            var retained = Entity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var other = Entity.FromCard(owner, Cards.FromId("CS2_024"), zone: owner.DeckZone);
            Flush();
            session.Packets.Clear();
            Generic.Draw(owner, retained); Generic.Draw(owner, other); Flush();
            var display = Rows().Single(r => r.HasShowEntity && r.ShowEntity.Name == "CS2_029" && r.ShowEntity.Tags.Any(t => t.Name == 4722 && t.Value == 319)).ShowEntity;
            SelfChecks.Check(display.Name == "CS2_029" && display.Entity != retained.Id
                && display.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD)
                && session.Client.Entities[display.Entity].CardId == "CS2_029"
                && session.Client.Entities[display.Entity].Get(GameTag.ZONE) == (int)Zone.GRAVEYARD
                && session.Client.Entities[display.Entity].Get(GameTag.REVEALED) == 0
                && !Rows().Any(r => r.HasHideEntity && r.HideEntity.Entity == display.Entity
                    || r.HasTagChange && r.TagChange.Entity == display.Entity && r.TagChange.Tag == (int)GameTag.ZONE)
                && session.Client.Entities[retained.Id].CardId == "", "public burn actor stays in graveyard; retained actor stays private");
            SelfChecks.Check(session.Client.Entities[effect.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 2,
                "Godfrey native backlog count increments");
            session.Packets.Clear();
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
            var selected = new[] { retained, other }.Single(card => card.Zone == owner.HandZone);
            var returned = Rows().Single(r => r.HasSubSpellStart && r.SubSpellStart.SpellPrefabGuid == DrawReplacementRules.ReturnPrefab);
            SelfChecks.Check(returned.SubSpellStart.SourceEntityId == owner.Hero.Id
                && returned.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { match.WireReturnId(selected.Id) }),
                "return override targets anonymous opposing card without retargeting to hero");
            SelfChecks.Check(session.Client.Entities[effect.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 1,
                "Godfrey native backlog count decrements");
            SelfChecks.Check(session.Client.Entities[match.WireReturnId(selected.Id)].CardId == (seat == 1 ? selected.Card.Id : ""),
                "Godfrey returned hand identity remains viewer-specific");
            if (seat == 2)
            {
                int alias = match.WireReturnId(selected.Id);
                SelfChecks.Check(alias != retained.Id && alias != other.Id && alias != display.Entity
                    && session.Client.Entities[retained.Id].Get(GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.SETASIDE
                    && session.Client.Entities[other.Id].Get(GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.SETASIDE
                    && session.Client.Entities[alias].Get(GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.HAND,
                    "two publicly correlated burns cannot identify anonymous return through old actors");
            }
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
            SelfChecks.Check(retained.Zone == owner.HandZone && other.Zone == owner.HandZone,
                "both retained originals return with exact client zone membership");
            effect.Remove(); Flush();
            SelfChecks.Check(session.Client.Entities[owner.Hero.Id].Get((GameTag)4717) == 0,
                "removing protection requests native portal Death");
            session.History.Finish();
            SelfChecks.Check(session.History.Findings.Count == 0, "Godfrey history shape: "
                + string.Join("; ", session.History.Findings.Select(f => f.Rule + " " + f.Detail)));
        }
        Console.WriteLine("PASS Godfrey HistoryShape: opening, retention and return, both viewers");
    }
}
