using System.Data;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using StringBuilder = System.Text.StringBuilder;

using Util = UploadFpInfo.FPUploadUtilities;
namespace UploadFpInfo;

public enum ReportLevel{ INFO, IMPORTANT, WARNING, ERROR, SUCCESS }

public record Report(string Message, ReportLevel Level=ReportLevel.INFO)
{
    public string ToAnsiString()
    {
        string colorCode = Level switch
        {
            ReportLevel.ERROR     => "\u001b[31m", // Red
            ReportLevel.SUCCESS   => "\u001b[32m", // Green
            ReportLevel.WARNING   => "\u001b[33m", // Yellow
            ReportLevel.IMPORTANT => "\u001b[36m", // Cyan
            _                     => "\u001b[37m"  // White
        };
        const string resetCode = "\u001b[0m";
        return $"{colorCode}{Message}{resetCode}";
    }
}

/// <summary>
/// Consolidates the parse/upload process for foolproof dummy sample sheets
/// The model to line database must be populated for insertion validation to succeed
/// </summary>
public class FPSheetUploader(IProgress<Report>? progress)
{
    // Progress update provider, determines where program output goes
    public IProgress<Report>? Progress = progress;

    // Creates a report and passes it on to the Progress instance
    private void Report(string msg, ReportLevel level = ReportLevel.INFO) => Progress?.Report(new(msg,level));

    /// <summary>
    /// Main entry point: initialize Progress to print to console, instantiate an uploader, then
    /// delegate the actual ETL process to the uploader
    /// </summary>
    /// <param name="args">Command line arguments, accepts 0-1</param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        // Initialize the progress manager to print to console
        Progress<Report> consoleProgress = new(report => Console.Write(report.ToAnsiString()));

        // If there was an input location argument, pass it along
        string potentialFile = "";
        if (args.Length > 0) potentialFile = args[0];

        // Exit static by creating an uploader
        FPSheetUploader uploader = new(consoleProgress);

        // Defaults to the input location in config
        await uploader.ExecuteAsync(potentialFile);
    }

    /// <summary>
    /// Identifies input location and whether it is a folder/file, then delegates to the batch/file handler
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(string filename)
    {
        string path = filename;
        bool containsDuplicate = false;
        bool containsMiscError = false;
        string duplicateMessage = "One or more files contain duplicate entries. If you wish to update, please do so manually. Otherwise, no action is required.";
        string miscErrorMessage = "One or more files contain invalid data. Scroll up to find out which file(s), and why.";

        try
        {
            if (!Path.Exists(path))
            {
                Report($"Path '{path}' is not a valid directory or Excel file. Using Config default ({Config.InputLocation}).\n", ReportLevel.WARNING);
            }
            else if (Directory.Exists(path))
            {
                (containsDuplicate, containsMiscError) = await RunBatch(path);
            }
            else if (File.Exists(path) && Util.IsExcelFile(path))
            {
                (containsDuplicate, containsMiscError) = await ProcessFile(path);
            }
            else
            {
                Report($"Could not find {path}. Please verify the path is correct, then try again.");
            }

            if (containsDuplicate) Report(duplicateMessage, ReportLevel.IMPORTANT);
            if (containsMiscError) Report(miscErrorMessage, ReportLevel.WARNING);
        }
        catch (Exception ex)
        {
            Report($"Fatal error: {ex.Message}", ReportLevel.ERROR);
        }
    }

    /// <summary>
    /// Processes a batch of FP info files
    /// </summary>
    /// <returns>An tuple representing whether the batch contains a file that 1) contains PK collision(s) and 2) has a miscellaneous error</returns>
    /// <exception cref="DirectoryNotFoundException">When the input location does not exist</exception>
    private async Task<(bool, bool)> RunBatch(string directoryPath)
    {
        DirectoryInfo inputDir = new(directoryPath);

        FileInfo[] files = inputDir.GetFiles("*.xlsx")
                            .Concat(inputDir.GetFiles("*.xls"))
                            .OrderBy(f => f.Name)
                            .ToArray();

        if (files.Length == 0)
        {
            Report("No Excel files found.", ReportLevel.ERROR);
            return (false, false);
        }

        Report($"Found {files.Length} files. Starting upload to {Config.DbName}...\n");

        bool currentContainsDuplicate = false;
        bool currentContainsMisc = false;
        bool batchContainsDuplicate = false;
        bool batchContainsMisc = false;
        foreach (FileInfo file in files)
        {
            try
            {
                (currentContainsDuplicate, currentContainsMisc) = await ProcessFile(file.FullName);

                // Assign batch & misc duplicate flag to current if it isn't already set (OR is short-circuiting so this is fast)
                batchContainsDuplicate = batchContainsDuplicate || currentContainsDuplicate;
                batchContainsMisc = batchContainsMisc || currentContainsMisc;
            }
            catch (Exception ex)
            {
                Report($"\t[SKIP] {ex.Message}\n", ReportLevel.WARNING);
                batchContainsMisc=true;
            }
        }
        return (batchContainsDuplicate, batchContainsMisc);
    }

    /// <summary>
    /// Processes one FP info file
    /// </summary>
    /// <param name="excelPath">The path to the file to be processed</param>
    /// <returns>A Task with a duplicate flag and a miscellaneous error flag</returns>
    /// <exception cref="Exception">When the file does not have a sheet at the specified index</exception>
    private async Task<(bool, bool)> ProcessFile(string excelPath)
    {
        // Load Excel file
        IWorkbook workbook;
        using (FileStream fs = new(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            workbook = excelPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? new XSSFWorkbook(fs)
                : new HSSFWorkbook(fs);
        }

        ISheet sheet = workbook.GetSheetAt(Config.SheetIndex)
                    ?? throw new Exception($"Sheet index {Config.SheetIndex} not found.\n");

        // Extract metadata (header row)
        (byte Revision, DateTime IssueDate, string? Issuer) = ParseMetadata(sheet);

        // Get column indices associated with column names
        Dictionary<string, int> colMap = Util.MapHeaderIndices(sheet);

        // Initialize flags for error detection and intention to repeat
        bool hasDuplicate = false;
        bool hasMiscError = false;
        bool applyAnotherFilter = false;

        // Start the loop for applying multiple filters (run at least once)
        do
        {
            (string Model, bool IsFiltering, int targetColIndex) = await CollectUserInput(Path.GetFileName(excelPath));
            if (Model.Equals("SKIP", StringComparison.OrdinalIgnoreCase)) return (hasDuplicate, hasMiscError);

            // Initialize DataTable for rows
            DataTable dt = Util.CreateFoolproofDataTable();
            int rowIndex = Config.DataStartRow - 1;
            int emptyStreak = 0;
            int rowsProcessed = 0;

            // Loop through each row in the file, or until we've seen a certain number of empty rows
            while (rowIndex <= sheet.LastRowNum && emptyStreak < Config.EmptyRowLimit)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (Util.IsRowEmpty(row))
                {
                    emptyStreak++;
                    rowIndex++;
                    continue;
                }

                emptyStreak = 0;

                short? dummySampleNum = Util.ExtractPartNumber(Util.GetCellText(row, colMap["DUMMY SAMPLE REQUIRED?"]));

                bool passesFilter = true; // denotes that the current row either fulfills the filter or there is no filter to fulfill
                if (IsFiltering)
                {
                    string filterCellValue = Util.GetCellText(row, targetColIndex);
                    if (string.IsNullOrWhiteSpace(filterCellValue))
                    {
                        passesFilter = false;
                    }
                }

                // Only add (and count) the row if it has a dummy sample associated with it (otherwise it is irrelevant for label making purposes)
                if (dummySampleNum != null && passesFilter)
                {
                    try
                    {
                        DataRow dr = dt.NewRow();

                        // Assign metadata
                        dr["model"] = Model;
                        dr["revision"] = Revision;
                        dr["issueDate"] = IssueDate;
                        dr["issuer"] = (object?)Issuer ?? DBNull.Value;

                        // Get the data for this row
                        dr["failureMode"] = Util.GetCellText(row, colMap["PROCESS FAILURE MODE"]).Replace("\n", "");
                        dr["rank"] = Util.GetCellText(row, colMap["RANK"]);
                        dr["location"] = Util.GetCellText(row, colMap["LOCATION"]);
                        dr["dummySampleNum"] = dummySampleNum;

                        dt.Rows.Add(dr);
                        await WriteRowToDatabase(dr);
                        rowsProcessed++;

                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        if (!hasDuplicate) Report("\n");
                        Report($"\t[ROW SKIP] Data at row {rowIndex + 1} matches existing rev {Revision} data for {Model} for dummy sample #{dummySampleNum}\n", ReportLevel.IMPORTANT);
                        hasDuplicate = true;
                    }
                    catch (Exception ex)
                    {
                        if (!hasMiscError) Report("\n");
                        Report($"\t[ROW SKIP] Error at row {rowIndex + 1}: {ex.Message}\n", ReportLevel.WARNING);
                        hasMiscError = true;
                    }
                }
                rowIndex++;
            }

            // Report parse success/failure
            if (Progress != null) ShowPreview(dt, rowsProcessed);

            if (IsFiltering)
            {
                Report($"\n\tWould you like to apply another filter to this same file/reuse this file's contents for another model? (y/n): ");
                string response = Console.ReadLine()?.Trim().ToLower() ?? "";
                applyAnotherFilter = response == "y" || response == "yes";
            }

        } while (applyAnotherFilter);

        return (hasDuplicate, hasMiscError);
    }

    /// <summary>
    /// Asks the user for C. Core model (mandatory) and column filter (optional), looping until valid input is provided
    /// </summary>
    /// <param name="filename">The name of the file provided by the user</param>
    /// <returns>A tuple representing the model, whether there is a filter, and the target column number</returns>
    private async Task<(string, bool, int)> CollectUserInput(string filename)
    {
        string model;
        bool isFiltering = false;
        int targetColIndex = -1;

        // Get and validate a model to which this file is to be associated
        while (true)
        {
            Report($"{new Report(filename, ReportLevel.IMPORTANT).ToAnsiString()}: Please enter the C. Core model name for the contents to be imported (or type 'SKIP' to proceed to the next file):\n\t");

            bool isValidModel;
            do // Use a do-while loop to get model data and try again on failure
            {
                model = Console.ReadLine()?.Trim() ?? string.Empty;
                if (model.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
                {
                    Report($"\tSkipping file: {filename}\n", ReportLevel.WARNING);
                    return (model, isFiltering, targetColIndex);
                }
                isValidModel = await ValidateModel(model);
                if (!isValidModel)
                    Report($"\t{model} is not a model in the model to line database. Please enter a different model name (or 'SKIP'):\n\t", ReportLevel.WARNING);
            } while (!isValidModel);

            Report($"\tEnter target Excel column name from BM to CJ, 'R' to re-enter model name, or just ENTER to proceed without a filter:\n\t");
            bool restartRequested = false;

            do
            {
                string filterColumnName = Console.ReadLine()?.Trim() ?? string.Empty;
                if (filterColumnName.Equals("R", StringComparison.OrdinalIgnoreCase))
                {
                    Report("\tReturning to model specification for this file...\n");
                    restartRequested = true; // Throw flag so outer loop knows to try again
                    break; // Only breaks the inner loop
                }

                targetColIndex = Util.ColumnIndex(filterColumnName);
                isFiltering = true;

                if(targetColIndex < 64 || targetColIndex > 87)
                {
                    if (targetColIndex != -1) {
                        Report($"\t{filterColumnName} is outside the valid range. Please enter a column name from BM-CJ, 'R' to re-enter model name, or ENTER to add no filter):\n\t", ReportLevel.WARNING);
                    }
                    isFiltering = false;
                }
            } while(!(targetColIndex == -1 || isFiltering));

            // Exit the loop if if
            if (!restartRequested) break;
        }
        return (model, isFiltering, targetColIndex);
    }

    /// <summary>
    /// Prints the contents of <paramref name="dt"/> to the console
    /// </summary>
    /// <param name="dt">The DataTable to display</param>
    /// <param name="rowsProcessed">The number of rows processed</param>
    public void ShowPreview(DataTable dt, int rowsProcessed)
    {
        // If there's no output defined, skip the preview entirely
        if (Progress==null) return;

        // If there's no content to print, tell the user and exit
        if(rowsProcessed == 0){
            Report("\tNo rows with valid data (under current filters).", ReportLevel.WARNING);
            return;
        }

        StringBuilder sb = new();

        sb.Append(new Report($"\t--- UPLOAD SUMMARY: {rowsProcessed} ROWS PROCESSED ---\n\t", ReportLevel.SUCCESS).ToAnsiString());

        // Define column widths for the ASCII table
        int modelWidth = 15;
        int modeWidth = 45;
        int locWidth = 15;
        int dummyWidth = 5;

        // Print table header
        string header = $"| {"Model".PadRight(modelWidth)} | {"Failure Mode".PadRight(modeWidth)} | {"Loc".PadRight(locWidth)} | {"Dummy #".PadRight(dummyWidth)} |\n\t";
        string divider = $"{new('-', header.Length)}\n\t";

        sb.Append(divider);
        sb.Append(header);
        sb.Append(divider);

        // Print each row from the DataTable
        foreach (DataRow row in dt.Rows)
        {
            string modelStr = row["model"]?.ToString()?.Length > modelWidth
                ? string.Concat(row["model"].ToString().AsSpan(0, modelWidth - 3), "...")
                : row["model"]?.ToString() ?? "";

            string modeStr = row["failureMode"]?.ToString()?.Length > modeWidth
                ? string.Concat(row["failureMode"].ToString().AsSpan(0, modeWidth - 3), "...")
                : row["failureMode"]?.ToString() ?? "";

            string locStr = row["location"]?.ToString()?.Length > locWidth
                ? string.Concat(row["location"].ToString().AsSpan(0, locWidth - 3), "...")
                : row["location"]?.ToString() ?? "";

            string dummyStr = row["dummySampleNum"]?.ToString() ?? "";

            string line = $"| {modelStr.PadRight(modelWidth)} | {modeStr.PadRight(modeWidth)} | {locStr.PadRight(locWidth)} | {dummyStr.PadRight(dummyWidth)} |\n\t";
            sb.Append(new Report(line).ToAnsiString());
        }

        sb.Append(divider.TrimEnd('\t'));

        Progress.Report(new(sb.ToString()));
    }

    /// <summary>
    /// Verifies that a particular model exists in the model to line (MTL) database
    /// </summary>
    /// <param name="toValidate">The model name to validate</param>
    /// <returns>Whether <paramref name="toValidate"/> exists in the MTL database</returns>
    private static async Task<bool> ValidateModel(string? toValidate)
    {
        if(string.IsNullOrWhiteSpace(toValidate)) return false;

        using SqlConnection conn = new(Config.GetConnectionString());
        await conn.OpenAsync();

        string sql = @"
            SELECT COUNT(*) FROM dbo.ModelToLine
                   WHERE shortDesc LIKE @model";

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@model", toValidate);

        int count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        return count > 0;
    }

    /// <summary>
    /// Gets the revision, issue date and issuer from the file header
    /// </summary>
    /// <param name="sheet">The sheet to be parsed</param>
    /// <returns>A tuple containing the desired metadata</returns>
    private static (byte Revision, DateTime IssueDate, string Issuer) ParseMetadata(ISheet sheet)
    {
        IRow dataRow = sheet.GetRow(Config.GlobalStartRow - 1);
        int[] metadataIndices = Config.GlobalColumns.Select(Util.ColumnIndex).ToArray();

        string revRaw = Util.GetCellText(dataRow, metadataIndices[0]);
        string dateRaw = Util.GetCellText(dataRow, metadataIndices[1]);
        string issuer = Util.GetCellText(dataRow, metadataIndices[2]);

        byte revision = Util.TranslateRevString(revRaw);

        // Clean common Excel date string artifacts
        string cleanDate = dateRaw.Replace("th", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace("st", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace("nd", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace("rd", "", StringComparison.OrdinalIgnoreCase);

        if (!DateTime.TryParse(cleanDate, out DateTime issueDate))
            issueDate = DateTime.MinValue;

        return (revision, issueDate, issuer);
    }

    /// <summary>
    /// Asynchronously writes the input DataRow's contents to the FP info table
    /// </summary>
    /// <param name="dr">The DataRow whose contents will be written to the server</param>
    /// <returns></returns>
    private static async Task WriteRowToDatabase(DataRow dr)
    {
        using SqlConnection conn = new(Config.GetConnectionString());
        await conn.OpenAsync();

        string sql = @"
            INSERT INTO dbo.FoolproofInfo
            (model, revision, issueDate, issuer, failureMode, rank, location, dummySampleNum)
            VALUES
            (@model, @revision, @issueDate, @issuer, @failureMode, @rank, @location, @dummySampleNum)";

        using SqlCommand cmd = new(sql, conn);

        // Mapping parameters from the DataRow
        cmd.Parameters.AddWithValue("@model", dr["model"]);
        cmd.Parameters.AddWithValue("@revision", dr["revision"]);
        cmd.Parameters.AddWithValue("@issueDate", dr["issueDate"]);
        cmd.Parameters.AddWithValue("@issuer", dr["issuer"]);
        cmd.Parameters.AddWithValue("@failureMode", dr["failureMode"]);
        cmd.Parameters.AddWithValue("@rank", dr["rank"]);
        cmd.Parameters.AddWithValue("@location", dr["location"]);
        cmd.Parameters.AddWithValue("@dummySampleNum", dr["dummySampleNum"]);

        await cmd.ExecuteNonQueryAsync();
    }
}
