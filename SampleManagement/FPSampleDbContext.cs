using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleManagement;
/// <summary>
/// Represents the state of the database in a way friendly to EFCore
/// </summary>
/// <param name="options">The server details and login credentials</param>
public class FPSampleDbContext(DbContextOptions<FPSampleDbContext> options) : DbContext(options)
{
    // One set per table, MUST match table names
    public DbSet<FoolproofEntry> FoolproofInfo { get; set; }
    public DbSet<ModelLine> ModelToLine { get; set; }
}

[PrimaryKey(nameof(Model), nameof(Revision), nameof(Location), nameof(PartMasterNum))]
public class FoolproofEntry
{
    [Column("model")]
    public string Model { get; set; }

    [Column("revision")]
    public byte Revision { get; set; }

    [Column("issueDate")]
    public DateTime IssueDate { get; set; }

    [Column("issuer")]
    public string? Issuer { get; set; }

    [Column("failureMode")]
    public string FailureMode { get; set; }

    [Column("rank")]
    public string Rank { get; set; }

    [Column("location")]
    public string Location { get; set; }

    [Column("partMasterNum")]
    public short PartMasterNum { get; set; }
}

[PrimaryKey(nameof(IcsNum), nameof(WorkCenterCode))]
public class ModelLine
{
    [Column("icsNum")]
    public string IcsNum { get; set; }

    [Column("shortDesc")]
    public string? ShortDescription { get; set; }

    [Column("prodCellCode")]
    public string? ProdCellCode { get; set; }

    [Column("workCenterCode")]
    public string WorkCenterCode { get; set; }

    [Column("fullDesc")]
    public string? FullDescription { get; set; }
}
