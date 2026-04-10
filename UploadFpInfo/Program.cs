using System.Data;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

using Util = UploadFpInfo.FPUploadUtilities;
namespace UploadFpInfo;

/// <summary>
/// Consolidates the parse/upload process for foolproof dummy sample sheets
/// The model to line database must be populated for insertion validation to succeed
/// </summary>
public class FPSheetUploader
{
    // Determines where user input comes from
    public IInputProvider Input;

    // Determines where/how program output is displayed
    public IReportOutputProvider Output;

    /// <summary>
    /// The default constructor. Uses console for both input and output
    /// </summary>
    public FPSheetUploader()
    {
        Input = new ConsoleInputProvider();
        Output = new ConsoleReporter();
    }

    public FPSheetUploader(IInputProvider inputProvider, IReportOutputProvider outputProvider)
    {
        Input = inputProvider;
        Output = outputProvider;
    }

    // Creates a report and passes it on to the Progress instance
    private async Task Report(string msg, ReportLevel level = ReportLevel.INFO) => await Output.ReportAsync(new(msg,level));

    /// <summary>
    /// Main entry point: Instantiate an uploader using the default constructor to
    /// print to the console, then delegate the actual ETL process to the uploader
    /// </summary>
    /// <param name="args">Command line arguments, accepts an optional file path</param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        // If there was an input location argument, pass it along
        string? potentialFile = null;
        if (args.Length > 0) potentialFile = args[0];

        // Exit static by creating an uploader
        FPSheetUploader uploader = new();

        // Defaults to the input location in config
        await uploader.ExecuteAsync(potentialFile);
    }

    /// <summary>
    /// Identifies input location and whether it is a folder/file, then delegates to the batch/file handler.
    /// Recommended entry point for other programs which use this one.
    /// </summary>
    /// <param name="filename">An optional file path to override the one found in config</param>
    /// <returns></returns>
    public async Task ExecuteAsync(string? filename=null)
    {
        // Need to perform this check again because
        string path = Config.InputLocation;
        if (string.IsNullOrWhiteSpace(filename)) await Report($"No file argument detected. Defaulting to config file input location ({Config.InputLocation})\n");
        else path = filename;

        bool containsDuplicate = false;
        bool containsMiscError = false;
        string duplicateMessage = "One or more files contain duplicate entries. If you wish to update, please do so manually. Otherwise, no action is required.";
        string miscErrorMessage = "One or more files contain invalid data. Scroll up to find out which file(s), and why.";

        try
        {
            if (!Path.Exists(path))
            {
                await Report($"Path '{path}' is not a valid directory or Excel file. Using Config default ({Config.InputLocation}).\n", ReportLevel.WARNING);
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
                await Report($"Could not find {path}. Please verify the path is correct, then try again.");
            }

            if (containsDuplicate) await Report(duplicateMessage, ReportLevel.IMPORTANT);
            if (containsMiscError) await Report(miscErrorMessage, ReportLevel.WARNING);
        }
        catch (Exception ex)
        {
            await Report($"Fatal error: {ex.Message}", ReportLevel.ERROR);
        }
    }

    /// <summary>
    /// Processes a batch of FP info files
    /// </summary>
    /// <returns>An tuple representing whether the batch contains a file that 1) contains PK collision(s) and 2) has a miscellaneous error</returns>
    private async Task<(bool, bool)> RunBatch(string directoryPath)
    {
        DirectoryInfo inputDir = new(directoryPath);

        FileInfo[] files = inputDir.GetFiles("*.xlsx")
                            .Concat(inputDir.GetFiles("*.xls"))
                            .OrderBy(f => f.Name)
                            .ToArray();

        if (files.Length == 0)
        {
            await Report("No Excel files found.", ReportLevel.ERROR);
            return (false, false);
        }

        await Report($"Found {files.Length} files. Starting upload to {Config.DbName}...\n");

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
                await Report($"\t[SKIP] {ex.Message}\n", ReportLevel.WARNING);
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
        // Load Excel file, grab the sheet, then close the Excel file
        ISheet sheet;
        using (FileStream fs = new(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (IWorkbook workbook = excelPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XSSFWorkbook(fs) : new HSSFWorkbook(fs))
        {
            sheet = workbook.GetSheetAt(Config.SheetIndex)
                    ?? throw new Exception($"Sheet index {Config.SheetIndex} not found.\n");
        }

        // Extract metadata (header row)
        (byte Revision, DateTime IssueDate, string? Issuer) = ParseMetadata(sheet);

        // Get column indices associated with column names
        Dictionary<string, int> colMap = Util.MapHeaderIndices(sheet);

        // Initialize flags for error detection and intention to repeat
        bool hasDuplicate = false;
        bool hasMiscError = false;
        bool applyAnotherFilter = false;
        bool isNewFile = true;

        // One DB connection to be used across all rows of this file
        using SqlConnection conn = new(Config.GetConnectionString());
        await conn.OpenAsync();

        // Start the loop for applying multiple filters (run at least once)
        do
        {
            (string Model, bool IsFiltering, int targetColIndex) = await CollectUserInput(Path.GetFileName(excelPath), isNewFile);
            if (Model.Equals("SKIP", StringComparison.OrdinalIgnoreCase)) return (hasDuplicate, hasMiscError);
            else isNewFile = true; // For the next iteration

            // Initialize DataTable for rows
            DataTable dt = Util.CreateFoolproofDataTable();
            int rowIndex = Config.DataStartRow - 1;
            int emptyStreak = 0;

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
                        await WriteRowToDatabase(dr, conn);

                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        if (!hasDuplicate) await Report("\n");
                        await Report($"\t[ROW SKIP] Data at row {rowIndex + 1} matches existing rev {Revision} data for {Model} for dummy sample #{dummySampleNum}\n", ReportLevel.IMPORTANT);
                        hasDuplicate = true;
                    }
                    catch (Exception ex)
                    {
                        if (!hasMiscError) await Report("\n");
                        await Report($"\t[ROW SKIP] Error at row {rowIndex + 1}: {ex.Message}\n", ReportLevel.WARNING);
                        hasMiscError = true;
                    }
                }
                rowIndex++;
            }

            // Report parse success/failure
            await Output.ShowPreview(dt);

            if (IsFiltering)
            {
                applyAnotherFilter = await Input.GetConfirmAsync(new("\tWould you like to apply another filter/reuse this file for another model?"));
                isNewFile = !applyAnotherFilter;
            }
            else
                applyAnotherFilter = false;

        } while (applyAnotherFilter);

        return (hasDuplicate, hasMiscError);
    }

    /// <summary>
    /// Asks the user for C. Core model (mandatory) and column filter (optional), looping until valid input is provided
    /// </summary>
    /// <param name="filename">The name of the file provided by the user</param>
    /// <param name="lastModel">The most recent model name</param>
    /// <returns>A tuple representing the model, whether there is a filter, and the target column number</returns>
    private async Task<(string, bool, int)> CollectUserInput(string filename, bool isNewModel)
    {
        string model = string.Empty;
        bool isFiltering = false;
        int targetColIndex = -1;

        // This outer loop controls redirects to the model prompt (i.e. bad model name or 'R' in response to the column prompt)
        while (true)
        {
            await Report($"{(isNewModel ? "[NEW]" : "[REPEAT]")} {filename}\n", ReportLevel.IMPORTANT);
            Report modelPrompt = new("\tPlease enter the C. Core model name for the contents to be imported (or type 'SKIP' to proceed to the next file):");
            model = (await Input.GetInputAsync(modelPrompt)).Trim();

            if (model.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
            {
                await Report($"\tSkipping file: {filename}\n", ReportLevel.WARNING);
                return (model, isFiltering, targetColIndex);
            }

            if (!await ValidateModel(model))
            {
                await Report($"\t{model} is not in the model to line database. Please try again.\n", ReportLevel.WARNING);
                isNewModel = false;
                continue;
            }

            // This inner loop controls redirects to the column prompt (i.e. bad column )
            while (true)
            {
                string colPrompt = $"\t[{model}] Enter Excel column name (BM-CJ), 'R' to change model, or ENTER for no filter:";
                string filterColumnName = (await Input.GetInputAsync(new(colPrompt))).Trim();

                if (filterColumnName.Equals("R", StringComparison.OrdinalIgnoreCase))
                { // Signal that this is a repeat, then repeat by breaking the inner loop, redirecting to outer loop
                    isNewModel = false;
                    break;
                }

                targetColIndex = Util.ColumnIndex(filterColumnName);

                if (string.IsNullOrEmpty(filterColumnName))
                {
                    isFiltering = false;
                    return (model, isFiltering, -1);
                }

                if (targetColIndex >= 64 && targetColIndex <= 87)
                {
                    isFiltering = true;
                    return (model, isFiltering, targetColIndex);
                }

                await Report($"\t{filterColumnName} is out of range. Please try again.\n", ReportLevel.WARNING);
            }
        }
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
    private static async Task WriteRowToDatabase(DataRow dr, SqlConnection conn)
    {
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
