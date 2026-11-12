using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier C: https://hearthstone.wiki.gg/wiki/Explosive_Runes#Notes (health-based split).
// Tier E: https://us.forums.blizzard.com/en/hearthstone/t/explosive-runes-vs-divine-shield/96912 (independent split report).
// Tier E: https://us.forums.blizzard.com/en/hearthstone/t/prestors-pyromancer-explosive-runes-bugged-interaction/102323 (owner bonus at trigger time).
// Tier C: https://hearthstone.wiki.gg/wiki/Secret#Notes (removed target leaves secret unrevealed).
// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class ExplosiveRunesProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_101", (g, p, id) => {
        var secret = Play(g, p, id); Play(g, p, "CS2_182"); Core2026Proof.Check(p.SecretZone.Count == 1, "own minion never triggers secret");
        Summon(p, "CS2_142"); Summon(p.Opponent, "EX1_012"); Summon(p.Opponent, "CS2_142");
        End(g); Play(g, p.Opponent, "GAME_005"); var summoned = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.SecretZone.Count == 1 && summoned.Damage == 0, "opponent spells and summons do not trigger secret");
        PoolPilotProof.Resume(g, next => {
            var target = (Minion)Play(next, next.Player2, "CS2_171");
            Core2026Proof.Check(target.Zone == next.Player2.GraveyardZone && next.Player2.Hero.Damage == 6 && next.Player1.SecretZone.Count == 0, "owner Spell Damage adds one; opponent Spell Damage ignored; one Health leaves six excess across clone/snapshot");
            Play(next, next.Player2, "CS2_182"); Core2026Proof.Check(next.Player2.Hero.Damage == 6, "consumed secret cannot repeat");
        });
        End(g); Play(g, p, id); End(g); int before = p.Opponent.Hero.Damage;
        var shield = (Minion)Play(g, p.Opponent, "EX1_008");
        Core2026Proof.Check(shield.Zone == p.Opponent.BoardZone && shield.Health == 1 && !shield.HasDivineShield && p.Opponent.Hero.Damage == before + 6, "Shield blocks only minion allocation, six excess still hits hero");
        End(g); Play(g, p, id); End(g); before = p.Opponent.Hero.Damage;
        var large = (Minion)Play(g, p.Opponent, "EX1_563");
        Core2026Proof.Check(large.Zone == p.Opponent.BoardZone && large.Damage == 7 && large.Health == 5 && p.Opponent.Hero.Damage == before, "twelve Health survives exactly seven damage; its Spell Damage does not amplify enemy secret");
        End(g); ClearBoard(g, p); ClearBoard(g, p.Opponent); var retainedSecret = Play(g, p, id);
        var ambusher = Summon(p.Opponent, "FP1_026"); ambusher.Damage = 4;
        End(g);
        PoolPilotProof.Resume(g, next => {
            int heroDamage = next.Player2.Hero.Damage;
            var archer = Play(next, next.Player2, "CS2_189", (Minion)next.IdEntityDic[ambusher.Id]);
            Core2026Proof.Check(next.IdEntityDic[ambusher.Id].Zone == next.Player2.GraveyardZone && archer.Zone == next.Player2.HandZone,
                "Archer Battlecry kills one-Health Ambusher and its Deathrattle bounces Archer before secret");
            Core2026Proof.Check(next.Player1.SecretZone.Count == 1 && next.Player2.Hero.Damage == heroDamage && next.IdEntityDic[retainedSecret.Id][GameTag.REVEALED] == 0,
                "Runes stays unconsumed and deals no face damage when played minion already left board across clone/snapshot");
            Play(next, next.Player2, "CS2_171");
            Core2026Proof.Check(next.Player1.SecretZone.Count == 0 && next.Player2.Hero.Damage == heroDamage + 5,
                "preserved secret triggers normally on next board minion");
        });
    }, legacyPoolMember: true);
}
