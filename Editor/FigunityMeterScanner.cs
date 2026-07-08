using System;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityMeterScanner
    {
        public static bool TryResolve(FigunityNode node, out FigunityMeterShape meter)
        {
            meter = default;
            if (node == null || node.children == null || node.children.Count == 0)
            {
                return false;
            }

            var compactName = FigunityNameRules.Compact(node.name);
            var likelyMeter = compactName.Contains("slider") ||
                              compactName.Contains("progress") ||
                              compactName.Contains("capacitybar") ||
                              compactName.Contains("durability") ||
                              compactName.Contains("meter");

            FigunityNode track = null;
            FigunityNode fill = null;
            FigunityNode handle = null;

            for (var i = 0; i < node.children.Count; i++)
            {
                var child = node.children[i];
                if (child == null)
                {
                    continue;
                }

                var name = FigunityNameRules.Compact(child.name);
                if (track == null && name.Contains("track"))
                {
                    track = child;
                }
                else if (fill == null && name.Contains("fill"))
                {
                    fill = child;
                }
                else if (handle == null && name.Contains("handle"))
                {
                    handle = child;
                }
            }

            if (!likelyMeter && track == null && fill == null)
            {
                return false;
            }

            if (track == null || fill == null || track.bounds.width <= 0f)
            {
                return false;
            }

            meter = new FigunityMeterShape
            {
                container = node,
                track = track,
                fill = fill,
                handle = handle,
                normalizedValue = Mathf.Clamp01(fill.bounds.width / track.bounds.width),
                interactable = FigunityControlRules.Resolve(node) == FigunityControlKind.Slider
            };

            return true;
        }
    }

    public struct FigunityMeterShape
    {
        public FigunityNode container;
        public FigunityNode track;
        public FigunityNode fill;
        public FigunityNode handle;
        public float normalizedValue;
        public bool interactable;
    }
}
