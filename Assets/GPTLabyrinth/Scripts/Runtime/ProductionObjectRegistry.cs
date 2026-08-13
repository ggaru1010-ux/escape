using System.Collections.Generic;
using GPTLabyrinth.Data;
using UnityEngine;

namespace GPTLabyrinth.Runtime
{
    [CreateAssetMenu(menuName = "GPTLabyrinth/Production Object Registry", fileName = "ProductionObjectRegistry")]
    public sealed class ProductionObjectRegistry : ScriptableObject
    {
        [SerializeField] private List<ProductionObjectEntry> entries = new List<ProductionObjectEntry>();

        public IReadOnlyList<ProductionObjectEntry> Entries => entries;
        public int EntryCount => entries != null ? entries.Count : 0;

        public ProductionObjectRegistryValidationResult ValidateAll()
        {
            return ProductionObjectRegistryValidator.ValidateAll(entries);
        }

        public bool TryGetByObjectId(string objectId, out ProductionObjectEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(objectId) || entries == null)
                return false;

            string id = objectId.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                ProductionObjectEntry candidate = entries[i];
                if (candidate != null && candidate.ObjectId != null && candidate.ObjectId.Trim() == id)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public List<ProductionObjectEntry> GetByRoom(string roomCode)
        {
            var results = new List<ProductionObjectEntry>();
            if (string.IsNullOrWhiteSpace(roomCode) || entries == null)
                return results;

            string room = roomCode.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                ProductionObjectEntry entry = entries[i];
                if (entry != null && entry.RoomCode != null && entry.RoomCode.Trim() == room)
                    results.Add(entry);
            }

            return results;
        }

        public List<ProductionObjectEntry> GetByRoomAndView(string roomCode, RoomView view)
        {
            return GetByRoomAndView(roomCode, ViewToCode(view));
        }

        public List<ProductionObjectEntry> GetByRoomAndView(string roomCode, string viewCode)
        {
            var results = new List<ProductionObjectEntry>();
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(viewCode) || entries == null)
                return results;

            string room = roomCode.Trim();
            string view = ProductionObjectRegistryValidator.CanonicalizeViewCode(viewCode);
            for (int i = 0; i < entries.Count; i++)
            {
                ProductionObjectEntry entry = entries[i];
                if (entry == null)
                    continue;

                string entryRoom = entry.RoomCode != null ? entry.RoomCode.Trim() : string.Empty;
                string entryView = ProductionObjectRegistryValidator.CanonicalizeViewCode(entry.ViewCode);
                if (entryRoom == room && (entryView == view || entryView == "ALL"))
                    results.Add(entry);
            }

            return results;
        }

        public void SetEntriesForValidationOnly(List<ProductionObjectEntry> validationEntries)
        {
            entries = validationEntries ?? new List<ProductionObjectEntry>();
        }

        public static string ViewToCode(RoomView view)
        {
            switch (view)
            {
                case RoomView.Front:
                    return "FRONT";
                case RoomView.Back:
                    return "BACK";
                case RoomView.Left:
                    return "LEFT";
                case RoomView.Right:
                    return "RIGHT";
                case RoomView.Ceiling:
                    return "CEILING";
                case RoomView.Floor:
                    return "FLOOR";
                default:
                    return string.Empty;
            }
        }
    }
}
