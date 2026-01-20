using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BooticeWinUI.Models;

namespace BooticeWinUI.Services
{
    public class UefiService
    {
        private readonly BcdService _bcdService;

        public UefiService()
        {
            // Reusing the private helper from BcdService would be nice, 
            // but for now we'll duplicate the process execution or refactor BcdService to expose it.
            // Let's refactor BcdService to be more open or just inherit? 
            // Composition is better. But BcdService methods are private.
            // Let's just create a new helper here for simplicity in this turn.
            _bcdService = new BcdService();
        }

        // We need to execute bcdedit.
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
                StandardOutputEncoding = System.Text.Encoding.ASCII
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                // Note: bcdedit might return non-zero for some query empty results, handle if needed
                return output;
            }
        }

        public async Task<List<UefiEntry>> EnumFirmwareEntriesAsync()
        {
            // /enum firmware /v
            string output = await RunBcdEditAsync("/enum firmware /v");
            return ParseUefiOutput(output);
        }

        private List<UefiEntry> ParseUefiOutput(string output)
        {
            var entries = new List<UefiEntry>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            UefiEntry currentEntry = null;
            
            // "Firmware Boot Manager" block contains "displayorder"
            // Individual "Firmware Application" blocks contain details.

            // First pass: just get all blocks
            
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;
                if (trimmedLine.StartsWith("Active code page:")) continue; // Skip chcp output
                if (trimmedLine.StartsWith("---")) continue; // Separator

                var match = Regex.Match(trimmedLine, @"^([a-zA-Z0-9]+)\s+(.*)$");
                if (match.Success)
                {
                    string key = match.Groups[1].Value.ToLower();
                    string value = match.Groups[2].Value.Trim();

                    if (key == "identifier")
                    {
                        currentEntry = new UefiEntry { Identifier = value };
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
                        }
                    }
                }
            }
            
            // Filter: Only keep "Firmware Application" entries (usually starting with {fwbootmgr} is the manager)
            // Actually, we want the entries that appear in the boot order.
            // But listing all is fine for now.
            // The identifier for UEFI entries usually looks like {GUID}.
            // The Firmware Boot Manager is {fwbootmgr}.
            
            return entries;
        }

        public async Task SetBootOrderAsync(List<string> orderedIds)
        {
            // bcdedit /set {fwbootmgr} displayorder {id1} {id2} ...
            if (orderedIds == null || orderedIds.Count == 0) return;
            
            string ids = string.Join(" ", orderedIds);
            string args = $"/set {{fwbootmgr}} displayorder {ids}";
            
            await RunBcdEditAsync(args);
        }
        
        public async Task SetTopAsync(string id)
        {
            // bcdedit /set {fwbootmgr} displayorder {id} /addfirst
            string args = $"/set {{fwbootmgr}} displayorder {id} /addfirst";
            await RunBcdEditAsync(args);
        }
    }
}
