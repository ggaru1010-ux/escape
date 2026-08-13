using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GPTLabyrinth.P02
{
    public sealed class P02ObjectPngOverlayController : MonoBehaviour
    {
        private const string ObjectPngRoot = "GPTLabyrinth/object_png";
        private const byte BlackKeyThreshold = 8;
        private const byte WhiteKeyThreshold = 245;

        private static readonly Rect P00DoorRect = new Rect(0.410f, 0.275f, 0.180f, 0.430f);
        private static readonly Rect P01GateRect = new Rect(0.335f, 0.205f, 0.330f, 0.570f);
        private static readonly Rect OwlRect = new Rect(0.384f, 0.180f, 0.232f, 0.550f);
        private static readonly Rect P03LampRect = new Rect(0.350f, 0.150f, 0.300f, 0.520f);
        private static readonly Rect P04FakeCoreDoorRect = new Rect(0.305f, 0.185f, 0.390f, 0.615f);
        private static readonly Rect P05DeltaGlyphRect = new Rect(0.375f, 0.335f, 0.250f, 0.220f);
        private static readonly Rect LeftEyeRect = new Rect(0.463f, 0.610f, 0.017f, 0.030f);
        private static readonly Rect RightEyeRect = new Rect(0.507f, 0.610f, 0.017f, 0.030f);
        private static readonly Vector2 LeftEyeAnchor = new Vector2(0.46686f, 0.61738f);
        private static readonly Vector2 RightEyeAnchor = new Vector2(0.50332f, 0.61869f);
        private static readonly Vector2 EyeSize = new Vector2(0.017f, 0.030f);

        private readonly List<OverlayEntry> _entries = new List<OverlayEntry>();
        private RectTransform _root;
        private RectTransform _previewRect;
        private string _roomId;
        private string _viewName;

        public RectTransform Root => _root;
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;
        public IReadOnlyList<OverlayEntry> Entries => _entries;

        public bool TryGetEntry(string objectName, out OverlayEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].ObjectName, objectName, StringComparison.Ordinal))
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public void Initialize(RawImage previewImage, RectTransform previewRect)
        {
            if (_root != null)
                return;

            _previewRect = previewRect;

            var rootGo = new GameObject("ROOM_OBJECT_PNG_OVERLAY_ROOT_TEMP", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(_previewRect, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.localScale = Vector3.one;
            _root.localRotation = Quaternion.identity;

            CreateImageEntry("P00_DOOR_FRONT_PNG_TEMP", "p00.png", P00DoorRect, true, 1f, "P00", "Front");
            CreateImageEntry("P01_ANCIENT_GATE_PNG_TEMP", "p01final.png", P01GateRect, true, 1f, "P01", "Front");
            CreateImageEntry("P02_OWL_BODY_PNG_TEMP", "p02Final.png", OwlRect, false, 1f);
            CreateAnchoredImageEntry("P02_FRONT_LeftEye_Anchor", "P02_OWL_LEFT_EYE_PNG_TEMP", "p02eye.png", LeftEyeAnchor, EyeSize, true, 0.70f);
            CreateAnchoredImageEntry("P02_FRONT_RightEye_Anchor", "P02_OWL_RIGHT_EYE_PNG_TEMP", "p02eye.png", RightEyeAnchor, EyeSize, true, 0.70f);
            CreateImageEntry("P03_ROTATING_LAMP_PNG_TEMP", "p03final.png", P03LampRect, true, 1f, "P03", "Front");
            CreateImageEntry("P04_FAKE_CORE_DOOR_PNG_TEMP", "p04final.png", P04FakeCoreDoorRect, true, 1f, "P04", "Front");
            CreateImageEntry("P05_DELTA_GLYPH_PNG_TEMP", "p05final.png", P05DeltaGlyphRect, true, 1f, "P05", "Front");

            ApplyRoomView(string.Empty, string.Empty);
        }

        public void ApplyRoomView(string roomId, string viewName)
        {
            _roomId = roomId;
            _viewName = viewName;
            bool anyVisible = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                bool visible = _entries[i].Matches(roomId, viewName);
                _entries[i].SetVisible(visible);
                anyVisible |= visible;
            }

            SetVisible(anyVisible);
        }

        public string GetRuntimeAudit()
        {
            int loaded = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].TextureLoaded)
                    loaded++;

            return $"Room={_roomId}; View={_viewName}; Visible={IsVisible}; Loaded={loaded}/{_entries.Count}; PreviewScale={(_previewRect != null ? _previewRect.localScale.ToString("F3") : "null")}";
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void CreateImageEntry(string objectName, string fileName, Rect normalizedRect, bool cropToVisibleContent, float alpha, string roomId = "P02", string viewName = "Front")
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_root, false);
            ApplyNormalizedRect(rt, normalizedRect);

            var image = go.GetComponent<RawImage>();
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            Texture2D texture = LoadObjectTexture(fileName, cropToVisibleContent);
            image.texture = texture;

            _entries.Add(new OverlayEntry
            {
                ObjectName = objectName,
                FileName = fileName,
                NormalizedRect = normalizedRect,
                RectTransform = rt,
                Image = image,
                TextureLoaded = texture != null,
                RoomId = roomId,
                ViewName = viewName,
            });
        }

        private void CreateAnchoredImageEntry(string anchorName, string objectName, string fileName, Vector2 anchorPoint, Vector2 normalizedSize, bool cropToVisibleContent, float alpha, string roomId = "P02", string viewName = "Front")
        {
            var anchorGo = new GameObject(anchorName, typeof(RectTransform));
            var anchorRt = anchorGo.GetComponent<RectTransform>();
            anchorRt.SetParent(_root, false);
            anchorRt.anchorMin = anchorPoint;
            anchorRt.anchorMax = anchorPoint;
            anchorRt.anchoredPosition = Vector2.zero;
            anchorRt.sizeDelta = Vector2.zero;
            anchorRt.pivot = new Vector2(0.5f, 0.5f);
            anchorRt.localScale = Vector3.one;
            anchorRt.localRotation = Quaternion.identity;

            var go = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(anchorRt, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var image = go.GetComponent<RawImage>();
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            Texture2D texture = LoadObjectTexture(fileName, cropToVisibleContent);
            image.texture = texture;

            _entries.Add(new OverlayEntry
            {
                ObjectName = objectName,
                FileName = fileName,
                NormalizedRect = new Rect(anchorPoint.x - normalizedSize.x * 0.5f, anchorPoint.y - normalizedSize.y * 0.5f, normalizedSize.x, normalizedSize.y),
                RectTransform = rt,
                AnchorTransform = anchorRt,
                Image = image,
                TextureLoaded = texture != null,
                RoomId = roomId,
                ViewName = viewName,
            });
        }

        private void LateUpdate()
        {
            ApplyAnchoredEntrySizes();
        }

        private void ApplyAnchoredEntrySizes()
        {
            if (_previewRect == null)
                return;

            Rect rect = _previewRect.rect;
            for (int i = 0; i < _entries.Count; i++)
            {
                OverlayEntry entry = _entries[i];
                if (entry.AnchorTransform == null || entry.RectTransform == null)
                    continue;

                entry.RectTransform.sizeDelta = new Vector2(
                    Mathf.Abs(rect.width) * entry.NormalizedRect.width,
                    Mathf.Abs(rect.height) * entry.NormalizedRect.height);
            }
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

        private static Texture2D LoadObjectTexture(string fileName, bool cropToVisibleContent)
        {
            string fullPath = Path.Combine(Application.dataPath, ObjectPngRoot, fileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning("[P02_OBJECT_PNG] Missing " + fullPath);
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                    return null;
                texture.name = "P02_RUNTIME_" + Path.GetFileNameWithoutExtension(fileName);
                ApplyBlackKeyTransparency(texture);
                if (cropToVisibleContent)
                    texture = CropToVisibleContent(texture);
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[P02_OBJECT_PNG] Failed to load " + fullPath + ": " + ex.Message);
                return null;
            }
        }

        private static void ApplyBlackKeyTransparency(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                bool blackKey = p.r <= BlackKeyThreshold && p.g <= BlackKeyThreshold && p.b <= BlackKeyThreshold;
                bool whiteKey = p.r >= WhiteKeyThreshold && p.g >= WhiteKeyThreshold && p.b >= WhiteKeyThreshold;
                if (blackKey || whiteKey)
                    p.a = 0;
                pixels[i] = p;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static Texture2D CropToVisibleContent(Texture2D source)
        {
            Color32[] pixels = source.GetPixels32();
            int minX = source.width;
            int minY = source.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a == 0)
                        continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return source;

            const int padding = 8;
            minX = Mathf.Max(0, minX - padding);
            minY = Mathf.Max(0, minY - padding);
            maxX = Mathf.Min(source.width - 1, maxX + padding);
            maxY = Mathf.Min(source.height - 1, maxY + padding);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            Color32[] croppedPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceStart = (minY + y) * source.width + minX;
                int targetStart = y * width;
                Array.Copy(pixels, sourceStart, croppedPixels, targetStart, width);
            }

            var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cropped.SetPixels32(croppedPixels);
            cropped.Apply(false, false);
            cropped.name = source.name + "_CROPPED";
            return cropped;
        }

        public sealed class OverlayEntry
        {
            public string ObjectName;
            public string FileName;
            public Rect NormalizedRect;
            public RectTransform RectTransform;
            public RectTransform AnchorTransform;
            public RawImage Image;
            public bool TextureLoaded;
            public string RoomId;
            public string ViewName;
            public bool IsActive
            {
                get
                {
                    if (AnchorTransform != null)
                        return AnchorTransform.gameObject.activeInHierarchy;
                    return RectTransform != null && RectTransform.gameObject.activeInHierarchy;
                }
            }

            public bool Matches(string roomId, string viewName)
            {
                return string.Equals(RoomId, roomId, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(ViewName, viewName, StringComparison.OrdinalIgnoreCase);
            }

            public void SetVisible(bool visible)
            {
                if (AnchorTransform != null)
                {
                    AnchorTransform.gameObject.SetActive(visible);
                    return;
                }

                if (RectTransform != null)
                    RectTransform.gameObject.SetActive(visible);
            }
        }
    }
}


