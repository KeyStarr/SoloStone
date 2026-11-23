# Nefarian Rises! (local co-op)

The Nefarian Rises! Tavern Brawl for two people at one computer. One player is Anduin (Priest) and the other Morgl
(Shaman), each with the brawl's preset deck, and together they fight Nefarian. If either hero falls, or either player
concedes, you both lose. Kill Nefarian and you both win.

## How to play

1. On the home screen, press **Tavern Brawl** (the fourth slot of the box), then **Play**.
2. The two classes are dealt at random; the player whose hero is at the bottom of the screen starts.
3. When a player ends the turn, a **"<name>'s turn"** window asks to hand the screen over. The next player presses
   **Continue**, and only then is that player's hand shown.
4. The player who starts mulligans; the second player keeps the opening hand (see *Differences from retail*).

There is no turn timer. The match does not count for the ladder, gives no gold or experience, and is not saved: a match
that is closed or crashes is simply over.

## Nefarian

- A 0/200 minion with Taunt that can't be targeted by spells. He always stands on the side of the player whose turn it is
  **not**, so the active player attacks him, and his Taunt keeps the two of you from attacking each other.
- At the end of every turn he moves to the side of the player who just ended it (a full side has all its minions destroyed
  first) and casts a spell for that player: Bamboozle (the players swap hands), Cleave, Flame Missiles, Immolate or an
  Explosive Rune. A Rune explodes for 9 at the start of its owner's next turn, so the teammate has to destroy it first.
  Some turns he casts nothing.
- At 160 and at 60 Health a hit stops at the threshold and he is Immune for the rest of the turn. At 160 he gains 5 Attack
  and casts Elemental Eruption; at 60 he gains 5 more, casts Twisted Light (heals himself 30, 30 damage to every other
  minion) and from then on uses the stronger forms of his spells. If you take too long, he gains his Attack anyway.
- He hovers in purple smoke, plays his swap effect when he changes sides, and each of his spells plays an effect (missiles,
  a swipe, a fireball, a lightning storm and others), all from your game client.
- He speaks with the voice lines in your game client: on the first turn, with every Bamboozle, Elemental Eruption, Glorious
  Finale and Twisted Light, and each time he gains Attack.

## The brawl's cards

| Card | Text |
|---|---|
| Main Tank | Battlecry: Give all other minions +2/+2, except the Boss |
| Raid Healer | Whenever your hero is healed, also heal your teammate for that much |
| Dragonscale Warrior | Whenever any player targets this minion with a spell, that player draws a card |
| Freewheeling Skulker | At the end of your turn, switch sides (it is Immune while attacking on the teammate's side) |
| Intrepid Dragonstalker | Whenever ANY player plays a card, gain +1/+1 |

## Differences from retail

- The brawl's game script plays a line for each of a few event numbers but names no moment for them. Which line goes with
  which spell here follows what the lines are about (minds manipulated for Bamboozle, the Shaman's elements for Elemental
  Eruption, the Priest's light for Twisted Light, the finale for Glorious Finale).
- His card holds one effect for each of his spells but does not say which is which; the pairing here follows what each
  effect looks like (missiles for Flame Missiles, a swipe for Cleave, a fireball for Immolate, a lightning storm for Elemental
  Eruption, a holy nova for Twisted Light, an arcane burst for the Rune and an arcane explosion for Bamboozle).
- Nefarian's retail spell order was never published. His choice here is a seeded random pick from the spells the wiki lists
  for each phase, never the same twice in a row; the turn count after which he gains Attack on his own is ours.
- The second player cannot mulligan: the game's mulligan screen opens only once, at the start, for the player at the
  screen.
- The decks are the current lists from the [Hearthstone Wiki](https://hearthstone.wiki.gg/wiki/Nefarian_Rises!) (the Priest
  deck without the earlier Shadow or Light?).

## For developers

- Content: `data/coop/nefarian-rises-v1.json` (scenario 1865, board 7, the two decks, the boss); parsed by
  `plugin/CoopDefinition.cs`, started by `plugin/NativeCoopMode.cs`.
- Engine: `GameConfig.Coop` and `Actions/CoopRules.cs` (the shared result), `CardSets/Client/Adventures/NefarianRises*`
  (Nefarian, his spells, the player cards). Proof: `dotnet run --project tests/engine-proof/EngineProof.csproj -- --nefarian-rises-only`.
- Hot-seat: `NativeMatch` plays the seat in `localSeat` and hands the client the other seat with a game-state-swap
  `GAME_RESET` (the new seat gets account Lo 1 and `TEAM_ID` 1). The debug scenario key `hotSeat=1` stages any match
  this way; test control's `handover-continue` answers the handover window.
- Developer builds also read the debug scenario file at the brawl's start (`human…` keys = the player who starts, `npc…`
  keys = the other player), e.g. `humanBoard`/`npcBoard` with `…BoardStats=999,999` to reach the win in three turns.
