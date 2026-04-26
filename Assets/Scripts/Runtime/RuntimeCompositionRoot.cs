using Game.Domain.Cards;
using Game.Domain.Village;
using Game.Runtime.Player;
using UnityEngine;

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
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
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

            if (cachedAuthoritativeDrawService != null)
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

            if (cachedAuthoritativeVillageUpgradeService != null)
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
