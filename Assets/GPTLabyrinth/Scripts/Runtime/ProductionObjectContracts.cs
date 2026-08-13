using System;
using UnityEngine;

namespace GPTLabyrinth.Runtime
{
    public enum ProductionObjectType
    {
        Decorative,
        Interactable,
        PuzzleObject,
        Hotspot,
        Overlay,
        Mock,
        Proof,
        ValidationOnly,
    }

    public enum ProductionObjectApprovalStatus
    {
        Unapproved,
        ValidationOnly,
        ProductionCandidate,
        ProductionApproved,
        Deprecated,
    }

    public enum ProductionObjectSpawnMode
    {
        RoomScoped,
        RoomCached,
        ViewSpawn,
        Persistent,
        SceneSerialized,
    }

    public enum ProductionObjectVisibilityRule
    {
        AlwaysInRoom,
        RoomAndView,
        PuzzleCondition,
        AcquiredCondition,
        SolvedCondition,
        CustomApproved,
    }

    public enum ProductionObjectInteractionType
    {
        None,
        Pointer,
        Inspect,
        Use,
        PuzzleAction,
        Navigation,
    }

    public enum ProductionObjectSaveStatePolicy
    {
        Ephemeral,
        Derived,
        Persistent,
    }

    [Serializable]
    public sealed class ProductionObjectEntry
    {
        public int SchemaVersion = ProductionObjectRegistryValidator.CurrentSchemaVersion;
        public string RoomCode = "P00";
        public string ViewCode = "FRONT";
        public string ObjectId = string.Empty;
        public ProductionObjectType ObjectType = ProductionObjectType.Decorative;
        public GameObject PrefabReference;
        public ProductionObjectApprovalStatus ApprovalStatus = ProductionObjectApprovalStatus.Unapproved;
        public ProductionObjectSpawnMode SpawnMode = ProductionObjectSpawnMode.RoomScoped;
        public Vector3 LocalPosition = Vector3.zero;
        public Vector3 LocalRotationEuler = Vector3.zero;
        public Vector3 LocalScale = Vector3.one;
        public string SortingLayer = "Default";
        public int SortingOrder = 0;
        public ProductionObjectVisibilityRule VisibilityRule = ProductionObjectVisibilityRule.RoomAndView;
        public ProductionObjectInteractionType InteractionType = ProductionObjectInteractionType.None;
        public string PuzzleBindingId = string.Empty;
        public ProductionObjectSaveStatePolicy SaveStatePolicy = ProductionObjectSaveStatePolicy.Ephemeral;
        public string ValidationSource = string.Empty;
    }
}
