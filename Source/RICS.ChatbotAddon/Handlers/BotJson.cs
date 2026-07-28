// BotJson.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using Newtonsoft.Json;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    /// <summary>
    /// Strongly-typed JSON only (no dynamic) — matches RICS Mono safety rules.
    /// </summary>
    internal static class BotJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }
    }
}
