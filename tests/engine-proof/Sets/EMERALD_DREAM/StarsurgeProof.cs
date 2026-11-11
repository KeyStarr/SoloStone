using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StarsurgeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_941", (g, p, id) => {
        Hidden(id);
        Automatic(id);
        LegacyProducers(id);
        var target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 50;
        Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 1, "base one damage");
        Kill(g, Summon(p, "CS2_231"));
        Kill(g, Summon(p.Opponent, "CS2_231"));
        Generic.DiscardBlock(p, Add(p, "CS2_231"));
        End(g); End(g);
        var source = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(source.Card.ConsumesHistory && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "prior-turn friendly death before creation; enemy death/discard excluded");
        var clone = g.Clone();
        Kill(clone, Summon(clone.Player1, "CS2_231"));
        PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[source.Id], (Minion)clone.IdEntityDic[target.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[target.Id]).Damage == 4 && target.Damage == 1, "clone diverges without changing original");
        Summon(p, "CS2_142");
        source[GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
        PlayEntity(g, p, source, target);
        Core2026Proof.Check(target.Damage == 4, "resolution reads deaths not display; Spell Damage applies");
        TargetRules(g, p, id, target, Summon(p, "CS2_182"), false);
    });

    private static void Automatic(string id)
    {
        foreach (var kind in new[] { HistoryCastKind.Effect, HistoryCastKind.Drawn, HistoryCastKind.Repeat })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            Kill(g, Summon(p, "CS2_231"));
            var legal = Summon(p.Opponent, "CS2_182");
            var elusive = Summon(p.Opponent, "EDR_598");
            var stealth = Summon(p.Opponent, "CS2_182"); stealth.HasStealth = true;
            var auto = Entity.FromCard(p, Cards.FromId(id));
            var rng = g.Random.CaptureState();
            new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, auto.Id).CastSpell(auto.Id, kind, elusive.Id);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(legal.Damage == 2 && elusive.Damage == 0 && stealth.Damage == 0 && !rng.Equals(g.Random.CaptureState()), $"automatic {kind}: legal={legal.Damage}, elusive={elusive.Damage}, stealth={stealth.Damage}, rngChanged={!rng.Equals(g.Random.CaptureState())}");
        }
    }

    private static void LegacyProducers(string id)
    {
        foreach (bool archivist in new[] { true, false })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            Kill(g, Summon(p, "CS2_231"));
            var elusive = Summon(p.Opponent, "EDR_598"); var target = Summon(p.Opponent, "CS2_182");
            IPlayable spell;
            if (archivist)
            {
                spell = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
                var source = Summon(p, "LOOT_414"); source.CantBeTargetedBySpells = true;
                End(g);
            }
            else
            {
                spell = Add(p, id); End(g);
                var source = (Minion)Add(p.Opponent, "DAL_538"); source.CantBeTargetedBySpells = true;
                PlayEntity(g, p.Opponent, source);
            }
            Core2026Proof.Check(target.Damage == 2 && elusive.Damage == 0 && spell.Zone.Type == Zone.GRAVEYARD,
                "actual Archivist/Saboteur chooses legal target and reads prior death");
        }
        var drawn = HistoryFactsProof.Fixture(); var owner = drawn.Player1;
        Kill(drawn, Summon(owner, "CS2_231"));
        var protectedMinion = Summon(owner.Opponent, "EDR_598"); var victim = Summon(owner.Opponent, "CS2_182");
        var card = Cards.FromId(id).Clone(); card.Tags[GameTag.CASTSWHENDRAWN] = 1;
        card.Power = new SabberStoneCore.Enchants.Power {
            PowerTask = card.Power.PowerTask,
            TopdeckTask = new SabberStoneCore.Tasks.SimpleTasks.CustomTask((g, c, s, t, stack) => Generic.CastSpell(c, g, (Spell)s, null, 0))
        };
        var automatic = Entity.FromCard(owner, card, zone: owner.DeckZone);
        Generic.Draw(owner, automatic); drawn.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(victim.Damage == 2 && protectedMinion.Damage == 0
            && drawn.HistoryFacts.Query.Casts().Count(f => f.EntityId == automatic.Id && f.CasterKind == HistoryCastKind.Drawn) == 2,
            "actual when-drawn dispatch chooses legal target and preserves Drawn attribution");
    }

    internal static void Hidden(string id, Action<Game, Controller> action = null)
    {
        foreach (bool deck in new[] { false, true })
        {
            Game Fixture(bool withCard)
            {
                var g = HistoryFactsProof.Fixture(); var owner = g.Player2;
                var card = Cards.FromId(withCard ? id : "GAME_005");
                if (deck) Entity.FromCard(owner, card, zone: owner.DeckZone); else Entity.FromCard(owner, card, zone: owner.HandZone);
                g.AuraUpdate();
                if (action != null) action(g, owner); else Kill(g, Summon(owner, "CS2_231"));
                g.AuraUpdate(); return g;
            }
            var a = Fixture(true); var b = Fixture(false);
            var packetsA = Packets(a); var packetsB = Packets(b);
            Core2026Proof.Check(packetsA.Count == packetsB.Count && packetsA.Zip(packetsB).All(pair => pair.First.SequenceEqual(pair.Second)), id + " hidden permutation serialized packets identical");
            var fa = FairBotState.Create(a, 1, 42, DeckStyle.Control); var fb = FairBotState.Create(b, 1, 42, DeckStyle.Control);
            var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
            var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
            ga.AuraUpdate(); gb.AuraUpdate();
            Core2026Proof.Check(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate() && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())), id + " hidden projected state/actions identical");
        }
    }

    internal static List<byte[]> Packets(Game g, int? skip = null)
    {
        var packets = new List<byte[]>();
        var match = new NativeMatch((id, message) => { using var stream = new MemoryStream(); message.Serialize(stream); packets.Add(stream.ToArray()); });
        typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
        var send = typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        if (skip.HasValue)
        {
            // Prime both transport caches, even for an empty prefix, before comparing action suffixes.
            send.Invoke(match, new object[] { g.PowerHistory.Full.Take(skip.Value).ToList() });
            packets.Clear();
        }
        send.Invoke(match, new object[] { g.PowerHistory.Full.Skip(skip ?? 0).ToList() });
        return packets;
    }
}
