using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Editor
{
    public sealed class FigunityUgUiComposer
    {
        public FigunityBuildResult ComposeScreen(FigunityScreen screen, Transform parent, FigunityFrameOptions options)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (screen.tree == null) throw new ArgumentException("FIGUNITY screen payload has no tree.", nameof(screen));
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            var rootBounds = options.useCrop
                ? options.crop
                : new FigunityBounds(0f, 0f, Mathf.Max(1f, screen.width), Mathf.Max(1f, screen.height));

            var rootName = string.IsNullOrWhiteSpace(options.rootName)
                ? FigunityNameRules.ToObjectName(screen.frameName)
                : options.rootName;

            var root = MakeRect(rootName, parent, rootBounds, rootBounds);
            root.gameObject.SetActive(options.active);

            if (options.addRootMask)
            {
                root.gameObject.AddComponent<RectMask2D>();
            }

            if (options.includeRootBackground)
            {
                AttachGraphic(screen.tree, root, preferSolid: true);
            }

            var report = new FigunityBuildReport();
            if (screen.tree.children != null)
            {
                for (var i = 0; i < screen.tree.children.Count; i++)
                {
                    ComposeNode(screen.tree.children[i], root, rootBounds, options, report);
                }
            }

            return new FigunityBuildResult(root, report);
        }

        public static Slider AddReadonlyProgress(string name, RectTransform parent, float x, float y, float width, float height, Color trackColor, Color fillColor, float value)
        {
            var root = MakeRect(name, parent, new FigunityBounds(x, y, width, height), new FigunityBounds(0f, 0f, 0f, 0f));
            return AddSlider(root, width, height, trackColor, fillColor, Mathf.Clamp01(value), null);
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void ComposeNode(FigunityNode node, RectTransform parent, FigunityBounds parentBounds, FigunityFrameOptions options, FigunityBuildReport report)
        {
            if (node == null || string.Equals(node.renderMode, "skip", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (options.useCrop && !options.crop.HoldsCenterOf(node.bounds))
            {
                return;
            }

            FigunityMeterShape meter;
            if (FigunityMeterScanner.TryResolve(node, out meter))
            {
                ComposeMeter(meter, parent, parentBounds, report);
                return;
            }

            var rect = MakeRect(FigunityNameRules.ToObjectName(node.name), parent, node.bounds, parentBounds);
            var graphic = AttachVisual(node, rect);

            if (node.CarriesText)
            {
                AttachText(node, rect);
                report.texts++;
            }
            else if (graphic != null)
            {
                report.graphics++;
            }

            if (node.clipsContent)
            {
                rect.gameObject.AddComponent<RectMask2D>();
            }

            AttachButtonWhenNamed(node, rect, graphic);

            if (node.children == null)
            {
                return;
            }

            for (var i = 0; i < node.children.Count; i++)
            {
                ComposeNode(node.children[i], rect, node.bounds, options, report);
            }
        }

        private static void ComposeMeter(FigunityMeterShape meter, RectTransform parent, FigunityBounds parentBounds, FigunityBuildReport report)
        {
            var node = meter.container;
            var rect = MakeRect(FigunityNameRules.ToObjectName(node.name), parent, node.bounds, parentBounds);

            Color trackColor;
            if (!FigunityPaintRules.TrySolid(meter.track, out trackColor))
            {
                trackColor = new Color32(35, 39, 44, 255);
            }

            Color fillColor;
            if (!FigunityPaintRules.TrySolid(meter.fill, out fillColor))
            {
                fillColor = new Color32(74, 142, 80, 255);
            }

            var trackHeight = Mathf.Max(2f, meter.track.bounds.height);
            var slider = AddSlider(rect, Mathf.Max(1f, node.bounds.width), trackHeight, trackColor, fillColor, meter.normalizedValue, meter);
            slider.name = rect.name;
            report.sliders++;
        }

        private static Slider AddSlider(RectTransform rect, float width, float height, Color trackColor, Color fillColor, float value, FigunityMeterShape? source)
        {
            var trackBounds = source.HasValue ? source.Value.track.bounds : new FigunityBounds(0f, 0f, width, height);
            var localTrack = source.HasValue
                ? new FigunityBounds(
                    trackBounds.x - source.Value.container.bounds.x,
                    trackBounds.y - source.Value.container.bounds.y,
                    trackBounds.width,
                    trackBounds.height)
                : trackBounds;

            var track = MakeRect("Track", rect, localTrack, new FigunityBounds(0f, 0f, 0f, 0f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = trackColor;
            trackImage.raycastTarget = false;

            var fillArea = MakeRect("Fill Area", rect, localTrack, new FigunityBounds(0f, 0f, 0f, 0f));
            fillArea.anchorMin = new Vector2(0f, 1f);
            fillArea.anchorMax = new Vector2(0f, 1f);

            var fill = MakeRect("Fill", fillArea, new FigunityBounds(0f, 0f, localTrack.width, localTrack.height), new FigunityBounds(0f, 0f, 0f, 0f));
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            var handleArea = MakeRect("Handle Slide Area", rect, localTrack, new FigunityBounds(0f, 0f, 0f, 0f));
            var handleSize = Mathf.Max(10f, localTrack.height + 6f);
            var handle = MakeRect(
                "Handle",
                handleArea,
                new FigunityBounds(value * localTrack.width - handleSize * 0.5f, (localTrack.height - handleSize) * 0.5f, handleSize, handleSize),
                new FigunityBounds(0f, 0f, 0f, 0f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0f);
            handleImage.raycastTarget = false;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            SetSliderValue(slider, value);
            return slider;
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            var clamped = Mathf.Clamp01(value);
            slider.SetValueWithoutNotify(clamped);

            if (slider.fillRect != null)
            {
                slider.fillRect.anchorMin = Vector2.zero;
                slider.fillRect.anchorMax = new Vector2(clamped, 1f);
                slider.fillRect.offsetMin = Vector2.zero;
                slider.fillRect.offsetMax = Vector2.zero;
            }

            if (slider.handleRect != null)
            {
                slider.handleRect.anchorMin = new Vector2(clamped, 0.5f);
                slider.handleRect.anchorMax = new Vector2(clamped, 0.5f);
                slider.handleRect.anchoredPosition = Vector2.zero;
            }
        }

        private static Graphic AttachVisual(FigunityNode node, RectTransform rect)
        {
            if (node.CarriesText)
            {
                return null;
            }

            var exportedVisual = string.Equals(node.renderMode, "visual", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(node.renderMode, "background", StringComparison.OrdinalIgnoreCase);
            var flat = FigunityPaintRules.IsFlatFill(node);
            if (!exportedVisual && !flat)
            {
                return null;
            }

            return AttachGraphic(node, rect, flat);
        }

        private static Graphic AttachGraphic(FigunityNode node, RectTransform rect, bool preferSolid)
        {
            if (node == null || rect == null)
            {
                return null;
            }

            Color color;
            var hasSolid = FigunityPaintRules.TrySolid(node, out color);
            if (preferSolid || FigunityPaintRules.IsFlatFill(node) || string.IsNullOrWhiteSpace(node.assetPath))
            {
                if (!hasSolid && string.IsNullOrWhiteSpace(node.assetPath))
                {
                    return null;
                }

                var image = rect.gameObject.AddComponent<Image>();
                image.color = hasSolid ? color : Color.white;
                image.raycastTarget = false;
                return image;
            }

            var rawImage = rect.gameObject.AddComponent<RawImage>();
            rawImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(node.assetPath);
            rawImage.color = rawImage.texture == null ? Color.clear : new Color(1f, 1f, 1f, FigunityPaintRules.NodeAlpha(node));
            rawImage.raycastTarget = false;
            return rawImage;
        }

        private static void AttachText(FigunityNode node, RectTransform rect)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = node.text != null ? node.text.characters ?? string.Empty : string.Empty;
            label.fontSize = node.text != null && node.text.fontSize > 0f ? node.text.fontSize : 16f;
            label.fontStyle = ResolveTextStyle(node.text != null ? node.text.fontName : null);
            label.alignment = FigunityPaintRules.TextAlign(node.text);
            label.color = FigunityPaintRules.TextColor(node);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }

        private static FontStyles ResolveTextStyle(FigunityFont font)
        {
            if (font == null || string.IsNullOrWhiteSpace(font.style))
            {
                return FontStyles.Normal;
            }

            var style = font.style.ToLowerInvariant();
            var resolved = FontStyles.Normal;
            if (style.Contains("bold") || style.Contains("black") || style.Contains("heavy") || style.Contains("semibold"))
            {
                resolved |= FontStyles.Bold;
            }

            if (style.Contains("italic"))
            {
                resolved |= FontStyles.Italic;
            }

            return resolved;
        }

        private static void AttachButtonWhenNamed(FigunityNode node, RectTransform rect, Graphic graphic)
        {
            var name = FigunityNameRules.Compact(node.name);
            if (!name.StartsWith("button", StringComparison.Ordinal) && !name.Contains("button"))
            {
                return;
            }

            var button = rect.gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }

            var target = graphic;
            if (target == null)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                target = image;
            }

            target.raycastTarget = true;
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static RectTransform MakeRect(string name, Transform parent, FigunityBounds nodeBounds, FigunityBounds parentBounds)
        {
            var go = new GameObject(UniqueName(parent, name), typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(nodeBounds.x - parentBounds.x, -(nodeBounds.y - parentBounds.y));
            rect.sizeDelta = new Vector2(Mathf.Max(0f, nodeBounds.width), Mathf.Max(0f, nodeBounds.height));
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static string UniqueName(Transform parent, string desired)
        {
            desired = string.IsNullOrWhiteSpace(desired) ? "Node" : desired;
            if (parent == null || parent.Find(desired) == null)
            {
                return desired;
            }

            var index = 2;
            string candidate;
            do
            {
                candidate = desired + "_" + index.ToString("00");
                index++;
            }
            while (parent.Find(candidate) != null);

            return candidate;
        }
    }

    public struct FigunityFrameOptions
    {
        public string rootName;
        public bool active;
        public bool useCrop;
        public FigunityBounds crop;
        public bool includeRootBackground;
        public bool addRootMask;
    }

    public readonly struct FigunityBuildResult
    {
        public readonly RectTransform root;
        public readonly FigunityBuildReport report;

        public FigunityBuildResult(RectTransform root, FigunityBuildReport report)
        {
            this.root = root;
            this.report = report;
        }
    }

    public struct FigunityBuildReport
    {
        public int texts;
        public int graphics;
        public int sliders;
    }
}
