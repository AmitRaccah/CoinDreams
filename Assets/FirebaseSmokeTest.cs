//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using UnityEngine;
//using Firebase;
//using Firebase.Auth;
//using Firebase.Firestore;

//public sealed class FirebaseSmokeTest : MonoBehaviour
//{
//    [Header("Firestore Test")]
//    [SerializeField] private string collectionName = "smoke_tests";

//    private async void Start()
//    {
//        Debug.Log("FirebaseSmokeTest Start");

//        try
//        {
//            FirebaseApp.LogLevel = LogLevel.Debug;

//            // 1) Dependencies
//            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
//            Debug.Log("Firebase dependency status: " + status);

//            if (status != DependencyStatus.Available)
//            {
//                Debug.LogError("Firebase dependencies not available: " + status);
//                return;
//            }

//            // 2) Auth (anonymous)
//            FirebaseAuth auth = FirebaseAuth.DefaultInstance;

//            if (auth.CurrentUser == null)
//            {
//                AuthResult result = await auth.SignInAnonymouslyAsync();
//                Debug.Log("Signed in anonymously. UID: " + result.User.UserId);
//            }
//            else
//            {
//                Debug.Log("Already signed in. UID: " + auth.CurrentUser.UserId);
//            }

//            string uid = auth.CurrentUser.UserId;

//            // 3) Firestore write
//            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;

//            DocumentReference doc = db.Collection(collectionName).Document(uid);

//            Dictionary<string, object> payload = new Dictionary<string, object>
//            {
//                { "uid", uid },
//                { "timeUtc", DateTime.UtcNow.ToString("O") },
//                { "ping", "ok" }
//            };

//            await doc.SetAsync(payload);
//            Debug.Log("Firestore write OK: " + doc.Path);

//            // 4) Firestore read
//            DocumentSnapshot snap = await doc.GetSnapshotAsync();

//            if (!snap.Exists)
//            {
//                Debug.LogError("Firestore read failed: document does not exist after write.");
//                return;
//            }

//            Debug.Log("Firestore read OK: " + snap.Id);

//            if (snap.TryGetValue<string>("ping", out string ping))
//            {
//                Debug.Log("Firestore field ping = " + ping);
//            }
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Firebase smoke test failed: " + e);
//        }
//    }
//}
