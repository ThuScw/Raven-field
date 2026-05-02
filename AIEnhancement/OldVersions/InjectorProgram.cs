using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

class InjectorProgram
{
    static int Main(string[] args)
    {
        string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
        string targetDll = Path.Combine(gameManaged, "Assembly-CSharp.dll");
        string backupPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.backup");
        string enhancementDll = Path.Combine(gameManaged, "AIEnhancement.dll");
        
        Console.WriteLine("Ravenfield AI Enhancement Injector");
        Console.WriteLine("===================================");
        
        try
        {
            if (!File.Exists(backupPath))
            {
                File.Copy(targetDll, backupPath, true);
                Console.WriteLine("Backup created: " + backupPath);
            }
            
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(gameManaged);
            
            var readParams = new ReaderParameters();
            readParams.AssemblyResolver = resolver;
            
            using (var assembly = AssemblyDefinition.ReadAssembly(targetDll, readParams))
            {
                using (var enhancementAssembly = AssemblyDefinition.ReadAssembly(enhancementDll, readParams))
                {
                    var initializerType = enhancementAssembly.MainModule.GetType("RavenfieldAIEnhancement.AIEnhancementAutoStart");
                    var gameManager = assembly.MainModule.GetType("GameManager");
                    
                    if (gameManager == null)
                    {
                        Console.WriteLine("ERROR: GameManager not found!");
                        return 1;
                    }
                    
                    Console.WriteLine("Found GameManager");
                    
                    MethodDefinition targetMethod = null;
                    foreach (var method in gameManager.Methods)
                    {
                        if (method.Name == "Awake")
                        {
                            targetMethod = method;
                            break;
                        }
                    }
                    
                    if (targetMethod == null)
                    {
                        foreach (var method in gameManager.Methods)
                        {
                            if (method.Name == "Start")
                            {
                                targetMethod = method;
                                break;
                            }
                        }
                    }
                    
                    if (targetMethod == null)
                    {
                        Console.WriteLine("ERROR: No Awake or Start method found!");
                        return 1;
                    }
                    
                    Console.WriteLine("Found method: " + targetMethod.Name);
                    
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
                        return 1;
                    }
                    
                    var importedMethod = assembly.MainModule.ImportReference(onGameStartMethod);
                    
                    var ilProcessor = targetMethod.Body.GetILProcessor();
                    var firstInstruction = targetMethod.Body.Instructions[0];
                    
                    var callInstruction = ilProcessor.Create(OpCodes.Call, importedMethod);
                    ilProcessor.InsertBefore(firstInstruction, callInstruction);
                    
                    Console.WriteLine("Injected call to OnGameStart()");
                }
                
                assembly.Write(targetDll + ".new");
                Console.WriteLine("Written to temporary file");
            }
            
            // Replace original
            File.Delete(targetDll);
            File.Move(targetDll + ".new", targetDll);
            
            Console.WriteLine("");
            Console.WriteLine("SUCCESS! Game DLL patched.");
            Console.WriteLine("Launch the game to see AI Enhancement in action!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
