using System;
using UnityEngine;

namespace Online {
    // Loaded from Assets/Resources/FirebaseConfig.json (git-ignored).
    // See docs/firebase/SETUP.md for how to create it.
    [Serializable]
    public class FirebaseConfig {
        public string apiKey;
        public string projectId;
        // the ranking API (ScoreApi); empty = the live site. "http://localhost:3000" to test
        // against freny-online's tools/api-server.js
        public string apiUrl;
        // tests only, the Firebase Emulator Suite (docs/firebase/SETUP.md): empty = the real services.
        // "http://127.0.0.1:9099" and "http://127.0.0.1:8080"
        public string authUrl;
        public string firestoreUrl;

        private const string ResourceName = "FirebaseConfig";

        public bool IsValid => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(projectId);

        public static FirebaseConfig Load() {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) {
                Debug.LogWarning("[Online] Assets/Resources/FirebaseConfig.json not found. Online features are disabled.");
                return null;
            }

            FirebaseConfig config = JsonUtility.FromJson<FirebaseConfig>(asset.text);
            if (config == null || !config.IsValid) {
                Debug.LogWarning("[Online] FirebaseConfig.json is missing apiKey or projectId. Online features are disabled.");
                return null;
            }
            return config;
        }
    }
}
