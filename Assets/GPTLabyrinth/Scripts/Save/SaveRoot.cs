using System;
using GPTLabyrinth.Data;

namespace GPTLabyrinth.Save
{
    /// <summary>
    /// Root of the entire save file. Composed only of bounded, serializable layers.
    /// This is the ONLY object SaveManager writes to disk.
    /// </summary>
    [Serializable]
    public class SaveRoot
    {
        public int save_version = GameCodes.SAVE_VERSION;
        public ProfileSave profile = new ProfileSave();
        public SettingsSave settings = new SettingsSave();
        public CurrentRunSave current_run = new CurrentRunSave();
        public WorldState world_state = new WorldState();
        public AICompanionState ai_companion = new AICompanionState();
        public GlobalMemory global_memory = new GlobalMemory();

        /// <summary>
        /// Build a fresh, safe default save (used for New Game and as a corruption fallback).
        /// </summary>
        public static SaveRoot CreateDefault()
        {
            var root = new SaveRoot();
            root.save_version = GameCodes.SAVE_VERSION;
            root.profile = new ProfileSave
            {
                profile_id = Guid.NewGuid().ToString("N"),
                created_version = "0.1.0-mvp",
                total_runs = 0,
                last_played_unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            root.settings = new SettingsSave();
            root.current_run = new CurrentRunSave();
            root.world_state = new WorldState();
            root.ai_companion = new AICompanionState();
            root.global_memory = new GlobalMemory();
            return root;
        }
    }
}
