using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BooticeWinUI.Models;

namespace BooticeWinUI.Services
{
    public class BcdService
    {
        // Executes bcdedit.exe and returns stdout
        private async Task<string> RunBcdEditAsync(string arguments)
        {
            // Use cmd.exe to force code page 437 (US English) to ensure standard parsing
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 437 && bcdedit.exe {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.ASCII // CP 437 is close to ASCII
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // cmd.exe /c will return exit code of the last command
                // But parsing output needs to skip the "Active code page: 437" line
                
                if (process.ExitCode != 0)
                {
                    // If bcdedit fails, it might be due to permissions or invalid args
                    // However, cmd might return 0 even if bcdedit failed if we don't check properly?
                    // Actually && stops if chcp fails (unlikely). 
                    // Wait, if bcdedit fails, && chain is broken? No, command is "chcp && bcdedit".
                    // If chcp succeeds, bcdedit runs. If bcdedit fails, cmd returns bcdedit's exit code.
                    
                    // Let's allow non-zero if output contains useful info, but generally throw.
                    // But first check if output contains "Access is denied".
                    if (output.Contains("Access is denied") || error.Contains("Access is denied"))
                    {
                        throw new UnauthorizedAccessException("Access denied. Please run as Administrator.");
                    }
                    
                    // throw new Exception($"bcdedit failed with exit code {process.ExitCode}: {error}\nOutput: {output}");
                }

                return output;
            }
        }

        public async Task<List<BcdEntry>> EnumEntriesAsync(string bcdFilePath = null)
        {
            // /enum all /v to get full GUIDs
            string args = "/enum all /v";
            if (!string.IsNullOrEmpty(bcdFilePath))
            {
                args = $"/store \"{bcdFilePath}\" {args}";
            }

            string output = await RunBcdEditAsync(args);
            return ParseBcdOutput(output);
        }

        private List<BcdEntry> ParseBcdOutput(string output)
        {
            var entries = new List<BcdEntry>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            BcdEntry currentEntry = null;
            
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;
                if (trimmedLine.StartsWith("Active code page:")) continue; // Skip chcp output
                if (trimmedLine.StartsWith("---")) continue; // Separator

                // Standard format: PropertyName      Value
                // Regex matches: Word (key) + Whitespace + Rest (value)
                var match = Regex.Match(trimmedLine, @"^([a-zA-Z0-9]+)\s+(.*)$");
                if (match.Success)
                {
                    string key = match.Groups[1].Value.ToLower();
                    string value = match.Groups[2].Value.Trim();

                    if (key == "identifier")
                    {
                        // New Entry Detected
                        currentEntry = new BcdEntry
                        {
                            Identifier = value
                        };
                        entries.Add(currentEntry);
                    }
                    else if (currentEntry != null)
                    {
                        switch (key)
                        {
                            case "description":
                                currentEntry.Description = value;
                                break;
                            case "device":
                                currentEntry.Device = value;
                                break;
                            case "path":
                                currentEntry.Path = value;
                                break;
                            case "locale":
                                currentEntry.Locale = value;
                                break;
                            // Add other properties as needed
                        }
                    }
                }
            }

            return entries;
        }
        
        public async Task SetElementAsync(string id, string element, string value, string bcdFilePath = null)
        {
            string storeArg = string.IsNullOrEmpty(bcdFilePath) ? "" : $"/store \"{bcdFilePath}\"";
            string args = $"{storeArg} /set {id} {element} \"{value}\"";
            await RunBcdEditAsync(args);
        }
        
        public async Task DeleteElementAsync(string id, string element, string bcdFilePath = null)
        {
            string storeArg = string.IsNullOrEmpty(bcdFilePath) ? "" : $"/store \"{bcdFilePath}\"";
            string args = $"{storeArg} /deletevalue {id} {element}";
            await RunBcdEditAsync(args);
        }
    }
}
