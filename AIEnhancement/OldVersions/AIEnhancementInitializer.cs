using System;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Initializer that auto-starts when the game loads
    /// This class is designed to be instantiated by the modified game code
    /// </summary>
    public class AIEnhancementInitializer : MonoBehaviour
    {
        private static bool _initialized = false;
        
        void Awake()
        {
            if (_initialized)
            {
                Destroy(gameObject);
                return;
            }
            
            _initialized = true;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("========================================");
            Debug.Log("[AIEnhancement] Threat Assessment System v1.0");
            Debug.Log("[AIEnhancement] Initializing...");
            
            try
            {
                // Install the threat assessment system
                InstallThreatAssessment();
                
                Debug.Log("[AIEnhancement] System active!");
                Debug.Log("========================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEnhancement] Failed to initialize: {ex}");
            }
        }
        
        private void InstallThreatAssessment()
        {
            // Create the updater component
            GameObject updaterObject = new GameObject("AIThreatUpdater");
            updaterObject.transform.SetParent(transform);
            updaterObject.AddComponent<AIThreatUpdater>();
            
            Debug.Log("[AIEnhancement] Threat assessment updater installed");
        }
    }
}
