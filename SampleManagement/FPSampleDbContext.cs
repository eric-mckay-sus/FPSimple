using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleManagement;
/// <summary>
/// Represents the state of the database in a way friendly to EFCore
/// </summary>
/// <param name="options">The server details and login credentials</param>
public class FPSampleDbContext(DbContextOptions<FPSampleDbContext> options) : DbContext(options)
{
    // One set per table, MUST match table names
}
