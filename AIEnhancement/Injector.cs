using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Injector
{
    static int Main(string[] args)
    {
        string logFile = @"c:\temp\ravenfield_inject.log";
        FileStream logStream = null;
        StreamWriter logWriter = null;

        try
        {
            logStream = new FileStream(logFile, FileMode.Create, FileAccess.Write);
            logWriter = new StreamWriter(logStream);
        }
        catch { }

        Action<string> Log = (msg) =>
        {
            Console.WriteLine(msg);
            if (logWriter != null)
            {
                logWriter.WriteLine(msg);
                logWriter.Flush();
            }
        };

        string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
        string targetDll = Path.Combine(gameManaged, "Assembly-CSharp.dll");
        string backupPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.backup");
        string enhancementDll = Path.Combine(gameManaged, "AIEnhancement.dll");
        string workDir = @"c:\temp\ravenfield_patch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string workTarget = Path.Combine(workDir, "Assembly-CSharp.dll");
        string workEnhancement = Path.Combine(workDir, "AIEnhancement.dll");
        string outputFile = Path.Combine(workDir, "Assembly-CSharp-patched.dll");

        Log("Ravenfield AI Enhancement V3 Injector");
        Log("======================================");

        try
        {
            // Create work directory
            Directory.CreateDirectory(workDir);
            Log("Created work directory: " + workDir);

            // Copy files to work directory
            File.Copy(backupPath, workTarget, true);
            File.Copy(enhancementDll, workEnhancement, true);
            Log("Copied files to work directory");

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(workDir);
            resolver.AddSearchDirectory(gameManaged);

            var readParams = new ReaderParameters();
            readParams.AssemblyResolver = resolver;
            readParams.ReadWrite = false;

            using (var assembly = AssemblyDefinition.ReadAssembly(workTarget, readParams))
            {
                using (var enhancementAssembly = AssemblyDefinition.ReadAssembly(workEnhancement, readParams))
                {
                    // Add reference to AIEnhancement.dll
                    var enhancementName = enhancementAssembly.Name;
                    assembly.MainModule.AssemblyReferences.Add(enhancementName);
                    Log("Added reference to AIEnhancement.dll: " + enhancementName.Name + " v" + enhancementName.Version);

                    var initializerType = enhancementAssembly.MainModule.GetType("RavenfieldAIEnhancement.AIEnhancementAutoStart");
                    var gameManager = assembly.MainModule.GetType("GameManager");

                    if (gameManager == null)
                    {
                        Log("ERROR: GameManager not found!");
                        return 1;
                    }

                    Log("Found GameManager");

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
                        Log("ERROR: No Awake or Start method found!");
                        return 1;
                    }

                    Log("Found method: " + targetMethod.Name);

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
                        Log("ERROR: OnGameStart not found!");
                        return 1;
                    }

                    var importedMethod = assembly.MainModule.ImportReference(onGameStartMethod);
                    Log("Imported OnGameStart method");

                    var ilProcessor = targetMethod.Body.GetILProcessor();
                    var firstInstruction = targetMethod.Body.Instructions[0];

                    var callInstruction = ilProcessor.Create(OpCodes.Call, importedMethod);
                    ilProcessor.InsertBefore(firstInstruction, callInstruction);
                    Log("Inserted call instruction");

                    var writeParams = new WriterParameters();
                    writeParams.WriteSymbols = false;

                    assembly.Write(outputFile, writeParams);
                    Log("Wrote modified Assembly-CSharp.dll to: " + outputFile);
                }
            }

            Log("");
            Log("========================================");
            Log("INJECTION COMPLETE");
            Log("========================================");
            Log("Output file: " + outputFile);
            Log("Target file: " + targetDll);
            Log("");
            Log("Please manually copy the output file to replace the original:");
            Log("  copy \"" + outputFile + "\" \"" + targetDll + "\"");
            Log("");

            return 0;
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            Log(ex.StackTrace);
            return 1;
        }
        finally
        {
            if (logWriter != null) logWriter.Close();
            if (logStream != null) logStream.Close();
        }
    }
}
