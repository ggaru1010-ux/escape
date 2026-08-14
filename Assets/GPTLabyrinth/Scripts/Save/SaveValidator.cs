using System;
using System.Collections.Generic;
using GPTLabyrinth.Data;

namespace GPTLabyrinth.Save
{
    /// <summary>
    /// Result of a validation/sanitization pass.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid = true;
        public bool WasModified = false;
        public List<string> Messages = new List<string>();

        public void Note(string msg)
        {
            Messages.Add(msg);
            WasModified = true;
        }

        public void Fail(string msg)
        {
            Messages.Add("REJECT: " + msg);
            IsValid = false;
        }
    }

    /// <summary>
    /// The defense layer for the save system. Enforces STATE_ONLY and the bounded
    /// schema. Two responsibilities:
    ///   1) Sanitize(SaveRoot)  - clamp/repair an in-memory object before save or after load.
    ///   2) ScanRawJson(string) - detect forbidden keys / free-text leakage in raw JSON.
    /// SaveValidator never makes game-logic decisions and never touches AI output.
    /// </summary>
    public static class SaveValidator
    {
        /// <summary>
        /// Substrings that must never appear as keys in a save file. Detecting any of
        /// these means something violated the STATE_ONLY / D-BLOCK principle.
        /// </summary>
        private static readonly string[] ForbiddenKeyFragments = new[]
        {
            "dialogue", "prompt", "raw_text", "rawtext", "chat", "message_text",
            "ai_response", "airesponse", "response_text", "transcript",
            "password", "passwd", "email", "phone", "address", "account",
            "api_key", "apikey", "token", "secret", "voice_raw", "voiceraw",
            "real_name", "realname", "credit", "ssn"
        };

        // ------------------------------------------------------------------
        // In-memory sanitization
        // ------------------------------------------------------------------

        public static ValidationResult Sanitize(SaveRoot root)
        {
            var result = new ValidationResult();

            if (root == null)
            {
                result.Fail("SaveRoot is null.");
                return result;
            }

            // save_version
            if (root.save_version != GameCodes.SAVE_VERSION)
            {
                result.Note($"save_version {root.save_version} != {GameCodes.SAVE_VERSION}; normalizing.");
                root.save_version = GameCodes.SAVE_VERSION;
            }

            // Required layers exist (null correction)
            if (root.profile == null) { root.profile = new ProfileSave(); result.Note("profile was null; recreated."); }
            if (root.settings == null) { root.settings = new SettingsSave(); result.Note("settings was null; recreated."); }
            if (root.current_run == null) { root.current_run = new CurrentRunSave(); result.Note("current_run was null; recreated."); }
            if (root.world_state == null) { root.world_state = new WorldState(); result.Note("world_state was null; recreated."); }
            if (root.ai_companion == null) { root.ai_companion = new AICompanionState(); result.Note("ai_companion was null; recreated."); }
            if (root.global_memory == null) { root.global_memory = new GlobalMemory(); result.Note("global_memory was null; recreated."); }

            SanitizeProfile(root.profile, result);
            SanitizeSettings(root.settings, result);
            SanitizeCurrentRun(root.current_run, result);
            SanitizeWorldState(root.world_state, result);
            SanitizeAICompanion(root.ai_companion, result);
            SanitizeGlobalMemory(root.global_memory, result);

            return result;
        }

        private static void SanitizeProfile(ProfileSave p, ValidationResult r)
        {
            p.profile_id = ClampCode(p.profile_id, r, "profile.profile_id");
            p.created_version = ClampCode(p.created_version, r, "profile.created_version");
            if (p.total_runs < 0) { p.total_runs = 0; r.Note("profile.total_runs < 0; clamped."); }
        }

        private static void SanitizeSettings(SettingsSave s, ValidationResult r)
        {
            s.master_volume = ClampFloat(s.master_volume, GameCodes.VOLUME_MIN, GameCodes.VOLUME_MAX, r, "settings.master_volume");
            s.bgm_volume = ClampFloat(s.bgm_volume, GameCodes.VOLUME_MIN, GameCodes.VOLUME_MAX, r, "settings.bgm_volume");
            s.sfx_volume = ClampFloat(s.sfx_volume, GameCodes.VOLUME_MIN, GameCodes.VOLUME_MAX, r, "settings.sfx_volume");
            s.language_code = ClampCode(s.language_code, r, "settings.language_code");
            if (s.text_speed < 0 || s.text_speed > 2) { s.text_speed = 1; r.Note("settings.text_speed out of range; reset to 1."); }
        }

        private static void SanitizeCurrentRun(CurrentRunSave c, ValidationResult r)
        {
            c.run_id = ClampCode(c.run_id, r, "current_run.run_id");
            c.current_room_code = ClampCode(c.current_room_code, r, "current_run.current_room_code");
            c.current_view_code = SanitizeRoomViewCode(c.current_view_code, r, "current_run.current_view_code");
            c.current_zoom = ClampFloat(c.current_zoom, 1f, 3f, r, "current_run.current_zoom");
            if (c.current_ring < 0) { c.current_ring = 0; r.Note("current_run.current_ring < 0; clamped."); }
            if (c.run_death_count < 0) { c.run_death_count = 0; r.Note("current_run.run_death_count < 0; clamped."); }
        }

        private static void SanitizeWorldState(WorldState w, ValidationResult r)
        {
            if (w.room_flags == null) { w.room_flags = new List<FlagEntry>(); r.Note("world_state.room_flags was null; recreated."); }
            for (int i = w.room_flags.Count - 1; i >= 0; i--)
            {
                var f = w.room_flags[i];
                if (f == null || string.IsNullOrEmpty(f.code))
                {
                    w.room_flags.RemoveAt(i);
                    r.Note("world_state.room_flags had a null/empty entry; removed.");
                    continue;
                }
                f.code = ClampCode(f.code, r, "world_state.flag.code");
            }
        }

        private static void SanitizeAICompanion(AICompanionState a, ValidationResult r)
        {
            a.ai_trust_level = ClampInt(a.ai_trust_level, GameCodes.STAT_MIN, GameCodes.STAT_MAX, r, "ai_companion.ai_trust_level");
            a.fear_level = ClampInt(a.fear_level, GameCodes.STAT_MIN, GameCodes.STAT_MAX, r, "ai_companion.fear_level");
            a.stress_level = ClampInt(a.stress_level, GameCodes.STAT_MIN, GameCodes.STAT_MAX, r, "ai_companion.stress_level");
            a.ai_state_code = ClampCode(a.ai_state_code, r, "ai_companion.ai_state_code");
            if (string.IsNullOrEmpty(a.ai_state_code)) a.ai_state_code = GameCodes.AI_STATE_NEUTRAL;
        }

        private static void SanitizeGlobalMemory(GlobalMemory g, ValidationResult r)
        {
            if (g.death_events == null) { g.death_events = new List<DeathEvent>(); r.Note("global_memory.death_events was null; recreated."); }
            if (g.ai_memory_events == null) { g.ai_memory_events = new List<AIMemoryEvent>(); r.Note("global_memory.ai_memory_events was null; recreated."); }

            // Remove null entries.
            g.death_events.RemoveAll(e => e == null);
            g.ai_memory_events.RemoveAll(e => e == null);

            // Enforce caps (keep most recent).
            TrimToCap(g.death_events, GameCodes.MAX_DEATH_EVENTS, r, "death_events");
            TrimToCap(g.ai_memory_events, GameCodes.MAX_AI_MEMORY_EVENTS, r, "ai_memory_events");

            foreach (var d in g.death_events)
            {
                d.death_event_id = ClampCode(d.death_event_id, r, "death_event.death_event_id");
                d.run_id = ClampCode(d.run_id, r, "death_event.run_id");
                d.death_room_code = ClampCode(d.death_room_code, r, "death_event.death_room_code");
                d.death_pattern_code = ClampCode(d.death_pattern_code, r, "death_event.death_pattern_code");
                d.memory_summary_code = ClampCode(d.memory_summary_code, r, "death_event.memory_summary_code");
                d.death_cause = ValidateEnum(d.death_cause, DeathCause.UNKNOWN, r, "death_event.death_cause");
                d.trap_type = ValidateEnum(d.trap_type, TrapType.UNKNOWN, r, "death_event.trap_type");
            }

            foreach (var m in g.ai_memory_events)
            {
                m.memory_id = ClampCode(m.memory_id, r, "ai_memory.memory_id");
                m.source_death_event_id = ClampCode(m.source_death_event_id, r, "ai_memory.source_death_event_id");
                m.memory_summary_code = ClampCode(m.memory_summary_code, r, "ai_memory.memory_summary_code");
                m.related_room_code = ClampCode(m.related_room_code, r, "ai_memory.related_room_code");
                m.related_pattern_code = ClampCode(m.related_pattern_code, r, "ai_memory.related_pattern_code");
                m.recall_intensity = ValidateEnum(m.recall_intensity, RecallIntensity.FAINT, r, "ai_memory.recall_intensity");
            }
        }

        // ------------------------------------------------------------------
        // Raw JSON scanning (forbidden-key / free-text leak detection)
        // ------------------------------------------------------------------

        /// <summary>
        /// Scans raw JSON for forbidden key fragments. Returns true if SAFE (no forbidden
        /// keys found). Populates <paramref name="violations"/> with any detected fragments.
        /// </summary>
        public static bool ScanRawJson(string json, out List<string> violations)
        {
            violations = new List<string>();
            if (string.IsNullOrEmpty(json)) return true;

            string lower = json.ToLowerInvariant();
            foreach (var frag in ForbiddenKeyFragments)
            {
                // Look for it as a JSON key: "fragment...":
                if (ContainsKeyLike(lower, frag))
                    violations.Add(frag);
            }
            return violations.Count == 0;
        }

        private static bool ContainsKeyLike(string lowerJson, string fragment)
        {
            int idx = 0;
            while ((idx = lowerJson.IndexOf(fragment, idx, StringComparison.Ordinal)) >= 0)
            {
                // Heuristic: treat as a key hit if the fragment appears immediately
                // after a quote (i.e. "fragment...). This avoids false positives in
                // arbitrary value text while still catching key leakage.
                if (idx > 0 && lowerJson[idx - 1] == '"')
                    return true;
                idx += fragment.Length;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string ClampCode(string value, ValidationResult r, string field)
        {
            if (value == null) return "";
            if (value.Length > GameCodes.MAX_CODE_LENGTH)
            {
                r.Note($"{field} exceeded {GameCodes.MAX_CODE_LENGTH} chars; truncated.");
                return value.Substring(0, GameCodes.MAX_CODE_LENGTH);
            }
            return value;
        }

        private static int ClampInt(int value, int min, int max, ValidationResult r, string field)
        {
            if (value < min) { r.Note($"{field} {value} < {min}; clamped."); return min; }
            if (value > max) { r.Note($"{field} {value} > {max}; clamped."); return max; }
            return value;
        }

        private static float ClampFloat(float value, float min, float max, ValidationResult r, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) { r.Note($"{field} was NaN/Inf; reset to {min}."); return min; }
            if (value < min) { r.Note($"{field} {value} < {min}; clamped."); return min; }
            if (value > max) { r.Note($"{field} {value} > {max}; clamped."); return max; }
            return value;
        }

        private static string SanitizeRoomViewCode(string value, ValidationResult r, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                r.Note($"{field} was empty; reset to FRONT.");
                return "FRONT";
            }

            string normalized = value.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "FRONT":
                case "BACK":
                case "LEFT":
                case "RIGHT":
                case "CEILING":
                case "FLOOR":
                    return normalized;
                default:
                    r.Note($"{field} '{value}' was invalid; reset to FRONT.");
                    return "FRONT";
            }
        }

        private static T ValidateEnum<T>(T value, T fallback, ValidationResult r, string field) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                r.Note($"{field} had undefined enum value; reset to {fallback}.");
                return fallback;
            }
            return value;
        }

        private static void TrimToCap<T>(List<T> list, int cap, ValidationResult r, string field)
        {
            if (list.Count > cap)
            {
                int remove = list.Count - cap;
                list.RemoveRange(0, remove); // drop oldest
                r.Note($"{field} exceeded cap {cap}; removed {remove} oldest entries.");
            }
        }
    }
}
