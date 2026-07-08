using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityDiagnosticsWriter
    {
        public static void Write(FigunityDocument document, FigunitySettingsAsset settings, IReadOnlyList<FigunityBuildResult> buildResults)
        {
            if (document == null || settings == null || !settings.writeDiagnostics)
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("# FIGUNITY Diagnostics");
            report.AppendLine();
            report.AppendLine("- Source: " + document.source);
            if (!string.IsNullOrWhiteSpace(document.currentFileName))
            {
                report.AppendLine("- Figma file: " + document.currentFileName);
            }

            report.AppendLine("- Exported at: " + document.exportedAt);
            report.AppendLine("- Transport port: " + document.port);
            report.AppendLine();

            var totals = new Counters();
            for (var i = 0; i < document.frames.Count; i++)
            {
                var frame = document.frames[i];
                if (frame == null)
                {
                    continue;
                }

                var counters = Count(frame.tree);
                totals.Add(counters);
                report.AppendLine("## " + frame.frameName);
                report.AppendLine();
                report.AppendLine("- Key: `" + frame.key + "`");
                report.AppendLine("- Slug: `" + frame.slug + "`");
                report.AppendLine("- Size: " + frame.width + "x" + frame.height);
                report.AppendLine("- Nodes: " + frame.nodeCount);
                report.AppendLine("- Text nodes: " + frame.textCount);
                report.AppendLine("- Visual nodes: " + frame.visualCount);
                report.AppendLine("- Masks: " + counters.masks);
                report.AppendLine("- Auto-layout groups: " + counters.layoutGroups);
                report.AppendLine("- Buttons: " + counters.buttons);
                report.AppendLine("- Toggles: " + counters.toggles);
                report.AppendLine("- Inputs: " + counters.inputs);
                report.AppendLine("- Dropdowns: " + counters.dropdowns);
                report.AppendLine("- Scroll views: " + counters.scrollViews);
                report.AppendLine("- Repeated groups: " + counters.repeatedGroups);
                report.AppendLine("- Manual overrides: " + counters.manualOverrides);
                report.AppendLine("- Responsive constraints: " + counters.responsiveConstraints);
                if (!string.IsNullOrWhiteSpace(frame.screenshotPath))
                {
                    report.AppendLine("- Figma screenshot: `" + frame.screenshotPath + "`");
                }

                if (buildResults != null && i < buildResults.Count)
                {
                    var build = buildResults[i].report;
                    report.AppendLine("- Built TMP texts: " + build.texts);
                    report.AppendLine("- Built graphics: " + build.graphics);
                    report.AppendLine("- Built sliders: " + build.sliders);
                    report.AppendLine("- Built layout groups: " + build.layoutGroups);
                }

                var highlights = new List<string>();
                CollectRecognitionHighlights(frame.tree, highlights, 16);
                if (highlights.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("### Recognition Highlights");
                    for (var h = 0; h < highlights.Count; h++)
                    {
                        report.AppendLine("- " + highlights[h]);
                    }
                }

                report.AppendLine();
            }

            report.AppendLine("## Totals");
            report.AppendLine();
            report.AppendLine("- Masks: " + totals.masks);
            report.AppendLine("- Auto-layout groups: " + totals.layoutGroups);
            report.AppendLine("- Buttons: " + totals.buttons);
            report.AppendLine("- Toggles: " + totals.toggles);
            report.AppendLine("- Inputs: " + totals.inputs);
            report.AppendLine("- Dropdowns: " + totals.dropdowns);
            report.AppendLine("- Scroll views: " + totals.scrollViews);
            report.AppendLine("- Repeated groups: " + totals.repeatedGroups);
            report.AppendLine("- Manual overrides: " + totals.manualOverrides);
            report.AppendLine("- Responsive constraints: " + totals.responsiveConstraints);
            report.AppendLine();
            report.AppendLine("## Visual Diff");
            report.AppendLine();
            report.AppendLine("Unity screenshot comparison is available through `FigunityVisualDiff.Compare` when a Unity render PNG is supplied.");

            var fullPath = FigunitySettings.ProjectAbsolutePath(settings.diagnosticsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, report.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static Counters Count(FigunityNode root)
        {
            var counters = new Counters();
            CountRecursive(root, counters, new Dictionary<string, int>());
            return counters;
        }

        private static void CountRecursive(FigunityNode node, Counters counters, Dictionary<string, int> repeats)
        {
            if (node == null)
            {
                return;
            }

            if (node.isMask || node.clipsContent) counters.masks++;
            if (node.autoLayout != null && node.autoLayout.Enabled) counters.layoutGroups++;
            if (!string.IsNullOrWhiteSpace(node.overrideHint)) counters.manualOverrides++;
            if (HasResponsiveConstraint(node)) counters.responsiveConstraints++;
            switch (FigunityControlRules.Resolve(node))
            {
                case FigunityControlKind.Button:
                case FigunityControlKind.Tab:
                    counters.buttons++;
                    break;
                case FigunityControlKind.Toggle:
                    counters.toggles++;
                    break;
                case FigunityControlKind.Input:
                    counters.inputs++;
                    break;
                case FigunityControlKind.Dropdown:
                    counters.dropdowns++;
                    break;
                case FigunityControlKind.Scroll:
                    counters.scrollViews++;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(node.repeatKey))
            {
                repeats.TryGetValue(node.repeatKey, out var count);
                repeats[node.repeatKey] = count + 1;
            }

            if (node.children != null)
            {
                for (var i = 0; i < node.children.Count; i++)
                {
                    CountRecursive(node.children[i], counters, repeats);
                }
            }

            counters.repeatedGroups = 0;
            foreach (var item in repeats)
            {
                if (item.Value > 1)
                {
                    counters.repeatedGroups++;
                }
            }
        }

        private static void CollectRecognitionHighlights(FigunityNode node, List<string> rows, int limit)
        {
            if (node == null || rows == null || rows.Count >= limit)
            {
                return;
            }

            var hasOverride = !string.IsNullOrWhiteSpace(node.overrideHint);
            var hasControl = !string.IsNullOrWhiteSpace(node.controlHint);
            var hasConstraint = HasResponsiveConstraint(node);
            var isSpecialRender =
                string.Equals(node.renderMode, "composite", System.StringComparison.OrdinalIgnoreCase);
            if (hasOverride || hasControl || hasConstraint || isSpecialRender || node.isMask)
            {
                var pieces = new List<string>();
                if (hasOverride) pieces.Add("override=" + node.overrideHint);
                if (hasControl) pieces.Add("control=" + node.controlHint);
                if (hasConstraint) pieces.Add("constraints=" + node.constraints.horizontal + "/" + node.constraints.vertical);
                if (!string.IsNullOrWhiteSpace(node.renderMode)) pieces.Add("render=" + node.renderMode);
                if (!string.IsNullOrWhiteSpace(node.decisionReason)) pieces.Add("reason=" + node.decisionReason);
                if (node.isMask) pieces.Add("mask=true");
                if (node.clipsContent) pieces.Add("clips=true");

                var label = string.IsNullOrWhiteSpace(node.path) ? node.name : node.path;
                rows.Add("`" + label + "` - " + string.Join(", ", pieces));
            }

            if (node.children == null)
            {
                return;
            }

            for (var i = 0; i < node.children.Count && rows.Count < limit; i++)
            {
                CollectRecognitionHighlights(node.children[i], rows, limit);
            }
        }

        private static bool HasResponsiveConstraint(FigunityNode node)
        {
            if (node == null || node.constraints == null)
            {
                return false;
            }

            return IsResponsiveConstraint(node.constraints.horizontal) ||
                   IsResponsiveConstraint(node.constraints.vertical);
        }

        private static bool IsResponsiveConstraint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            return normalized != "MIN" && normalized != "LEFT" && normalized != "TOP";
        }

        private sealed class Counters
        {
            public int masks;
            public int layoutGroups;
            public int buttons;
            public int toggles;
            public int inputs;
            public int dropdowns;
            public int scrollViews;
            public int repeatedGroups;
            public int manualOverrides;
            public int responsiveConstraints;

            public void Add(Counters other)
            {
                masks += other.masks;
                layoutGroups += other.layoutGroups;
                buttons += other.buttons;
                toggles += other.toggles;
                inputs += other.inputs;
                dropdowns += other.dropdowns;
                scrollViews += other.scrollViews;
                repeatedGroups += other.repeatedGroups;
                manualOverrides += other.manualOverrides;
                responsiveConstraints += other.responsiveConstraints;
            }
        }
    }
}
