using System;
using System.IO;
using System.Reflection;

class AnalyzeEnums
{
    static void Main()
    {
        string dllPath = @"c:\Users\86134\Desktop\AI-zone\Raven-field\Ravenfield_Data\Managed\Assembly-CSharp.dll";
        string outputPath = @"c:\Users\86134\Desktop\AI-zone\Raven-field\enums_analysis.txt";
        
        var assembly = Assembly.LoadFrom(dllPath);
        var types = assembly.GetTypes();
        
        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("=== ENUM ANALYSIS ===");
            writer.WriteLine();
            
            foreach (var t in types)
            {
                if (t.IsEnum)
                {
                    writer.WriteLine("Enum: " + t.FullName);
                    foreach (var name in Enum.GetNames(t))
                    {
                        writer.WriteLine("  " + name + " = " + (int)Enum.Parse(t, name));
                    }
                    writer.WriteLine();
                }
            }
        }
        
        Console.WriteLine("Analysis complete. Output: " + outputPath);
    }
}
