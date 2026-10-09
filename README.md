# Auto Battler

A small grid-based auto battler made in Unity 6 (URP 2D) for the browser. Spend gold in the shop, place units on
your half of the board, then watch them fight a wave of enemies on their own. Buy three copies of the same unit and
they merge into a stronger, starred version. Survive all 10 rounds to win.

![Gameplay GIF](docs/gameplay.gif)
<!-- TODO: record a GIF of a round and save it as docs/gameplay.gif -->

**Play it on itch.io:** _coming soon_ <!-- TODO: replace with https://<user>.itch.io/auto-battler -->

## How to play

| Phase  | What happens |
| ------ | ------------ |
| Shop   | You get gold every round (5 + 1 per 10 gold banked, max +3, +1 for a win). Buy units, reroll the shop, sell units, and arrange your board. |
| Battle | Units act on their own: each one targets the nearest enemy, walks toward it, and attacks once it's in range. |
| Result | Losing costs 2 health plus 1 for each enemy still standing. You lose at 0 health and win by clearing round 10. |

Units: **Knight** (1g, sturdy melee), **Archer** (1g, range 3), **Rogue** (2g, fast melee), **Mage** (3g, heavy
ranged hits), **Golem** (3g, huge HP). You can field 3 units in round 1, and one more each round after that (up to 8).

## Controls

| Input | Action |
| ----- | ------ |
| Left-click a unit, then a cell/bench slot | Move it there (swaps if the target is occupied) |
| Right-click / `Esc` | Cancel selection |
| `1`–`5` or click a shop card | Buy that unit |
| `R` | Reroll the shop (1 gold) |
| `S` | Sell the selected unit |
| `Space` | Start the battle |

## Architecture

```
Assets/
  Scripts/
    Core/   AutoBattler.Core  – pure C# game rules, no UnityEngine reference (noEngineReferences)
    Game/   AutoBattler.Game  – MonoBehaviours: bootstrap, rendering, HUD, input
  Tests/
    EditMode/ AutoBattler.Tests.EditMode – NUnit tests against Core
```

- **Core** holds all the rules. `GameSession` runs the economy, the shop, bench and board slots, merging, and the
  round loop. `BattleSimulator` is a deterministic, tick-based battle that returns `BattleEvent`s (move, attack,
  death). `EnemyWaves` and `Shop` use an injected `IRandom` (`SeededRandom`), so a seed always replays the same run.
- **Game** only draws and forwards input. `GameBootstrap` runs from
  `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and builds everything in code: camera, global 2D light,
  EventSystem, generated placeholder sprites (`SpriteFactory`), a UI Toolkit HUD (`Hud`), and the `GameController`,
  which plays battle events back at a fixed tick rate. Nothing needs to be wired up in the editor.
- Input goes through the Input System package (`Mouse.current` / `Keyboard.current`, and `InputSystemUIInputModule`
  for the UI).

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

## CI

`.github/workflows/ci.yml` uses [GameCI](https://game.ci) to run the EditMode tests and then build WebGL, uploading
the build as an artifact. Before it can run, add these repository secrets: `UNITY_LICENSE` (the contents of your
`.ulf` file), `UNITY_EMAIL`, and `UNITY_PASSWORD`. See GameCI's [activation guide](https://game.ci/docs/github/activation).
