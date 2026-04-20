// <copyright file="Program.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace PrintLabel;

using System.Text;
using System.Collections.Generic;
using Zebra.Sdk.Comm;
using Zebra.Sdk.Printer;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

/// <summary>
/// A simple test program to connect to a ZPL printer and upload a template to the printer or print a sample by ID.
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">The command line arguments specifying whether to upload, print, or both (and which file/sample ID).</param>
    /// <returns>A Task representing program completion.</returns>
    public static async Task Main(string[] args)
    {
        // The program requires at least one argument
        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        // Immediately identify and verify the first argument (must be upload or print)
        string modeArg = args[0].ToUpper();
        bool isUpload = modeArg.Contains("UPLOAD");
        bool isPrint = modeArg.Contains("PRINT");

        // If the first argument doesn't specify upload/print, cut it off early
        if (!(isUpload || isPrint))
        {
            ShowUsage();
            return;
        }

        // Create a list from the array to consume remaining arguments
        List<string> remainingArgs = [.. args.Skip(1)];
        string fileName = Config.InputLocation;
        int sampleId = -1;

        // By only consuming the next argument if in print mode, we guarantee the last one is the filename (if present)
        if (isPrint)
        {
            if (remainingArgs.Count == 0)
            {
                Console.WriteLine("PRINT mode requires a sample ID argument.");
                ShowUsage();
                return;
            }

            if (!int.TryParse(remainingArgs[0], out sampleId))
            {
                Console.WriteLine($"Sample ID '{remainingArgs[0]}' is not an integer. Please try again.");
                return;
            }

            remainingArgs.RemoveAt(0);
        }

        // If no file name provided, fall back on config default
        if (isPrint && args.Length < 3)
        {
            Console.WriteLine($"No input file name detected. Defaulting to Config file input location ({Config.InputLocation}).");
            fileName = Config.InputLocation;
        }

        // Filename argument is optional, so only validate if it was provided
        if (remainingArgs.Count > 0)
        {
            string providedFile = remainingArgs[0];
            if (File.Exists(providedFile))
            {
                fileName = providedFile;
            }
            else
            {
                Console.WriteLine($"File '{providedFile}' not found. Using config file default: {Config.InputLocation}");
            }
        }

        // If there wasn't a filename argument, there's no problem, but inform the user of the implied filename
        else
        {
            Console.WriteLine($"No file specified. Using default: {Config.InputLocation}");
        }

        // Establish connection with printer regardless of command
        TcpConnection tcpConn = new (Config.GetPrinterIp(), TcpConnection.DEFAULT_ZPL_TCP_PORT);

        await ExecuteAsync(isUpload, isPrint, fileName, tcpConn, new (Config.GetConnectionString()), sampleId);
    }

    /// <summary>
    /// Uploads/prints <paramref name="fileName"/> to the connected ZPL printer.
    /// </summary>
    /// <param name="isUpload">Whether to upload <paramref name="fileName"/>.</param>
    /// <param name="isPrint">Whether to print <paramref name="fileName"/> from printer's internal memory.</param>
    /// <param name="fileName">The file name to upload/print (if printing without upload, file must already be in printer memory).</param>
    /// <param name="tcpConn">The TCP connection to the printer.</param>
    /// <param name="sqlConn">The connection to the SQL database.</param>
    /// <param name="sampleId">The sample ID (if printing).</param>
    /// <returns>A Task representing that the upload/print is complete.</returns>
    public static async Task ExecuteAsync(bool isUpload, bool isPrint, string fileName, TcpConnection tcpConn, SqlConnection sqlConn, int sampleId = -1)
    {
        if (!tcpConn.Connected)
        {
            tcpConn.Open();
        }

        ZebraPrinter printer = ZebraPrinterFactory.GetInstance(tcpConn);

        try
            {
            if (isUpload)
            {
                // TODO load template from local file
                string zplTemplate = @"
                    ^XA
                    ^DFR:FPSAMPLE203.ZPL^FS
                    ^CI28
                    ^LH3,3
                    ^FO0,0^GB603,197,3^FS
                    ^FO0,13^AF,26^FB80,,,C^FN1^FS
                    ^FO80,13^AF,26^FB462,,,C^FN2^FS
                    ^FO542,13^AF,26^FB60,,,C^FN3^FS
                    ^XZ";

                // If the ZPL doesn't contain a DF command (to switch the printer to download mode), don't send it over.
                if (!zplTemplate.Contains("^DF"))
                {
                    Console.WriteLine($"{fileName} does not have a download command and would print immediately. Canceling upload...");
                    return;
                }

                // Send template to printer memory (execute the download command printer-side)
                tcpConn.Write(Encoding.UTF8.GetBytes(zplTemplate));
            }

            if (isPrint)
            {
                // Map ^FN numbers to values
                Dictionary<int, string> fields = await SampleMapFromId(sampleId, sqlConn);

                Console.WriteLine(fields.Values.Count);

                if (fields.Values.Count != 10)
                {
                    Console.WriteLine($"{sampleId} is not the ID of a sample in the database. Please try again.");
                    return;
                }

                StringBuilder sb = new ();

                // Recall and print
                sb.Append($"^XA^XF{fileName}");
                foreach (KeyValuePair<int, string> entry in fields)
                {
                    sb.Append($"^FN{entry.Key}^FD{entry.Value}^FS");
                }

                sb.Append("^XZ");

                tcpConn.Write(Encoding.UTF8.GetBytes(sb.ToString()));
            }
        }
        catch (ConnectionException e)
        {
            Console.WriteLine($"Printer Error: {e.Message}");
        }
        finally
        {
            tcpConn.Close();
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run <UPLOAD|PRINT|UPLOAD-PRINT> [sample ID] [file_path.zpl]");
    }

    /// <summary>
    /// Queries the sample table by target ID and collects the info necessary to fill out a sample label.
    /// </summary>
    /// <param name="id">The sample serial number.</param>
    /// <param name="conn">The connection to the SQL database.</param>
    /// <returns>A dictionary mapping field numbers (for the ZPL template) to field data (from the database).</returns>
    private static async Task<Dictionary<int, string>> SampleMapFromId(int id, SqlConnection conn)
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        var fieldMap = new Dictionary<int, string>();

        // Define the query to pull fields required by the ZPL template
        string query = @"
            SELECT
                dummySampleNum, model, rank, workCenterCode,
                iteration, creationDate, failureMode, location,
                creatorName, approverName
            FROM Samples
            WHERE sampleID = @id";

        using (SqlCommand cmd = new (query, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    // Helper to format strings with the ZPL centering suffix
                    static string Format(object value) => $"{value?.ToString() ?? string.Empty}\\&";

                    // Mapping database columns to ZPL ^FN indices
                    fieldMap.Add(1,  Format(reader["dummySampleNum"]));
                    fieldMap.Add(2,  Format(reader["model"]));
                    fieldMap.Add(3,  Format(reader["rank"]));
                    fieldMap.Add(4,  Format(id));
                    fieldMap.Add(5,  Format(reader["workCenterCode"]));
                    fieldMap.Add(6,  Format(reader["iteration"]));
                    fieldMap.Add(7,  Format(((DateTime)reader["creationDate"]).ToString("MM/dd/yyyy")));
                    fieldMap.Add(8,  Format(reader["failureMode"]));
                    fieldMap.Add(9,  Format(reader["location"]));
                    fieldMap.Add(10, Format(reader["creatorName"]));
                }
            }
        }

        return fieldMap;
    }
}
