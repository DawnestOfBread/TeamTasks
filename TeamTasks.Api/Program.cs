using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using StackExchange.Redis;
using TeamTasks.Api.EndpointDefs;
using TeamTasks.Api.Services;
using TeamTasks.Application.Cache;
using TeamTasks.Application.Common;
using TeamTasks.Infrastructure;
using TeamTasks.Infrastructure.Cache;
using TeamTasks.Infrastructure.Security;

namespace TeamTasks.Api;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// Add services to the container.
		builder.Services.AddAuthorization();
		
		builder.Services.AddControllers();
		
		builder.Services.AddCors(options =>
		{
			options.AddPolicy("AllowAll",
				policy =>
				{
					policy.SetIsOriginAllowed(_ => true)
						.AllowAnyHeader()
						.AllowAnyMethod()
						.AllowCredentials();
				});
		});

		// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddOpenApi();
		
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
		
		var jwtSettings = builder.Configuration.GetSection("Jwt");
		byte[] key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);
		builder.Services.AddAuthentication(options => {
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
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
			.AddOpenIdConnect("Google", options => {
				options.Authority = "https://accounts.google.com";
				options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
				options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
				options.CallbackPath = "/signin-google";
			});
		
		builder.Services.AddScoped<IPasswordService, PasswordService>();
		
		var app = builder.Build();
		
		// Apply database migrations
		using (var scope = app.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			dbContext.Database.Migrate(); 
		}

		// Configure the HTTP request pipeline.
		if (app.Environment.IsDevelopment()) 
			app.MapOpenApi();
		
		app.MapControllers();

		app.UseRateLimiter();

		app.UseCors("AllowAll");
		app.UseHttpsRedirection();
		app.UseAuthorization();
		
		app.UseMiddleware<ExceptionMetricsMiddleware>();
		app.UseHttpMetrics();
		app.MapMetrics();

		app.Run();
	}
}