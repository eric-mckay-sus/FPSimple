// <copyright file="CreateSample.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages;
using Microsoft.EntityFrameworkCore;
using ToastType = BlazorBootstrap.ToastType;

using PrintLabel;
using InterProcessIO;
using System.Net.Sockets;

/// <summary>
/// Code-behind for the CreateSample page.
/// </summary>
public partial class CreateSample : TableManager<Sample>
{
    /// <summary>
    /// List of all entries in the MTL table.
    /// </summary>
    private IList<ModelLine> allMappings = [];

    /// <summary>
    /// The pending sample to be added upon validation.
    /// </summary>
    private SampleFormData formData = new ();

    // Filtered lists to use for autofill

    /// <summary>
    /// The list of all models available for the current line.
    /// </summary>
    private IList<string> availableModels = [];

    /// <summary>
    /// The list of all lines available for the current model.
    /// </summary>
    private IList<string> availableLines = [];

    /// <summary>
    /// The list of all dummy sample numbers avaiable for the current model.
    /// </summary>
    private List<short> availableSampleNums = [];

    private string lastModel = string.Empty;

    // UI properties

    /// <summary>
    /// The DPI to with which to print samples.
    /// </summary>
    private int printDpi = 203;

    /// <summary>
    /// The number of samples successfully printed in the current batch.
    /// </summary>
    private int printed = 0;

    /// <summary>
    /// The current batch size.
    /// </summary>
    private int totalFromQueue = 0;

    /// <summary>
    /// Flag to expand/collapse sample form.
    /// </summary>
    private bool isFormExpanded = false;

    /// <summary>
    /// Flag to switch between normal view and print select view.
    /// </summary>
    private bool printModeEngaged = false;

    /// <summary>
    /// Flag to prevent double-clicks while a print is processing.
    /// </summary>
    private bool isPrinting = false;

    /// <summary>
    /// The list of samples selected for printing.
    /// Could swap out List for HashSet, but the benefit here is that execution order matches selection order.
    /// </summary>
    private List<Sample> selectedForPrint = [];

    /// <summary>
    /// Error message about pending sample, if applicable.
    /// </summary>
    private string? errorMessage;

    /// <summary>
    /// Allows for cancelling mid-print.
    /// </summary>
    private CancellationTokenSource? printCts;

    /// <summary>
    /// Gets the message to display when <see cref="TableManager{T}.DataView"/> is empty.
    /// </summary>
    public override string EmptyMessage => "No samples matching these filters.";

    /// <summary>
    /// Gets a value indicating whether the sample form is ready for a dummy sample number.
    /// </summary>
    private bool NotReadyForSampleNum =>
        string.IsNullOrWhiteSpace(this.formData.Model) ||
        string.IsNullOrWhiteSpace(this.formData.WorkCenterCode);

    /// <summary>
    /// Gets a value indicating whether the sample form is ready for associate signature.
    /// </summary>
    private bool NotReadyForSignature =>
        this.NotReadyForSampleNum ||
        this.formData.DummySampleNum < 1;

    /// <summary>
    /// Resets the filters and fetches the sample table.
    /// </summary>
    /// <param name="keepPage"><inheritdoc/></param>
    /// <returns>A Task representing that data has been successfully refreshed.</returns>
    public override async Task RefreshData(bool keepPage = false)
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();

        // Fetch the mapping table once to handle bidirectional filtering in memory
        this.allMappings = await context.ModelToLine.ToListAsync();

        // Initialize the UI lists with everything
        this.availableModels = this.allMappings.Select(m => m.Model).Distinct().OrderBy(x => x).ToList();
        this.availableLines = this.allMappings.Select(m => m.Line).Distinct().OrderBy(x => x).ToList();

        await base.RefreshData(keepPage);
    }

    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.SortList.Add(new ("CreationDate", SortDir.Desc));
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Filters out samples samples that are inactive and approved, so they are not inadvertently printed.
    /// Also applies model/line filters if applicable.
    /// </summary>
    /// <param name="query"><inheritdoc/></param>
    /// <returns>The <paramref name="query"/> where remake date is null.</returns>
    protected override IQueryable<Sample> ApplyFilters(IQueryable<Sample> query)
    {
        query = query.Where(s => s.IsActive || s.ApproverNum == null);

        if (this.ModelFilter.Value != null && this.ModelFilter.IsActive)
        {
            query = query.Where(x => x.Model.Contains(this.ModelFilter.Value));
        }

        if (this.LineFilter.Value != null && this.LineFilter.IsActive)
        {
            query = query.Where(x => x.Line.Contains(this.LineFilter.Value));
        }

        return query;
    }

    /// <summary>
    /// Navigates to the remake page with the desired sample.
    /// </summary>
    /// <param name="sample">The sample for which to request a remake.</param>
    private void HandleNavigateToRemake(Sample sample) => this.Navigation.NavigateTo($"/request-remake?sampleId={sample.SampleId}");

    /// <summary>
    /// Filters the autofill lists based on what fields in the add form have values.
    /// </summary>
    /// <returns>A Task representing that filters have been refreshed.</returns>
    private async Task RefreshFilters()
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();

        // Normalize inputs to handle casing and extra whitespace
        string searchModel = this.formData.Model.Trim();
        string searchLine = this.formData.WorkCenterCode.Trim();

        bool hasModel = !string.IsNullOrEmpty(searchModel);
        bool hasLine = !string.IsNullOrEmpty(searchLine);

        // These manual state checks look inefficient, but they are very readable, and the compiler optimizes them.
        switch (hasModel, hasLine)
        {
            // If there's no model or line, clear any existing filters
            case (false, false):
                this.availableLines = this.allMappings.Select(m => m.Line).Distinct().OrderBy(x => x).ToList();
                this.availableModels = this.allMappings.Select(m => m.Model).Distinct().OrderBy(x => x).ToList();
                break;

            // If line is selected, use it for filtering the models
            case (false, true):
                this.availableModels = this.allMappings
                    .Where(x => x.Line == searchLine)
                    .Select(x => x.Model)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                break;

            // If model is selected, use it for filtering the lines
            case (true, false):
                this.availableLines = this.allMappings
                    .Where(x => x.Model == searchModel)
                    .Select(x => x.Line)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                break;
        }

        // Update sample numbers when model is selected
        if (hasModel)
        {
            // Only hit DB when it's a new model
            if (searchModel != this.lastModel)
            {
                this.availableSampleNums = await context.FoolproofInfo
                    .Where(f => f.Model == searchModel)
                    .Select(f => f.DummySampleNum)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
                this.lastModel = searchModel;
            }
        }
        else
        {
            this.availableSampleNums.Clear();
            this.formData.DummySampleNum = 0;
        }
    }

    private void TogglePrintMode()
    {
        this.printModeEngaged = !this.printModeEngaged;
        if (!this.printModeEngaged)
        {
            this.selectedForPrint.Clear(); // ensure selections do not persist between prints
        }
    }

    /// <summary>
    /// Remove add form flag, clear input, error message and autofill list filters.
    /// </summary>
    private void CloseForm()
    {
        this.isFormExpanded = false;
        this.formData = new ();
        this.errorMessage = null;

        // Reload available lists
        this.availableModels = this.allMappings.Select(m => m.Model).Distinct().OrderBy(x => x).ToList();
        this.availableLines = this.allMappings.Select(m => m.Line).Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Attempts to run the stored procedure with the current form input, populating error message as necessary.
    /// </summary>
    /// <returns>A Task representing successful submission.</returns>
    private async Task HandleSubmit()
    {
        this.errorMessage = null; // Ensure any error messages are for this submission

        try
        {
            using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();

            // ExecuteSqlInterpolatedAsync internally wraps each parameter in an injection-safe DbParameter
            await context.Database.ExecuteSqlInterpolatedAsync($@"
            EXEC [dbo].[CreateSample]
                @model = {this.formData.Model},
                @workCenterCode = {this.formData.WorkCenterCode},
                @dummySampleNum = {this.formData.DummySampleNum},
                @creatorNum = {this.formData.CreatorNum}");

            this.formData = new (); // Reset form
            await this.RefreshData();
            this.isFormExpanded = false; // Auto-collapse on success to show the table
            this.ToastService.Notify(new (ToastType.Success, "Sample created successfully!"));
        }
        catch (Exception ex)
        {
            this.errorMessage = $"Database Error: {ex.Message}";
            this.ToastService.Notify(new (ToastType.Danger, "Sample creation failed."));
        }
    }

    /// <summary>
    /// Prints one sample.
    /// </summary>
    /// <param name="sample">The <see cref="Sample"/> to print.</param>
    /// <returns>A Task representing that the print request has been issued (toast reports actual status).</returns>
    private async Task HandlePrint(Sample sample)
    {
        this.isPrinting = true;
        try
        {
            ZplCommand cmd = new () { SampleId = sample.SampleId, PrintDpi = this.printDpi };
            ZebraUploadPrint zupObject = new (this.InputProvider, this.Reporter);
            Report statusReport = await zupObject.ExecuteAsync(cmd);
            if (statusReport.level == ReportLevel.SUCCESS)
            {
                this.ToastService.Notify(new (ToastType.Success, $"Sample {sample.SampleId} sent to printer."));
            }
            else
            {
                this.ToastService.Notify(new (ToastType.Danger, statusReport.message));
            }
        }
        catch (Exception ex)
        {
            this.ToastService.Notify(new (ToastType.Danger, $"Print failed: {ex.Message}"));
        }
        finally
        {
            this.isPrinting = false;
        }
    }

    /// <summary>
    /// Prints all samples in <see cref="selectedForPrint"/>, batching over one TCP connection.
    /// </summary>
    /// <returns>A Task representing that all print requests have been issued.</returns>
    private async Task HandlePrint()
    {
        this.isPrinting = true;
        this.printCts = new ();
        this.totalFromQueue = this.selectedForPrint.Count;
        HashSet<Sample> failedSamples = []; // Takes up some more space, but cuts a future query

        using TcpClient conn = new ();
        try
        {
            await conn.ConnectAsync(PrintLabel.Config.PrinterIp, PrintLabel.Config.PrinterPort, this.printCts.Token);

            foreach (Sample sample in this.selectedForPrint)
            {
                // If the printer is mid-print, let it finish the current label before canceling
                this.printCts.Token.ThrowIfCancellationRequested();

                // Create a print request for each sample
                ZplCommand cmd = new () { SampleId = sample.SampleId, PrintDpi = this.printDpi };
                ZebraUploadPrint zupObject = new (this.InputProvider, this.Reporter);
                Report statusReport = await zupObject.ExecuteAsync(cmd, conn, leaveOpen: true);
                if (statusReport.level == ReportLevel.SUCCESS)
                {
                    this.ToastService.Notify(new (ToastType.Success, $"Sample #{sample.SampleId} sent to printer."));
                    this.printed++;
                }
                else
                {
                    this.ToastService.Notify(new (ToastType.Danger, $"Sample {sample.SampleId}: {statusReport.message}"));
                    failedSamples.Add(sample);
                }

                await Task.Delay(PrintLabel.Config.InterPrintDelayMs, this.printCts.Token); // Wait a second between prints to ensure each toast is visible and that printer isn't overloaded
            }

            // By setting selectedForPrint to only the failed IDs, the user can see easily which samples to investigate
            this.selectedForPrint = failedSamples.ToList();

            // If no prints failed, inform the user and exit print mode
            if (this.selectedForPrint.Count == 0)
            {
                this.ToastService.Notify(new (ToastType.Success, $"Successfully printed all {this.printed} samples!"));
                this.printModeEngaged = false;
            }

            // Otherwise, tell the user which prints failed
            else if (this.selectedForPrint.Count == this.totalFromQueue)
            {
                this.ToastService.Notify(new (ToastType.Danger, "Total Failure", "All prints failed."));
            }
            else
            {
                this.ToastService.Notify(new (ToastType.Warning,
                                            $"Printed {this.printed} of {this.printed + failedSamples.Count} samples (unsuccessful prints still selected)",
                                            $"Failed to print samples: {string.Join(", ", failedSamples.Select(s => s.SampleId))}"));
            }
        }

        // Go here when the user cancels a batch print
        catch (OperationCanceledException)
        {
            this.ToastService.Notify(new (ToastType.Warning, $"Print batch cancelled after {this.printed + failedSamples.Count} of {this.totalFromQueue} labels."));
        }

        // Have to handle Socket & IO exceptions here because this component owns the TCP connection
        catch (SocketException e)
        {
            this.ToastService.Notify(new (ToastType.Danger, $"Error connecting to printer: {e.Message}"));
        }
        catch (IOException e)
        {
            this.ToastService.Notify(new (ToastType.Danger, $"Error executing the print command: {e.Message}"));
        }
        finally
        {
            if (conn.Connected)
            {
                conn.Close();
            }

            this.printCts.Dispose();
            this.printCts = null;
            this.isPrinting = false;
        }
    }

    /// <summary>
    /// Represents the data enclosed in the sample addition form
    /// </summary>
    public record SampleFormData
    {
        /// <summary>
        /// Gets or sets the new sample's model.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new sample's work center code (building and line name).
        /// </summary>
        public string WorkCenterCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new sample's dummy sample number.
        /// </summary>
        public short DummySampleNum { get; set; } = 0;

        /// <summary>
        /// Gets or sets the new sample's creator name.
        /// </summary>
        public string CreatorNum { get; set; } = string.Empty;
    }
}
