using System.Collections.Generic;
using System.IO;
using GPTLabyrinth.Data;
using GPTLabyrinth.P02;
using GPTLabyrinth.P05;
using GPTLabyrinth.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace GPTLabyrinth.UI
{
    /// <summary>
    /// Prototype Room Multi-View Explorer (v0.8C polish). Browses A-team room art: select a
    /// room (Prev/Next or thumbnail list), switch among six directional views
    /// (FRONT/BACK/LEFT/RIGHT/CEILING/FLOOR), and zoom the preview (in/out/reset).
    ///
    /// v0.8C adds: zoom clipping via RectMask2D on a preview viewport, aspect-correct display
    /// via AspectRatioFitter, a current-view-name label, and a two-line Room/Pattern info block.
    ///
    /// Boundaries (v0.4 UI_Layer_Responsibility): NO Save/Load, puzzle logic, object
    /// interaction, gameplay events, clear state, or CoreContract. Reads art from disk and
    /// never writes. Code-driven scene (UIFactory), legacy UGUI, InputSystemUIInputModule.
    ///
    /// Hierarchy:
    ///   RoomNavCanvas
    ///   ├ RoomPreviewPanel
    ///   │   ├ PreviewViewport (RectMask2D)  ── PreviewImage (RawImage + AspectRatioFitter)
    ///   │   ├ RoomInfo (2 lines)   ├ ViewName   ├ ViewSwitch(cross)   └ ZoomControl
    ///   ├ NavigationPanel  { PreviousButton, NextButton }
    ///   └ RoomSelectPanel  { P00 … P10 }
    /// </summary>
    public class RoomNavigationUIController : MonoBehaviour
    {
        private const string ArtRoot = "GPTLabyrinth/Art/Rooms"; // relative to Application.dataPath

        private const float ZoomStep = 0.25f;
        private readonly List<RoomViewSet> _rooms = new List<RoomViewSet>();
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly List<Button> _selectButtons = new List<Button>();
        private readonly Dictionary<RoomView, Button> _viewButtons = new Dictionary<RoomView, Button>();

        private RawImage _preview;
        private RectTransform _previewRt;
        private AspectRatioFitter _fitter;
        private P02ObjectPngOverlayController _p02ObjectPngOverlay;
        private P05FrontOverlayProofController _p05Overlay;
        private Text _roomInfo;
        private Text _viewName;
        private Text _zoomLabel;
        private int _index;
        private RoomView _view = RoomView.Front;
        private float _zoom = 1f;

        public string CurrentRoomCode
        {
            get
            {
                if (_rooms.Count == 0 || _index < 0 || _index >= _rooms.Count)
                    return string.Empty;
                return _rooms[_index].Room_ID;
            }
        }

        public RoomView CurrentView => _view;
        public float CurrentZoom => _zoom;
        public event System.Action<string, string> RoomChanged;
        public event System.Action<RoomView, RoomView> ViewChanged;

        private void Start()
        {
            _rooms.AddRange(RoomViewSetDatabase.BuildDefault());

            UIFactory.EnsureCamera();
            UIFactory.EnsureEventSystem();

            var canvas = UIFactory.CreateCanvas("RoomNavCanvas");
            BuildPreviewPanel(canvas.transform);
            BuildNavigationPanel(canvas.transform);
            BuildRoomSelectPanel(canvas.transform);

            SelectRoom(0);
        }

        // ---- RoomPreviewPanel ----

        private void BuildPreviewPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "RoomPreviewPanel", UIFactory.PanelColor,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(280f, 150f), new Vector2(-20f, -20f));

            // Viewport clips any zoom overflow (TASK 01: RectMask2D).
            var viewport = UIFactory.CreatePanel(panel, "PreviewViewport", new Color(0.02f, 0.02f, 0.03f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 80f), new Vector2(-260f, -110f));
            viewport.gameObject.AddComponent<RectMask2D>();

            // Preview image fills viewport; AspectRatioFitter keeps art undistorted (TASK 02).
            var go = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            _previewRt = go.GetComponent<RectTransform>();
            _previewRt.SetParent(viewport, false);
            _previewRt.anchorMin = new Vector2(0.5f, 0.5f);
            _previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            _previewRt.sizeDelta = new Vector2(900f, 600f);
            _preview = go.GetComponent<RawImage>();
            _preview.color = Color.white;
            _fitter = go.GetComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = 1.5f; // updated per-texture on load
            _p02ObjectPngOverlay = go.AddComponent<P02ObjectPngOverlayController>();
            _p02ObjectPngOverlay.Initialize(_preview, _previewRt);
            _p05Overlay = go.AddComponent<P05FrontOverlayProofController>();
            _p05Overlay.Initialize(_preview, _previewRt);

            // Two-line Room / Pattern info (TASK 04).
            _roomInfo = UIFactory.CreateText(panel, "RoomInfo", "", 26, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -104f), new Vector2(-260f, -16f));
            _roomInfo.fontStyle = FontStyle.Bold;

            // Current view name (TASK 03).
            _viewName = UIFactory.CreateText(panel, "ViewName", "", 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -70f), new Vector2(-16f, -16f));
            _viewName.color = UIFactory.AccentColor;
            _viewName.fontStyle = FontStyle.Bold;

            BuildViewSwitch(panel);
            BuildZoomControl(panel);
        }

        /// <summary>Six-direction view switch laid out as a cross on the right side.</summary>
        private void BuildViewSwitch(Transform previewPanel)
        {
            var cross = UIFactory.CreatePanel(previewPanel, "ViewSwitch", new Color(0f, 0f, 0f, 0f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-240f, -180f), new Vector2(-20f, 180f));
            AddViewButton(cross, RoomView.Ceiling, "UP", new Vector2(0f, 150f));
            AddViewButton(cross, RoomView.Left,    "LEFT", new Vector2(-90f, 40f));
            AddViewButton(cross, RoomView.Front,   "FRONT", new Vector2(0f, 40f));
            AddViewButton(cross, RoomView.Right,   "RIGHT", new Vector2(90f, 40f));
            AddViewButton(cross, RoomView.Back,    "BACK", new Vector2(0f, -70f));
            AddViewButton(cross, RoomView.Floor,   "DOWN", new Vector2(0f, -180f));
        }

        private void AddViewButton(Transform parent, RoomView view, string label, Vector2 pos)
        {
            var btn = UIFactory.CreateButton(parent, "View_" + view, label, UIFactory.ButtonColor,
                pos, new Vector2(84f, 84f), () => ShowView(view), 20);
            _viewButtons[view] = btn;
        }

        private void BuildZoomControl(Transform previewPanel)
        {
            var bar = UIFactory.CreatePanel(previewPanel, "ZoomControl", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 12f), new Vector2(-260f, 72f));

            UIFactory.CreateButton(bar, "ZoomOutBtn", "-", UIFactory.ButtonColor,
                new Vector2(-220f, 0f), new Vector2(64f, 56f), ZoomOut, 32);
            UIFactory.CreateButton(bar, "ZoomResetBtn", "Reset", UIFactory.ButtonColor,
                new Vector2(0f, 0f), new Vector2(140f, 56f), ZoomReset, 24);
            UIFactory.CreateButton(bar, "ZoomInBtn", "+", UIFactory.ButtonColor,
                new Vector2(220f, 0f), new Vector2(64f, 56f), ZoomIn, 32);

            _zoomLabel = UIFactory.CreateText(bar, "ZoomLabel", "100%", 22, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, -28f), new Vector2(210f, 28f));
        }

        // ---- NavigationPanel ----

        private void BuildNavigationPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "NavigationPanel", new Color(0.05f, 0.05f, 0.07f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(280f, 20f), new Vector2(-20f, 140f));

            UIFactory.CreateButton(panel, "PreviousButton", "Prev Room", UIFactory.AccentColor,
                new Vector2(-260f, 0f), new Vector2(220f, 80f), PreviousRoom, 28);
            UIFactory.CreateButton(panel, "NextButton", "Next Room", UIFactory.AccentColor,
                new Vector2(260f, 0f), new Vector2(220f, 80f), NextRoom, 28);
        }

        // ---- RoomSelectPanel ----

        private void BuildRoomSelectPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "RoomSelectPanel", new Color(0.06f, 0.06f, 0.09f, 0.95f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(260f, -20f));

            var title = UIFactory.CreateText(panel, "SelectTitle", "ROOMS", 24, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(0f, -12f));
            title.fontStyle = FontStyle.Bold;

            for (int i = 0; i < _rooms.Count; i++)
            {
                int idx = i;
                float y = -80f - i * 74f;
                var btn = UIFactory.CreateButton(panel, "Select_" + _rooms[i].Room_ID,
                    _rooms[i].Room_ID + "  " + _rooms[i].Room_Name, UIFactory.ButtonColor,
                    Vector2.zero, new Vector2(220f, 62f), () => SelectRoom(idx), 20);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, y);
                _selectButtons.Add(btn);
            }
        }

        // ---- Room selection / navigation ----

        private void PreviousRoom() => SelectRoom(_index - 1);
        private void NextRoom() => SelectRoom(_index + 1);

        private void SelectRoom(int idx)
        {
            if (_rooms.Count == 0) return;
            string previousRoom = CurrentRoomCode;
            RoomView previousView = _view;
            _index = ((idx % _rooms.Count) + _rooms.Count) % _rooms.Count; // wrap
            _view = RoomView.Front;
            HighlightSelectedRoom();
            ShowView(RoomView.Front, false);
            NotifyRoomChanged(previousRoom, CurrentRoomCode);
            NotifyViewChanged(previousView, _view);
        }

        private void HighlightSelectedRoom()
        {
            for (int i = 0; i < _selectButtons.Count; i++)
            {
                var img = _selectButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == _index) ? UIFactory.AccentColor : UIFactory.ButtonColor;
            }
        }

        // ---- View switching ----

        private void ShowView(RoomView view)
        {
            ShowView(view, true);
        }

        private void ShowView(RoomView view, bool notifyViewChange)
        {
            RoomView previousView = _view;
            _view = view;
            var room = _rooms[_index];
            string rel = room.GetImage(view);

            var tex = LoadTexture(rel);
            if (tex != null)
            {
                _preview.texture = tex;
                _preview.color = Color.white;
                if (_fitter != null && tex.height > 0)
                    _fitter.aspectRatio = GetDisplayAspect(room, tex); // TASK 02: no distortion
                _roomInfo.text = $"Room:\n{room.Room_ID}  {room.Room_Name}\n\nPattern:\n{room.Pattern_ID}";
            }
            else
            {
                _preview.texture = null;
                _preview.color = new Color(0.15f, 0.05f, 0.05f, 1f);
                _roomInfo.text = $"Room:\n{room.Room_ID}  {room.Room_Name}\n\nPattern:\n{room.Pattern_ID}\n\n(이미지 없음)";
            }

            _viewName.text = view.ToString().ToUpperInvariant() + " VIEW";
            ApplyCalibration(room);
            ZoomReset();
            HighlightSelectedView();
            if (_p02ObjectPngOverlay != null)
                _p02ObjectPngOverlay.ApplyRoomView(room.Room_ID, _view.ToString());
            if (_p05Overlay != null)
                _p05Overlay.ApplyRoomView(room.Room_ID, _view.ToString());
            if (notifyViewChange)
                NotifyViewChanged(previousView, _view);
        }

        public bool TryRestoreState(string roomCode, string viewCode, float zoom)
        {
            int roomIndex = FindRoomIndex(roomCode);
            if (roomIndex < 0)
                return false;

            string previousRoom = CurrentRoomCode;
            RoomView previousView = _view;
            RoomView restoredView = ParseViewOrFront(viewCode);
            _index = roomIndex;
            _view = restoredView;
            HighlightSelectedRoom();
            ShowView(restoredView, false);

            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                zoom = 1f;
            SetZoom(zoom);
            NotifyRoomChanged(previousRoom, CurrentRoomCode);
            NotifyViewChanged(previousView, _view);
            return true;
        }

        private void NotifyRoomChanged(string previousRoom, string currentRoom)
        {
            if (previousRoom == currentRoom)
                return;
            RoomChanged?.Invoke(previousRoom, currentRoom);
        }

        private void NotifyViewChanged(RoomView previousView, RoomView currentView)
        {
            if (previousView == currentView)
                return;
            ViewChanged?.Invoke(previousView, currentView);
        }

        private void HighlightSelectedView()
        {
            foreach (var kv in _viewButtons)
            {
                var img = kv.Value.GetComponent<Image>();
                if (img != null)
                    img.color = (kv.Key == _view) ? UIFactory.AccentColor : UIFactory.ButtonColor;
            }
        }

        // ---- Image zoom (RawImage localScale, clipped by viewport RectMask2D) ----

        private void ZoomIn() => SetZoom(_zoom + ZoomStep);
        private void ZoomOut() => SetZoom(_zoom - ZoomStep);
        private void ZoomReset() => SetZoom(1f);

        private void SetZoom(float value)
        {
            var calibration = GetCurrentCalibration();
            _zoom = Mathf.Clamp(value, calibration.ZoomMin, calibration.ZoomMax);
            if (_previewRt != null)
                _previewRt.localScale = new Vector3(calibration.Scale * _zoom, calibration.Scale * _zoom, 1f);
            if (_zoomLabel != null)
                _zoomLabel.text = Mathf.RoundToInt(_zoom * 100f) + "%";
        }

        private void ApplyCalibration(RoomViewSet room)
        {
            if (_previewRt == null)
                return;

            var calibration = ApplyArtisticOverride(room, _view);
            _previewRt.anchoredPosition = new Vector2(calibration.OffsetX, calibration.OffsetY);
        }

        private RoomViewCalibration GetCurrentCalibration()
        {
            if (_rooms.Count == 0 || _index < 0 || _index >= _rooms.Count)
                return RoomViewCalibration.Default;
            return ApplyArtisticOverride(_rooms[_index], _view);
        }

        private int FindRoomIndex(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return -1;

            string normalized = roomCode.Trim();
            for (int i = 0; i < _rooms.Count; i++)
                if (_rooms[i].Room_ID == normalized)
                    return i;
            return -1;
        }

        private static RoomView ParseViewOrFront(string viewCode)
        {
            if (string.IsNullOrWhiteSpace(viewCode))
                return RoomView.Front;

            switch (viewCode.Trim().ToUpperInvariant())
            {
                case "BACK":
                    return RoomView.Back;
                case "LEFT":
                    return RoomView.Left;
                case "RIGHT":
                    return RoomView.Right;
                case "CEILING":
                    return RoomView.Ceiling;
                case "FLOOR":
                    return RoomView.Floor;
                default:
                    return RoomView.Front;
            }
        }

        private static RoomViewCalibration NormalizeCalibration(RoomViewCalibration calibration)
        {
            if (calibration.Scale <= 0f)
                calibration.Scale = 1f;
            if (calibration.ZoomMin <= 0f)
                calibration.ZoomMin = 1f;
            if (calibration.ZoomMax < calibration.ZoomMin)
                calibration.ZoomMax = calibration.ZoomMin;
            return calibration;
        }

        private static RoomViewCalibration ApplyArtisticOverride(RoomViewSet room, RoomView view)
        {
            var calibration = NormalizeCalibration(room.Calibration);
            var artistic = RoomViewArtisticOverridePolicy.Get(room.Room_ID, view);
            if (!artistic.Enabled)
                return calibration;

            float scale = artistic.Scale <= 0f ? 1f : artistic.Scale;
            calibration.Scale *= scale;
            calibration.OffsetX += artistic.OffsetX;
            calibration.OffsetY += artistic.OffsetY;
            return calibration;
        }

        private static float GetDisplayAspect(RoomViewSet room, Texture2D tex)
        {
            if (room.Calibration.DisplayAspect > 0f)
                return room.Calibration.DisplayAspect;
            return tex.height > 0 ? (float)tex.width / tex.height : 1f;
        }

        // ---- Loading (runtime PNG read; never touches originals) ----

        private Texture2D LoadTexture(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return null;
            if (_cache.TryGetValue(relPath, out var cached) && cached != null) return cached;

            string full = Path.Combine(Application.dataPath, ArtRoot, relPath);
            if (!File.Exists(full)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                tex.name = relPath;
                _cache[relPath] = tex;
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomNav] Failed to load {relPath}: {e.Message}");
                return null;
            }
        }
    }
}
