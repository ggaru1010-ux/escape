using System;
using UnityEngine;

namespace GPTLabyrinth.Runtime
{
    public static class RoomPattern001PuzzleContract
    {
        public const string PatternId = "ROOM_PATTERN_001";
        public const string P01PuzzleFlag = "PZL_P01_SOLVED";
        public const string P01RequiredFlagCode = "p01_real_handle_identified";
        public const string P01SolvedTexturePath = "Assets/GPTLabyrinth/object_png/p01final.png";
        public const string P02PuzzleFlag = "PZL_P02_SOLVED";
        public const string P02RequiredFlagCode = P01PuzzleFlag;
        public const string P02LeftEyeInsertedFlag = "PZL_P02_LEFT_EYE_INSERTED";
        public const string P02RightEyeInsertedFlag = "PZL_P02_RIGHT_EYE_INSERTED";
        public const string P02SolvedTexturePath = "Assets/GPTLabyrinth/object_png/p02eye.png";
        public const string P03PuzzleFlag = "PZL_P03_SOLVED";
        public const string P03RequiredFlagCode = P02PuzzleFlag;
        public const string P03SolvedTexturePath = "Assets/GPTLabyrinth/object_png/p03final.png";
        // Rotation stored as 4 bool flags encoding step 0-3 (step3 = target / solved)
        public const string P03RotStep0 = "PZL_P03_ROT_STEP_0";
        public const string P03RotStep1 = "PZL_P03_ROT_STEP_1";
        public const string P03RotStep2 = "PZL_P03_ROT_STEP_2";
        public const string P03RotStep3 = "PZL_P03_ROT_STEP_3";
        public const int P03TargetStep = 3;  // 270 degrees
        public const string P04PuzzleFlag = "PZL_P04_SOLVED";
        public const string P04RequiredFlagCode = P03PuzzleFlag;
        public const string P04SolvedTexturePath = "Assets/GPTLabyrinth/object_png/p04final.png";
        // Door stage flags: stage advances each click
        public const string P04StageInspected = "PZL_P04_STAGE_INSPECTED";
        public const string P04StageRevealed  = "PZL_P04_STAGE_REVEALED";
        public const string P05PuzzleFlag = "PZL_P05_SOLVED";
        public const string P05RequiredFlagCode = P04PuzzleFlag;
        public const string P05SolvedTexturePath = "Assets/GPTLabyrinth/object_png/p05_middle.png";
        // The correct choice is the Right glyph (FADE)
        public const string P05ChoiceLeft   = "PZL_P05_CHOICE_LEFT";
        public const string P05ChoiceCenter = "PZL_P05_CHOICE_CENTER";
        public const string P05ChoiceRight  = "PZL_P05_CHOICE_RIGHT";
        public const string P05CorrectChoice = P05ChoiceRight;
    }

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
