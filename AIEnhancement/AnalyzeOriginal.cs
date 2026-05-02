using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class AnalyzeOriginal
{
    static void Main(string[] args)
    {
        string originalDll = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\原版\Ravenfield_Data\Managed\Assembly-CSharp.dll";
        string outputFile = @"c:\temp\original_ai_analysis.txt";

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(originalDll));

        var readParams = new ReaderParameters();
        readParams.AssemblyResolver = resolver;

        using (var assembly = AssemblyDefinition.ReadAssembly(originalDll, readParams))
        {
            using (var writer = new StreamWriter(outputFile))
            {
                // Analyze Squad class
                var squadType = assembly.MainModule.GetType("Squad");
                if (squadType != null)
                {
                    writer.WriteLine("=== SQUAD CLASS ANALYSIS ===");
                    writer.WriteLine();

                    // Analyze Update method
                    var updateMethod = squadType.Methods.FirstOrDefault(m => m.Name == "Update");
                    if (updateMethod != null)
                    {
                        writer.WriteLine("--- Squad.Update() ---");
                        WriteMethodIL(writer, updateMethod);
                        writer.WriteLine();
                    }

                    // Analyze MoveTo method
                    var moveToMethod = squadType.Methods.FirstOrDefault(m => m.Name == "MoveTo");
                    if (moveToMethod != null)
                    {
                        writer.WriteLine("--- Squad.MoveTo() ---");
                        WriteMethodIL(writer, moveToMethod);
                        writer.WriteLine();
                    }

                    // Analyze AttackSpawnPoint method
                    var attackMethod = squadType.Methods.FirstOrDefault(m => m.Name == "AttackSpawnPoint");
                    if (attackMethod != null)
                    {
                        writer.WriteLine("--- Squad.AttackSpawnPoint() ---");
                        WriteMethodIL(writer, attackMethod);
                        writer.WriteLine();
                    }

                    // Analyze NewAttackOrder method
                    var newAttackMethod = squadType.Methods.FirstOrDefault(m => m.Name == "NewAttackOrder");
                    if (newAttackMethod != null)
                    {
                        writer.WriteLine("--- Squad.NewAttackOrder() ---");
                        WriteMethodIL(writer, newAttackMethod);
                        writer.WriteLine();
                    }

                    // Analyze GetTarget method
                    var getTargetMethod = squadType.Methods.FirstOrDefault(m => m.Name == "GetTarget");
                    if (getTargetMethod != null)
                    {
                        writer.WriteLine("--- Squad.GetTarget() ---");
                        WriteMethodIL(writer, getTargetMethod);
                        writer.WriteLine();
                    }
                }

                // Analyze AiActorController class
                var aiType = assembly.MainModule.GetType("AiActorController");
                if (aiType != null)
                {
                    writer.WriteLine("=== AI ACTOR CONTROLLER ANALYSIS ===");
                    writer.WriteLine();

                    // Analyze Goto method
                    var gotoMethod = aiType.Methods.FirstOrDefault(m => m.Name == "Goto");
                    if (gotoMethod != null)
                    {
                        writer.WriteLine("--- AiActorController.Goto() ---");
                        WriteMethodIL(writer, gotoMethod);
                        writer.WriteLine();
                    }
                }

                writer.WriteLine("=== ANALYSIS COMPLETE ===");
            }
        }

        Console.WriteLine("Analysis complete. Output: " + outputFile);
    }

    static void WriteMethodIL(StreamWriter writer, MethodDefinition method)
    {
        if (!method.HasBody)
        {
            writer.WriteLine("No body");
            return;
        }

        writer.WriteLine("Parameters:");
        foreach (var param in method.Parameters)
        {
            writer.WriteLine("  " + param.ParameterType + " " + param.Name);
        }

        writer.WriteLine("IL Code:");
        foreach (var instruction in method.Body.Instructions)
        {
            writer.WriteLine("  " + instruction.Offset.ToString("X4") + ": " + instruction.OpCode + " " + instruction.Operand);
        }
    }
}
