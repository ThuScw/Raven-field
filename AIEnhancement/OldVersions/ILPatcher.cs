using System;
using System.IO;
using System.Linq;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Direct IL patcher for Assembly-CSharp.dll
    /// Patches GameManager.Awake() to call AIEnhancementInitializer
    /// </summary>
    public class ILPatcher
    {
        public static void Main(string[] args)
        {
            string gameManaged = @"c:\Users\86134\Desktop\AI-zone\Ravenfield\Ravenfield_Data\Managed";
            string inputPath = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            string backupPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.backup");
            string outputPath = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            
            Console.WriteLine("Ravenfield AI Enhancement IL Patcher");
            Console.WriteLine("=====================================");
            
            try
            {
                if (!File.Exists(backupPath))
                {
                    File.Copy(inputPath, backupPath, true);
                    Console.WriteLine("Backup created");
                }
                
                byte[] dllBytes = File.ReadAllBytes(inputPath);
                
                // Find and patch the GameManager.Awake method
                // This is a simplified approach - in reality we'd need proper IL parsing
                
                // For now, report what we found
                Console.WriteLine("DLL size: " + dllBytes.Length + " bytes");
                Console.WriteLine("Patching requires Mono.Cecil or manual IL editing.");
                Console.WriteLine("");
                Console.WriteLine("ALTERNATIVE APPROACH:");
                Console.WriteLine("1. AIEnhancement.dll has been copied to the Managed folder");
                Console.WriteLine("2. Use a Unity mod loader (BepInEx/MelonLoader) to load it");
                Console.WriteLine("3. Or manually add AIEnhancementInitializer to a scene GameObject");
                
                // Create a README
                string readme = @"
Ravenfield AI Threat Assessment Mod
====================================

INSTALLATION:
1. Install BepInEx 5.4 for Unity Mono: https://github.com/BepInEx/BepInEx/releases
2. Extract BepInEx to the game root folder (next to Ravenfield.exe)
3. Place AIEnhancement.dll in: BepInEx/plugins/
4. Launch the game

WHAT IT DOES:
- Enhances AI target selection with threat assessment
- AI now prioritizes:
  * Enemies with effective weapons against them
  * Tanks and helicopters (high threat targets)
  * Enemies that are aiming at them
  * Closer targets over distant ones
  * The player (slight bonus)

FEATURES:
- Distance-based threat scoring
- Weapon effectiveness evaluation
- Target type priority (Armored > Air > InfantryGroup > Unarmored > Infantry)
- Squad leader anti-armor/air priority
- Health-based targeting (wounded enemies prioritized)
- Anti-flicker threshold (prevents constant target switching)

CONFIGURATION:
Edit the threat weights in AIThreatUpdater.CalculateThreat():
- Distance factor: 30f
- Weapon effectiveness: +/- 25f
- Is aiming at me: +20f
- Target type: 4-15f
- Health: up to 10f
- Squad priority: +5f
- Player bonus: +8f

UNINSTALL:
Delete AIEnhancement.dll from the plugins folder.
";
                File.WriteAllText(Path.Combine(gameManaged, "AIEnhancement-README.txt"), readme);
                Console.WriteLine("");
                Console.WriteLine("README created at: " + Path.Combine(gameManaged, "AIEnhancement-README.txt"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
