using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Tier A: Blizzard 15.2 / 18.0.1 Discover rules; see CorePoolProof.
internal static class SilvermoonPortalProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_KAR_077", (g, p, id) => {
        var printed = ClientExport.TryCards();
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false);
        var location = LocationProofs.Location(p);
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, id, (Minion)next.IdEntityDic[friend.Id]);
            var token = next.Player1.BoardZone.Single(m => m.Id != friend.Id);
            CorePoolProof.Generated(next, next.Player1, id, token);
            if (printed != null)
            {
                var data = printed[token.Card.Id];
                Core2026Proof.Check(data.GetProperty("cost").GetInt32() == 2 && token.Controller == next.Player1
                    && token.Card.AssetId == data.GetProperty("dbfId").GetInt32()
                    && token.AttackDamage == data.GetProperty("atk").GetInt32() && token.Health == data.GetProperty("health").GetInt32(),
                    "random exact pinned 2-Cost minion identity/stats: " + token.Card.Id + " actual=" + token.Card.Cost + "/" + token.AttackDamage + "/" + token.Health + " export=" + data.GetProperty("cost") + "/" + data.GetProperty("atk") + "/" + data.GetProperty("health"));
            }
            else
            {
                ClientExport.Skip("Silvermoon Portal token against the client export");
                Core2026Proof.Check(token.Card.Cost == 2 && token.Controller == next.Player1, "random 2-Cost minion: " + token.Card.Id);
            }
            Core2026Proof.Check(next.Player1.BoardZone.GetSlot(2).Id == token.Id && next.Player1.BoardZone.GetSlot(1).Id == location.Id, "spell summon appends after physical location slot");
        });
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 7 && friend.AppliedEnchantments.Single().Card.Id == "KAR_077e", "exact +2/+2 Silver Might buff");
        friend.Silence(); Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5, "Silence removes buff");
        // A previously summoned Deathchiller can damage the enemy after this spell; verify the buff's maximum Health.
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.BaseHealth == 7 && p.Opponent.BoardZone.Count == 1, "enemy may be buffed, summon still belongs to caster");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id, friend);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && friend.AttackDamage == 6 && friend.Health == 7, "full board suppresses summon but still buffs target");
    }, legacyPoolMember: true);
}
