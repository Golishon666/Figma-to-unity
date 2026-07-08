using System;
using Figunity.Runtime;
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
            AttachMetadata(screen.tree, root);

            if (options.addRootMask || (options.createMasks && screen.tree.clipsContent))
            {
                EnsureComponent<RectMask2D>(root.gameObject);
            }

            if (options.includeRootBackground)
            {
                AttachGraphic(screen.tree, root, true, options);
            }

            var report = new FigunityBuildReport();
            ComposeChildren(screen.tree, root, rootBounds, options, report);

            return new FigunityBuildResult(root, report);
        }

        public static Slider AddReadonlyProgress(string name, RectTransform parent, float x, float y, float width, float height, Color trackColor, Color fillColor, float value)
        {
            var root = MakeRect(name, parent, new FigunityBounds(x, y, width, height), new FigunityBounds(0f, 0f, 0f, 0f));
            return AddSlider(root, width, height, trackColor, fillColor, Mathf.Clamp01(value), null, default);
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

            var controlKind = FigunityControlRules.Resolve(node);
            if (controlKind == FigunityControlKind.Scroll)
            {
                ComposeScrollView(node, parent, parentBounds, options, report);
                return;
            }

            FigunityMeterShape meter;
            if (FigunityMeterScanner.TryResolve(node, out meter))
            {
                ComposeMeter(meter, parent, parentBounds, options, report);
                return;
            }

            var rect = MakeRect(FigunityNameRules.ToObjectName(node.name), parent, node.bounds, parentBounds);
            AttachMetadata(node, rect);
            AttachLayoutElement(node, rect);
            var graphic = AttachVisual(node, rect, options);

            if (node.CarriesText)
            {
                AttachText(node, rect, options);
                report.texts++;
            }
            else if (graphic != null)
            {
                report.graphics++;
            }

            if (IsCompositeRasterNode(node))
            {
                return;
            }

            if (node.clipsContent)
            {
                rect.gameObject.AddComponent<RectMask2D>();
                report.masks++;
            }

            AttachMaskIfNeeded(node, rect, graphic, options, report);
            AttachAutoLayoutIfNeeded(node, rect, options, report);
            AttachControlIfNeeded(node, rect, graphic, controlKind, options, report);

            if (ControlOwnsChildren(controlKind))
            {
                return;
            }

            ComposeChildren(node, rect, node.bounds, options, report);
        }

        private static void ComposeScrollView(FigunityNode node, RectTransform parent, FigunityBounds parentBounds, FigunityFrameOptions options, FigunityBuildReport report)
        {
            var visibleBounds = VisibleBoundsWithinParent(node.bounds, parentBounds);
            var root = MakeRect(FigunityNameRules.ToObjectName(node.name), parent, visibleBounds, parentBounds);
            AttachMetadata(node, root);
            AttachLayoutElement(node, root);
            var graphic = AttachVisual(node, root, options);

            var viewport = MakeRect("Viewport", root, new FigunityBounds(0f, 0f, visibleBounds.width, visibleBounds.height), new FigunityBounds(0f, 0f, 0f, 0f));
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = MakeRect("Content", viewport, new FigunityBounds(0f, 0f, node.bounds.width, Mathf.Max(visibleBounds.height, EstimateContentHeight(node))), new FigunityBounds(0f, 0f, 0f, 0f));
            if (node.autoLayout != null && node.autoLayout.Enabled)
            {
                AttachAutoLayoutIfNeeded(node, content, options, report);
            }

            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 24f;
            report.scrollViews++;

            ComposeChildren(node, content, node.bounds, options, report);
        }

        private static float EstimateContentHeight(FigunityNode node)
        {
            var max = node.bounds.height;
            if (node.children == null)
            {
                return max;
            }

            for (var i = 0; i < node.children.Count; i++)
            {
                var child = node.children[i];
                if (child == null)
                {
                    continue;
                }

                max = Mathf.Max(max, child.bounds.y - node.bounds.y + child.bounds.height);
            }

            return max;
        }

        private static FigunityBounds VisibleBoundsWithinParent(FigunityBounds nodeBounds, FigunityBounds parentBounds)
        {
            var right = parentBounds.Right > parentBounds.x ? Mathf.Min(nodeBounds.Right, parentBounds.Right) : nodeBounds.Right;
            var bottom = parentBounds.Bottom > parentBounds.y ? Mathf.Min(nodeBounds.Bottom, parentBounds.Bottom) : nodeBounds.Bottom;
            return new FigunityBounds(
                nodeBounds.x,
                nodeBounds.y,
                Mathf.Max(1f, right - nodeBounds.x),
                Mathf.Max(1f, bottom - nodeBounds.y));
        }

        private static void ComposeChildren(FigunityNode owner, RectTransform parent, FigunityBounds parentBounds, FigunityFrameOptions options, FigunityBuildReport report)
        {
            if (owner?.children == null)
            {
                return;
            }

            for (var i = 0; i < owner.children.Count; i++)
            {
                var child = owner.children[i];
                if (child == null)
                {
                    continue;
                }

                if (options.createMasks && child.isMask)
                {
                    var maskRect = ComposeMaskHost(child, parent, parentBounds, options, report);
                    i++;
                    while (i < owner.children.Count && owner.children[i] != null && !owner.children[i].isMask)
                    {
                        ComposeNode(owner.children[i], maskRect, child.bounds, options, report);
                        i++;
                    }

                    i--;
                    continue;
                }

                ComposeNode(child, parent, parentBounds, options, report);
            }
        }

        private static RectTransform ComposeMaskHost(FigunityNode maskNode, RectTransform parent, FigunityBounds parentBounds, FigunityFrameOptions options, FigunityBuildReport report)
        {
            var rect = MakeRect(FigunityNameRules.ToObjectName(maskNode.name), parent, maskNode.bounds, parentBounds);
            AttachMetadata(maskNode, rect);
            AttachLayoutElement(maskNode, rect);
            var graphic = AttachMaskStencilGraphic(maskNode, rect, options);

            var mask = rect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            graphic.raycastTarget = false;
            report.masks++;
            return rect;
        }

        private static void ComposeMeter(FigunityMeterShape meter, RectTransform parent, FigunityBounds parentBounds, FigunityFrameOptions options, FigunityBuildReport report)
        {
            var node = meter.container;
            var rect = MakeRect(FigunityNameRules.ToObjectName(node.name), parent, node.bounds, parentBounds);
            AttachMetadata(node, rect);
            AttachLayoutElement(node, rect);

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
            var slider = AddSlider(rect, Mathf.Max(1f, node.bounds.width), trackHeight, trackColor, fillColor, meter.normalizedValue, meter, options);
            slider.name = rect.name;
            report.sliders++;
        }

        private static Slider AddSlider(RectTransform rect, float width, float height, Color trackColor, Color fillColor, float value, FigunityMeterShape? source, FigunityFrameOptions options)
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
            var trackNode = source.HasValue ? source.Value.track : null;
            var trackGraphic = trackNode != null ? AttachGraphic(trackNode, track, true, options) : null;
            if (trackGraphic == null)
            {
                var trackImage = track.gameObject.AddComponent<Image>();
                trackImage.color = trackColor;
                trackGraphic = trackImage;
            }

            trackGraphic.raycastTarget = false;

            var fillArea = MakeRect("Fill Area", rect, localTrack, new FigunityBounds(0f, 0f, 0f, 0f));
            fillArea.anchorMin = new Vector2(0f, 1f);
            fillArea.anchorMax = new Vector2(0f, 1f);

            var fill = MakeRect("Fill", fillArea, new FigunityBounds(0f, 0f, localTrack.width, localTrack.height), new FigunityBounds(0f, 0f, 0f, 0f));
            var fillNode = source.HasValue ? source.Value.fill : null;
            var fillGraphic = fillNode != null ? AttachGraphic(fillNode, fill, true, options) : null;
            if (fillGraphic == null)
            {
                var fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = fillColor;
                fillGraphic = fillImage;
            }

            fillGraphic.raycastTarget = false;

            var handleNode = source.HasValue ? source.Value.handle : null;
            var handleBounds = source.HasValue && source.Value.handle != null
                ? source.Value.handle.bounds
                : new FigunityBounds(trackBounds.x + value * trackBounds.width - Mathf.Max(10f, localTrack.height + 6f) * 0.5f, trackBounds.y, Mathf.Max(10f, localTrack.height + 6f), Mathf.Max(10f, localTrack.height + 6f));
            var handleWidth = Mathf.Max(1f, handleBounds.width);
            var handleHeight = Mathf.Max(1f, handleBounds.height);
            var drivenHandle = source.HasValue && source.Value.interactable;
            RectTransform handle;
            if (drivenHandle)
            {
                var handleAreaBounds = new FigunityBounds(
                    localTrack.x,
                    localTrack.y + (localTrack.height - handleHeight) * 0.5f,
                    localTrack.width,
                    handleHeight);
                var handleArea = MakeRect("Handle Slide Area", rect, handleAreaBounds, new FigunityBounds(0f, 0f, 0f, 0f));
                handle = MakeRect("Handle", handleArea, new FigunityBounds(0f, 0f, handleWidth, handleHeight), new FigunityBounds(0f, 0f, 0f, 0f));
                ConfigureDrivenHandle(handle, value, handleWidth);
            }
            else if (handleNode != null)
            {
                var localHandle = new FigunityBounds(
                    handleBounds.x - source.Value.container.bounds.x,
                    handleBounds.y - source.Value.container.bounds.y,
                    handleWidth,
                    handleHeight);
                handle = MakeRect("Handle", rect, localHandle, new FigunityBounds(0f, 0f, 0f, 0f));
            }
            else
            {
                handle = null;
            }

            Color handleColor;
            var handleGraphic = handle != null ? AttachHandleGraphic(handleNode, handle, options) : null;
            if (handle != null && handleGraphic == null)
            {
                var handleImage = handle.gameObject.AddComponent<Image>();
                handleImage.color = handleNode != null && FigunityPaintRules.TrySolid(handleNode, out handleColor)
                    ? handleColor
                    : (handleNode != null ? fillColor : new Color(fillColor.r, fillColor.g, fillColor.b, 0f));
                handleGraphic = handleImage;
            }

            if (handleGraphic != null)
            {
                handleGraphic.raycastTarget = drivenHandle;
            }

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = source.HasValue && source.Value.interactable;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = drivenHandle ? handle : null;
            slider.targetGraphic = handleGraphic;
            SetSliderValue(slider, value);
            return slider;
        }

        private static void ConfigureDrivenHandle(RectTransform handle, float value, float width)
        {
            var clamped = Mathf.Clamp01(value);
            handle.anchorMin = new Vector2(clamped, 0f);
            handle.anchorMax = new Vector2(clamped, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(Mathf.Max(1f, width), 0f);
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
                var width = Mathf.Max(1f, slider.handleRect.sizeDelta.x);
                ConfigureDrivenHandle(slider.handleRect, clamped, width);
            }
        }

        private static Graphic AttachVisual(FigunityNode node, RectTransform rect, FigunityFrameOptions options)
        {
            if (node.CarriesText)
            {
                return null;
            }

            var exportedVisual = string.Equals(node.renderMode, "visual", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(node.renderMode, "background", StringComparison.OrdinalIgnoreCase) ||
                                 IsCompositeRasterNode(node);
            var flat = FigunityPaintRules.IsFlatFill(node);
            if (!exportedVisual && !flat)
            {
                return null;
            }

            return AttachGraphic(node, rect, flat, options);
        }

        private static Graphic AttachGraphic(FigunityNode node, RectTransform rect, bool preferSolid, FigunityFrameOptions options)
        {
            if (node == null || rect == null)
            {
                return null;
            }

            Color color;
            var hasSolid = FigunityPaintRules.TrySolid(node, out color);
            var exactImage = AttachRawImage(node, rect);
            if (exactImage != null)
            {
                return exactImage;
            }

            if (preferSolid || FigunityPaintRules.IsFlatFill(node) || string.IsNullOrWhiteSpace(node.assetPath))
            {
                if (!hasSolid && string.IsNullOrWhiteSpace(node.assetPath))
                {
                    return null;
                }

                if (hasSolid && options.createRoundedRectGraphics && ShouldUseEllipseGraphic(node))
                {
                    var ellipse = rect.gameObject.AddComponent<FigunityEllipseGraphic>();
                    ellipse.color = color;
                    ellipse.raycastTarget = false;
                    return ellipse;
                }

                if (hasSolid && options.createRoundedRectGraphics && node.cornerRadius > 0.5f)
                {
                    var rounded = rect.gameObject.AddComponent<FigunityRoundedRectGraphic>();
                    rounded.color = color;
                    rounded.CornerRadius = node.cornerRadius;
                    rounded.raycastTarget = false;
                    return rounded;
                }

                var image = rect.gameObject.AddComponent<Image>();
                image.color = hasSolid ? color : Color.white;
                image.raycastTarget = false;
                return image;
            }

            return null;
        }

        private static Graphic AttachHandleGraphic(FigunityNode node, RectTransform rect, FigunityFrameOptions options)
        {
            return AttachRawImage(node, rect) ?? (node != null ? AttachGraphic(node, rect, true, options) : null);
        }

        private static RawImage AttachRawImage(FigunityNode node, RectTransform rect)
        {
            if (node == null || rect == null || string.IsNullOrWhiteSpace(node.assetPath))
            {
                return null;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(node.assetPath);
            if (texture == null)
            {
                return null;
            }

            if (texture.width <= 1 && texture.height <= 1 && (node.bounds.width > 2f || node.bounds.height > 2f))
            {
                return null;
            }

            var rawImage = rect.gameObject.AddComponent<RawImage>();
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            return rawImage;
        }

        private static bool IsCompositeRasterNode(FigunityNode node)
        {
            return node != null && string.Equals(node.renderMode, "composite", StringComparison.OrdinalIgnoreCase);
        }

        private static Graphic AttachMaskStencilGraphic(FigunityNode node, RectTransform rect, FigunityFrameOptions options)
        {
            if (node != null && options.createRoundedRectGraphics && ShouldUseEllipseGraphic(node))
            {
                var ellipse = rect.gameObject.AddComponent<FigunityEllipseGraphic>();
                ellipse.color = Color.white;
                ellipse.raycastTarget = false;
                return ellipse;
            }

            if (node != null && options.createRoundedRectGraphics && node.cornerRadius > 0.5f)
            {
                var rounded = rect.gameObject.AddComponent<FigunityRoundedRectGraphic>();
                rounded.color = Color.white;
                rounded.CornerRadius = node.cornerRadius;
                rounded.raycastTarget = false;
                return rounded;
            }

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static bool ShouldUseEllipseGraphic(FigunityNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (string.Equals(node.type, "ELLIPSE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var size = Mathf.Min(node.bounds.width, node.bounds.height);
            return size > 0.01f &&
                   Mathf.Abs(node.bounds.width - node.bounds.height) <= 0.5f &&
                   node.cornerRadius >= size * 0.49f;
        }

        private static void AttachText(FigunityNode node, RectTransform rect, FigunityFrameOptions options)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureTextLabel(label, node.text, FigunityPaintRules.TextColor(node), options);
        }

        private static void ConfigureTextLabel(TextMeshProUGUI label, FigunityText text, Color color, FigunityFrameOptions options)
        {
            label.text = text != null ? text.characters ?? string.Empty : string.Empty;
            label.fontSize = text != null && text.fontSize > 0f ? text.fontSize : 16f;
            label.fontStyle = ResolveTextStyle(text != null ? text.fontName : null);
            label.alignment = FigunityPaintRules.TextAlign(text);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            var font = ResolveTextFont(text, options.settings);
            if (font != null)
            {
                label.font = font;
            }
        }

        private static TMP_FontAsset ResolveTextFont(FigunityText text, FigunitySettingsAsset settings)
        {
            var mapped = ResolveMappedFont(text, settings);
            if (IsUsableFont(mapped))
            {
                return mapped;
            }

            var assetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (IsUsableFont(assetFont))
            {
                return assetFont;
            }

            var resourceFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (IsUsableFont(resourceFont))
            {
                return resourceFont;
            }

            var defaultFont = TMP_Settings.defaultFontAsset;
            return IsUsableFont(defaultFont) ? defaultFont : null;
        }

        private static TMP_FontAsset ResolveMappedFont(FigunityText text, FigunitySettingsAsset settings)
        {
            if (text == null || text.fontName == null || settings == null || settings.fontMappings == null)
            {
                return null;
            }

            for (var i = 0; i < settings.fontMappings.Count; i++)
            {
                var mapping = settings.fontMappings[i];
                if (mapping == null || mapping.fontAsset == null)
                {
                    continue;
                }

                var familyMatches = string.IsNullOrWhiteSpace(mapping.figmaFamily) ||
                                    string.Equals(mapping.figmaFamily, text.fontName.family, StringComparison.OrdinalIgnoreCase);
                var styleMatches = string.IsNullOrWhiteSpace(mapping.figmaStyle) ||
                                   string.Equals(mapping.figmaStyle, text.fontName.style, StringComparison.OrdinalIgnoreCase);
                if (familyMatches && styleMatches)
                {
                    return mapping.fontAsset;
                }
            }

            return null;
        }

        private static bool IsUsableFont(TMP_FontAsset font)
        {
            try
            {
                return font != null &&
                       font.atlasTextures != null &&
                       font.atlasTextures.Length > 0 &&
                       font.atlasTextures[0] != null;
            }
            catch (UnassignedReferenceException)
            {
                return false;
            }
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

        private static void AttachMaskIfNeeded(FigunityNode node, RectTransform rect, Graphic graphic, FigunityFrameOptions options, FigunityBuildReport report)
        {
            if (!options.createMasks || !node.isMask)
            {
                return;
            }

            var target = graphic;
            if (target == null)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;
                target = image;
            }

            var mask = rect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            target.raycastTarget = false;
            report.masks++;
        }

        private static void AttachAutoLayoutIfNeeded(FigunityNode node, RectTransform rect, FigunityFrameOptions options, FigunityBuildReport report)
        {
            if (!options.applyAutoLayoutGroups || node.autoLayout == null || !node.autoLayout.Enabled)
            {
                return;
            }

            if (string.Equals(node.autoLayout.layoutMode, "HORIZONTAL", StringComparison.OrdinalIgnoreCase))
            {
                var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
                ApplyLayout(layout, node.autoLayout);
            }
            else
            {
                var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
                ApplyLayout(layout, node.autoLayout);
            }

            report.layoutGroups++;
        }

        private static void ApplyLayout(HorizontalOrVerticalLayoutGroup layout, FigunityAutoLayout data)
        {
            layout.padding = new RectOffset(
                Mathf.RoundToInt(data.paddingLeft),
                Mathf.RoundToInt(data.paddingRight),
                Mathf.RoundToInt(data.paddingTop),
                Mathf.RoundToInt(data.paddingBottom));
            layout.spacing = data.itemSpacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = ResolveChildAlignment(data);
        }

        private static TextAnchor ResolveChildAlignment(FigunityAutoLayout data)
        {
            var counter = (data.counterAxisAlignItems ?? string.Empty).ToUpperInvariant();
            var primary = (data.primaryAxisAlignItems ?? string.Empty).ToUpperInvariant();
            var middle = counter.Contains("CENTER");
            var end = primary.Contains("MAX") || primary.Contains("END");

            if (middle && end) return TextAnchor.MiddleRight;
            if (middle) return TextAnchor.MiddleLeft;
            if (end) return TextAnchor.UpperRight;
            return TextAnchor.UpperLeft;
        }

        private static void AttachControlIfNeeded(FigunityNode node, RectTransform rect, Graphic graphic, FigunityControlKind kind, FigunityFrameOptions options, FigunityBuildReport report)
        {
            if (IsTabGroupNode(node, kind))
            {
                EnsureComponent<ToggleGroup>(rect.gameObject);
                return;
            }

            switch (kind)
            {
                case FigunityControlKind.Button:
                    AttachButton(rect, graphic);
                    report.buttons++;
                    break;
                case FigunityControlKind.Tab:
                    AttachTabToggle(rect, graphic);
                    report.toggles++;
                    break;
                case FigunityControlKind.Toggle:
                    AttachToggle(node, rect, graphic, options, report);
                    report.toggles++;
                    break;
                case FigunityControlKind.Input:
                    AttachInputField(node, rect, graphic, options);
                    report.inputs++;
                    break;
                case FigunityControlKind.Dropdown:
                    AttachDropdown(rect, graphic);
                    report.dropdowns++;
                    break;
            }
        }

        private static bool IsTabGroupNode(FigunityNode node, FigunityControlKind kind)
        {
            if (node == null || kind != FigunityControlKind.Tab || node.children == null || node.children.Count <= 1)
            {
                return false;
            }

            var name = FigunityNameRules.Compact(node.name);
            if (name.Contains("segmented") || name.Contains("tabbar") || name.Contains("tabs"))
            {
                return true;
            }

            var tabChildren = 0;
            for (var i = 0; i < node.children.Count; i++)
            {
                var childName = FigunityNameRules.Compact(node.children[i]?.name);
                if (childName.Contains("tab"))
                {
                    tabChildren++;
                }
            }

            return tabChildren > 1;
        }

        private static bool ControlOwnsChildren(FigunityControlKind kind)
        {
            return kind == FigunityControlKind.Input || kind == FigunityControlKind.Toggle;
        }

        private static void AttachButton(RectTransform rect, Graphic graphic)
        {
            var button = rect.gameObject.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            button.targetGraphic = EnsureRaycastGraphic(rect, graphic);
            button.transition = Selectable.Transition.ColorTint;
        }

        private static void AttachTabToggle(RectTransform rect, Graphic graphic)
        {
            var toggle = rect.gameObject.GetComponent<Toggle>() ?? rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = EnsureRaycastGraphic(rect, graphic);
            toggle.transition = Selectable.Transition.ColorTint;
            var group = rect.transform.parent != null ? rect.transform.parent.GetComponent<ToggleGroup>() : null;
            if (group == null && rect.transform.parent != null)
            {
                group = rect.transform.parent.gameObject.AddComponent<ToggleGroup>();
            }

            toggle.group = group;
            toggle.SetIsOnWithoutNotify(rect.GetSiblingIndex() == 0);
        }

        private static void AttachToggle(FigunityNode node, RectTransform rect, Graphic graphic, FigunityFrameOptions options, FigunityBuildReport report)
        {
            var toggle = rect.gameObject.GetComponent<Toggle>() ?? rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = EnsureRaycastGraphic(rect, graphic);
            toggle.transition = Selectable.Transition.ColorTint;

            var trackNode = FindChildByCompactName(node, "track");
            var handleNode = FindChildByCompactName(node, "handle");
            var labelNode = FindChildByCompactName(node, "label");

            Graphic handleGraphic = null;
            RectTransform handleRect = null;
            if (trackNode != null)
            {
                var trackRect = MakeRect("Track", rect, trackNode.bounds, node.bounds);
                AttachMetadata(trackNode, trackRect);
                var trackGraphic = AttachVisual(trackNode, trackRect, options);
                EnsureRaycastGraphic(trackRect, trackGraphic);
                report.graphics++;
            }

            if (handleNode != null)
            {
                handleRect = MakeRect("Handle", rect, handleNode.bounds, node.bounds);
                AttachMetadata(handleNode, handleRect);
                handleGraphic = AttachHandleGraphic(handleNode, handleRect, options);
                if (handleGraphic != null)
                {
                    handleGraphic.raycastTarget = false;
                    report.graphics++;
                }
            }

            if (labelNode != null)
            {
                var labelRect = MakeRect("Label", rect, labelNode.bounds, node.bounds);
                AttachMetadata(labelNode, labelRect);
                AttachText(labelNode, labelRect, options);
                report.texts++;
            }

            if (handleGraphic == null)
            {
                var checkmark = MakeRect("Checkmark", rect, new FigunityBounds(4f, 4f, 16f, 16f), new FigunityBounds(0f, 0f, 0f, 0f));
                var image = checkmark.gameObject.AddComponent<Image>();
                image.color = new Color32(90, 190, 118, 255);
                image.raycastTarget = false;
                handleGraphic = image;
            }

            toggle.SetIsOnWithoutNotify(IsToggleOn(trackNode, handleNode));
            if (trackNode != null && handleNode != null && handleRect != null)
            {
                var switchMotion = rect.gameObject.GetComponent<FigunityToggleSwitch>() ?? rect.gameObject.AddComponent<FigunityToggleSwitch>();
                var trackX = trackNode.bounds.x - node.bounds.x;
                var handleY = handleRect.anchoredPosition.y;
                var offPosition = new Vector2(trackX, handleY);
                var onPosition = new Vector2(trackX + Mathf.Max(0f, trackNode.bounds.width - handleRect.sizeDelta.x), handleY);
                switchMotion.Configure(toggle, handleRect, offPosition, onPosition);
                toggle.graphic = null;
            }
            else
            {
                toggle.graphic = handleGraphic;
            }
        }

        private static bool IsToggleOn(FigunityNode trackNode, FigunityNode handleNode)
        {
            if (trackNode == null || handleNode == null || trackNode.bounds.width <= 0f)
            {
                return true;
            }

            var handleCenter = handleNode.bounds.x + handleNode.bounds.width * 0.5f;
            return handleCenter >= trackNode.bounds.x + trackNode.bounds.width * 0.5f;
        }

        private static void AttachInputField(FigunityNode node, RectTransform rect, Graphic graphic, FigunityFrameOptions options)
        {
            var input = rect.gameObject.GetComponent<TMP_InputField>() ?? rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = EnsureRaycastGraphic(rect, graphic);
            input.transition = Selectable.Transition.None;

            var paddingX = Mathf.Min(14f, Mathf.Max(6f, rect.sizeDelta.x * 0.05f));
            var paddingY = Mathf.Min(10f, Mathf.Max(4f, rect.sizeDelta.y * 0.18f));
            var area = MakeRect("Text Area", rect, new FigunityBounds(paddingX, paddingY, Mathf.Max(1f, rect.sizeDelta.x - paddingX * 2f), Mathf.Max(1f, rect.sizeDelta.y - paddingY * 2f)), new FigunityBounds(0f, 0f, 0f, 0f));
            area.gameObject.AddComponent<RectMask2D>();

            var placeholderNode = FindChildByCompactName(node, "placeholder");
            var placeholderRect = MakeRect("Placeholder", area, new FigunityBounds(0f, 0f, area.sizeDelta.x, area.sizeDelta.y), new FigunityBounds(0f, 0f, 0f, 0f));
            var placeholder = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            var placeholderColor = placeholderNode != null ? FigunityPaintRules.TextColor(placeholderNode) : new Color(1f, 1f, 1f, 0.5f);
            placeholderColor.a = Mathf.Min(placeholderColor.a, 0.55f);
            ConfigureTextLabel(placeholder, placeholderNode != null ? placeholderNode.text : null, placeholderColor, options);
            if (string.IsNullOrEmpty(placeholder.text))
            {
                placeholder.text = "Placeholder";
            }

            var textRect = MakeRect("Text", area, new FigunityBounds(0f, 0f, area.sizeDelta.x, area.sizeDelta.y), new FigunityBounds(0f, 0f, 0f, 0f));
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureTextLabel(text, placeholderNode != null ? placeholderNode.text : null, Color.white, options);
            text.text = string.Empty;

            input.textViewport = area;
            input.placeholder = placeholder;
            input.textComponent = text;
        }

        private static void AttachDropdown(RectTransform rect, Graphic graphic)
        {
            var dropdown = rect.gameObject.GetComponent<TMP_Dropdown>() ?? rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = EnsureRaycastGraphic(rect, graphic);
            dropdown.options.Clear();
            dropdown.options.Add(new TMP_Dropdown.OptionData("Option A"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Option B"));
        }

        private static Graphic EnsureRaycastGraphic(RectTransform rect, Graphic graphic)
        {
            var target = graphic;
            if (rect == null)
            {
                return target;
            }

            if (target == null)
            {
                target = rect.GetComponent<Graphic>();
            }

            if (target == null)
            {
                var image = rect.gameObject.AddComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(1f, 1f, 1f, 0f);
                    target = image;
                }
            }

            if (target != null)
            {
                target.raycastTarget = true;
            }

            return target;
        }

        private static void AttachLayoutElement(FigunityNode node, RectTransform rect)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = Mathf.Max(0f, node.bounds.width);
            element.preferredHeight = Mathf.Max(0f, node.bounds.height);
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        private static void AttachMetadata(FigunityNode frameNode, RectTransform rect)
        {
            if (frameNode == null || rect == null)
            {
                return;
            }

            var metadata = rect.gameObject.GetComponent<FigunityImportedNode>() ?? rect.gameObject.AddComponent<FigunityImportedNode>();
            metadata.FigmaId = frameNode.id;
            metadata.FigmaName = frameNode.name;
            metadata.FigmaType = frameNode.type;
            metadata.RenderMode = frameNode.renderMode;
            metadata.SourcePath = frameNode.path;
            metadata.ComponentKey = frameNode.componentKey;
            metadata.RepeatKey = frameNode.repeatKey;
            metadata.ControlHint = frameNode.controlHint;
            metadata.OverrideHint = frameNode.overrideHint;
            metadata.DecisionReason = frameNode.decisionReason;
            metadata.IsMask = frameNode.isMask;
            metadata.IsRepeated = !string.IsNullOrWhiteSpace(frameNode.repeatKey);
        }

        private static FigunityNode FindChildByCompactName(FigunityNode node, string token)
        {
            if (node?.children == null || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var compactToken = FigunityNameRules.Compact(token);
            for (var i = 0; i < node.children.Count; i++)
            {
                var child = node.children[i];
                if (child == null)
                {
                    continue;
                }

                if (FigunityNameRules.Compact(child.name).Contains(compactToken))
                {
                    return child;
                }
            }

            return null;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
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
        public FigunitySettingsAsset settings;
        public string rootName;
        public bool active;
        public bool useCrop;
        public FigunityBounds crop;
        public bool includeRootBackground;
        public bool addRootMask;

        public bool applyAutoLayoutGroups => settings == null || settings.applyAutoLayoutGroups;
        public bool createMasks => settings == null || settings.createMasks;
        public bool createRoundedRectGraphics => settings == null || settings.createRoundedRectGraphics;
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

    public sealed class FigunityBuildReport
    {
        public int texts;
        public int graphics;
        public int sliders;
        public int masks;
        public int layoutGroups;
        public int buttons;
        public int toggles;
        public int inputs;
        public int dropdowns;
        public int scrollViews;
    }
}
