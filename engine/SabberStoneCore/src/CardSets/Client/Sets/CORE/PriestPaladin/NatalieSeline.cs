using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NatalieSeline
    {
        internal const int DbfId = 69617;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target)) return;
                int health = target.Health;
                // Her CardDef authors two sub-spells: the destroy on the victim, then the absorb into Natalie.
                using (SubSpell(g, s, target, DestroyPrefab))
                    new DestroyTask(EntityType.TARGET).Process(g, c, s, t, stack);
                using (SubSpell(g, s, target, AbsorbPrefab))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_198e"), (IPlayable)s, (IPlayable)s, health, 0, 0);
            }) };
        }

        internal const string DestroyPrefab = "NatalieSeline_Destroy_FX:0c0d7deab8ddbf04091e7a718a2db13b";
        internal const string AbsorbPrefab = "NatalieSeline_Absorb_FX:06a59d8bd8a6cc34f88c703092e61830";

        private static System.IDisposable SubSpell(Game g, IEntity source, IEntity target, string prefab)
        {
            if (!g.History) return null;
            g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart {
                Source = source.Id, SpellPrefabGuid = prefab, Targets = new List<int> { target.Id } });
            return new SubSpellEnd(g);
        }

        private sealed class SubSpellEnd : System.IDisposable
        {
            private readonly Game _game;
            internal SubSpellEnd(Game game) { _game = game; }
            public void Dispose() => _game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
