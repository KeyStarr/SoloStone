using SabberStoneCore.Model.Zones;
using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class KnockbackProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_517", (g, p, id) => {
        Privacy(g, id);
        var held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Card.ConsumesHistory && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "Knockback declares history consumer and displays base damage");
        var target = Summon(p.Opponent, "CS2_200");
        var friendly = Summon(p, "CS2_200");
        var options = p.Options().Where(t => t.Source?.Id == held.Id).ToArray();
        Core2026Proof.Check(options.Length == 2 && options.All(t => t.Target is Minion)
            && options.Any(t => t.Target.Id == target.Id) && options.Any(t => t.Target.Id == friendly.Id),
            "requires minion target on either side; heroes excluded");
        PlayEntity(g, p, held, target);
        Core2026Proof.Check(target.Damage == 1, "Knockback base damage");
        held = Add(p, id); g.AuraUpdate();
        UnderbrushTrackerProof.Shuffle(g, p.Opponent, 3);
        Generic.ShuffleIntoDeck(p, p.Opponent.Hero, Entity.FromCard(p, Cards.FromId("CS2_231")));
        Generic.ShuffleIntoDeck(p.Opponent, p.Hero, Entity.FromCard(p.Opponent, Cards.FromId("CS2_231")));
        p.DeckZone.Shuffle(); g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "opponent actor, foreign destination and plain deck reorder excluded");
        var trade = Add(p, "CORE_EX1_002"); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)) && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "Trade does not improve damage");
        UnderbrushTrackerProof.Shuffle(g, p, 3);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "three shuffled cards improve damage once");
        End(g); End(g);
        var late = Add(p, id);
        var deckCopy = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        var drawn = Generic.Draw(p, deckCopy); g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
            && drawn.Id == deckCopy.Id && drawn[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "previous-turn shuffle counts before creation and physical draw");
        var clone = g.Clone();
        UnderbrushTrackerProof.Shuffle(clone, clone.Player1);
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
            && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "clone history and display diverge independently");
        var clonedSpell = clone.IdEntityDic[held.Id];
        clonedSpell[GameTag.TAG_SCRIPT_DATA_NUM_1] = 999;
        PlayEntity(clone, clone.Player1, clonedSpell, (Minion)clone.IdEntityDic[target.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[target.Id]).Damage == 4 && target.Damage == 1,
            "resolution reads branch history, never cached display; original target unaffected");
        Summon(p, "CS2_052");
        PlayEntity(g, p, held, friendly);
        Core2026Proof.Check(friendly.Damage == 3, "friendly minion takes improved damage plus Spell Damage");
        var full = g.Clone(); var owner = full.Player1;
        while (owner.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) owner.DeckZone.Add(Entity.FromCard(owner, Cards.FromId("CS2_231")));
        UnderbrushTrackerProof.Shuffle(full, owner, 3);
        Core2026Proof.Check(owner.DeckZone.Count == DeckZone.DeckMaximumCapcity && full.IdEntityDic[late.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "partially successful shuffle counts once");
        UnderbrushTrackerProof.Shuffle(full, owner, 3);
        Core2026Proof.Check(full.IdEntityDic[late.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
            && late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "full deck adds no operation; fork remains isolated");
        Automatic(g.Clone(), id);
    });

    private static void Automatic(Game g, string id)
    {
        var p = g.Player1;
        StandardSetsProof.ClearBoard(g, p); StandardSetsProof.ClearBoard(g, p.Opponent);
        var elusive = Summon(p.Opponent, "EDR_598");
        var stealth = Summon(p.Opponent, "CS2_231"); stealth.HasStealth = true;
        var immune = Summon(p.Opponent, "CS2_231"); immune.IsImmune = true;
        var legal = Summon(p.Opponent, "CS2_200");
        var ctx = new EffectContext(g, p.Id, p.Hero.Id);
        var spell = Entity.FromCard(p, Cards.FromId(id));
        var rng = g.Random.Clone(); rng.Next(1);
        ctx.CastSpell(spell.Id, HistoryCastKind.Effect); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(legal.Damage == 2 && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0
            && g.Random.Next() == rng.Next(), "automatic cast uses game RNG and legal minion targeting");
        Kill(g, legal); rng = g.Random.Clone();
        spell = Entity.FromCard(p, Cards.FromId(id));
        ctx.CastSpell(spell.Id, HistoryCastKind.Repeat); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(g.Random.Next() == rng.Next() && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0,
            "automatic required-target cast fizzles without RNG when no legal targets");
    }

    private static List<byte[]> Packets(Game g, out PegasusGame.PowerHistory history)
    {
        var packets = new List<byte[]>(); PegasusGame.PowerHistory sent = null;
        var match = new NativeMatch((id, message) => {
            using var stream = new MemoryStream(); message.Serialize(stream); packets.Add(stream.ToArray());
            sent = message as PegasusGame.PowerHistory;
        });
        typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(match, new object[] { g.PowerHistory.Full });
        history = sent;
        return packets;
    }

    private static void Privacy(Game baseline, string id)
    {
        foreach (int viewer in new[] { 1, 2 })
        foreach (int hiddenZone in new[] { 0, 1, 2 }) // Opponent hand, own deck, opponent deck.
        {
            Game Fixture(bool knockback)
            {
                var g = baseline.CloneExact(history: true);
                var viewingPlayer = viewer == 1 ? g.Player1 : g.Player2;
                var owner = hiddenZone == 1 ? viewingPlayer : viewingPlayer.Opponent;
                Entity.FromCard(owner, Cards.FromId(knockback ? id : "GAME_005"),
                    zone: hiddenZone == 0 ? owner.HandZone : owner.DeckZone);
                UnderbrushTrackerProof.Shuffle(g, owner, 3);
                return g;
            }
            var a = Fixture(true); var b = Fixture(false);
            var fa = FairBotState.Create(a, viewer, 42, DeckStyle.Control);
            var fb = FairBotState.Create(b, viewer, 42, DeckStyle.Control);
            var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
            var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
            ga.AuraUpdate(); gb.AuraUpdate();
            Core2026Proof.Check(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
                && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
                "hidden Knockback permutation preserves projected tags/evaluation/options for both viewers");
            if (viewer != 1) continue;
            var pa = Packets(a, out _); var pb = Packets(b, out _);
            Core2026Proof.Check(pa.Count == pb.Count && pa.Zip(pb).All(pair => pair.First.SequenceEqual(pair.Second)),
                "hidden Knockback permutation produces byte-identical opponent-visible native packets");
        }
        var visible = baseline.CloneExact(history: true); var p = visible.Player1;
        var card = Add(p, id); visible.AuraUpdate(); UnderbrushTrackerProof.Shuffle(visible, p, 3);
        Packets(visible, out var packet);
        Core2026Proof.Check(packet.List.Any(e => e.TagChange?.Entity == card.Id
            && e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 && e.TagChange.Value == 2),
            "owner receives updated damage tag through native transport");
    }
}
