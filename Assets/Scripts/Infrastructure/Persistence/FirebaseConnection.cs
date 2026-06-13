using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirebaseConnection
    {
        private FirebaseApp app;
        private FirebaseAuth auth;
        private FirebaseFirestore firestore;

        public string AuthenticatedPlayerId { get; private set; } = string.Empty;

        public FirebaseFirestore Firestore
        {
            get { return firestore; }
        }

        public bool IsReady
        {
            get
            {
                return app != null
                    && auth != null
                    && firestore != null
                    && !string.IsNullOrEmpty(AuthenticatedPlayerId);
            }
        }

        public async Task<bool> InitializeAndAuthenticateAsync(
            bool forceFreshAnonymousIdentity,
            bool verboseLogging,
            MonoBehaviour context)
        {
            AuthenticatedPlayerId = string.Empty;

            try
            {
                DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    Debug.LogError(
                        "[FirebaseConnection] Firebase dependencies are not available: " + dependencyStatus + ".",
                        context);
                    return false;
                }

                // Suppress Firebase SDK's internal Info-level chatter (e.g. the
                // "USE_AUTH_EMULATOR not set." line that fires from native callbacks during
                // SignInAnonymously / DeleteUser). Errors and warnings still surface; flip
                // verboseLogging in PersistenceSettings to get the full Firebase output back.
                LogLevel postInitLevel = verboseLogging ? LogLevel.Info : LogLevel.Warning;

                // During DefaultInstance access Firebase emits a "Database URL not set"
                // warning because this project uses Firestore only (no Realtime Database).
                // Stay one notch quieter during the init burst to hide that benign warning,
                // then restore the normal level immediately after the SDK handles are cached.
                FirebaseApp.LogLevel = verboseLogging ? LogLevel.Info : LogLevel.Error;

                app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;
                firestore = FirebaseFirestore.DefaultInstance;

                FirebaseApp.LogLevel = postInitLevel;

                if (forceFreshAnonymousIdentity)
                {
                    await ResetIdentityAsync(verboseLogging, context);
                }

                if (auth.CurrentUser == null)
                {
                    await auth.SignInAnonymouslyAsync();
                }

                FirebaseUser currentUser = auth.CurrentUser;
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    Debug.LogError(
                        "[FirebaseConnection] Authentication succeeded but no user ID is available.",
                        context);
                    return false;
                }

                AuthenticatedPlayerId = currentUser.UserId;

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebaseConnection] Authenticated anonymous user: " + AuthenticatedPlayerId + ".",
                        context);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[FirebaseConnection] Failed to initialize Firebase: " + exception.Message,
                    context);
                return false;
            }
        }

        private async Task ResetIdentityAsync(bool verboseLogging, MonoBehaviour context)
        {
            if (auth == null)
            {
                return;
            }

            FirebaseUser currentUser = auth.CurrentUser;
            if (currentUser == null)
            {
                return;
            }

            try
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebaseConnection] Force fresh identity is enabled. Resetting current Firebase user.",
                        context);
                }

                if (currentUser.IsAnonymous)
                {
                    await currentUser.DeleteAsync();
                }
                else
                {
                    auth.SignOut();
                }
            }
            catch (Exception exception)
            {
                auth.SignOut();

                if (verboseLogging)
                {
                    Debug.LogWarning(
                        "[FirebaseConnection] Failed to delete current Firebase user. Continuing with SignOut. Error: "
                        + exception.Message,
                        context);
                }
            }
        }
    }
}
