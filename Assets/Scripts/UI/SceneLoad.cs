using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GoG
{
    public static class SceneLoad
    {
        static readonly PropertyInfo AdditionalCameraDatas =
            typeof(VolumeDebugSettings<UniversalAdditionalCameraData>).GetProperty(
                "additionalCameraDatas",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        public static void NextFrame(string sceneName)
        {
            PruneDestroyedCameras();
            var go = new GameObject("GoG.SceneLoad");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Runner>().sceneName = sceneName;
        }

        public static void PruneDestroyedCameras()
        {
            if (!(AdditionalCameraDatas?.GetValue(null) is IList list))
                return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!(list[i] is Object obj) || obj == null)
                    list.RemoveAt(i);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnRuntimeInit() => PruneDestroyedCameras();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void OnEditorInit()
        {
            UnityEditor.EditorApplication.playModeStateChanged += _ => PruneDestroyedCameras();
        }
#endif

        sealed class Runner : MonoBehaviour
        {
            public string sceneName;

            void LateUpdate()
            {
                string scene = sceneName;
                sceneName = null;
                enabled = false;
                PruneDestroyedCameras();
                SceneManager.LoadScene(scene);
                Destroy(gameObject);
            }
        }
    }
}
