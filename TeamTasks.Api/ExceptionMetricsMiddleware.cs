using Prometheus;

namespace TeamTasks.Api;

public class ExceptionMetricsMiddleware(RequestDelegate next)
{
	private static readonly Counter ErrorCounter = Metrics
		.CreateCounter("http_errors_total", "Total exceptions caught.", 
			new CounterConfiguration { LabelNames = ["exception_type"] });

	static ExceptionMetricsMiddleware()
	{
		ErrorCounter.Inc(0);
	}

	public async Task Invoke(HttpContext context)
	{
		try
		{
			await next(context);
		}
		catch (Exception ex)
		{
			ErrorCounter.WithLabels(ex.GetType().Name).Inc();
			throw;
		}
	}
}