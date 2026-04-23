// <copyright file="UploadZplTemplate.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace PrintLabel;

using System.Text;
using System.Net.Sockets;

using InterProcessIO;

/// <summary>
/// Defines methods used to upload a new ZPL template file.
/// </summary>
public static class UploadZplTemplate
{
    /// <summary>
    /// Prompts and validates for an upload path.
    /// </summary>
    /// <param name="uploadCmd">The <see cref="ZplCommand"/> in which to assign the upload path.</param>
    /// <param name="input">The <see cref="IInputProvider"/> from which to prompt input.</param>
    /// <returns>A Task representing that the upload path has been provided.</returns>
    public static async Task PromptUpload(ZplCommand uploadCmd, IInputProvider input)
    {
        // TODO want to ask for explicit filepath from console, but offer file input to Blazor. New method in IInputProvider?
        // User probably wants to pick file for upload the majority of the time
        string potentialUploadPath;
        string? error;
        do
        {
            error = null; // Don't persist error from last iteration
            potentialUploadPath = await input.GetInputAsync(new ("Please enter the filename of the template ZPL to upload (or just press ENTER to use the config file default): "), error);

            // Set error message if applicable (cheapest check first, first hit holds)
            if (!Path.GetExtension(potentialUploadPath).Equals(".zpl"))
            {
                error = $"Path '{potentialUploadPath}' is not a ZPL file. Please try again";
            }
            else if (!File.Exists(potentialUploadPath))
            {
                error = $"File '{potentialUploadPath}' was not found on this computer. Please try again.";
            }
        }
        while (error != null);

        // Leave on default if empty
        if (!string.IsNullOrWhiteSpace(potentialUploadPath))
        {
            uploadCmd.UploadPath = potentialUploadPath;
        }
    }

    /// <summary>
    /// Validates and uploads a file to the printer watching for data from <paramref name="stream"/>.
    /// </summary>
    /// <param name="uploadCmd">The <see cref="ZplCommand"/> containing print information (to attach print path shortcut in an upload-print).</param>
    /// <param name="stream">The <see cref="NetworkStream"/> to the printer.</param>
    /// <returns>A <see cref="Report"/> detailing the success/failure of the upload.</returns>
    public static async Task<Report> UploadAsync(ZplCommand uploadCmd, NetworkStream stream)
    {
        // Simple ZPL files are only ever a handful of kilobytes, so verify length, then grab it all for upload without memory concerns.
        FileInfo fileInfo = new (uploadCmd.UploadPath);
        int kbSize = Convert.ToInt32(fileInfo.Length / 1024);

        if (kbSize > Config.KbLimit)
        {
            return new Report($"{uploadCmd.UploadPath} exceeds the size limit of {Config.KbLimit}KB. Canceling upload...", ReportLevel.ERROR);
        }

        string toUpload = File.ReadAllText(uploadCmd.UploadPath);

        // If the ZPL doesn't contain a DF command (to switch the printer to download mode), don't send it over.
        if (!toUpload.Contains("^DF"))
        {
            return new Report($"{uploadCmd.UploadPath} does not have a download command and would print immediately. Canceling upload...", ReportLevel.ERROR);
        }

        // Get the print path "shortcut" (for an upload-print) from the template file itself
        if (uploadCmd.IsPrint)
        {
            string startMarker = "^DF";
            string endMarker = "^FS";

            int pFrom = toUpload.IndexOf(startMarker) + startMarker.Length;
            int pTo = toUpload.IndexOf(endMarker, pFrom);

            uploadCmd.PrintPath = toUpload[pFrom..pTo];
        }

        // Send template to printer memory (execute the download command printer-side)
        await stream.WriteAsync(Encoding.UTF8.GetBytes(toUpload));

        return new Report("Upload successful!", ReportLevel.SUCCESS);
    }
}
