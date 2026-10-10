# Auto Battler

A small grid-based auto battler made in Unity 6 (URP 2D) for the browser. Spend gold in the shop, place units on
your half of the board, then watch them fight a wave of enemies on their own. Buy three copies of the same unit and
they merge into a stronger, starred version. Build class synergies, collect items and augments, and beat the final
boss in round 10 to win.

![Gameplay GIF](docs/gameplay.gif)
<!-- TODO: record a GIF of a round and save it as docs/gameplay.gif -->

**Play it on itch.io:** _coming soon_ <!-- TODO: replace with https://<user>.itch.io/auto-battler -->

## How to play

| Phase  | What happens |
| ------ | ------------ |
| Shop   | Buy units, reroll the shop (1g), buy XP (4g), sell units, equip items, and arrange your board. |
| Battle | Units act on their own: fighters walk to the nearest enemy and attack once it's in range, archers shoot arrows that have to fly to their target, and monks heal whichever ally is most hurt. |
| Reward | After a win, pick one of three rewards: an item for one unit, or a team augment. |
| Result | Losing costs 2 health plus 1 for each enemy still standing. You lose at 0 health, or if the round 10 boss beats you. |

| Unit | Cost | Role |
| ---- | ---- | ---- |
| **Warrior** | 1g | Sturdy melee fighter |
| **Archer** | 2g | Fires arrows from 3 cells away |
| **Lancer** | 3g | Tank with a heavy lance thrust |
| **Monk** | 3g | Heals the ally with the lowest HP fraction, never attacks |

You play the blue faction and fight the red one.

### Economy and levels

- **Income** each round: 5 gold, +1 per 10 gold you still hold (max +5), and a streak bonus of +1/+2/+3 for 2/3/4+
  wins *or* losses in a row. Treasury augments add more.
- **Levels** set how many units you can field (level + 2, so 3 at level 1 and 8 at level 6). You earn 2 XP per
  round, and **Buy XP** gives 4 XP for 4 gold. XP needed per level: 2, 4, 8, 12, 18.
- **Shop odds** shift toward 3g units as you level:

  | Level | 1g | 2g | 3g |
  | ----: | -: | -: | -: |
  | 1 | 75% | 25% | 0% |
  | 2 | 60% | 32% | 8% |
  | 3 | 45% | 37% | 18% |
  | 4 | 33% | 38% | 29% |
  | 5 | 25% | 35% | 40% |
  | 6 | 17% | 33% | 50% |

### Synergies

Every unit on the board counts toward its class. Bonuses apply only to that class.

| Class | 2 units | 4 units |
| ----- | ------- | ------- |
| Warriors | 15% armor | 30% armor |
| Archers | +25% attack speed | +50% attack speed |
| Lancers | +20% max HP | +40% max HP |
| Monks | +30% healing | +60% healing |

Armor removes that share of incoming damage (always at least 1 gets through). Attack speed can't make an attack
faster than its animation.

### Rewards

After each win you choose one of three: always at least one item and one augment. Boss wins offer only the greater
versions.

| Reward | Normal | Greater (boss) |
| ------ | ------ | -------------- |
| Item: attack | Sword +25% attack | Great Sword +55% |
| Item: HP | Heart Amulet +30% HP | Dragon Heart +65% |
| Item: attack speed | Swift Gloves +25% | Storm Gloves +55% |
| Augment: attack | Rally, all units +10% | War Cry +22% |
| Augment: HP | Fortify, all units +12% | Bastion +25% |
| Augment: attack speed | Haste, all units +10% | Frenzy +22% |
| Augment: gold | Treasury, +2g per round | Royal Treasury +4g |

Each unit holds one item. Equipping a new one returns the old one to your inventory, and so does selling the unit.
When units merge, the upgraded unit keeps one item and the rest go back to your inventory.

### Enemies and bosses

Enemy waves are built from a per-round **power budget** (a unit's power is its cost × copies, so a 2-star Archer
is 6). The budget goes to a set number of units first, then to random star upgrades:

| Round | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
| ----- | - | - | - | - | - | - | - | - | - | -- |
| Budget | 4 | 7 | 10 | 14 | 18 (boss) | 23 | 28 | 33 | 38 | 45 (boss) |

Enemies use 1-2g units in rounds 1-2, 2-star upgrades from round 3, and 3-star upgrades from round 8. They get
class synergies too. **Rounds 5 and 10 are boss rounds:** a 2-star (round 5) or 3-star (round 10) Lancer with
+50% HP and +25% attack, drawn larger, plus support units bought with the rest of the budget.

## Controls

| Input | Action |
| ----- | ------ |
| Left-click a unit, then a cell/bench slot | Move it there (swaps if the target is occupied) |
| Right-click / `Esc` | Cancel selection |
| `1`–`5` or click a shop card | Buy that unit |
| `R` | Reroll the shop (1 gold) |
| `F` | Buy 4 XP (4 gold) |
| `S` | Sell the selected unit |
| Click an item, then a unit | Equip the item |
| `1`–`3` or click a card | Pick a reward after a win |
| `Space` | Start the battle (or leave the title screen) |
| `T` or the Speed button | Toggle battle speed between 1x and 2x |
| `M` or the Sound button | Mute/unmute (the button cycles 100% → 60% → 30% → off) |

Sound and speed settings are saved between sessions.

## Architecture

```
Assets/
  Scripts/
    Core/   AutoBattler.Core  – pure C# game rules, no UnityEngine reference (noEngineReferences)
    Game/   AutoBattler.Game  – MonoBehaviours: bootstrap, rendering, HUD, input
    Editor/ AutoBattler.Editor – art import settings, balance sim menu/CLI
  Tests/
    EditMode/ AutoBattler.Tests.EditMode – NUnit tests against Core
Tools/
  BalanceSim/ – console runner for the balance simulation (compiles the Core sources directly)
```

- **Core** holds all the rules. `GameSession` runs the economy, the shop, bench and board slots, merging, and the
  round loop. `BattleSimulator` is a deterministic battle running at 10 ticks per second. It returns `BattleEvent`s
  (move, action start, projectile launch, damage, heal, death). Each action takes effect `ImpactTicks` after it
  starts, and that number is the impact frame of the unit's 10 fps attack animation, so hits land on the right frame.
  Arrows are simulated projectiles: if their target dies while they are in flight, they miss. `EnemyWaves`, `Shop`
  and `Rewards` use an injected `IRandom` (`SeededRandom`), so a seed always replays the same run.
- **Every tunable number lives in `Balance`**: income, XP, shop odds, synergy bonuses, items, augments, enemy
  budgets and the boss bonus. Synergies, items, augments and boss bonuses all add up into one `StatBonus`, which
  `CombatStats.For` turns into a unit's battle stats.
- **Game** only draws and forwards input. `GameBootstrap` runs from
  `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and builds everything in code: camera, global 2D light,
  EventSystem, a UI Toolkit HUD (`Hud`), and the `GameController`, which plays battle events back at the tick rate.
  Nothing needs to be wired up in the editor.
- **Art** (`Art`) loads the Tiny Swords sheets from `Assets/Resources/Art/TinySwords` and slices them at runtime.
  Unit sheets are strips of square frames. The pack's UI pieces come with gaps between them, so they are stitched
  into 9-slice textures, then used for the wood panels, paper shop cards, ribbons, buttons, and the unit HP bars.
  `SpriteAnimator` plays the strips at 10 fps. The editor-only `TinySwordsImporter` (an `AssetPostprocessor`) imports
  the pack with Point filtering, no compression, and 64 pixels per unit, so one terrain tile is one board cell.
- **Audio** (`AudioManager`) is created by the bootstrap and loads Kenney clips from `Assets/Resources/Audio` with
  `Resources.Load`. Combat sounds play on the battle events (swing on attack start, hits on the impact frame, bow
  release and arrow impact, heal chime, death thud), with a few variants each and ±8% random pitch. Shop actions,
  UI clicks, round start, boss round start, victory and defeat have their own sounds and jingles. The editor-only
  `AudioImportSettings` makes effects mono and compresses all clips.
- **Polish:** a title screen with a short how-to-play, screen shake when a boss lands a hit (bigger when it dies),
  units that tip over and fade when they die, and a 1x/2x battle speed toggle (`Time.timeScale` during battles,
  so animations and impact frames stay in sync).
- Input goes through the Input System package (`Mouse.current` / `Keyboard.current`, and `InputSystemUIInputModule`
  for the UI).

## Balance

`BalanceSimulation` (in Core) plays seeded runs with `GreedyBot`, a simple deterministic player that uses the same
`GameSession` API you do. Each shop phase it equips items on its strongest units, buys copies that finish a merge,
buys XP when its board is full, buys the best-value units it can afford (pairs and synergy thresholds count extra),
rerolls up to twice, and fields its strongest units with melee in front, ranged at the back and healers between.
After wins it picks rewards by a simple score.

Final results with the numbers above (`dotnet run -c Release -- 1000 1`):

1000 runs (seeds 1-1000): bot won **56.2%** of runs.

| Round | Reached | Win rate | Avg gold at start | Avg gold banked at fight | Avg level | Avg health |
| ----: | ------: | -------: | ----------------: | -----------------------: | --------: | ---------: |
| 1 | 1000 | 93% | 10.0 | 0.6 | 1.0 | 20.0 |
| 2 | 1000 | 93% | 5.6 | 0.4 | 2.0 | 19.6 |
| 3 | 1000 | 95% | 6.3 | 0.5 | 2.0 | 19.2 |
| 4 | 1000 | 93% | 7.2 | 0.8 | 3.0 | 19.0 |
| 5 (boss) | 999 | 71% | 8.4 | 0.7 | 3.2 | 18.6 |
| 6 | 992 | 88% | 7.8 | 0.7 | 3.9 | 17.2 |
| 7 | 974 | 87% | 7.8 | 0.8 | 4.2 | 16.7 |
| 8 | 951 | 84% | 7.9 | 0.7 | 4.6 | 16.3 |
| 9 | 912 | 84% | 7.9 | 0.8 | 4.9 | 16.0 |
| 10 (boss) | 865 | 65% | 8.0 | 0.7 | 5.1 | 15.7 |

Seeds 5001-6000 give 56.1%, so the tuning isn't fitted to one seed range. Notes on the curve:

- Early rounds are forgiving (93-95%), so you can learn the shop. Each boss is a clear spike (71% and 65%), and
  rounds 6-9 wear health down so that about 87% of runs reach the final boss.
- **Gold doesn't pile up.** The bot starts each round with about 8g and banks almost nothing.
- **Banking for interest is a trap over 10 rounds.** The same bot told to keep a gold reserve does worse: 10g
  reserve → 35.2% of runs won, 20g → 14.5%, 50g → 0%. Interest pays back too slowly in a 10-round run, so the
  default bot spends everything, and the game is tuned against that stronger strategy.

A test (`TunedBalance_KeepsTheBotNearTheTargetWinRate`) runs 300 seeded runs and fails if the win rate leaves
45-68% or average starting gold reaches 25, so CI catches balance regressions.

### Running the simulation

- **Fastest (about 1 s for 1,000 runs):** with any .NET 8 SDK, including the one bundled with Unity at
  `Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet`:

  ```sh
  cd Tools/BalanceSim
  dotnet run -c Release -- 1000 1      # runs, first seed, [optional bot gold reserve]
  ```

- **In the editor:** **Auto Battler → Run Balance Simulation** logs the report to the Console.
- **Unity command line** (editor closed):

  ```sh
  Unity -batchmode -nographics -quit -projectPath . \
    -executeMethod AutoBattler.Editor.BalanceSimRunner.RunFromCommandLine -simRuns 1000 -simSeed 1
  ```

## Credits

- **Art:** [Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords) by **Pixel Frog**. Only the files this game
  uses are included, under `Assets/Resources/Art/TinySwords`. Check the pack's itch.io page for its license terms
  before you redistribute them.
- **Audio:** [RPG Audio](https://kenney.nl/assets/rpg-audio), [Impact Sounds](https://kenney.nl/assets/impact-sounds),
  [Interface Sounds](https://kenney.nl/assets/interface-sounds) and
  [Music Jingles](https://kenney.nl/assets/music-jingles) by **Kenney** ([kenney.nl](https://kenney.nl)), CC0.
  Only the 29 clips the game uses are included, under `Assets/Resources/Audio`.

## Running

Open the project in **Unity 6000.6.5f1** and press Play in any scene; the bootstrap builds the game for you.

## Tests

In the editor: **Window → General → Test Runner → EditMode → Run All**.

From the command line (close the editor first):

```sh
/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults results.xml
```

## Hosting the WebGL build

The build uses Brotli compression, so the server must send `Content-Encoding: br` for the `.br` files. If your host
doesn't (some don't), turn on **Player Settings → Publishing Settings → Decompression Fallback** before building.

## CI

`.github/workflows/ci.yml` uses [GameCI](https://game.ci) to run the EditMode tests and then build WebGL, uploading
the build as an artifact. Before it can run, add these repository secrets: `UNITY_LICENSE` (the contents of your
`.ulf` file), `UNITY_EMAIL`, and `UNITY_PASSWORD`. See GameCI's [activation guide](https://game.ci/docs/github/activation).
