using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BooticeWinUI.Services
{
    public class VhdService
    {
        // Executes diskpart scripts
        private async Task RunDiskpartAsync(string script)
        {
            // Write script to temp file
            string tempScript = Path.GetTempFileName();
            File.WriteAllText(tempScript, script);

            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{tempScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        // Diskpart errors are usually in stdout too
                        throw new Exception($"Diskpart failed:\n{output}\n{error}");
                    }
                    
                    // Check output for keywords like "failed" because diskpart sometimes returns 0 even on failure
                    if (output.Contains("DiskPart has encountered an error", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Diskpart error: {output}");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempScript))
                    File.Delete(tempScript);
            }
        }

        public async Task AttachVhdAsync(string path, bool readOnly)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("VHD file not found", path);

            string script = $"select vdisk file=\"{path}\"\n";
            if (readOnly)
                script += "attach vdisk readonly\n";
            else
                script += "attach vdisk\n";
            
            await RunDiskpartAsync(script);
        }

        public async Task DetachVhdAsync(string path)
        {
             if (!File.Exists(path)) throw new FileNotFoundException("VHD file not found", path);
             
             string script = $"select vdisk file=\"{path}\"\ndetach vdisk\n";
             await RunDiskpartAsync(script);
        }

        public async Task CreateVhdAsync(string path, long sizeMb, bool isVhdx, bool isFixed)
        {
            // create vdisk file="c:\test.vhd" maximum=1000 type=fixed
            string type = isFixed ? "fixed" : "expandable";
            
            string script = $"create vdisk file=\"{path}\" maximum={sizeMb} type={type}\n";
            // For VHDX, extension determines it usually, but diskpart handles it.
            // If user provides .vhdx extension, diskpart creates vhdx.
            // If .vhd, it creates vhd.
            
            // Ensure path has correct extension if we want to enforce format
            // But usually we trust the path provided by UI which checks format.
            
            await RunDiskpartAsync(script);
        }
    }
}
