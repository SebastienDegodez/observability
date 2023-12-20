using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using WeatherApi.OpenTelemetry;

namespace WeatherApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public WeatherForecastController(ILogger<WeatherForecastController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            this._logger.LogInformation("Hello from Get");

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }


        [HttpPost(Name = "PostLongRunning")]
        public async Task<bool> PostLongRunning()
        {
            using (var activity = Instrumentation.ActivitySource.StartActivity("GetLongRunning"))
            {
                this._logger.LogInformation("Hello from GetLongRunning");

                activity?.AddBaggage("test", "test");
                activity?.AddEvent(new ActivityEvent("google"));
                await this._httpClientFactory.CreateClient().GetAsync("https://www.google.fr");

                
                await Task.Delay(1000);
            }

            using (var activity = Instrumentation.ActivitySource.StartActivity("Exception"))
            {
                throw new Exception("Exception");
            }

            return true;
        }
    }
}