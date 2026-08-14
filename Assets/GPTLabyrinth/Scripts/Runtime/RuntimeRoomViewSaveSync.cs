using GPTLabyrinth.Data;
using GPTLabyrinth.Managers;
using GPTLabyrinth.Save;
using GPTLabyrinth.UI;
using UnityEngine;

namespace GPTLabyrinth.Runtime
{
    /// <summary>
    /// B-team boundary between the live room-view UI state and CurrentRunSave.
    /// It does not perform file IO, scene selection, or gameplay decisions.
    /// </summary>
    public class RuntimeRoomViewSaveSync : MonoBehaviour
    {
        private static RuntimeRoomViewSaveSync _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        public static bool CaptureCurrentViewStateToRun()
        {
            var controller = FindAnyObjectByType<RoomNavigationUIController>();
            if (controller == null)
            {
                Debug.LogError("[RoomViewSaveSync] Capture failed: RoomNavigationUIController not found.");
                return false;
            }

            if (!GameSession.HasSession || GameSession.Current.current_run == null)
            {
                Debug.LogError("[RoomViewSaveSync] Capture failed: CurrentRunSave not available.");
                return false;
            }

            string runRoomCode = ToRunRoomCode(controller.CurrentRoomCode);
            if (string.IsNullOrEmpty(runRoomCode))
            {
                Debug.LogError("[RoomViewSaveSync] Capture failed: unsupported room '" + controller.CurrentRoomCode + "'.");
                return false;
            }

            CurrentRunSave run = GameSession.Current.current_run;
            run.current_room_code = runRoomCode;
            run.current_view_code = ToViewCode(controller.CurrentView);
            run.current_zoom = controller.CurrentZoom;
            return true;
        }

        public static bool RestoreSavedStateAfterSceneReady()
        {
            var controller = FindAnyObjectByType<RoomNavigationUIController>();
            if (controller == null)
            {
                Debug.LogError("[RoomViewSaveSync] Restore failed: RoomNavigationUIController not found.");
                return false;
            }

            if (!GameSession.HasSession || GameSession.Current.current_run == null)
            {
                Debug.LogError("[RoomViewSaveSync] Restore failed: CurrentRunSave not available.");
                return false;
            }

            CurrentRunSave run = GameSession.Current.current_run;
            string roomId = ToRoomViewId(run.current_room_code);
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogError("[RoomViewSaveSync] Restore failed: unsupported run room '" + run.current_room_code + "'.");
                return false;
            }

            return controller.TryRestoreState(roomId, run.current_view_code, run.current_zoom);
        }

        private static string ToViewCode(RoomView view)
        {
            return view.ToString().ToUpperInvariant();
        }

        private static string ToRunRoomCode(string roomId)
        {
            switch (roomId)
            {
                case "P00":
                    return GameCodes.ROOM_P00;
                case "P01":
                    return GameCodes.ROOM_P01;
                case "P09":
                    return GameCodes.ROOM_P09;
                default:
                    return null;
            }
        }

        private static string ToRoomViewId(string runRoomCode)
        {
            switch (runRoomCode)
            {
                case GameCodes.ROOM_P00:
                    return "P00";
                case GameCodes.ROOM_P01:
                    return "P01";
                case GameCodes.ROOM_P09:
                    return "P09";
                default:
                    return null;
            }
        }
    }
}
