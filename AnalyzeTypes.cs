using System;
using System.IO;
using System.Reflection;
using System.Text;

class AnalyzeTypes
{
    static void Main()
    {
        string dllPath = @"c:\Users\86134\Desktop\AI-zone\Raven-field\Ravenfield_Data\Managed\Assembly-CSharp.dll";
        string outputPath = @"c:\Users\86134\Desktop\AI-zone\Raven-field\types_analysis.txt";
        
        var assembly = Assembly.LoadFrom(dllPath);
        var types = assembly.GetTypes();
        
        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("=== RAVENFIELD TYPE ANALYSIS ===");
            writer.WriteLine("Total types: " + types.Length);
            writer.WriteLine();
            
            AnalyzeType(writer, types, "Actor");
            AnalyzeType(writer, types, "AiActorController");
            AnalyzeType(writer, types, "ActorManager");
            AnalyzeType(writer, types, "Weapon");
            AnalyzeType(writer, types, "Vehicle");
            AnalyzeType(writer, types, "GameManager");
            AnalyzeType(writer, types, "Squad");
            AnalyzeType(writer, types, "SquadManager");
            AnalyzeType(writer, types, "SpawnPoint");
            AnalyzeType(writer, types, "SpawnManager");
            AnalyzeType(writer, types, "AiController");
            AnalyzeType(writer, types, "CoverPoint");
            AnalyzeType(writer, types, "CoverManager");
            AnalyzeType(writer, types, "Loadout");
            AnalyzeType(writer, types, "Seat");
        }
        
        Console.WriteLine("Analysis complete. Output: " + outputPath);
    }
    
    static void AnalyzeType(StreamWriter writer, Type[] types, string typeName)
    {
        Type type = Array.Find(types, t => t.Name == typeName);
        if (type == null)
        {
            writer.WriteLine("=== " + typeName + ": NOT FOUND ===");
            writer.WriteLine();
            return;
        }
        
        writer.WriteLine("=== " + type.FullName + " ===");
        writer.WriteLine("Base: " + (type.BaseType != null ? type.BaseType.Name : "none"));
        
        writer.WriteLine("\n-- Fields --");
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string access = f.IsPublic ? "public" : (f.IsPrivate ? "private" : "protected");
            string stat = f.IsStatic ? " static" : "";
            writer.WriteLine("  " + access + stat + " " + f.FieldType.Name + " " + f.Name);
        }
        
        writer.WriteLine("\n-- Properties --");
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string getter = p.CanRead ? "get;" : "";
            string setter = p.CanWrite ? "set;" : "";
            writer.WriteLine("  " + p.PropertyType.Name + " " + p.Name + " { " + getter + " " + setter + " }");
        }
        
        writer.WriteLine("\n-- Methods --");
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            if (m.IsSpecialName) continue;
            string access = m.IsPublic ? "public" : (m.IsPrivate ? "private" : "protected");
            string stat = m.IsStatic ? " static" : "";
            StringBuilder paramStr = new StringBuilder();
            foreach (var p in m.GetParameters())
            {
                if (paramStr.Length > 0) paramStr.Append(", ");
                paramStr.Append(p.ParameterType.Name + " " + p.Name);
            }
            writer.WriteLine("  " + access + stat + " " + m.ReturnType.Name + " " + m.Name + "(" + paramStr.ToString() + ")");
        }
        
        writer.WriteLine();
    }
}
