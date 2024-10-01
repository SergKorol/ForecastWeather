using System.Text;
using System.Text.Json;

namespace ForecastWeather;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var city = GetArgumentValue(args, "--city");
        var weatherApiKey = GetArgumentValue(args, "--weather-api-key");
        var templateFilePath = GetArgumentValue(args, "--template-file");
        var outputFilePath = GetArgumentValue(args, "--out-file");
        var days = int.TryParse(GetArgumentValue(args, "--days"), out var parsedDays) ? parsedDays : 3;

        if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(weatherApiKey) || string.IsNullOrEmpty(templateFilePath) || string.IsNullOrEmpty(outputFilePath))
        {
            Console.WriteLine("Missing required arguments: --city, --weather-api-key, --template-file, --out-file");
            return;
        }

        await FetchWeatherForecast(city, weatherApiKey, templateFilePath, outputFilePath, days);
    }

    private static string? GetArgumentValue(string[] args, string key)
    {
        return (from arg in args where arg.StartsWith(key) select arg.Split('=')[1].Trim('\'')).FirstOrDefault();
    }

    private static async Task FetchWeatherForecast(string city, string weatherApiKey, string? templateFilePath, string? outputFilePath, int days)
    {
        var url = $"https://api.weatherapi.com/v1/forecast.json?key={weatherApiKey}&q={city}&days={days}";

        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var weatherJson = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherApiResponse>(weatherJson);

            if (weatherData != null)
            {
                var todayWeather = weatherData.Forecast.Forecastday[0];
                var updatedContent = await GenerateWeatherReport(todayWeather, weatherData.Location, templateFilePath);

                if (outputFilePath != null) await File.WriteAllTextAsync(outputFilePath, updatedContent);
                Console.WriteLine($"Weather report created at {outputFilePath}");
            }
            else
            {
                Console.WriteLine("No weather data found.");
            }
        }
        else
        {
            Console.WriteLine($"Failed to fetch the weather data. Status Code: {response.StatusCode}");
        }
    }

    private static async Task<string> GenerateWeatherReport(Forecastday todayWeather, Location location, string? templateFilePath)
    {
        var templateContent = await File.ReadAllTextAsync(templateFilePath);
        var weatherTableContent = GenerateHourlyWeatherTable(todayWeather.Hour);

        var updatedContent = templateContent
            .Replace("{{ City }}", location.Name)
            .Replace("{{ Country }}", location.Country)
            .Replace("{{ TodayWeatherDate }}", todayWeather.Date.ToString("yyyy-MM-dd"))
            .Replace("{{ TodayWeatherIcon }}", $"https{todayWeather.Day.Condition.Icon}")
            .Replace("{{ TodayWeatherCondition }}", todayWeather.Day.Condition.Text)
            .Replace("{{ TodayWeatherConditionIcon }}", $"https{todayWeather.Day.Condition.Icon}")
            .Replace("{{ WeathersTable }}", weatherTableContent)
            .Replace("{{ UpdatedDateTime }}", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

        return updatedContent;
    }

    private static string GenerateHourlyWeatherTable(Current[] hourlyWeathers)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("<table>");
        stringBuilder.AppendLine("<tr><th>Hour</th>");

        foreach (var hour in hourlyWeathers)
        {
            stringBuilder.AppendLine($"<td>{hour.Time.Split(' ')[1]}</td>");
        }
        stringBuilder.AppendLine("</tr>");

        stringBuilder.AppendLine("<tr><th>Weather</th>");
        foreach (var hour in hourlyWeathers)
        {
            stringBuilder.AppendLine($"<td><img src=\"{hour.Condition.Icon}\" alt=\"Weather Icon\"></td>");
        }
        stringBuilder.AppendLine("</tr>");

        stringBuilder.AppendLine("<tr><th>Condition</th>");
        foreach (var hour in hourlyWeathers)
        {
            stringBuilder.AppendLine($"<td>{hour.Condition.Text}</td>");
        }
        stringBuilder.AppendLine("</tr>");

        stringBuilder.AppendLine("<tr><th>Temperature</th>");
        foreach (var hour in hourlyWeathers)
        {
            stringBuilder.AppendLine($"<td>{hour.TempC} °C</td>");
        }
        stringBuilder.AppendLine("</tr>");

        stringBuilder.AppendLine("<tr><th>Wind</th>");
        foreach (var hour in hourlyWeathers)
        {
            stringBuilder.AppendLine($"<td>{hour.WindKph} kph</td>");
        }
        stringBuilder.AppendLine("</tr>");

        stringBuilder.AppendLine("</table>");
        return stringBuilder.ToString();
    }
}