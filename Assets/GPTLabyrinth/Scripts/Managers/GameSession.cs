using GPTLabyrinth.Save;
using UnityEngine;

namespace GPTLabyrinth.Managers
{
    /// <summary>
    /// Holds the single in-memory SaveRoot for the current play session and brokers
    /// persistence through SaveManager. Plain static service so code-driven scenes do
    /// not need a wired singleton GameObject.
    /// </summary>
    public static class GameSession
    {
        public static SaveRoot Current { get; private set; }

        public static bool HasSession => Current != null;

        /// <summary>Ensures a SaveRoot exists in memory (loads from disk if present).</summary>
        public static SaveRoot EnsureLoaded()
        {
            if (Current == null)
                Current = SaveManager.Load();
            return Current;
        }

        /// <summary>Starts a brand-new save (New Game). Overwrites the in-memory session.</summary>
        public static SaveRoot NewGame()
        {
            Current = SaveRoot.CreateDefault();
            return Current;
        }

        public static void SetCurrent(SaveRoot root)
        {
            Current = root;
        }

        /// <summary>Persists the current session to disk via the safe-write pipeline.</summary>
        public static bool Persist()
        {
            if (Current == null)
            {
                Debug.LogWarning("[GameSession] Persist called with no active session.");
                return false;
            }
            return SaveManager.Save(Current);
        }

        public static void Clear()
        {
            Current = null;
        }
    }
}
