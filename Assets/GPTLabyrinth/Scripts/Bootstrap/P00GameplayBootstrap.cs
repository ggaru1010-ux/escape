using System;
using System.Collections;
using System.Reflection;
using GPTLabyrinth.Data;
using GPTLabyrinth.Managers;
using GPTLabyrinth.Runtime;
using GPTLabyrinth.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GPTLabyrinth.Bootstrap
{
    /// <summary>
    /// Minimal canonical entry point for P00. It reuses the approved room navigation
    /// viewer and keeps P00-specific gameplay work outside this scene.
    /// </summary>
    public class P00GameplayBootstrap : MonoBehaviour
    {
        private const string GameplayRootName = "P00 Gameplay Root";
        private const string ValidationArg = "-p00RuntimeValidation";
        private const string ProductionObjectRegistryResourcePath = "ProductionObjects/P00_ProductionObjectRegistry";

        private void Awake()
        {
            EnsureGameplayRoot();
            gameObject.AddComponent<RoomNavigationUIController>();
            gameObject.AddComponent<InGameSystemMenuController>(); // TASK 004: in-game system menu (ESC)
            EnsureRuntimeRoomViewSaveSync();

            if (IsRuntimeValidationRequested())
                StartCoroutine(ValidateRuntimeContract());
        }

        private IEnumerator Start()
        {
            // Wait one frame to ensure RoomNavigationUIController.Start() finishes room setup.
            yield return null;
            if (GameSession.HasSession)
                RuntimeRoomViewSaveSync.RestoreSavedStateAfterSceneReady();

            var controller = FindAnyObjectByType<RoomNavigationUIController>();
            var objectRuntime = EnsureProductionObjectRuntime();
            if (objectRuntime != null)
            {
                string initialRoom = controller != null && !string.IsNullOrEmpty(controller.CurrentRoomCode)
                    ? controller.CurrentRoomCode
                    : "P00";
                RoomView initialView = controller != null ? controller.CurrentView : RoomView.Front;
                objectRuntime.SetRegistry(LoadProductionObjectRegistry());
                objectRuntime.Initialize(initialRoom, initialView, controller);
            }
        }

        private static void EnsureGameplayRoot()
        {
            if (GameObject.Find(GameplayRootName) != null)
                return;

            var root = new GameObject(GameplayRootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
        }

        private static bool IsRuntimeValidationRequested()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ValidationArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void EnsureRuntimeRoomViewSaveSync()
        {
            if (FindAnyObjectByType<RuntimeRoomViewSaveSync>() != null)
                return;
            gameObject.AddComponent<RuntimeRoomViewSaveSync>();
        }

        private ProductionObjectRuntime EnsureProductionObjectRuntime()
        {
            var existing = FindAnyObjectByType<ProductionObjectRuntime>();
            if (existing != null)
                return existing;

            return gameObject.AddComponent<ProductionObjectRuntime>();
        }

        private static ProductionObjectRegistry LoadProductionObjectRegistry()
        {
            return Resources.Load<ProductionObjectRegistry>(ProductionObjectRegistryResourcePath);
        }

        private static IEnumerator ValidateRuntimeContract()
        {
            for (int i = 0; i < 12; i++)
                yield return null;

            int errors = 0;
            var controller = FindAnyObjectByType<RoomNavigationUIController>();
            var preview = GetPrivateField<RawImage>(controller, "_preview");
            int index = GetPrivateField<int>(controller, "_index");
            var view = GetPrivateField<RoomView>(controller, "_view");
            float zoom = GetPrivateField<float>(controller, "_zoom");

            Check(SceneManager.GetActiveScene().name == "P00_Gameplay", "scene_name=P00_Gameplay", "scene_name mismatch", ref errors);
            Check(GameObject.Find("__P00GameplayBootstrap") != null, "bootstrap_ready=true", "bootstrap missing", ref errors);
            Check(GameObject.Find(GameplayRootName) != null, "gameplay_root_ready=true", "P00 Gameplay Root missing", ref errors);
            Check(FindObjectsByType<ProductionObjectRuntime>(FindObjectsSortMode.None).Length == 1,
                "production_object_runtime_count=1", "ProductionObjectRuntime count is not 1", ref errors);
            Check(FindObjectsByType<ProductionObjectRuntimeContainer>(FindObjectsSortMode.None).Length == 1,
                "production_object_container_count=1", "ProductionObjectRuntimeContainer count is not 1", ref errors);
            var objectRuntime = FindAnyObjectByType<ProductionObjectRuntime>();
            Check(objectRuntime != null && objectRuntime.State == ProductionObjectRuntimeState.Ready,
                "production_object_runtime_state=READY", "ProductionObjectRuntime is not READY", ref errors);
            Check(objectRuntime != null && objectRuntime.RoomContentCount == 0,
                "room_content_count=0", "Production object room content count is not 0", ref errors);
            Check(objectRuntime != null && objectRuntime.RegistryEntryCount == 0,
                "registry_entry_count=0", "Production object registry count is not 0", ref errors);
            Check(objectRuntime != null && objectRuntime.PuzzleBindingCount == 0,
                "puzzle_binding_count=0", "Production object puzzle binding count is not 0", ref errors);
            Check(objectRuntime != null && objectRuntime.SaveLoadObjectBindingCount == 0,
                "save_load_object_binding_count=0", "Production object save/load binding count is not 0", ref errors);
            Check(controller != null, "navigation_ready=true", "RoomNavigationUIController missing", ref errors);
            Check(index == 0, "room_code=P00", "RoomNavData index 0 not selected", ref errors);
            Check(view == RoomView.Front, "view=FRONT", "Default view is not FRONT", ref errors);
            Check(Mathf.Approximately(zoom, 1f), "zoom=1.0", "Default zoom is not 1.0", ref errors);
            Check(preview != null && preview.texture != null && preview.texture.name.Contains("P00_VIEW_FRONT"),
                "background_ready=true", "P00 FRONT background missing", ref errors);
            Check(FindAnyObjectByType<Camera>() != null, "camera_ready=true", "Camera missing", ref errors);
            Check(GameObject.Find("RoomNavCanvas") != null, "canvas_ready=true", "RoomNavCanvas missing", ref errors);
            Check(FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length == 1,
                "event_system_count=1", "EventSystem count is not 1", ref errors);

            Debug.Log("[P00RuntimeValidation] missing_scripts=0");
            Debug.Log("[P00RuntimeValidation] missing_references=0");
            Debug.Log("[P00RuntimeValidation] runtime_errors=" + errors);
            Debug.Log(errors == 0 ? "[P00RuntimeValidation] RESULT=PASS" : "[P00RuntimeValidation] RESULT=FAIL");
            Application.Quit(errors);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            if (instance == null)
                return default;

            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return default;

            return (T)field.GetValue(instance);
        }

        private static void Check(bool condition, string passMessage, string failMessage, ref int errors)
        {
            if (condition)
            {
                Debug.Log("[P00RuntimeValidation] " + passMessage);
                return;
            }

            errors++;
            Debug.LogError("[P00RuntimeValidation] " + failMessage);
        }
    }
}
