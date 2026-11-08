# SoloStone

SoloStone is an unofficial offline single-player mod for Hearthstone®. It requires your own legally obtained Hearthstone client. Not affiliated with, endorsed by, or sponsored by Blizzard Entertainment. Hearthstone is a trademark or registered trademark of Blizzard Entertainment, Inc.

**Play Hearthstone offline, on your own PC, against computer opponents.** You get the game's own screens, ranked and
casual matches, deck building, packs and progress, all kept on your machine.

[Install](docs/INSTALL.md) · [How it works](#how-it-works) · [Contributing](#contributing) · [Build from source](docs/BUILDING.md)

## What it is

- **A mod for a separate copy of your game.** SoloStone copies the Hearthstone you already have, adds its own files
  to the copy and blocks the copy from the internet. Your normal Hearthstone folder is only read.
- **A local game with computer opponents.** Opponents have their own decks, skill levels and personalities. You climb
  a local ladder, build decks, open packs and earn rewards by playing.
- **One fixed card set.** The first release plays the Standard format as it stood just before the *Reign of the Black
  Empire* expansion (the "Violet Hold meta" in the files). It does not follow later balance changes.

## What it is not

- **Not online.** No server, no matchmaking, no multiplayer. SoloStone never logs in anywhere.
- **Not a way to get Hearthstone.** You need the game installed through Battle.net first. SoloStone ships no
  Hearthstone program files, art or sound.
- **Not for sale.** SoloStone takes no money of any kind, from anyone.

The card data file does contain card names and English card text taken from the game, which belong to Blizzard.
[NOTICE](NOTICE) says what is where.

## How it works

```mermaid
flowchart LR
    HS["Your Hearthstone<br/>(only read)"]
    GH["GitHub releases"]
    subgraph PC["Your PC"]
        L["SoloStone launcher<br/>checks version and firewall"]
        subgraph COPY["Separate game copy, blocked from the internet"]
            UI["Game screens"]
            P["SoloStone plugin"]
            E["Rules engine<br/>(based on SabberStone)"]
            AI["Computer opponents"]
        end
        S["Your saves"]
    end
    HS -->|copied once| COPY
    GH -.->|signed updates,<br/>launcher only| L
    L -->|starts| COPY
    UI <-->|game messages| P
    P --> E
    P --> AI
    P --> S
```

1. **Install.** The setup copies your Hearthstone into `%LOCALAPPDATA%\SoloStone\client`, adds the mod files and asks
   Windows to block every program in the copy from the internet.
2. **Launch.** The launcher checks your client version and the firewall rules, then starts the copy.
3. **Load.** A standard Unity mod loader ([BepInEx](https://github.com/BepInEx/BepInEx)) loads the SoloStone plugin
   inside the game. The plugin changes the game's code in memory only, each time the game starts. On disk, the copy
   gets the mod files and its own company name, so its settings stay apart from your Hearthstone.
4. **Play.** Where the game would talk to Blizzard's servers, the plugin answers instead:
   - your moves go to a local **rules engine**, based on the open-source
     [SabberStone](https://github.com/HearthSim/SabberStone) simulator;
   - the engine's results come back as the same messages a server would send, so the game's own screens show them;
   - **computer opponents** choose their moves from what a real player could see: no peeking at your hand, no rigged
     draws.
5. **Save.** Your collection, decks, rank and rewards live in saves on your PC (`%LOCALAPPDATA%\SoloStone\saves`).
   Rewards are saved before they are shown, and the saves are backed up, so a crash does not lose or repeat them.

**Safety checks.** SoloStone refuses to start if the firewall rules are missing, if it finds a network connection,
or if your game version is one it can't handle. Your own Hearthstone, your Battle.net
app and your account are never touched.

## Install

Download `SoloStone-Setup-<version>.exe` from the [Releases page](https://github.com/KeyStarr/SoloStone/releases), **quit Battle.net first**,
run the setup and follow its five steps. Windows will warn about an unknown publisher, because the installer is not
signed with a paid certificate.

| You need | |
|---|---|
| Windows 10 or 11, 64-bit | Steam Deck and Linux are not supported yet |
| Hearthstone from Battle.net | Tested with client 37.0.0.254477; the setup shows the version of yours |
| About 12.5 GB free | For the copy |

The full steps, including how to check your download, are in [INSTALL.md](docs/INSTALL.md).

## What to expect

- **This is a beta.** It has rough edges and has been tested on few machines.
- **Windows asks for permission once**, to add the firewall rules.
- **Hearthstone updates:**

  | Your client changes by | SoloStone |
  |---|---|
  | a small patch | keeps working, no message |
  | a medium update | shows a warning you can click through |
  | a new major version | waits for a matching SoloStone release; copies already installed keep working |

- **Updates to SoloStone** come only through the launcher, before the game starts, and are checked against a
  signature. You can turn them off. [INSTALL.md](docs/INSTALL.md) lists exactly what connects.
- **Card text** comes from SoloStone's own file, in English, so it can differ from your updated client or the live game.

## Contributing

SoloStone is a small project, so for now it takes **issues, not pull requests**
([CONTRIBUTING.md](.github/CONTRIBUTING.md)). The most useful help:

| You can | How |
|---|---|
| Report a bug | Open an [issue](https://github.com/KeyStarr/SoloStone/issues): your SoloStone and Hearthstone versions, what you did, what you expected, what happened |
| Report a wrong card or rule | Name the card (or its ID), say what it did and what it should do. A link to a source helps |
| Tell us an opponent played badly | Say what it did and what a good player would have done |
| Build it yourself | [BUILDING.md](docs/BUILDING.md). You need your own Hearthstone client as reference libraries |
| Report a security problem | [SECURITY.md](.github/SECURITY.md). Not as a public issue |

Please don't post account details, passwords or tokens, and don't attach game files or card art. Logs can include
your Windows user name: check them first.

Chat: [Discord](https://discord.gg/e45QgKHmC2)

### Repository layout

| Folder | What is in it |
|---|---|
| `engine/` | The rules engine: a modified SabberStone (AGPL) with the newer card mechanics |
| `plugin/` | The plugin that runs inside the game: game messages, screens, opponents, saves |
| `installer/` | The setup and the launcher (`SoloStone.exe`), with its tests |
| `facts/` | The card data for the fixed card set, one file per meta |
| `data/` | Opponent decks and personalities, card pools, shop, packs and progression data |

## Source and licence

The source for every download is in this repository, at that release's tag. [BUILDING.md](docs/BUILDING.md) says how to
build it. The card and rules test suites are not published yet.

SoloStone's own code is licensed under the GNU AGPL version 3 or later ([LICENSE](LICENSE)), with an additional
permission for its own files ([LICENSE-ADDITIONAL-PERMISSION.md](LICENSE-ADDITIONAL-PERMISSION.md)). The rules engine
is based on SabberStone, which is also AGPL. Other people's work and its licences are listed in [NOTICE](NOTICE).
