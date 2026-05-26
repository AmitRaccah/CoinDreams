using Game.Domain.Cards;
using Game.Domain.Village;
using Game.Runtime.Cards;
using Game.Runtime.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class RuntimeCompositionRoot : MonoBehaviour
    {
        [Header("Shared Runtime")]
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;

        [Header("Authoritative Services")]
        [SerializeField] private MonoBehaviour authoritativeDrawServiceSource;
        [SerializeField] private MonoBehaviour authoritativeVillageUpgradeServiceSource;

        private static RuntimeCompositionRoot active;
        private IAuthoritativeDrawService authoritativeDrawService;
        private IAuthoritativeVillageUpgradeService authoritativeVillageUpgradeService;

        public static RuntimeCompositionRoot Active
        {
            get { return active; }
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                Debug.LogWarning(
                    "[RuntimeCompositionRoot] Multiple runtime composition roots found. The newest root is now active.",
                    this);
            }

            active = this;
            ResolveLocalPlayerContext();
            ResolveLocalAuthoritativeServices();

            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            if (active == this)
            {
                active = null;
            }

            // Cached interface references may outlive their MonoBehaviour sources; flush to avoid MissingReferenceException.
            RuntimeServiceResolver.ClearCaches();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            RuntimeServiceResolver.ClearCaches();
        }

        public bool TryGetPlayerRuntimeContext(out PlayerRuntimeContext context)
        {
            ResolveLocalPlayerContext();
            context = playerRuntimeContext;
            return context != null;
        }

        public bool TryGetAuthoritativeDrawService(out IAuthoritativeDrawService service, out MonoBehaviour source)
        {
            ResolveLocalAuthoritativeDrawService();
            service = authoritativeDrawService;
            source = authoritativeDrawServiceSource;
            return service != null;
        }

        public bool TryGetAuthoritativeVillageUpgradeService(
            out IAuthoritativeVillageUpgradeService service,
            out MonoBehaviour source)
        {
            ResolveLocalAuthoritativeVillageUpgradeService();
            service = authoritativeVillageUpgradeService;
            source = authoritativeVillageUpgradeServiceSource;
            return service != null;
        }

        private void ResolveLocalPlayerContext()
        {
            if (playerRuntimeContext != null)
            {
                return;
            }

            playerRuntimeContext = GetComponent<PlayerRuntimeContext>();
            if (playerRuntimeContext != null)
            {
                return;
            }

            playerRuntimeContext = GetComponentInChildren<PlayerRuntimeContext>(true);
        }

        private void ResolveLocalAuthoritativeServices()
        {
            ResolveLocalAuthoritativeDrawService();
            ResolveLocalAuthoritativeVillageUpgradeService();
        }

        private void ResolveLocalAuthoritativeDrawService()
        {
            if (TryResolveService(authoritativeDrawServiceSource, out authoritativeDrawService))
            {
                return;
            }

            if (TryResolveServiceInChildren(out authoritativeDrawService, out authoritativeDrawServiceSource))
            {
                return;
            }

            authoritativeDrawService = null;
        }

        private void ResolveLocalAuthoritativeVillageUpgradeService()
        {
            if (TryResolveService(authoritativeVillageUpgradeServiceSource, out authoritativeVillageUpgradeService))
            {
                return;
            }

            if (TryResolveServiceInChildren(
                    out authoritativeVillageUpgradeService,
                    out authoritativeVillageUpgradeServiceSource))
            {
                return;
            }

            authoritativeVillageUpgradeService = null;
        }

        private static bool TryResolveService<TService>(MonoBehaviour source, out TService service)
            where TService : class
        {
            service = source as TService;
            return service != null;
        }

        private bool TryResolveServiceInChildren<TService>(out TService service, out MonoBehaviour source)
            where TService : class
        {
            service = null;
            source = null;

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            int i;
            for (i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                service = behaviour as TService;
                if (service == null)
                {
                    continue;
                }

                source = behaviour;
                return true;
            }

            return false;
        }
    }

    public static class RuntimeServiceResolver
    {
        private static RuntimeCompositionRoot cachedRoot;
        private static PlayerRuntimeContext cachedPlayerRuntimeContext;
        private static IAuthoritativeDrawService cachedAuthoritativeDrawService;
        private static MonoBehaviour cachedAuthoritativeDrawServiceSource;
        private static IAuthoritativeVillageUpgradeService cachedAuthoritativeVillageUpgradeService;
        private static MonoBehaviour cachedAuthoritativeVillageUpgradeServiceSource;
        private static ICardDrawWorkflowCommands cachedDrawWorkflowCommands;
        private static MonoBehaviour cachedDrawWorkflowCommandsSource;
        private static bool drawServiceSearchAttempted;
        private static bool villageServiceSearchAttempted;
        private static bool drawWorkflowSearchAttempted;

        public static void ClearCaches()
        {
            cachedRoot = null;
            cachedPlayerRuntimeContext = null;
            cachedAuthoritativeDrawService = null;
            cachedAuthoritativeDrawServiceSource = null;
            cachedAuthoritativeVillageUpgradeService = null;
            cachedAuthoritativeVillageUpgradeServiceSource = null;
            cachedDrawWorkflowCommands = null;
            cachedDrawWorkflowCommandsSource = null;
            drawServiceSearchAttempted = false;
            villageServiceSearchAttempted = false;
            drawWorkflowSearchAttempted = false;
        }

        public static bool TryResolveDrawWorkflowCommands(
            MonoBehaviour configuredSource,
            out ICardDrawWorkflowCommands commands,
            out MonoBehaviour source)
        {
            if (TryResolveConfiguredService(configuredSource, out commands))
            {
                source = configuredSource;
                cachedDrawWorkflowCommands = commands;
                cachedDrawWorkflowCommandsSource = source;
                return true;
            }

            if (cachedDrawWorkflowCommandsSource == null)
            {
                cachedDrawWorkflowCommands = null;
                cachedDrawWorkflowCommandsSource = null;
            }
            else if (cachedDrawWorkflowCommands != null)
            {
                commands = cachedDrawWorkflowCommands;
                source = cachedDrawWorkflowCommandsSource;
                return true;
            }

            if (drawWorkflowSearchAttempted)
            {
                commands = null;
                source = null;
                return false;
            }

            drawWorkflowSearchAttempted = true;
            return TryResolveSceneService(
                out cachedDrawWorkflowCommands,
                out cachedDrawWorkflowCommandsSource,
                out commands,
                out source);
        }

        public static bool TryResolvePlayerContext(
            PlayerRuntimeContext configuredContext,
            out PlayerRuntimeContext context)
        {
            if (configuredContext != null)
            {
                cachedPlayerRuntimeContext = configuredContext;
                context = configuredContext;
                return true;
            }

            if (cachedPlayerRuntimeContext != null)
            {
                context = cachedPlayerRuntimeContext;
                return true;
            }

            RuntimeCompositionRoot root = ResolveRoot();
            if (root != null && root.TryGetPlayerRuntimeContext(out context))
            {
                cachedPlayerRuntimeContext = context;
                return true;
            }

            context = Object.FindFirstObjectByType<PlayerRuntimeContext>();
            if (context != null)
            {
                cachedPlayerRuntimeContext = context;
                return true;
            }

            return false;
        }

        public static bool TryResolveAuthoritativeDrawService(
            MonoBehaviour configuredSource,
            out IAuthoritativeDrawService service,
            out MonoBehaviour source)
        {
            if (TryResolveConfiguredService(configuredSource, out service))
            {
                source = configuredSource;
                cachedAuthoritativeDrawService = service;
                cachedAuthoritativeDrawServiceSource = source;
                return true;
            }

            // Unity-nullity check: the MonoBehaviour overload of == flags destroyed objects so we re-resolve after scene unload.
            if (cachedAuthoritativeDrawServiceSource == null)
            {
                cachedAuthoritativeDrawService = null;
                cachedAuthoritativeDrawServiceSource = null;
            }
            else if (cachedAuthoritativeDrawService != null)
            {
                service = cachedAuthoritativeDrawService;
                source = cachedAuthoritativeDrawServiceSource;
                return true;
            }

            RuntimeCompositionRoot root = ResolveRoot();
            if (root != null && root.TryGetAuthoritativeDrawService(out service, out source))
            {
                cachedAuthoritativeDrawService = service;
                cachedAuthoritativeDrawServiceSource = source;
                return true;
            }

            if (drawServiceSearchAttempted)
            {
                service = null;
                source = null;
                return false;
            }

            drawServiceSearchAttempted = true;
            return TryResolveSceneService(
                out cachedAuthoritativeDrawService,
                out cachedAuthoritativeDrawServiceSource,
                out service,
                out source);
        }

        public static bool TryResolveAuthoritativeVillageUpgradeService(
            MonoBehaviour configuredSource,
            out IAuthoritativeVillageUpgradeService service,
            out MonoBehaviour source)
        {
            if (TryResolveConfiguredService(configuredSource, out service))
            {
                source = configuredSource;
                cachedAuthoritativeVillageUpgradeService = service;
                cachedAuthoritativeVillageUpgradeServiceSource = source;
                return true;
            }

            if (cachedAuthoritativeVillageUpgradeServiceSource == null)
            {
                cachedAuthoritativeVillageUpgradeService = null;
                cachedAuthoritativeVillageUpgradeServiceSource = null;
            }
            else if (cachedAuthoritativeVillageUpgradeService != null)
            {
                service = cachedAuthoritativeVillageUpgradeService;
                source = cachedAuthoritativeVillageUpgradeServiceSource;
                return true;
            }

            RuntimeCompositionRoot root = ResolveRoot();
            if (root != null && root.TryGetAuthoritativeVillageUpgradeService(out service, out source))
            {
                cachedAuthoritativeVillageUpgradeService = service;
                cachedAuthoritativeVillageUpgradeServiceSource = source;
                return true;
            }

            if (villageServiceSearchAttempted)
            {
                service = null;
                source = null;
                return false;
            }

            villageServiceSearchAttempted = true;
            return TryResolveSceneService(
                out cachedAuthoritativeVillageUpgradeService,
                out cachedAuthoritativeVillageUpgradeServiceSource,
                out service,
                out source);
        }

        private static RuntimeCompositionRoot ResolveRoot()
        {
            if (cachedRoot != null)
            {
                return cachedRoot;
            }

            cachedRoot = RuntimeCompositionRoot.Active;
            if (cachedRoot != null)
            {
                return cachedRoot;
            }

            cachedRoot = Object.FindFirstObjectByType<RuntimeCompositionRoot>();
            return cachedRoot;
        }

        private static bool TryResolveConfiguredService<TService>(
            MonoBehaviour configuredSource,
            out TService service)
            where TService : class
        {
            service = configuredSource as TService;
            return service != null;
        }

        private static bool TryResolveSceneService<TService>(
            out TService cachedService,
            out MonoBehaviour cachedSource,
            out TService service,
            out MonoBehaviour source)
            where TService : class
        {
            cachedService = null;
            cachedSource = null;
            service = null;
            source = null;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int i;
            for (i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                service = behaviour as TService;
                if (service == null)
                {
                    continue;
                }

                source = behaviour;
                cachedService = service;
                cachedSource = source;
                return true;
            }

            return false;
        }
    }
}
