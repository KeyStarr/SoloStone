using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>What a wish is for. Zephrys offers the best card of the strongest categories.</summary>
    internal enum WishCategory { BoardClear, Removal, Counter, Survival, Burst, Proactive, DrawGen }

    [Flags]
    internal enum WishFlags
    {
        None = 0,
        /// <summary>Outcome is random: judged on several seeded runs, and lethal only if every run is lethal.</summary>
        Random = 1,
        /// <summary>Retail never finds lethal through this card (wiki: Tundra Rhino, Blessing of Might, Sinister Strike).</summary>
        NeverForLethal = 2,
        /// <summary>Offered only when it can be played this turn (Harrison Jones).</summary>
        ThisTurnOnly = 4,
        /// <summary>Draws cards: not offered when the deck is (nearly) empty.</summary>
        Draws = 8,
    }

    /// <summary>Which characters a targeted wish is tried on. Pointless targets are skipped to keep the search small.</summary>
    internal enum WishTargets { Enemy, Friendly, Any }

    internal sealed class WishEntry
    {
        internal string Id;
        internal WishCategory Category;
        internal WishFlags Flags;
        internal WishTargets Targets;
        /// <summary>Cheap test on public state; null means always eligible.</summary>
        internal Func<WishView, bool> Gate;
    }

    /// <summary>
    /// Zephrys the Great's pool: Basic and Classic cards as they stood before patch 20.0, without any card that was ever in the
    /// Hall of Fame (wiki Hall_of_Fame roster), without return-to-hand cards (Shadowstep, Youthful and Ancient Brewmaster) and
    /// without Zephrys, curated to the "interesting" cards. The 2020 Priest and Demon Hunter Initiate additions are left out
    /// because the engine has no rows for them. Sources: wiki Template:Zephrys_the_Great_notes (offers and lethal notes),
    /// Blizzard patch notes 15.0.4-30.4, and the Fireplace simulator's candidate list as a cross-check only.
    /// The order is fixed: it breaks ties, so it must never depend on hashing.
    /// </summary>
    internal static class ZephrysPool
    {
        private const string Doomsayer = "NEW1_021";

        internal static readonly WishEntry[] Entries = Build();

        private static WishEntry[] Build()
        {
            var list = new List<WishEntry>();
            void Add(string id, WishCategory category, WishTargets targets = WishTargets.Enemy, WishFlags flags = WishFlags.None,
                Func<WishView, bool> gate = null) =>
                list.Add(new WishEntry { Id = id, Category = category, Flags = flags, Targets = targets, Gate = gate });

            const WishCategory Clear = WishCategory.BoardClear, Kill = WishCategory.Removal, Counter = WishCategory.Counter,
                Live = WishCategory.Survival, Burst = WishCategory.Burst, Board = WishCategory.Proactive, Draw = WishCategory.DrawGen;
            const WishTargets E = WishTargets.Enemy, F = WishTargets.Friendly, A = WishTargets.Any;
            const WishFlags R = WishFlags.Random, D = WishFlags.Draws;

            // Board clears.
            Add("EX1_400", Clear, A);                                   // Whirlwind
            Add("CS2_025", Clear);                                      // Arcane Explosion
            Add("EX1_129", Clear, flags: D);                            // Fan of Knives
            Add("EX1_259", Clear, flags: R);                            // Lightning Storm
            Add("CS2_012", Clear);                                      // Swipe
            Add("CS2_062", Clear);                                      // Hellfire
            Add("CS2_093", Clear);                                      // Consecration
            Add("CS2_233", Clear, gate: v => v.MyWeaponAttack > 0);    // Blade Flurry
            Add("EX1_275", Clear, gate: v => v.EnemyMinions > 0);       // Cone of Cold
            Add("EX1_303", Clear, F, gate: v => v.MyMinions > 0);       // Shadowflame
            Add("EX1_626", Clear, flags: D, gate: v => v.EnemyMinions > 0); // Mass Dispel
            Add("CS1_112", Clear);                                      // Holy Nova
            Add("EX1_407", Clear, flags: R, gate: v => v.EnemyMinions > 0); // Brawl
            Add("CS2_028", Clear, gate: v => v.EnemyMinions > 0);       // Blizzard
            Add("CS2_064", Clear);                                      // Dread Infernal
            Add("CS2_032", Clear, gate: v => v.EnemyMinions > 0);       // Flamestrike
            Add("EX1_312", Clear, gate: v => v.EnemyMinions > 0);       // Twisting Nether
            Add("EX1_249", Clear);                                      // Baron Geddon
            Add("NEW1_030", Clear, gate: v => v.EnemyMinions > 0);      // Deathwing
            Add("CS2_026", Clear, gate: v => v.UnfrozenEnemyMinions > 0); // Frost Nova: never against frozen enemies (17.0)
            Add("NEW1_007", Clear, gate: v => v.EnemyMinions > 1);      // Starfall: never against a single minion (16.0)
            Add("EX1_251", Clear, flags: R, gate: v => v.EnemyMinions > 0); // Forked Lightning
            Add("CS2_114", Clear, flags: R, gate: v => v.EnemyMinions > 0); // Cleave
            Add("DS1_183", Clear, flags: R, gate: v => v.EnemyMinions > 0); // Multi-Shot
            Add("EX1_537", Clear);                                      // Explosive Shot
            Add("EX1_126", Clear, gate: v => v.EnemyMinions > 1);       // Betrayal (15.0.4: counts as removal too)

            // Single-target removal.
            Add("CS2_008", Kill);                                       // Moonfire
            Add("CS2_072", Kill);                                       // Backstab
            Add("EX1_332", Kill, A);                                    // Silence: also unfreezes or frees a friendly attacker for lethal
            Add("EX1_607", Kill, A);                                    // Inner Rage
            Add("CS2_037", Kill);                                       // Frost Shock
            Add("DS1_185", Kill);                                       // Arcane Shot
            Add("EX1_238", Kill);                                       // Lightning Bolt
            Add("EX1_245", Kill);                                       // Earth Shock
            Add("EX1_302", Kill);                                       // Mortal Coil
            Add("EX1_308", Kill);                                       // Soulfire
            Add("EX1_360", Kill);                                       // Humility
            Add("EX1_410", Kill, gate: v => v.MyArmor > 0);             // Shield Slam
            Add("EX1_578", Kill, gate: v => v.MyHeroAttack > 0);        // Savagery
            Add("CS2_024", Kill);                                       // Frostbolt
            Add("CS2_084", Kill);                                       // Hunter's Mark
            Add("CS2_108", Kill);                                       // Execute
            Add("CS2_234", Kill);                                       // Shadow Word: Pain
            Add("EX1_059", Kill, A);                                    // Crazed Alchemist (never on your own Doomsayer: target filter)
            Add("EX1_124", Kill);                                       // Eviscerate
            Add("EX1_581", Kill);                                       // Sap
            Add("EX1_154", Kill);                                       // Wrath
            Add("CS2_057", Kill);                                       // Shadow Bolt
            Add("CS2_203", Kill, A);                                    // Ironbeak Owl
            Add("EX1_134", Kill);                                       // SI:7 Agent
            Add("EX1_241", Kill);                                       // Lava Burst
            Add("EX1_539", Kill);                                       // Kill Command
            Add("EX1_617", Kill, flags: R, gate: v => v.EnemyMinions > 0); // Deadly Shot
            Add("EX1_622", Kill);                                       // Shadow Word: Death
            Add("CS2_022", Kill);                                       // Polymorph
            Add("CS2_029", Kill);                                       // Fireball
            Add("CS2_094", Kill, flags: D);                             // Hammer of Wrath
            Add("EX1_246", Kill);                                       // Hex
            Add("EX1_334", Kill);                                       // Shadow Madness
            Add("CS2_076", Kill);                                       // Assassinate
            Add("EX1_005", Kill);                                       // Big Game Hunter
            Add("NEW1_041", Kill, flags: R, gate: v => v.EnemyMinions > 0); // Stampeding Kodo
            Add("CS2_042", Kill);                                       // Fire Elemental
            Add("EX1_002", Kill);                                       // The Black Knight
            Add("EX1_091", Kill);                                       // Cabal Shadow Priest
            Add("EX1_309", Kill);                                       // Siphon Soul
            Add("CS1_113", Kill);                                       // Mind Control
            Add("EX1_279", Kill);                                       // Pyroblast

            // Counters to one specific threat.
            Add("EX1_066", Counter, gate: v => v.EnemyWeapon);          // Acidic Swamp Ooze
            Add("NEW1_025", Counter, gate: v => v.EnemyWeapon);         // Bloodsail Corsair
            Add("EX1_558", Counter, flags: WishFlags.ThisTurnOnly | D, gate: v => v.EnemyWeapon); // Harrison Jones
            Add("EX1_544", Counter, flags: D, gate: v => v.EnemySecrets > 0 || v.EnemyStealth); // Flare
            Add("EX1_186", Counter, flags: R, gate: v => v.EnemySecrets > 0); // SI:7 Infiltrator
            Add("NEW1_017", Counter, A, gate: v => v.Murlocs);          // Hungry Crab
            Add("NEW1_003", Counter, A, gate: v => v.Demons);           // Sacrificial Pact
            Add("EX1_590", Counter, gate: v => v.DivineShields);        // Blood Knight

            // Survival.
            Add("CS2_041", Live, F);                                    // Ancestral Healing
            Add("EX1_621", Live, F);                                    // Circle of Healing
            Add("EX1_192", Live, F);                                    // Radiance
            Add("CS2_089", Live, F);                                    // Holy Light
            Add("CS2_007", Live, F);                                    // Healing Touch
            Add("EX1_606", Live, flags: D);                             // Shield Block
            Add("CS2_088", Live);                                       // Guardian of Kings
            Add("EX1_354", Live, F, flags: D);                          // Lay on Hands
            Add("CS2_027", Live);                                       // Mirror Image
            Add("EX1_570", Live, E);                                    // Bite
            Add("CS2_005", Live);                                       // Claw
            Add("EX1_561", Live, A);                                    // Alexstrasza (also burst: either hero)

            // Burst.
            Add("CS1_129", Burst, F);                                   // Inner Fire
            Add("CS2_075", Burst, flags: WishFlags.NeverForLethal);     // Sinister Strike
            Add("CS2_087", Burst, F, WishFlags.NeverForLethal);         // Blessing of Might
            Add("CS2_039", Burst, F);                                   // Windfury
            Add("CS2_045", Burst, F);                                   // Rockbiter Weapon
            Add("CS2_073", Burst, F);                                   // Cold Blood
            Add("CS2_105", Burst);                                      // Heroic Strike
            Add("CS2_011", Burst);                                      // Savage Roar
            Add("CS2_046", Burst);                                      // Bloodlust
            Add("EX1_538", Burst, gate: v => v.EnemyMinions > 0);       // Unleash the Hounds
            Add("EX1_355", Burst, F);                                   // Blessed Champion
            Add("EX1_384", Burst, flags: R);                            // Avenging Wrath (15.0.4: now offered)
            Add("CS2_074", Burst, gate: v => v.MyWeaponAttack > 0);     // Deadly Poison
            Add("EX1_543", Burst);                                      // King Krush
            Add("CS2_146", Burst, gate: v => v.MyWeaponAttack > 0);     // Southsea Deckhand
            Add("DS1_178", Burst, flags: WishFlags.NeverForLethal);     // Tundra Rhino
            Add("CS2_104", Burst, F);                                   // Rampage
            Add("EX1_587", Burst, F, gate: v => v.MyMinions > 0);       // Windspeaker

            // Proactive: minions, weapons, buffs and ramp.
            Add("CS2_065", Board);                                      // Voidwalker
            Add("EX1_319", Board);                                      // Flame Imp
            Add("EX1_608", Board);                                      // Sorcerer's Apprentice
            Add("CS2_009", Board, F);                                   // Mark of the Wild
            Add("EX1_014", Board);                                      // King Mukla
            Add("EX1_507", Board);                                      // Murloc Warleader
            Add("NEW1_031", Board, flags: R);                           // Animal Companion
            Add("EX1_248", Board);                                      // Feral Spirit
            Add("CS2_033", Board);                                      // Water Elemental
            Add("EX1_043", Board, gate: v => v.MyHand >= 5);            // Twilight Drake
            Add("EX1_093", Board, gate: v => v.MyMinions > 0);          // Defender of Argus
            Add("DS1_070", Board, F);                                   // Houndmaster
            Add("NEW1_022", Board);                                     // Dread Corsair
            Add("EX1_165", Board);                                      // Druid of the Claw
            Add("EX1_250", Board);                                      // Earth Elemental
            Add("EX1_534", Board);                                      // Savannah Highmane
            Add("EX1_564", Board, A, gate: v => v.MyMinions + v.EnemyMinions > 0); // Faceless Manipulator
            Add("EX1_178", Board);                                      // Ancient of War
            Add("EX1_190", Board, gate: v => v.FriendlyDiedThisTurn > 0); // High Inquisitor Whitemane
            Add("EX1_383", Board);                                      // Tirion Fordring
            Add("EX1_323", Board);                                      // Lord Jaraxxus
            Add("EX1_572", Board);                                      // Ysera
            Add("EX1_586", Board);                                      // Sea Giant
            Add("CS2_013", Board);                                      // Wild Growth
            Add("EX1_613", Board);                                      // Edwin VanCleef
            Add("CS2_092", Board, F);                                   // Blessing of Kings
            Add("EX1_155", Board, F);                                   // Mark of Nature
            Add("CS2_004", Board, F, D);                                // Power Word: Shield
            Add("EX1_160", Board);                                      // Power of the Wild
            Add("EX1_183", Board, gate: v => v.MyMinions > 0);          // Gift of the Wild
            Add("EX1_158", Board, gate: v => v.MyMinions > 0);          // Soul of the Forest
            Add("CS2_097", Board);                                      // Truesilver Champion
            Add("CS2_080", Board);                                      // Assassin's Blade
            Add("CS2_112", Board);                                      // Arcanite Reaper
            Add("DS1_188", Board);                                      // Gladiator's Longbow
            Add("EX1_567", Board);                                      // Doomhammer
            Add("EX1_411", Board);                                      // Gorehowl
            Add("EX1_536", Board);                                      // Eaglehorn Bow
            Add("NEW1_027", Board);                                     // Southsea Captain

            // Card draw and generation: never with a (nearly) empty deck, generation instead (17.0).
            Add("CS2_023", Draw, flags: D);                             // Arcane Intellect
            Add("CS2_077", Draw, flags: D);                             // Sprint
            Add("EX1_164", Draw);                                       // Nourish (ramp option stays useful)
            Add("EX1_392", Draw, flags: D);                             // Battle Rage
            Add("EX1_096", Draw, flags: D);                             // Loot Hoarder
            Add("EX1_339", Draw, gate: v => v.EnemyDeck >= 2);          // Thoughtsteal
            Add("EX1_189", Draw, flags: R);                             // Brightwing
            return list.ToArray();
        }

        /// <summary>Card draw needs cards left to draw.</summary>
        internal static bool Eligible(WishEntry entry, WishView view) =>
            (!entry.Flags.HasFlag(WishFlags.Draws) || view.MyDeck >= 3) && (entry.Gate == null || entry.Gate(view));

        /// <summary>Documented target exceptions.</summary>
        internal static bool TargetAllowed(WishEntry entry, Controller me, IPlayable target)
        {
            if (target == null) return true;
            bool friendly = target.Controller.Id == me.Id;
            if (entry.Id == "EX1_059" && friendly && target.Card.Id == Doomsayer) return false;
            return entry.Targets switch {
                WishTargets.Enemy => !friendly,
                WishTargets.Friendly => friendly,
                _ => true
            };
        }

        internal static bool IsDoomsayer(IPlayable p) => p.Card.Id == Doomsayer;
    }
}
