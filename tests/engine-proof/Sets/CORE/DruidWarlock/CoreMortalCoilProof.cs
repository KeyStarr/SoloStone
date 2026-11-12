using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreMortalCoilProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_302", (g, p, id) => {
        var enemy = Summon(p.Opponent, "EX1_506a"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, enemyOnly: false); PoolPilotProof.EmptyHand(p);
        var top = p.DeckZone.TopCard; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Single().Id == top.Id, "killing target draws exact top card");
        int hand = p.HandZone.Count; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Damage == 1 && p.HandZone.Count == hand, "surviving friendly target gives no draw");
        var shield = Summon(p.Opponent, "EX1_008"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.HandZone.Count == hand, "prevented damage gives no draw");
        PoolPilotProof.EmptyHand(p); var spellDamage = Summon(p, "CS2_142"); enemy = Summon(p.Opponent, "CS2_172");
        top = p.DeckZone.TopCard; int deck = p.DeckZone.Count; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Single().Id == top.Id && p.DeckZone.Count == deck - 1,
            "+1 Spell Damage kills two-health target and draws exactly the top card");
        spellDamage.Silence(); g.DeathProcessingAndAuraUpdate();
        PoolPilotProof.EmptyHand(p); for (int i=0;i<9;i++) Add(p,"CS2_182");
        enemy = Summon(p.Opponent,"EX1_506a"); top=p.DeckZone.TopCard; Play(g,p,id,enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Any(c=>c.Id==top.Id), "playing from full hand frees the slot for draw");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); enemy=Summon(p.Opponent,"EX1_506a"); Play(g,p,id,enemy);
        Core2026Proof.Check(p.Hero.Fatigue==1 && p.Hero.Damage==1 && p.HandZone.Count==0, "lethal Coil attempts draw from empty deck and fatigues");
    }, legacyPoolMember: true);
}
