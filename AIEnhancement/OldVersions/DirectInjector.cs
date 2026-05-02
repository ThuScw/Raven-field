using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RavenfieldAIEnhancement
{
    class DirectInjector
    {
        static void Main(string[] args)
        {
            string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
            string targetDll = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            string backupPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.backup");
            string enhancementDll = Path.Combine(gameManaged, "AIEnhancement.dll");
            
            Console.WriteLine("Ravenfield AI Enhancement Direct Injector");
            Console.WriteLine("==========================================");
            
            try
            {
                if (!File.Exists(backupPath))
                {
                    File.Copy(targetDll, backupPath, true);
                    Console.WriteLine("Backup created");
                }
                
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(gameManaged);
                
                var readParams = new ReaderParameters { AssemblyResolver = resolver };
                
                using (var assembly = AssemblyDefinition.ReadAssembly(targetDll, readParams))
                {
                    using (var enhancementAssembly = AssemblyDefinition.ReadAssembly(enhancementDll))
                    {
                        var initializerType = enhancementAssembly.MainModule.GetType("RavenfieldAIEnhancement.AIEnhancementAutoStart");
                        
                        var gameManager = assembly.MainModule.GetType("GameManager");
                        if (gameManager == null)
                        {
                            Console.WriteLine("ERROR: GameManager not found!");
                            return;
                        }
                        
                        Console.WriteLine("Found GameManager");
                        
                        MethodDefinition awakeMethod = null;
                        foreach (var method in gameManager.Methods)
                        {
                            if (method.Name == "Awake")
                            {
                                awakeMethod = method;
                                break;
                            }
                        }
                        
                        if (awakeMethod == null)
                        {
                            foreach (var method in gameManager.Methods)
                            {
                                if (method.Name == "Start")
                                {
                                    awakeMethod = method;
                                    break;
                                }
                            }
                        }
                        
                        if (awakeMethod == null)
                        {
                            Console.WriteLine("ERROR: No Awake or Start method found!");
                            return;
                        }
                        
                        Console.WriteLine("Found method: " + awakeMethod.Name);
                        
                        var ilProcessor = awakeMethod.Body.GetILProcessor();
                        var firstInstruction = awakeMethod.Body.Instructions[0];
                        
                        MethodDefinition onGameStartMethod = null;
                        foreach (var method in initializerType.Methods)
                        {
                            if (method.Name == "OnGameStart")
                            {
                                onGameStartMethod = method;
                                break;
                            }
                        }
                        
                        if (onGameStartMethod == null)
                        {
                            Console.WriteLine("ERROR: OnGameStart not found!");
                            return;
                        }
                        
                        var importedMethod = assembly.MainModule.ImportReference(onGameStartMethod);
                        
                        var callInstruction = ilProcessor.Create(OpCodes.Call, importedMethod);
                        ilProcessor.InsertBefore(firstInstruction, callInstruction);
                        
                        Console.WriteLine("Injected call to OnGameStart()");
                    }
                    
                    assembly.Write(targetDll + ".temp");
                    File.Delete(targetDll);
                    File.Move(targetDll + ".temp", targetDll);
                    
                    Console.WriteLine("");
                    Console.WriteLine("SUCCESS! Game DLL patched.");
                    Console.WriteLine("Launch the game to see AI Enhancement in action!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
