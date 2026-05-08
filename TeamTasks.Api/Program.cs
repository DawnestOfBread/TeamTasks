using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using TeamTasks.Api.EndpointDefs;
using TeamTasks.Api.Services;
using TeamTasks.Application.Common;
using TeamTasks.Infrastructure;
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
					policy
						.AllowAnyOrigin()
						.AllowAnyMethod()
						.AllowAnyHeader();
				});
		});

		// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddOpenApi();
		
		builder.Services.AddHttpContextAccessor();
		
		builder.Services.AddScoped<IJwtService, JwtService>();
		builder.Services.AddScoped<ITenantProvider, TenantProvider>();
		
		string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
		builder.Services.AddDbContext<AppDbContext>(options =>
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

		app.UseCors("AllowAll");
		app.UseHttpsRedirection();
		app.UseAuthorization();
		
		app.UseMiddleware<ExceptionMetricsMiddleware>();
		app.UseHttpMetrics();
		app.MapMetrics();

		var summaries = new[]
		{
			"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
		};

		app.MapGet("/weatherforecast", (HttpContext httpContext) =>
			{
				var forecast = Enumerable.Range(1, 5).Select(index =>
						new WeatherForecast
						{
							Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
							TemperatureC = Random.Shared.Next(-20, 55),
							Summary = summaries[Random.Shared.Next(summaries.Length)]
						})
					.ToArray();
				return forecast;
			})
			.WithName("GetWeatherForecast");
		// app.MapPost("/auth/register", httpContext =>
		// {
		// 	httpContext.Request.Body.
		// }).WithName("Register");

		app.Run();
	}
}