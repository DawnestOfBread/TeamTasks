using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi; // <-- Added for Forwarding
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using TeamTasks.Api.EndpointDefs;
using TeamTasks.Api.Services;
using TeamTasks.Application.Cache;
using TeamTasks.Application.Common;
using TeamTasks.Application.Common.Interfaces;
using TeamTasks.Infrastructure;
using TeamTasks.Infrastructure.Cache;
using TeamTasks.Infrastructure.Messaging;
using TeamTasks.Infrastructure.Security;

namespace TeamTasks.Api;

public class Program
{
    public static void Main(string[] args)
    {
       var builder = WebApplication.CreateBuilder(args);
       
       Log.Logger = new LoggerConfiguration()
          .ReadFrom.Configuration(builder.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
          .WriteTo.Seq(builder.Configuration.GetConnectionString("Seq") ?? "http://seq:5341")
          .CreateLogger();
       builder.Host.UseSerilog();

       // Add services to the container.
       builder.Services.AddAuthorization();
       builder.Services.AddControllers();
       
       builder.Services.AddCors(options =>
       {
          string frontendUrl = builder.Configuration["Frontend:BaseUrl"] ?? "/";
          options.AddPolicy("Frontend",
             policy =>
             {
                policy.SetIsOriginAllowed(uri => uri == frontendUrl)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
             });
       });

       builder.Services.AddOpenApi();
       builder.Services.Configure<OpenApiOptions>(options =>
       {
          options.AddDocumentTransformer((document, context, cancellationToken) =>
          {
             document.Info = new OpenApiInfo
             {
                Title = "TeamTasks SaaS API",
                Version = "v1",
                Description = "Multi-tenant enterprise task management engine API spec."
             };
             
             var securityScheme = new OpenApiSecurityScheme
             {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Auth-Token",
                In = ParameterLocation.Cookie,
                Description = "JWT authentication token passed via HttpOnly Cookie."
             };

             document.Components ??= new OpenApiComponents();
             document.Components.SecuritySchemes!.Add("CookieAuth", securityScheme);
             
             document.Security = new List<OpenApiSecurityRequirement>
             {
                new()
                {
                   [new OpenApiSecuritySchemeReference("CookieAuth")] = []
                }
             };

             return Task.CompletedTask;
          });
       });
       builder.Services.AddHttpContextAccessor();
       
       builder.Services.AddScoped<IJwtService, JwtService>();
       builder.Services.AddScoped<ITenantProvider, TenantProvider>();
       
       builder.Services.AddCustomRateLimiting(builder.Configuration);
       
       builder.Services.AddStackExchangeRedisCache(options =>
       {
          options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
          options.InstanceName = "TeamTasks:";
       });
       
       builder.Services.AddSingleton(new JsonSerializerOptions
       {
          ReferenceHandler = ReferenceHandler.Preserve,
          WriteIndented = false
       });
       var dtoSerializerOptions = new JsonSerializerOptions
       {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
       };
       
       builder.Services.AddSingleton(dtoSerializerOptions);
       builder.Services.AddScoped<ICacheService, RedisCacheService>();
       
       string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
       builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
          options.UseNpgsql(connectionString));
       
       builder.Services.Configure<ForwardedHeadersOptions>(options =>
       {
          options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
          options.KnownIPNetworks.Clear();
          options.KnownProxies.Clear();
       });

       var jwtSettings = builder.Configuration.GetSection("Jwt");
       byte[] key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);
       
       builder.Services.AddAuthentication(options => {
             options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
             options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
             
             options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
          })
          .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
          {
             options.TokenValidationParameters = new TokenValidationParameters
             {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
             };
             
             options.Events = new JwtBearerEvents
             {
                OnMessageReceived = context =>
                {
                   context.Token = context.Request.Cookies["X-Auth-Token"];
                   return Task.CompletedTask;
                }
             };
          })
          .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
          {
             options.Cookie.Name = "TeamTasks.ExternalAuth";
             options.Cookie.HttpOnly = true;
             options.Cookie.SecurePolicy = CookieSecurePolicy.None;
             options.Cookie.SameSite = SameSiteMode.Lax;
             options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
          })
          .AddGoogle(GoogleDefaults.AuthenticationScheme, options => {
             options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
             options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
             
             options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
             options.CorrelationCookie.SameSite = SameSiteMode.Lax;
          });
       
       builder.Services.AddScoped<IPasswordService, PasswordService>();
       
       builder.Services.AddMassTransit(x =>
       {
          x.UsingRabbitMq((context, cfg) =>
          {
             cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost", "/", h =>
             {
                h.Username("guest");
                h.Password("guest");
             });
          });
       });

       builder.Services.AddScoped<IEventBus, EventBus>();
       
       try
       {
          Log.Information("Starting TeamTasks...");
          var app = builder.Build();
          app.UseSerilogRequestLogging();
          
          using (var scope = app.Services.CreateScope())
          {
             var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
             dbContext.Database.Migrate(); 
          }
          
          app.MapOpenApi();
          app.MapScalarApiReference(options =>
          {
             options.WithTitle("TeamTasks API Console")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
          });
          
          
          app.UseForwardedHeaders();
          app.UseHttpsRedirection();
          app.UseRouting();
          app.UseCors("Frontend");
          app.UseResponseCaching();
          app.UseRateLimiter();
          app.UseAuthentication();
          app.UseAuthorization();
          app.UseMiddleware<ExceptionMetricsMiddleware>();
          app.UseHttpMetrics();
          app.MapControllers();
          app.MapMetrics();

          app.Run();
       }
       catch (Exception ex)
       {
          Log.Fatal(ex, "Host terminated unexpectedly");
       }
       finally
       {
          Log.CloseAndFlush();
       }
    }
}