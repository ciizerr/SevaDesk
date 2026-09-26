using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Services;

public class ChecklistVerificationResult
{
    public List<ApplicationChecklistItem> Checklist { get; set; } = [];
    public bool AllDocsReady => Checklist.Count > 0 && Checklist.All(x => x.IsCompleted);
    public int CompletedCount => Checklist.Count(x => x.IsCompleted);
    public int TotalCount => Checklist.Count;
    public string SuggestedStatus => AllDocsReady ? "Docs Ready" : "Draft";
}

public static class ApplicationDocumentVerifier
{
    private static readonly Dictionary<string, string[]> DocumentAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aadhaar", new[] { "aadhaar", "adhar", "uidai", "aadhar" } },
        { "pan", new[] { "pan", "pancard" } },
        { "photo", new[] { "photo", "pic", "passport", "image", "avatar" } },
        { "sign", new[] { "sign", "signature", "hastakshar", "sign_eng", "sign_hindi" } },
        { "marksheet", new[] { "marksheet", "result", "10th", "12th", "matric", "inter", "diploma", "degree", "grad", "scorecard" } },
        { "voter", new[] { "voter", "epic", "election" } },
        { "ration", new[] { "ration", "rashan" } },
        { "bank", new[] { "bank", "passbook", "statement", "cheque" } },
        { "income", new[] { "income", "aay", "income_cert" } },
        { "caste", new[] { "caste", "jati", "caste_cert" } },
        { "domicile", new[] { "domicile", "niwas", "resident", "residence", "domicile_cert" } },
        { "cert", new[] { "cert", "certificate", "praman" } }
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "card", "copy", "original", "front", "back", "document", "docs", "doc", "xerox", "scan", "file", "of", "and", "the"
    };

    public static async Task<ChecklistVerificationResult> VerifyChecklistAsync(
        ApplicationItem app,
        string? workingFolderPath = null,
        string? backupFolderPath = null)
    {
        var result = new ChecklistVerificationResult();
        if (app == null || string.IsNullOrWhiteSpace(app.RequiredDocs))
        {
            return result;
        }

        // 1. Resolve folder paths if not provided
        if (string.IsNullOrWhiteSpace(workingFolderPath) && !string.IsNullOrWhiteSpace(app.CustomerId))
        {
            var customer = await AppServices.Customers.GetByIdAsync(app.CustomerId);
            if (customer != null)
            {
                workingFolderPath = AppServices.FolderManager.GetCustomerFolderPath(customer.Name, customer.Code);
                backupFolderPath = AppServices.FolderManager.GetCustomerBackupFolderPath(customer.Name, customer.Code);
            }
        }

        // 2. Collect all available customer files
        var availableFiles = new List<string>();
        if (!string.IsNullOrWhiteSpace(workingFolderPath) && Directory.Exists(workingFolderPath))
        {
            try
            {
                availableFiles.AddRange(Directory.GetFiles(workingFolderPath, "*.*", SearchOption.TopDirectoryOnly));

                // Check scheme subfolder if exists
                if (!string.IsNullOrWhiteSpace(app.Title))
                {
                    var subfolder = Path.Combine(workingFolderPath, app.Title);
                    if (Directory.Exists(subfolder))
                    {
                        availableFiles.AddRange(Directory.GetFiles(subfolder, "*.*", SearchOption.TopDirectoryOnly));
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(backupFolderPath) && Directory.Exists(backupFolderPath))
        {
            try
            {
                availableFiles.AddRange(Directory.GetFiles(backupFolderPath, "*.*", SearchOption.TopDirectoryOnly));
            }
            catch { }
        }

        // 3. Match each required document against files
        var docs = app.RequiredDocs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            var item = new ApplicationChecklistItem
            {
                Title = doc,
                Category = "Document"
            };

            var matchedFile = FindMatchingFile(doc, availableFiles, usedFiles);
            if (!string.IsNullOrWhiteSpace(matchedFile))
            {
                item.IsCompleted = true;
                item.MatchedFileName = Path.GetFileName(matchedFile);
                usedFiles.Add(matchedFile);
            }
            else
            {
                // If application was already marked completed, retain checkmark
                item.IsCompleted = app.Status == "Completed";
                item.MatchedFileName = null;
            }

            result.Checklist.Add(item);
        }

        return result;
    }

    public static async Task<ChecklistVerificationResult> CheckAndAutoUpdateStatusAsync(
        ApplicationItem app,
        string? workingFolderPath = null,
        string? backupFolderPath = null)
    {
        var result = await VerifyChecklistAsync(app, workingFolderPath, backupFolderPath);
        if (app == null) return result;

        // Auto-transitions apply only to non-completed applications
        if (!string.Equals(app.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            if (result.AllDocsReady && !string.Equals(app.Status, "Docs Ready", StringComparison.OrdinalIgnoreCase))
            {
                app.Status = "Docs Ready";
                app.UpdatedAt = DateTime.UtcNow;
                await AppServices.Applications.UpdateAsync(app);
            }
            else if (!result.AllDocsReady && string.Equals(app.Status, "Docs Ready", StringComparison.OrdinalIgnoreCase))
            {
                app.Status = "Draft";
                app.UpdatedAt = DateTime.UtcNow;
                await AppServices.Applications.UpdateAsync(app);
            }
        }

        return result;
    }

    public static async Task CompleteApplicationAsync(ApplicationItem app)
    {
        if (app == null) return;
        app.Status = "Completed";
        app.UpdatedAt = DateTime.UtcNow;
        await AppServices.Applications.UpdateAsync(app);
    }

    private static string? FindMatchingFile(string docTitle, List<string> files, HashSet<string> usedFiles)
    {
        var titleLower = docTitle.Trim().ToLowerInvariant();

        // 1. Check alias groups
        foreach (var (groupKey, aliases) in DocumentAliases)
        {
            bool docMentionsGroup = aliases.Any(a => titleLower.Contains(a));
            if (docMentionsGroup)
            {
                var candidate = files.FirstOrDefault(f =>
                    !usedFiles.Contains(f) &&
                    aliases.Any(a => Path.GetFileNameWithoutExtension(f).Contains(a, StringComparison.OrdinalIgnoreCase)));

                if (candidate != null) return candidate;
            }
        }

        // 2. Check significant words in document title
        var titleWords = Regex.Split(titleLower, @"[^\w]+")
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .ToList();

        if (titleWords.Count > 0)
        {
            var candidate = files.FirstOrDefault(f =>
            {
                if (usedFiles.Contains(f)) return false;
                var fileNameOnly = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                return titleWords.Any(w => fileNameOnly.Contains(w));
            });

            if (candidate != null) return candidate;
        }

        // 3. Check normalized tag match
        var tagKey = SmartTagHelper.NormalizeTagKey(docTitle);
        var tagCandidate = files.FirstOrDefault(f =>
            !usedFiles.Contains(f) &&
            Path.GetFileNameWithoutExtension(f).Contains(tagKey, StringComparison.OrdinalIgnoreCase));

        return tagCandidate;
    }
}
