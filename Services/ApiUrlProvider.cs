namespace VitalBand.Services
{
    public class ApiUrlProvider : IApiUrlProvider
    {
        private readonly string _apiBaseUrl;

        public ApiUrlProvider(IConfiguration configuration, IHostEnvironment environment)
        {
            var configuredBaseUrl = configuration["ApiSettings:BaseUrl"];
            var productionBaseUrl = configuration["ApiSettings:ProductionBaseUrl"];

            if (environment.IsProduction() && !string.IsNullOrWhiteSpace(productionBaseUrl))
            {
                _apiBaseUrl = productionBaseUrl;
            }
            else if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                _apiBaseUrl = configuredBaseUrl;
            }
            else
            {
                _apiBaseUrl = "https://localhost:7116";
            }
        }

        public string GetApiBaseUrl() => _apiBaseUrl.TrimEnd('/');

        public string GetApiUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return GetApiBaseUrl();
            }

            var normalized = relativePath.StartsWith('/') ? relativePath[1..] : relativePath;
            return $"{GetApiBaseUrl()}/{normalized}";
        }
    }
}
