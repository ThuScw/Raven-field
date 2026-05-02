using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Direct IL injector using System.Reflection.Emit and ModuleBuilder
    /// This creates a modified version of Assembly-CSharp.dll with threat assessment built-in
    /// </summary>
    public class DirectILInjector
    {
        public static void Main(string[] args)
        {
            string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
            string assemblyCSharp = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            string outputPath = Path.Combine(gameManaged, "Assembly-CSharp-Patched.dll");
            
            Console.WriteLine("AI Threat Assessment Injector");
            Console.WriteLine("=============================");
            
            try
            {
                // Load the original assembly
                Assembly originalAssembly = Assembly.LoadFrom(assemblyCSharp);
                Console.WriteLine($"Loaded: {originalAssembly.FullName}");
                
                // For now, we'll create a companion assembly that hooks into the game
                // Since direct IL modification is complex without Mono.Cecil
                CreateCompanionAssembly(gameManaged);
                
                Console.WriteLine("Done! Companion assembly created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }
        
        static void CreateCompanionAssembly(string gameManagedPath)
        {
            // Create a dynamic assembly that will be loaded by the game
            // This assembly contains the threat assessment logic and uses reflection to hook into AI
            
            string assemblyName = "AIThreatCompanion";
            AssemblyName name = new AssemblyName(assemblyName);
            
            AppDomain domain = AppDomain.CurrentDomain;
            AssemblyBuilder assemblyBuilder = domain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Save);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName, $"{assemblyName}.dll");
            
            // Create the main class
            TypeBuilder typeBuilder = moduleBuilder.DefineType(
                "RavenfieldAIEnhancement.ThreatCompanion",
                TypeAttributes.Public | TypeAttributes.Class);
            
            // Add a static Initialize method
            MethodBuilder initMethod = typeBuilder.DefineMethod(
                "Initialize",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(void),
                Type.EmptyTypes);
            
            ILGenerator il = initMethod.GetILGenerator();
            il.Emit(OpCodes.Ldstr, "[AIEnhancement] Companion assembly loaded!");
            il.Emit(OpCodes.Call, typeof(UnityEngine.Debug).GetMethod("Log", new[] { typeof(object) }));
            il.Emit(OpCodes.Ret);
            
            Type finalType = typeBuilder.CreateType();
            
            // Save the assembly
            string outputPath = Path.Combine(gameManagedPath, $"{assemblyName}.dll");
            assemblyBuilder.Save($"{assemblyName}.dll");
            
            Console.WriteLine($"Created companion assembly: {outputPath}");
        }
    }
}
