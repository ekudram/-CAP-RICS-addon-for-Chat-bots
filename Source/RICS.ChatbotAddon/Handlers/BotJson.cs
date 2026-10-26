// BotJson.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    /// <summary>
    /// Strongly-typed JSON only (no dynamic) — matches RICS Mono safety rules.
    /// Color tags are dropped. The words inside them stay.
    /// </summary>
    internal static class BotJson
    {
        private static readonly Regex ColorTag = new Regex("</?color[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static string Serialize(object value)
        {
            return WithoutColor(JsonConvert.SerializeObject(value, Settings));
        }

        public static string WithoutColor(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("color", StringComparison.OrdinalIgnoreCase) < 0)
                return text;
            return ColorTag.Replace(text, string.Empty);
        }
    }
}
