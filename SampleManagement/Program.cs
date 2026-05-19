// <copyright file="Program.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

using SampleManagement.Authentication;
using SampleManagement.Components;
using InterProcessIO;

/// <summary>
/// Hosts the application startup and configuration.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments supplied by the host.</param>
    public static void Main(string[] args)
    {
        // Check for environment variables (just DB connection, printing will throw exception later)
        try
        {
            Config.GetConnectionString();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Database Configuration
        builder.Services.AddDbContextFactory<FPSampleDbContext>(options =>
            options.UseSqlServer(Config.GetConnectionString()));

        builder.Services.AddTransient<BlazorInputProvider>();
        builder.Services.AddTransient<IInputProvider>(sp => sp.GetRequiredService<BlazorInputProvider>());
        builder.Services.AddTransient<BlazorReporter>();
        builder.Services.AddTransient<IOutputProvider>(sp => sp.GetRequiredService<BlazorReporter>());

        builder.Services.AddScoped<IUserIdentityService, UserIdentityService>();
        builder.Services.AddMemoryCache();

        // Authentication & Authorization
        builder.Services.AddAuthentication("AutoAuth")
            .AddAutoAuthentication();

        builder.Services.AddAuthorization(); // Required for attribute-based security
        builder.Services.AddAuthenticationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, AutoAuthStateProvider>();

        builder.Services.AddBlazorBootstrap();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        /* --- STATUS CODE HANDLING --- */

        // This tells the server: "If you see a 401 or 403, don't tell the browser yet.
        // Re-run the pipeline at the root path so Blazor can load and handle it."
        app.UseStatusCodePagesWithReExecute("/");

        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
