using CodeReview.API.Extensions;
using CodeReview.API.Middleware;
using CodeReview.Application;
using CodeReview.Infrastructure;
using CodeReview.Infrastructure.Services;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CodeReview AI API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "CodeReviewAI")
            .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    );

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services
        .AddSwagger()
        .AddJwtAuthentication(builder.Configuration)
        .AddCorsPolicy(builder.Configuration)
        .AddHealthChecksConfiguration(builder.Configuration);

    builder.Services.AddControllers();

    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.GetLevel = (ctx, elapsed, ex) => ex != null || ctx.Response.StatusCode > 499
            ? LogEventLevel.Error
            : elapsed > 1000 ? LogEventLevel.Warning
            : LogEventLevel.Information;
    });

    app.UseResponseCompression();
    app.UseCors("AllowAngularClient");
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts =>
        {
            opts.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeReview AI API v1");
            opts.RoutePrefix = string.Empty; 
        });
    }

    app.MapControllers();
    app.MapHub<AnalysisHub>("/hubs/analysis")
        .RequireAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = hc => hc.Tags.Contains("infrastructure"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CodeReview.Infrastructure.Persistence.AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
