using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;

internal static class PetrifiedOgreProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_253", (g, p, id) => {
        var ogre = (Minion)Play(g, p, id);
        Core2026Proof.Check(ogre.IsDormant && ogre.AttackDamage == 5 && ogre.Health == 5 && DormantRules.TurnsRemaining(ogre) == 0,
            "Unpinned_OgreEitherOr starts indefinite sleep without summon-turn roll");
        Core2026Proof.Check(ogre.AppliedEnchantments.Select(e => e.Card.Id).OrderBy(x => x).SequenceEqual(new[] { "TLC_253e", "TLC_253e1" }),
            "exact condition and asleep listener actors");
        Core2026Proof.Check(Cards.FromId("TLC_253e").AssetId == 118255 && Cards.FromId("TLC_253e1").AssetId == 126033
            && Cards.FromId("TLC_253e2").AssetId == 118256, "pinned helper identities");
        ogre.Silence(); ogre.Destroy(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ogre.IsDormant && !ogre.IsSilenced && ogre.Zone == p.BoardZone && !p.BoardZone.GetAll().Contains(ogre),
            "Unpinned_DormantExclusions ordinary Silence/destroy/active selectors exclude sleeper");
        var fireball = Add(p, "CS2_029");
        Core2026Proof.Check(!p.Options().Any(o => o.Source?.Id == fireball.Id && o.Target?.Id == ogre.Id), "sleeping minion unavailable to targeted spell");
        PoolPilotProof.EmptyHand(p);
        bool buff = false, wake = false;
        for (int turn = 0; turn < 12; turn++)
        {
            if (!ogre.IsDormant)
            {
                p.SetasideZone.Add(p.BoardZone.Remove(ogre));
                ogre = Summon(p, id);
            }
            int attack = ogre.AttackDamage, health = ogre.Health;
            var rng = g.Random.CaptureState();
            TemporaryRulesProof.End(g);
            Core2026Proof.Check(ogre.IsDormant && ogre.AttackDamage == attack && ogre.Health == health && g.Random.CaptureState().Equals(rng),
                "Unpinned_DormantWhile opponent start neither rolls nor buffs");
            var predicted = g.CloneExact(history: true);
            bool awakens = predicted.Random.Next(2) == 0;
            long draws = g.Random.DrawCount;
            int entity = ogre.Id;
            PoolPilotProof.Resume(g, TemporaryRulesProof.End);
            ogre = (Minion)g.IdEntityDic[entity];
            Core2026Proof.Check(g.Random.DrawCount == draws + 1 && g.Random.CaptureState().Equals(predicted.Random.CaptureState()),
                "Unpinned_OgreEitherOr exactly one game RNG coin per own start; clone/load same result");
            Core2026Proof.Check(ogre.IsDormant != awakens && ogre.AttackDamage == attack + (awakens ? 0 : 2)
                && ogre.Health == health + (awakens ? 0 : 2), "Unpinned_OgreEitherOr buff OR awaken, never both");
            if (awakens)
            {
                wake = true;
                Core2026Proof.Check(ogre.AppliedEnchantments.All(e => e.Card.Id != "TLC_253e" && e.Card.Id != "TLC_253e1")
                    && ogre[GameTag.DORMANT_AWAKENED_THIS_TURN] == 1 && ogre.IsExhausted,
                    "Unpinned_DormantCounterProjection Unpinned_DormantLifecycle wake retires helpers and preserves buffs/readiness");
            }
            else
            {
                buff = true;
                Core2026Proof.Check(ogre.AppliedEnchantments.Count(e => e.Card.Id == "TLC_253e2") == (ogre.AttackDamage - 5) / 2,
                    "each +2/+2 application has exact exported enchantment");
            }
        }
        Core2026Proof.Check(buff && wake, "deterministic seeds exercise both coin outcomes");
        if (ogre.IsDormant) DormantRules.Awaken(ogre);
        ogre.Silence(); DormantRules.Make(ogre, null, "TLC_253e", p.Hero);
        var quiet = g.Random.CaptureState(); TemporaryRulesProof.End(g); TemporaryRulesProof.End(g);
        Core2026Proof.Check(ogre.IsDormant && ogre.AttackDamage == 5 && g.Random.CaptureState().Equals(quiet)
            && ogre.AppliedEnchantments.All(e => e.Card.Id != "TLC_253e1"), "awake Silence removes later while-asleep text");
        p.SetasideZone.Add(p.BoardZone.Remove(ogre));
        var departed = Summon(p, id); var stale = departed.AppliedEnchantments.Single(e => e.Card.Id == "TLC_253e1");
        p.SetasideZone.Add(p.BoardZone.Remove(departed)); quiet = g.Random.CaptureState();
        stale.Card.Power.Trigger.SingleTask.Process(g, p, stale, null, new TaskStack());
        TemporaryRulesProof.End(g); TemporaryRulesProof.End(g);
        Core2026Proof.Check(departed.AttackDamage == 5 && departed.AppliedEnchantments.Count == 0 && g.Random.CaptureState().Equals(quiet),
            "Unpinned_DormantWhile source exit retires helpers; stale callback cannot consume RNG");
    });
}
