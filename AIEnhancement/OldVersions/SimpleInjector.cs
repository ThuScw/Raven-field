using System;
using System.IO;
using System.Reflection;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Simple injector that modifies Assembly-CSharp.dll to call our enhancement
    /// Uses direct binary patching approach
    /// </summary>
    public class SimpleInjector
    {
        public static void Main(string[] args)
        {
            string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
            string assemblyCSharp = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            string backupPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.backup");
            string patchedPath = Path.Combine(gameManaged, "Assembly-CSharp-Patched.dll");
            
            Console.WriteLine("Ravenfield AI Enhancement Injector");
            Console.WriteLine("===================================");
            
            try
            {
                // Create backup if not exists
                if (!File.Exists(backupPath))
                {
                    File.Copy(assemblyCSharp, backupPath, true);
                    Console.WriteLine("Backup created: " + backupPath);
                }
                
                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(assemblyCSharp);
                
                // Find GameManager type
                Type gameManagerType = assembly.GetType("GameManager");
                if (gameManagerType == null)
                {
                    Console.WriteLine("ERROR: GameManager not found!");
                    return;
                }
                
                Console.WriteLine("Found GameManager type");
                
                // Find Awake or Start method
                MethodInfo awakeMethod = gameManagerType.GetMethod("Awake", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo startMethod = gameManagerType.GetMethod("Start", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                MethodInfo targetMethod = awakeMethod ?? startMethod;
                
                if (targetMethod == null)
                {
                    Console.WriteLine("ERROR: No Awake or Start method found in GameManager!");
                    return;
                }
                
                Console.WriteLine("Found target method: " + targetMethod.Name);
                
                // Since we can't easily modify IL without Mono.Cecil, 
                // we'll use a different approach: create a bootstrapper that modifies
                // the method at runtime using MethodInfo.MethodHandle
                
                Console.WriteLine("Method found. Runtime patching required.");
                Console.WriteLine("Please use the AIEnhancement.dll with a mod loader like BepInEx,");
                Console.WriteLine("or manually add the initializer to a GameObject in the scene.");
                
                // Alternative: Create a simple loader script
                CreateLoaderScript(gameManaged);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
        }
        
        static void CreateLoaderScript(string gameManaged)
        {
            string loaderScript = @"
using UnityEngine;
using RavenfieldAIEnhancement;

public class AIEnhancementLoader : MonoBehaviour
{
    void Awake()
    {
        GameObject initObject = new GameObject(""AIEnhancementInit"");
        initObject.AddComponent<AIEnhancementInitializer>();
    }
}
";
            string loaderPath = Path.Combine(gameManaged, "AIEnhancementLoader.cs");
            File.WriteAllText(loaderPath, loaderScript);
            Console.WriteLine("Created loader script: " + loaderPath);
        }
    }
}
