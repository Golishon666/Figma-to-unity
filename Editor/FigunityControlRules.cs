using System;

namespace Figunity.Editor
{
    public enum FigunityControlKind
    {
        None,
        Button,
        Toggle,
        Input,
        Dropdown,
        Scroll,
        Tab,
        Slider,
        PassiveSlider
    }

    public static class FigunityControlRules
    {
        public static FigunityControlKind Resolve(FigunityNode node)
        {
            if (node != null && node.CarriesText)
            {
                return FigunityControlKind.None;
            }

            var hint = FigunityNameRules.Compact(node != null ? node.controlHint : string.Empty);
            var name = FigunityNameRules.Compact(node != null ? node.name : string.Empty);
            if (!string.IsNullOrEmpty(hint))
            {
                if (hint.Contains("passiveslider")) return FigunityControlKind.PassiveSlider;
                if (hint.Contains("slider")) return FigunityControlKind.Slider;
                if (hint.Contains("scroll")) return FigunityControlKind.Scroll;
                if (hint.Contains("toggle")) return FigunityControlKind.Toggle;
                if (hint.Contains("input")) return FigunityControlKind.Input;
                if (hint.Contains("dropdown")) return FigunityControlKind.Dropdown;
                if (hint.Contains("tab")) return FigunityControlKind.Tab;
                if (hint.Contains("button") && name.Contains("tab")) return FigunityControlKind.Tab;
                if (hint.Contains("button")) return FigunityControlKind.Button;
            }

            if (name.Contains("scrollview") || name.Contains("scrollrect") || name.StartsWith("scroll", StringComparison.Ordinal))
            {
                return FigunityControlKind.Scroll;
            }

            if (name.StartsWith("toggle", StringComparison.Ordinal) || name.Contains("checkbox") || name.Contains("switch"))
            {
                return FigunityControlKind.Toggle;
            }

            if (name.StartsWith("input", StringComparison.Ordinal) || name.Contains("textfield") || name.Contains("textinput"))
            {
                return FigunityControlKind.Input;
            }

            if (name.StartsWith("dropdown", StringComparison.Ordinal) || name.Contains("select"))
            {
                return FigunityControlKind.Dropdown;
            }

            if (name.StartsWith("tab", StringComparison.Ordinal) || name.Contains("segmented") || name.Contains("tab"))
            {
                return FigunityControlKind.Tab;
            }

            if (name.Contains("slider"))
            {
                return name.Contains("passive") || name.Contains("readonly") ? FigunityControlKind.PassiveSlider : FigunityControlKind.Slider;
            }

            if (name.Contains("progress") || name.Contains("capacitybar") || name.Contains("meter"))
            {
                return FigunityControlKind.PassiveSlider;
            }

            if (name.StartsWith("button", StringComparison.Ordinal) || name.Contains("button"))
            {
                return FigunityControlKind.Button;
            }

            return FigunityControlKind.None;
        }
    }
}
