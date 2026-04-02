using CodeReview.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace CodeReview.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CodeReview AI API",
                Version = "v1",
                Description = "Automated AI-powered code review platform. " +
                              "Analyzes code for SOLID violations, anti-patterns, complexity, and readability.",
                Contact = new OpenApiContact { Name = "CodeReview AI", Email = "support@codereview.ai" }
            });

            // JWT Bearer security definition
            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token: **Bearer {token}**",
                Reference = new OpenApiReference { Id = JwtBearerDefaults.AuthenticationScheme, Type = ReferenceType.SecurityScheme }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { jwtScheme, Array.Empty<string>() }
            });

            // Include XML comments for endpoint documentation
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Support JWT in SignalR query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAngularClient", policy =>
            {
                var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:4200"];

                policy
                    .WithOrigins(origins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials(); // Required for SignalR
            });
        });

        return services;
    }

    public static IServiceCollection AddHealthChecksConfiguration(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                config.GetConnectionString("PostgreSQL")!,
                name: "postgresql",
                tags: ["db", "sql", "infrastructure"])
            .AddRedis(
                config.GetConnectionString("Redis")!,
                name: "redis",
                tags: ["cache", "infrastructure"]);

        return services;
    }
}
