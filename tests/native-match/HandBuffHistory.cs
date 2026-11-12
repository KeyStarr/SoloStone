using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private const string HandBuffPrefab = "ReuseFX_Generic_HandBuff_ScaleUp_OpponentAE_Gold_Super:2fb243a4d1ef3324fa09d018ce3f4418";

    private static void HandBuffHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (var scenario in new[] { (Card: "CORE_RLK_712", Corpses: 0, Buffs: 1),
            (Card: "CORE_RLK_712", Corpses: 2, Buffs: 2),
            (Card: "CFM_753", Corpses: 0, Buffs: 1), (Card: "MEND_803", Corpses: 0, Buffs: 1) })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            var minions = (scenario.Card == "MEND_803" ? new[] { "CS2_101t", "CS2_101t" } : new[] { "CS2_182", "CS2_231" }).Select(id =>
                (Minion)EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone)).ToArray();
            var untouched = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(scenario.Card), zone: owner.HandZone);
            CorpseRules.Gain(owner, scenario.Corpses);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(minions.All(m => m.AttackDamage == m.Card.ATK + scenario.Buffs && m.Health == m.Card.Health + scenario.Buffs),
                "Hand buff stats unchanged: " + scenario);
            Check(CorpseRules.Available(owner) == 0, "Blood Tap spends only the two available Corpses");
            var enchants = minions.SelectMany(m => m.AppliedEnchantments).ToArray();
            Check(enchants.Length == 2 * scenario.Buffs, "Each hand minion receives both Blood Tap buffs");
            if (bot)
            {
                Check(!data.Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), "Hidden hand buffs must not emit targeted FX");
                Check(data.Where(e => enchants.Any(a => a.Id == SnapshotEntity(e))).All(e =>
                    string.IsNullOrEmpty(SnapshotName(e)) && SnapshotTag(e, GameTag.ATTACHED) == 0
                    && SnapshotTag(e, GameTag.CARDTYPE) == 0 && SnapshotTag(e, GameTag.CREATOR) == 0),
                    "Opponent hand enchantments must not leak identity, attachment or creator");
                Check(!data.Any(e => minions.Any(m => m.Id == SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                    "Opponent buff must not reveal host cards");
            }
            else
            {
                int activeTarget = 0, scopes = 0, changes = 0;
                foreach (var entry in data)
                {
                    if (entry.HasSubSpellStart)
                    {
                        Check(activeTarget == 0 && entry.SubSpellStart.SpellPrefabGuid == HandBuffPrefab
                            && entry.SubSpellStart.SourceEntityId == owner.Hero.Id && entry.SubSpellStart.TargetEntityIds.Count == 1,
                            "Hand buff uses retail prefab, owner hero and one explicit target");
                        activeTarget = entry.SubSpellStart.TargetEntityIds.Single();
                        Check(minions.Any(m => m.Id == activeTarget), "Only buffed minions become FX targets");
                        scopes++;
                    }
                    if (entry.HasShowEntity && enchants.Any(e => e.Id == entry.ShowEntity.Entity))
                        Check(activeTarget == SnapshotTag(entry, GameTag.ATTACHED), "Enchantment reveal belongs inside its hand FX scope");
                    if (entry.HasTagChange && minions.Any(m => m.Id == entry.TagChange.Entity)
                        && entry.TagChange.Tag is (int)GameTag.ATK or (int)GameTag.HEALTH)
                    {
                        Check(activeTarget == entry.TagChange.Entity, "Each hand stat change has its own active FX target");
                        changes++;
                    }
                    if (entry.HasSubSpellEnd) { Check(activeTarget != 0, "Balanced hand FX end"); activeTarget = 0; }
                }
                Check(activeTarget == 0 && scopes == enchants.Length && changes == enchants.Length * 2,
                    "Every buff, including Corpse bonus, has one complete animation range");
            }
            Check(untouched.AppliedEnchantments == null || untouched.AppliedEnchantments.Count == 0, "Spells not buffed");
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch();
            var owner = bot ? match.Game.Player2 : match.Game.Player1;
            var deckCard = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            Generic.AddEnchantmentBlock(match.Game, Cards.FromId("RLK_712e"), owner.Hero, deckCard);
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(!data.Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), "Deck buffs have no hand FX");
            Check(data.Where(e => deckCard.AppliedEnchantments.Any(a => a.Id == SnapshotEntity(e))).All(e =>
                string.IsNullOrEmpty(SnapshotName(e)) && SnapshotTag(e, GameTag.ATTACHED) == 0),
                "Neither player's deck enchantments reveal hidden hosts");
            capture.SerializeAll();
        }
        {
            var (match, capture) = AnimationMatch();
            var owner = match.Game.Player1;
            var minion = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            for (int i = 0; i < 2; i++)
            {
                Generic.AddEnchantmentBlock(match.Game, Cards.FromId("CFM_064e"), owner.Hero, minion);
                match.Game.AuraUpdate();
            }
            FlushFieldHistory(match);
            Check(minion.AttackDamage == minion.Card.ATK + 2 && minion.Health == minion.Card.Health + 2,
                "Repeated ongoing hand enchantment applies both stat increases");
            Check(History(capture, cursor).Count(e => e.HasSubSpellStart) == 2,
                "Ongoing enchantment count updates also animate each hand buff");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS hand-buff FX: Blood Tap base/Corpse bonus, Outfitter, hand auras, ongoing buffs, hidden hand/decks");
    }
}
