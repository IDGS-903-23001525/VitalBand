namespace VitalBand.Services
{
    public interface IApiUrlProvider
    {
        string GetApiBaseUrl();
        string GetApiUrl(string relativePath);
    }
}
