using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace UploadFpInfo;

/// <summary>
/// Consolidates the parse/upload process for foolproof dummy sample sheets
/// The model to line database must be populated for insertion validation to succeed
/// </summary>
public class UploadFoolproofToDb
{
    /// <summary>
    /// Main entry point: detect input location argument and delegate to file/batch handler based on whether input location is file or folder
    /// </summary>
    /// <param name="args">Command line arguments, accepts 0-1</param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        try
        {
            string path = args.Length > 0 ? args[0] : Config.InputLocation;
            bool containsDuplicate = false;
            bool containsMiscError = false;
            string duplicateMessage = "One or more files contain duplicate entries. If you wish to update, please do so manually. Otherwise, no action is required.";
            string miscErrorMessage = "One or more files contain invalid data. Scroll up to find out which file(s), and why.";

            if (Directory.Exists(path))
            {
                Config.InputLocation = path;
                (containsDuplicate, containsMiscError) = await RunBatch();
            }
            else if (File.Exists(path) && IsExcelFile(path))
            {
                (containsDuplicate, containsMiscError) = await ProcessFile(path);
                if (containsDuplicate) PrintInColor(duplicateMessage ,ConsoleColor.Cyan);
            }
            else
            {
                PrintInColor($"Path '{path}' is not a valid directory or Excel file. Using Config default ({Config.InputLocation}).", ConsoleColor.Yellow);
            }

            if (containsDuplicate) PrintInColor(duplicateMessage, ConsoleColor.Cyan);
            if (containsMiscError) PrintInColor(miscErrorMessage, ConsoleColor.Yellow);
        }
        catch (Exception ex)
        {
            PrintInColor($"Fatal error: {ex.Message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Process a batch of FP info files
    /// </summary>
    /// <returns>An tuple representing whether the batch contains a file that 1) contains PK collision(s) and 2) has a miscellaneous error</returns>
    /// <exception cref="DirectoryNotFoundException">When the input location does not exist</exception>
    private static async Task<(bool, bool)> RunBatch()
    {
        var inputDir = new DirectoryInfo(Config.InputLocation);
        if (!inputDir.Exists) throw new DirectoryNotFoundException(Config.InputLocation);

        FileInfo[] files = inputDir.GetFiles("*.xlsx")
                            .Concat(inputDir.GetFiles("*.xls"))
                            .OrderBy(f => f.Name)
                            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No Excel files found.");
            return (false, false);
        }

        Console.WriteLine($"Found {files.Length} files. Starting upload to {Config.DbName}...");

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
                PrintInColor($"[SKIP] {ex.Message}", ConsoleColor.Yellow);
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
    private static async Task<(bool, bool)> ProcessFile(string excelPath)
    {
        // Load Excel file
        IWorkbook workbook;
        using (var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            workbook = excelPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? new XSSFWorkbook(fs)
                : new HSSFWorkbook(fs);
        }

        ISheet sheet = workbook.GetSheetAt(Config.SheetIndex)
                    ?? throw new Exception($"Sheet index {Config.SheetIndex} not found.");

        // Extract metadata (header row)
        (byte Revision, DateTime IssueDate, string? Issuer) = ParseMetadata(sheet);

        // Get column indices associated with column names
        Dictionary<string, int> colMap = MapHeaderIndices(sheet);

        // Initialize flags for file processing loop
        bool hasDuplicate = false;
        bool hasMiscError = false;
        bool applyAnotherFilter;

        do
        {
            // Get and validate a model to which this file is to be associated
            Console.WriteLine($"Please enter the C. Core model name for the contents of {Path.GetFileName(excelPath)} to be imported:");
            bool isValidModel;
            string model;
            do // Use a do-while loop to get data and try again on failure
            {
                model = Console.ReadLine()?.Trim() ?? string.Empty;
                isValidModel = await ValidateModel(model);
                if (isValidModel)
                    RewriteInColor(model, ConsoleColor.Green);
                else {
                    RewriteInColor(model, ConsoleColor.Red);
                    Console.WriteLine($"{model} is not a model in the model to line database. Please enter a different model name:");
                }
            } while (!isValidModel);

            Console.WriteLine($"Enter target Excel column name from BM to CJ, R to re-enter model name, or just ENTER to proceed without a filter:");

            bool isFiltering;
            int targetColIndex;
            do
            {
                string filterColumnName = Console.ReadLine() ?? string.Empty;
                targetColIndex = ColumnIndex(filterColumnName);
                isFiltering = true;
                if(targetColIndex < 64 || targetColIndex > 87)
                {
                    if (targetColIndex != -1) {
                        RewriteInColor(filterColumnName, ConsoleColor.Red);
                        Console.WriteLine($"{filterColumnName} is outside the valid range of BM-CJ. Please enter a different column name (or just press ENTER to add no filter):");
                    }
                    isFiltering = false;
                }
            } while(!(targetColIndex == -1 || isFiltering));

            // Initialize DataTable for rows
            DataTable dt = CreateFoolproofDataTable();
            int rowIndex = Config.DataStartRow - 1;
            int emptyStreak = 0;
            int rowsProcessed = 0;

            // Loop through each row in the file, or until we've seen a certain number of empty rows
            while (rowIndex <= sheet.LastRowNum && emptyStreak < Config.EmptyRowLimit)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (IsRowEmpty(row))
                {
                    emptyStreak++;
                    rowIndex++;
                    continue;
                }

                emptyStreak = 0;

                short? dummySampleNum = ExtractPartNumber(GetCellText(row, colMap["DUMMY SAMPLE REQUIRED?"]));

                bool passesFilter = true; // denotes that the current row either fulfills the filter or there is no filter to fulfill
                if (isFiltering)
                {
                    string filterCellValue = GetCellText(row, targetColIndex);
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
                        dr["model"] = model;
                        dr["revision"] = Revision;
                        dr["issueDate"] = IssueDate;
                        dr["issuer"] = (object?)Issuer ?? DBNull.Value;

                        // Get the data for this row
                        dr["failureMode"] = GetCellText(row, colMap["PROCESS FAILURE MODE"]);
                        dr["rank"] = GetCellText(row, colMap["RANK"]);
                        dr["location"] = GetCellText(row, colMap["LOCATION"]);
                        dr["dummySampleNum"] = dummySampleNum;

                        dt.Rows.Add(dr);
                        await WriteRowToDatabase(dr);
                        rowsProcessed++;

                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        if (!hasDuplicate) Console.WriteLine();
                        PrintInColor($"\t[ROW SKIP] Data at row {rowIndex + 1} matches existing rev {Revision} data for {model} for dummy sample #{dummySampleNum}", ConsoleColor.Cyan);
                        hasDuplicate = true;
                    }
                    catch (Exception ex)
                    {
                        if (!hasMiscError) Console.WriteLine();
                        PrintInColor($"\t[ROW SKIP] Error at row {rowIndex + 1}: {ex.Message}", ConsoleColor.Yellow);
                        hasMiscError = true;
                    }
                }
                rowIndex++;
            }

            // Report parse success/failure
            ShowPreview(dt, rowsProcessed);

            Console.Write($"\nWould you like to apply another filter to this same file? (y/n): ");
            string response = Console.ReadLine()?.Trim().ToLower() ?? "";
            applyAnotherFilter = response == "y" || response == "yes";

        } while (applyAnotherFilter);

        return (hasDuplicate, hasMiscError);
    }

    /// <summary>
    /// Prints the contents of <paramref name="dt"/> to the console
    /// </summary>
    /// <param name="dt">The DataTable to display</param>
    /// <param name="rowsProcessed">The number of rows processed</param>
    public static void ShowPreview(DataTable dt, int rowsProcessed)
    {
        if(rowsProcessed == 0){
            PrintInColor("No rows with valid data (under current filters).", ConsoleColor.DarkYellow);
            return;
        }

        Console.WriteLine();
        PrintInColor($"--- UPLOAD SUMMARY: {rowsProcessed} ROWS PROCESSED ---", ConsoleColor.Green);

        // Define column widths for the ASCII table
        int modelWidth = 15;
        int modeWidth = 30;
        int locWidth = 15;
        int dummyWidth = 10;

        // Print table header
        string header = $"| {"Model".PadRight(modelWidth)} | {"Failure Mode".PadRight(modeWidth)} | {"Loc".PadRight(locWidth)} | {"Dummy #".PadRight(dummyWidth)} |";
        string divider = new('-', header.Length);

        Console.WriteLine(divider);
        Console.WriteLine(header);
        Console.WriteLine(divider);

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

            Console.WriteLine($"| {modelStr.PadRight(modelWidth)} | {modeStr.PadRight(modeWidth)} | {locStr.PadRight(locWidth)} | {dummyStr.PadRight(dummyWidth)} |");
        }

        Console.WriteLine(divider);
    }

    /// <summary>
    /// Verifies that a particular model exists in the model to line (MTL) database
    /// </summary>
    /// <param name="toValidate">The model name to validate</param>
    /// <returns>Whether <paramref name="toValidate"/> exists in the MTL database</returns>
    private static async Task<bool> ValidateModel(string? toValidate)
    {
        if(string.IsNullOrWhiteSpace(toValidate)) return false;

        using var conn = new SqlConnection(Config.GetConnectionString());
        await conn.OpenAsync();

        string sql = @"
            SELECT COUNT(*) FROM dbo.ModelToLine
                   WHERE shortDesc LIKE @model";

        using var cmd = new SqlCommand(sql, conn);
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
        int[] metadataIndices = Config.GlobalColumns.Select(ColumnIndex).ToArray();

        string revRaw = GetCellText(dataRow, metadataIndices[0]);
        string dateRaw = GetCellText(dataRow, metadataIndices[1]);
        string issuer = GetCellText(dataRow, metadataIndices[2]);

        byte revision = TranslateRevString(revRaw);

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
    /// Creates a dictionary mapping header names to indices
    /// </summary>
    /// <param name="sheet">The sheet in which the headers reside</param>
    /// <returns>The dictionary mapping header names to header indices</returns>
    private static Dictionary<string, int> MapHeaderIndices(ISheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        IRow headerRow = sheet.GetRow(Config.DataHeaderRow - 1);

        // Required target columns
        string[] targets = ["PROCESS FAILURE MODE", "RANK", "LOCATION", "DUMMY SAMPLE REQUIRED?"];
        foreach (string t in targets) map[t] = -1;

        for (int i = 0; i < headerRow.LastCellNum; i++)
        {
            string val = GetCellText(headerRow, i).ToUpper().Trim();
            if (map.ContainsKey(val)) map[val] = i;
        }

        return map;
    }

    /// <summary>
    /// Get the part master number from an input string
    /// First tries to get a numeric value after the # character, but falls back to any number in the input
    /// If neither work, defaults to null (to denote this entry is irrelvant from a label-making standpoint)
    /// </summary>
    /// <param name="raw">The string to check for part master number</param>
    /// <returns>The part master number as a short, or DBNull if one does not exist</returns>
    private static short? ExtractPartNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        Match match = Regex.Match(raw, @"#(\d+)");
        if (match.Success && short.TryParse(match.Groups[1].Value, out short result))
            return result;

        string digits = new(raw.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(digits) && short.TryParse(digits, out short fallback))
            return fallback;

        return null;
    }

    /// <summary>
    /// Asynchronously writes the input DataRow's contents to the FP info table
    /// </summary>
    /// <param name="dr">The DataRow whose contents will be written to the server</param>
    /// <returns></returns>
    private static async Task WriteRowToDatabase(DataRow dr)
    {
        using var conn = new SqlConnection(Config.GetConnectionString());
        await conn.OpenAsync();

        string sql = @"
            INSERT INTO dbo.FoolproofInfo
            (model, revision, issueDate, issuer, failureMode, rank, location, dummySampleNum)
            VALUES
            (@model, @revision, @issueDate, @issuer, @failureMode, @rank, @location, @dummySampleNum)";

        using var cmd = new SqlCommand(sql, conn);

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

    /// <summary>
    /// Translates the string with revision number info, handling standard aliases
    /// and clipping REV or R to return just the numeric data
    /// </summary>
    /// <param name="rev">The string containing revision information</param>
    /// <returns>A byte representation of the revision number</returns>
    private static byte TranslateRevString(string rev)
    {
        rev = rev.ToUpper();
        if (rev == "ORIG" || rev == "DRAFT") return 0;
        string clean = Regex.Replace(rev, "[REV|R]", "");
        return byte.TryParse(clean, out byte result) ? result : (byte)0;
    }

    /// <summary>
    /// Constructs a DataTable mappable to the table on SQL Server.
    /// The MaxLength attribute ensures no columns overflow before the server is contacted.
    /// </summary>
    /// <returns>A datatable compliant with the column names and datatypes in the FP table</returns>
    private static DataTable CreateFoolproofDataTable()
    {
        DataSet ds = new();
        DataTable dt = ds.Tables.Add("FoolproofInfo");
        dt.Columns.Add("model", typeof(string)).MaxLength = 32;
        dt.Columns.Add("revision", typeof(byte));
        dt.Columns.Add("issueDate", typeof(DateTime));
        dt.Columns.Add("issuer", typeof(string)).MaxLength = 32;
        dt.Columns.Add("failureMode", typeof(string)).MaxLength = 100;
        dt.Columns.Add("rank", typeof(string)).MaxLength = 1;
        dt.Columns.Add("location", typeof(string)).MaxLength = 32;
        dt.Columns.Add("dummySampleNum", typeof(short));

        ds.EnforceConstraints = true;
        return dt;
    }

    /// <summary>
    /// Locates and reads the text of a cell with the specified row-col 'coordinates'
    /// </summary>
    /// <param name="row">The row object containing the desired data (and providing the y-coordinate)</param>
    /// <param name="colIndex">The x-coordinate of the data to get</param>
    /// <returns>A string of the text in the target cell</returns>
    private static string GetCellText(IRow? row, int colIndex)
    {
        if (row == null || colIndex < 0) return "";
        ICell cell = row.GetCell(colIndex);
        if (cell == null) return "";

        if (cell.CellType == CellType.Formula)
            return ResolveCellText(cell, cell.CachedFormulaResultType);

        return ResolveCellText(cell, cell.CellType);
    }

    /// <summary>
    /// Reads the data inside a cell object based on its type
    /// </summary>
    /// <param name="cell">The cell object to read</param>
    /// <param name="type">The datatype in the cell</param>
    /// <returns>A string representation of the data in the specified cell</returns>
    private static string ResolveCellText(ICell cell, CellType type)
    {
        return type switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                                ? cell.DateCellValue?.ToString("yyyy-MM-dd") ?? ""
                                : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.String => cell.StringCellValue ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Verifies whether a row is empty
    /// </summary>
    /// <param name="row">The row for which to check the contents</param>
    /// <returns>Whether the row is empty</returns>
    private static bool IsRowEmpty(IRow? row)
    {
        if (row == null) return true;
        return row.Cells.All(c => string.IsNullOrWhiteSpace(ResolveCellText(c, c.CellType == CellType.Formula ? c.CachedFormulaResultType : c.CellType)));
    }

    /// <summary>
    /// Gets the column number (0-based) of an Excel alpha-column index (e.g. ...Y=25, Z=26, AA=27, AB=28)
    /// Returns -1 in the case of the empty string
    /// </summary>
    /// <remarks>
    /// Excel column enumeration is really just base 26 represented by letters instead of numbers.
    /// </remarks>
    /// <param name="col">The alpha-column index</param>
    /// <returns>The number column index, or -1 for the empty string</returns>
    private static int ColumnIndex(string col)
    {
        int index = 0;
        foreach (char c in col.ToUpper()) index = index * 26 + c - 'A' + 1;
        return index-1;
    }

    /// <summary>
    /// Checks the file extension of <paramref name="path"/> to see if it matches one of the Excel formats
    /// </summary>
    /// <param name="path">The filepath</param>
    /// <returns>Whether <paramref name="path"/> is an Excel file</returns>
    private static bool IsExcelFile(string path) =>
        path.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prints <paramref name="msg"/> in the specified <paramref name="color"/>
    /// </summary>
    /// <param name="msg">The text to be printed</param>
    /// <param name="color">The color in which to print</param>
    private static void PrintInColor(string msg, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    /// <summary>
    /// Overwrites the console contents on the last line with <paramref name="msg"/> in the specified <paramref name="color"/>.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="color"></param>
    private static void RewriteInColor(string msg, ConsoleColor color)
    {
        Console.SetCursorPosition(0, Console.CursorTop-1);
        PrintInColor(msg, color);
    }
}
