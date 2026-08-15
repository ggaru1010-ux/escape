using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GPTLabyrinth.P05
{
    public sealed class P05FrontOverlayProofController : MonoBehaviour
    {
        public enum OverlayState
        {
            Normal,
            Selected,
            Hidden,
            Disabled
        }

        private readonly Dictionary<string, OverlayEntry> _entries = new Dictionary<string, OverlayEntry>();
        private RectTransform _root;
        private RawImage _previewImage;
        private RectTransform _previewRect;
        private string _roomId;
        private string _viewName;
        private string _selectedObjectId;

        public string LastSelectedObjectId => _selectedObjectId;
        public RectTransform Root => _root;
        public IReadOnlyDictionary<string, OverlayEntry> Entries => _entries;
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Initialize(RawImage previewImage, RectTransform previewRect)
        {
            if (_root != null)
                return;

            _previewImage = previewImage;
            _previewRect = previewRect;

            var rootGo = new GameObject("P05_FRONT_OBJECT_OVERLAY_ROOT_TEMP", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(_previewRect, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.localScale = Vector3.one;
            _root.localRotation = Quaternion.identity;

            CreateEntry("OPEN", "P05_DOOR_LEFT_OPEN", "P05_OPEN_OVERLAY_TEMP", "P05_OPEN_HOTSPOT_TEMP",
                new Rect(0.18f, 0.30f, 0.22f, 0.43f), new Color(0.28f, 0.78f, 1f, 0.58f));
            CreateEntry("BAN", "P05_DOOR_CENTER_BAN", "P05_BAN_OVERLAY_TEMP", "P05_BAN_HOTSPOT_TEMP",
                new Rect(0.39f, 0.30f, 0.22f, 0.43f), new Color(1f, 0.28f, 0.28f, 0.58f));
            CreateEntry("FADE", "P05_DOOR_RIGHT_FADE", "P05_FADE_OVERLAY_TEMP", "P05_FADE_HOTSPOT_TEMP",
                new Rect(0.60f, 0.30f, 0.22f, 0.43f), new Color(0.92f, 0.72f, 1f, 0.58f));

            ApplyRoomView(string.Empty, string.Empty);
        }

        public void ApplyRoomView(string roomId, string viewName)
        {
            _roomId = roomId;
            _viewName = viewName;
            bool visible = string.Equals(roomId, "P05", StringComparison.OrdinalIgnoreCase)
                           && string.Equals(viewName, "Front", StringComparison.OrdinalIgnoreCase);
            SetVisible(visible);
            if (visible)
                SetAllState(OverlayState.Normal);
        }

        public void SetAllState(OverlayState state)
        {
            foreach (var entry in _entries.Values)
                SetState(entry.ObjectId, state);
        }

        public void SetHiddenForProof(bool hidden)
        {
            if (hidden)
                SetAllState(OverlayState.Hidden);
            else if (IsP05Front())
                SetAllState(OverlayState.Normal);
        }

        public void SetDisabledForProof(string objectId, bool disabled)
        {
            SetState(objectId, disabled ? OverlayState.Disabled : OverlayState.Normal);
        }

        public void SelectObject(string objectId)
        {
            if (!IsP05Front())
                return;

            foreach (var entry in _entries.Values)
                SetState(entry.ObjectId, entry.ObjectId == objectId ? OverlayState.Selected : OverlayState.Normal);

            _selectedObjectId = objectId;
            Debug.Log("[P05_OVERLAY_SELECT] " + objectId);
        }

        public void SetState(string objectId, OverlayState state)
        {
            if (!_entries.TryGetValue(objectId, out var entry))
                return;

            entry.State = state;
            bool visible = state != OverlayState.Hidden && IsP05Front();
            entry.Overlay.gameObject.SetActive(visible);
            entry.Hotspot.gameObject.SetActive(visible);
            entry.Hotspot.raycastTarget = visible && state != OverlayState.Disabled;

            Color color = entry.BaseColor;
            if (state == OverlayState.Selected)
                color = Color.Lerp(color, Color.white, 0.45f);
            else if (state == OverlayState.Disabled)
                color.a *= 0.25f;
            entry.Overlay.color = color;

            entry.SelectionFrame.gameObject.SetActive(visible && state == OverlayState.Selected);
        }

        public string GetRuntimeAudit()
        {
            return $"Room={_roomId}; View={_viewName}; Visible={IsVisible}; Selected={_selectedObjectId}; PreviewScale={(_previewRect != null ? _previewRect.localScale.ToString(\"F3\") : \"null\")}";
        }

        private bool IsP05Front()
        {
            return string.Equals(_roomId, "P05", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(_viewName, "Front", StringComparison.OrdinalIgnoreCase);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void CreateEntry(string role, string objectId, string overlayName, string hotspotName, Rect normalizedRect, Color color)
        {
            var overlayGo = new GameObject(overlayName, typeof(RectTransform), typeof(Image));
            var overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.SetParent(_root, false);
            ApplyNormalizedRect(overlayRt, normalizedRect);
            var overlayImage = overlayGo.GetComponent<Image>();
            overlayImage.sprite = CreateRuntimeSprite(role, color);
            overlayImage.type = Image.Type.Sliced;
            overlayImage.color = color;
            overlayImage.raycastTarget = false;

            var frameGo = new GameObject(role + "_SelectionFrame_CANDIDATE", typeof(RectTransform), typeof(Image));
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.SetParent(overlayRt, false);
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = new Vector2(-6f, -6f);
            frameRt.offsetMax = new Vector2(6f, 6f);
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.color = new Color(1f, 1f, 0.2f, 0.82f);
            frameImage.raycastTarget = false;
            frameGo.SetActive(false);

            var hotspotRoot = _root.Find("P05_FRONT_HOTSPOT_ROOT_TEMP") as RectTransform;
            if (hotspotRoot == null)
            {
                var hotspotRootGo = new GameObject("P05_FRONT_HOTSPOT_ROOT_TEMP", typeof(RectTransform));
                hotspotRoot = hotspotRootGo.GetComponent<RectTransform>();
                hotspotRoot.SetParent(_root, false);
                hotspotRoot.anchorMin = Vector2.zero;
                hotspotRoot.anchorMax = Vector2.one;
                hotspotRoot.offsetMin = Vector2.zero;
                hotspotRoot.offsetMax = Vector2.zero;
            }

            var hotspotGo = new GameObject(hotspotName, typeof(RectTransform), typeof(Image), typeof(P05FrontOverlayHotspot_TEMP));
            var hotspotRt = hotspotGo.GetComponent<RectTransform>();
            hotspotRt.SetParent(hotspotRoot, false);
            ApplyNormalizedRect(hotspotRt, normalizedRect);
            var hotspotImage = hotspotGo.GetComponent<Image>();
            hotspotImage.color = new Color(1f, 1f, 1f, 0.01f);
            hotspotImage.raycastTarget = true;
            var hotspot = hotspotGo.GetComponent<P05FrontOverlayHotspot_TEMP>();
            hotspot.Bind(this, objectId);

            _entries[objectId] = new OverlayEntry
            {
                Role = role,
                ObjectId = objectId,
                NormalizedRect = normalizedRect,
                Overlay = overlayImage,
                Hotspot = hotspotImage,
                SelectionFrame = frameImage,
                BaseColor = color,
                State = OverlayState.Normal
            };
        }

        private static void ApplyNormalizedRect(RectTransform rt, Rect rect)
        {
            rt.anchorMin = new Vector2(rect.xMin, rect.yMin);
            rt.anchorMax = new Vector2(rect.xMax, rect.yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static Sprite CreateRuntimeSprite(string role, Color color)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs((x / 63f) - 0.5f);
                    float ny = Mathf.Abs((y / 63f) - 0.5f);
                    bool border = nx > 0.40f || ny > 0.40f;
                    bool diagonal = role == "FADE" && Mathf.Abs(x - y) < 4;
                    bool cross = role == "BAN" && (Mathf.Abs(x - y) < 4 || Mathf.Abs((size - 1 - x) - y) < 4);
                    bool circle = role == "OPEN" && Mathf.Abs(Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)) - 18f) < 4f;
                    tex.SetPixel(x, y, border || diagonal || cross || circle ? color : clear);
                }
            }
            tex.Apply(false, false);
            tex.name = "P05_" + role + "_TEMP_RUNTIME_OVERLAY_TEXTURE";
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public sealed class OverlayEntry
        {
            public string Role;
            public string ObjectId;
            public Rect NormalizedRect;
            public Image Overlay;
            public Image Hotspot;
            public Image SelectionFrame;
            public Color BaseColor;
            public OverlayState State;
        }
    }

    public sealed class P05FrontOverlayHotspot_TEMP : MonoBehaviour, IPointerClickHandler
    {
        private P05FrontOverlayProofController _controller;
        private string _objectId;

        public void Bind(P05FrontOverlayProofController controller, string objectId)
        {
            _controller = controller;
            _objectId = objectId;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // First update the local visual selection state
            _controller?.SelectObject(_objectId);
            // Then route through the V1.1 Contract bridge for world-state evaluation
            GPTLabyrinth.P02.P05RoomPattern001Bridge.OnGlyphSelected(_objectId);
        }
    }
}
