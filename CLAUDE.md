# Project rules
- Unity 6.6, C#, URP Universal 2D. Target WebGL.
- Never require manual editor wiring. Bootstrap the game from a static method with [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] that creates all GameObjects in code.
- Use the new Input System package (UnityEngine.InputSystem), not the legacy Input class.
- Placeholder art: generate sprites in code (colored squares/circles). Use TextMeshPro or UI Toolkit for UI.
- Game logic lives in plain C# classes (no MonoBehaviour) under Assets/Scripts/Core with its own .asmdef so it is unit-testable.
- EditMode tests under Assets/Tests/EditMode with an .asmdef referencing Core and the test framework.
- Add .github/workflows/ci.yml using game-ci to run tests and build WebGL.
- README: description, controls, architecture, how to run tests, GIF placeholder, itch.io link placeholder.
- Small commits with clear messages. Do not touch Library/, Temp/, or Logs/.
