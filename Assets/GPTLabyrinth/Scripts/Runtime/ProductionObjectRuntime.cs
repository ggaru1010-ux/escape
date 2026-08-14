using GPTLabyrinth.Data;
using GPTLabyrinth.UI;
using UnityEngine;

namespace GPTLabyrinth.Runtime
{
    public sealed class ProductionObjectRuntime : MonoBehaviour
    {
        private const string LogPrefix = "[ProductionObjectRuntime]";
        private const string GameplayRootName = "P00 Gameplay Root";

        private static ProductionObjectRuntime _instance;

        [SerializeField] private ProductionObjectRuntimeState state = ProductionObjectRuntimeState.Uninitialized;
        [SerializeField] private ProductionObjectRuntimeContainer container;
        [SerializeField] private ProductionObjectRegistry registry;
        [SerializeField] private string currentRoom = string.Empty;
        [SerializeField] private RoomView currentView = RoomView.Front;
        [SerializeField] private bool objectLayerActive = true;

        private RoomNavigationUIController navigationSource;
        private bool subscribed;
        private ProductionObjectRuntimeFactory factory;
        private ProductionObjectInstanceTracker instanceTracker;
        private ProductionObjectVisibilityRuntime visibilityRuntime;

        public static ProductionObjectRuntime Instance => _instance;
        public ProductionObjectRuntimeState State => state;
        public ProductionObjectRuntimeContainer Container => container;
        public ProductionObjectRegistry Registry => registry;
        public ProductionObjectRuntimeFactory Factory => factory;
        public ProductionObjectInstanceTracker InstanceTracker => instanceTracker;
        public ProductionObjectVisibilityRuntime VisibilityRuntime => visibilityRuntime;
        public string CurrentRoom => currentRoom;
        public RoomView CurrentView => currentView;
        public bool IsSubscribedToNavigation => subscribed;
        public int RoomContentCount => factory != null ? factory.ActiveInstanceCount : 0;
        public int RegistryEntryCount => registry != null ? registry.EntryCount : 0;
        public int PuzzleBindingCount => container != null ? container.PuzzleBindingCount : 0;
        public int SaveLoadObjectBindingCount => container != null ? container.SaveLoadObjectBindingCount : 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Report("DUPLICATE_RUNTIME_OWNER", "ERROR", "Duplicate runtime owner component was blocked.");
                Destroy(this);
                return;
            }

            _instance = this;
        }

        public bool Initialize(string initialRoom, RoomView initialView, RoomNavigationUIController navigation)
        {
            if (_instance != null && _instance != this)
            {
                Report("DUPLICATE_RUNTIME_OWNER", "ERROR", "Duplicate runtime owner Initialize was blocked.");
                state = ProductionObjectRuntimeState.Faulted;
                return false;
            }

            if (_instance == null)
                _instance = this;

            if (state == ProductionObjectRuntimeState.Ready || state == ProductionObjectRuntimeState.Initializing)
            {
                Report("DUPLICATE_INITIALIZE", "WARNING", "Initialize was called more than once and was ignored safely.");
                return true;
            }

            if (state == ProductionObjectRuntimeState.ShuttingDown)
            {
                Report("INITIALIZATION_ORDER_ERROR", "ERROR", "Initialize was called while runtime is shutting down.");
                return false;
            }

            state = ProductionObjectRuntimeState.Initializing;

            if (!IsValidRoom(initialRoom))
            {
                Report("INVALID_ROOM_EVENT", "ERROR", "Initial room is invalid: " + initialRoom);
                state = ProductionObjectRuntimeState.Faulted;
                return false;
            }

            if (!IsValidView(initialView))
            {
                Report("INVALID_VIEW_EVENT", "ERROR", "Initial view is invalid: " + initialView);
                state = ProductionObjectRuntimeState.Faulted;
                return false;
            }

            Transform gameplayRoot = FindGameplayRoot();
            container = ProductionObjectRuntimeContainer.GetOrCreate(gameplayRoot, Report);
            if (container == null)
            {
                Report("MISSING_RUNTIME_CONTAINER", "ERROR", "Runtime container could not be created or connected.");
                state = ProductionObjectRuntimeState.Faulted;
                return false;
            }

            currentRoom = initialRoom;
            currentView = initialView;
            instanceTracker = new ProductionObjectInstanceTracker();
            visibilityRuntime = new ProductionObjectVisibilityRuntime();
            factory = new ProductionObjectRuntimeFactory(container, instanceTracker, visibilityRuntime);
            SetObjectLayerActive(objectLayerActive);
            ConnectNavigationSource(navigation);
            state = ProductionObjectRuntimeState.Ready;
            if (registry != null)
                factory.SpawnRoomEntries(registry, currentRoom, ProductionObjectRegistry.ViewToCode(currentView));
            Debug.Log(LogPrefix + " state=READY room=" + currentRoom + " view=" + currentView + " room_content_count=0 registry_entry_count=0");
            return true;
        }

        public bool HandleRoomChanged(string previousRoom, string nextRoom)
        {
            if (state != ProductionObjectRuntimeState.Ready)
            {
                Report("ROOM_EVENT_BEFORE_INITIALIZE", "WARNING", "Room event ignored because runtime is not ready.");
                return false;
            }

            if (!IsValidRoom(nextRoom))
            {
                Report("INVALID_ROOM_EVENT", "ERROR", "Invalid room event: " + nextRoom);
                return false;
            }

            currentRoom = nextRoom;
            Debug.Log("[ROOM_RUNTIME] ROOM_ENTER=" + currentRoom);
            
            var roomRegistry = UnityEngine.Resources.Load<ProductionObjectRegistry>($"ProductionObjects/{currentRoom}_ProductionObjectRegistry");
            if (roomRegistry != null)
            {
                // SetRegistry is the single owner of the clear + spawn + visibility pass for the
                // new room's registry. TASK045: the previous duplicate spawn block below was removed
                // so REGISTRY_QUERY / ENTRY_FOUND fire exactly once per room change.
                SetRegistry(roomRegistry);
            }
            else if (factory != null)
            {
                // No production registry for this room: clear the previous room so nothing lingers.
                factory.ClearRoom(previousRoom);
            }
            Debug.Log(LogPrefix + " room_changed previous=" + previousRoom + " current=" + nextRoom + " room_content_count=" + RoomContentCount);
            return true;
        }

        public bool HandleViewChanged(RoomView previousView, RoomView nextView)
        {
            if (state != ProductionObjectRuntimeState.Ready)
            {
                Report("VIEW_EVENT_BEFORE_INITIALIZE", "WARNING", "View event ignored because runtime is not ready.");
                return false;
            }

            if (!IsValidView(nextView))
            {
                Report("INVALID_VIEW_EVENT", "ERROR", "Invalid view event: " + nextView);
                return false;
            }

            currentView = nextView;
            if (factory != null)
            {
                factory.SpawnViewEntries(registry, currentRoom, ProductionObjectRegistry.ViewToCode(currentView));
                factory.ApplyVisibility(currentRoom, ProductionObjectRegistry.ViewToCode(currentView));
            }
            Debug.Log(LogPrefix + " view_changed previous=" + previousView + " current=" + nextView);
            return true;
        }

        public bool ClearRoomObjects()
        {
            if (container == null)
            {
                Report("MISSING_RUNTIME_CONTAINER", "ERROR", "ClearRoomObjects failed because container is missing.");
                return false;
            }

            if (factory != null)
                factory.ClearAll();
            else
                container.ClearRoomObjects();
            return true;
        }

        public bool SetObjectLayerActive(bool active)
        {
            objectLayerActive = active;
            if (container == null)
            {
                Report("MISSING_RUNTIME_CONTAINER", "ERROR", "SetObjectLayerActive failed because container is missing.");
                return false;
            }

            container.gameObject.SetActive(active);
            return true;
        }

        public bool SetRegistry(ProductionObjectRegistry nextRegistry)
        {
            registry = nextRegistry;
            if (registry == null)
                return true;

            ProductionObjectRegistryValidationResult result = registry.ValidateAll();
            if (!result.IsValid)
            {
                Report("INVALID_REGISTRY", "ERROR", "Production object registry validation failed with " + result.Errors.Count + " error(s).");
                registry = null;
                return false;
            }

            if (state == ProductionObjectRuntimeState.Ready && factory != null)
            {
                factory.ClearAll();
                factory.SpawnRoomEntries(registry, currentRoom, ProductionObjectRegistry.ViewToCode(currentView));
                factory.ApplyVisibility(currentRoom, ProductionObjectRegistry.ViewToCode(currentView));
            }

            return true;
        }

        public bool Shutdown()
        {
            if (state == ProductionObjectRuntimeState.Shutdown || state == ProductionObjectRuntimeState.Uninitialized)
                return true;

            if (state == ProductionObjectRuntimeState.ShuttingDown)
            {
                Report("SHUTDOWN_CLEANUP_FAILURE", "WARNING", "Duplicate Shutdown call ignored safely while already shutting down.");
                return true;
            }

            state = ProductionObjectRuntimeState.ShuttingDown;
            DisconnectNavigationSource();
            bool cleared = container == null || ClearRoomObjects();
            if (!cleared)
                Report("SHUTDOWN_CLEANUP_FAILURE", "ERROR", "Shutdown could not clear runtime-owned room objects.");

            state = ProductionObjectRuntimeState.Shutdown;
            factory = null;
            instanceTracker = null;
            visibilityRuntime = null;
            if (_instance == this)
                _instance = null;
            return cleared;
        }

        private void OnDestroy()
        {
            if (_instance == this && state != ProductionObjectRuntimeState.Shutdown)
                Shutdown();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void ConnectNavigationSource(RoomNavigationUIController navigation)
        {
            DisconnectNavigationSource();
            navigationSource = navigation;
            if (navigationSource == null)
            {
                Report("EVENT_SOURCE_MISSING", "WARNING", "RoomNavigationUIController source is missing; runtime initialized without transition subscription.");
                return;
            }

            navigationSource.RoomChanged += OnNavigationRoomChanged;
            navigationSource.ViewChanged += OnNavigationViewChanged;
            subscribed = true;
        }

        private void DisconnectNavigationSource()
        {
            if (navigationSource != null && subscribed)
            {
                navigationSource.RoomChanged -= OnNavigationRoomChanged;
                navigationSource.ViewChanged -= OnNavigationViewChanged;
            }

            subscribed = false;
            navigationSource = null;
        }

        private void OnNavigationRoomChanged(string previousRoom, string nextRoom)
        {
            HandleRoomChanged(previousRoom, nextRoom);
        }

        private void OnNavigationViewChanged(RoomView previousView, RoomView nextView)
        {
            HandleViewChanged(previousView, nextView);
        }

        private static Transform FindGameplayRoot()
        {
            GameObject root = GameObject.Find(GameplayRootName);
            if (root != null)
                return root.transform;

            var go = new GameObject(GameplayRootName);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static bool IsValidRoom(string room)
        {
            if (string.IsNullOrEmpty(room) || room.Length != 3 || room[0] != 'P')
                return false;

            if (!int.TryParse(room.Substring(1), out int roomNumber))
                return false;

            return roomNumber >= 0 && roomNumber <= 10;
        }

        private static bool IsValidView(RoomView view)
        {
            return System.Enum.IsDefined(typeof(RoomView), view);
        }

        private void Report(string code, string severity, string message)
        {
            string text = LogPrefix + "\ncode=" + code
                + "\nseverity=" + severity
                + "\nstate=" + state
                + "\nroom=" + currentRoom
                + "\nview=" + currentView
                + "\nmessage=" + message;

            if (severity == "ERROR")
                Debug.LogError(text);
            else if (severity == "WARNING")
                Debug.LogWarning(text);
            else
                Debug.Log(text);
        }
    }
}
