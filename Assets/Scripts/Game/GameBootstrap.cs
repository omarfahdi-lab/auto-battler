using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace AutoBattler.Game
{
    /// <summary>Builds the whole game from code after the first scene loads; no scene wiring required.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<GameController>() != null) return;

            var camera = EnsureCamera();
            EnsureGlobalLight();
            EnsureEventSystem();

            var root = new GameObject("AutoBattler");
            var audio = AudioManager.Create(root.transform);
            var controller = root.AddComponent<GameController>();
            controller.Initialize(camera, Random.Range(int.MinValue, int.MaxValue), audio);
        }

        private static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            if (Object.FindAnyObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void EnsureGlobalLight()
        {
            foreach (var light in Object.FindObjectsByType<Light2D>())
                if (light.lightType == Light2D.LightType.Global) return;

            var go = new GameObject("Global Light 2D");
            var global = go.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
