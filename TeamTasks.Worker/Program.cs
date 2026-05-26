using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TeamTasks.Application.Common;
using TeamTasks.Worker.Consumers;
using TeamTasks.Worker.Hubs;
using TeamTasks.Worker.Services;

namespace TeamTasks.Worker;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.Services.AddSignalR();

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

		builder.Services.AddMassTransit(x =>
		{
			x.AddConsumer<TaskActivityConsumer>();

			x.UsingRabbitMq((context, cfg) =>
			{
				cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost", "/", h =>
				{
					h.Username("guest");
					h.Password("guest");
				});

				cfg.ReceiveEndpoint("task-activity-broadcast", e =>
				{
					e.ConfigureConsumer<TaskActivityConsumer>(context);
				});
			});
		});
		
		var jwtSettings = builder.Configuration.GetSection("Jwt");
		byte[] key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

		builder.Services.AddAuthentication(options =>
			{
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
						if (string.IsNullOrEmpty(context.Token) && context.Request.Query.ContainsKey("access_token"))
							context.Token = context.Request.Query["access_token"];
						return Task.CompletedTask;
					}
				};
			});
		
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ITenantProvider, TenantProvider>();

		var app = builder.Build();

		app.UseCors("Frontend");

		app.UseAuthentication();
		app.UseAuthorization();
		
		app.MapHub<TaskActivityHub>("/hubs/task-activity");

		app.Run();
	}
}