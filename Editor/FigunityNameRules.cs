using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityNameRules
    {
        private static readonly Regex AnnotationPattern = new Regex(@"\[(background|visual|container|text)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NonWordPattern = new Regex(@"[^A-Za-z0-9]+", RegexOptions.Compiled);

        public static string ToObjectName(string figmaName)
        {
            var value = string.IsNullOrWhiteSpace(figmaName) ? "Node" : figmaName.Trim();
            value = AnnotationPattern.Replace(value, string.Empty);
            value = value.Replace(" / ", " ");
            value = value.Replace("/", " ");
            value = value.Replace("\\", " ");

            string category = null;
            var divider = value.IndexOf(" - ", StringComparison.Ordinal);
            if (divider > 0)
            {
                category = Pascalize(value.Substring(0, divider));
                value = value.Substring(divider + 3);
            }

            var cleaned = Pascalize(value);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = "Node";
            }

            return string.IsNullOrWhiteSpace(category) ? cleaned : category + "_" + cleaned;
        }

        public static string Compact(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return NonWordPattern.Replace(value, string.Empty).ToLowerInvariant();
        }

        public static string Compact(Transform transform)
        {
            return transform == null ? string.Empty : Compact(transform.name);
        }

        private static string Pascalize(string value)
        {
            value = NonWordPattern.Replace(value ?? string.Empty, " ").Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            var pieces = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder();
            var textInfo = CultureInfo.InvariantCulture.TextInfo;

            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece.Length == 0)
                {
                    continue;
                }

                var upper = piece.ToUpperInvariant();
                if (upper == "HP" || upper == "XP" || upper == "UI")
                {
                    builder.Append(upper[0]);
                    for (var c = 1; c < upper.Length; c++)
                    {
                        builder.Append(char.ToLowerInvariant(upper[c]));
                    }

                    continue;
                }

                builder.Append(textInfo.ToTitleCase(piece.ToLowerInvariant()));
            }

            return builder.ToString();
        }
    }
}
