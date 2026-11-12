using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class EventualityProof
{
    // Lean_EventualityDamageThenImbue: design §6 / imbue-16; Bronze identity pinned by imbue-17/23.
    internal static void Run() => StandardSetsProof.Case("END_000", (g, p, id) => {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Type == CardType.SPELL && card.Class == CardClass.ROGUE
            && card[GameTag.SPELL_SCHOOL] == 6 && card.Cost == 2, "Eventuality is a 2-mana Rogue Shadow spell");
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id).Select(t => t.Target?.Id).ToHashSet();
        Core2026Proof.Check(targets.SetEquals(new int?[] { p.Hero.Id, p.Opponent.Hero.Id, friendly.Id, enemy.Id }),
            "either side's heroes and minions are exactly the legal targets");
        string before = Canonical.State(g);
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, spell)) && Canonical.State(g) == before,
            "missing mandatory target rejects before payment or Imbue");
        int cursor = g.PowerHistory.Full.Count;
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], next.Player1.Hero));
        var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
        int damageAt = Array.FindIndex(entries, e => e is PowerHistoryTagChange t
            && t.EntityId == p.Hero.Id && t.Tag == GameTag.DAMAGE && t.Value == 2);
        int imbueAt = Array.FindIndex(entries, e => e is PowerHistoryTagChange t
            && t.EntityId == p.Id && t.Tag == GameTag.IMBUE && t.Value == 1);
        var power = p.Hero.HeroPower;
        Core2026Proof.Check(p.Hero.Damage == 2 && power.Card.Id == "END_000p" && ImbueRules.Count(p) == 1
            && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && damageAt >= 0 && imbueAt > damageAt,
            "friendly hero takes two before Rogue installs Bronze");
        foreach (var target in new ICharacter[] { p.Opponent.Hero, friendly, enemy })
        {
            Play(g, p, id, target);
            Core2026Proof.Check(target.Damage == 2, "two damage to each remaining side/type");
        }
        Core2026Proof.Check(p.Hero.HeroPower.Id == power.Id && ImbueRules.Count(p) == 4
            && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && power[GameTag.TAG_SCRIPT_DATA_NUM_2] == 4,
            "later Eventualities upgrade same Bronze power and both text numbers");
        Summon(p, "CS2_052");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && ImbueRules.Count(p) == 5, "Spell Damage +1 deals three then Imbues");
        friendly.HasDivineShield = true;
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 2 && !friendly.HasDivineShield && ImbueRules.Count(p) == 6,
            "Divine Shield prevents damage but not later Imbue");
        p.Hero.IsImmune = true;
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 2 && ImbueRules.Count(p) == 7, "friendly Immune target prevents damage but still Imbues");
        enemy.IsImmune = true;
        spell = Add(p, id); before = Canonical.State(g);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == enemy.Id)
            && !g.Process(PlayCardTask.Any(p, spell, enemy)) && Canonical.State(g) == before,
            "hostile Immune target is illegal before payment");

        var supported = ImbueProof.Make(CardClass.PALADIN, (int)g.Seed); var owner = supported.Player1;
        Play(supported, owner, id, owner.Opponent.Hero);
        Core2026Proof.Check(owner.UsedMana == 2 && owner.Opponent.Hero.Damage == 2 && ImbueRules.Count(owner) == 1
            && owner.Hero.HeroPower.Card.Id == "EDR_445p", "Paladin ordinary cast damages then installs Dragon");
        foreach (bool context in new[] { false, true })
        {
            var direct = Add(owner, id); owner.SetasideZone.Add(owner.HandZone.Remove(direct));
            int progress = ImbueRules.Count(owner);
            Resume(supported, next => {
                var cp = next.Player1; var cast = (Spell)next.IdEntityDic[direct.Id];
                if (context) new EffectContext(next, cp.Id, cp.Hero.Id).CastSpell(cast.Id, HistoryCastKind.Effect, cp.Opponent.Hero.Id);
                else Generic.CastSpell(cp, next, cast, cp.Opponent.Hero, 0);
                next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
            });
            Core2026Proof.Check(owner.UsedMana == 2 && owner.Opponent.Hero.Damage == 2 * (progress + 1)
                && ImbueRules.Count(owner) == progress + 1, "Paladin indirect casts damage and Imbue without another payment");
        }
    });
}
