using Microsoft.EntityFrameworkCore;
using Prometheus;
using TeamTasks.Api.EndpointDefs;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// Add services to the container.
		builder.Services.AddAuthorization();

		// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddOpenApi();
		
		string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
		builder.Services.AddDbContext<AppDbContext>(options =>
			options.UseNpgsql(connectionString));
		
		var app = builder.Build();
		
		// Apply database migrations
		using (var scope = app.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			dbContext.Database.Migrate(); 
		}

		// Configure the HTTP request pipeline.
		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
		}

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

		app.Run();
	}
}