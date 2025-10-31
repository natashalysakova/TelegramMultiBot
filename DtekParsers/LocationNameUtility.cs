namespace DtekParsers;

public class LocationNameUtility
{
    public static string GetLocationByUrl(string url)
    {
        var region = GetRegionByUrl(url);
        return GetLocationByRegion(region);
    }

    public static string GetRegionByUrl(string url)
    {
        var urlPattern = "https://www.dtek-";

        if (!url.StartsWith(urlPattern))
        {
            return string.Empty;
        }

        var dotIndex = url.IndexOf('.', urlPattern.Length);

        return url.Substring(urlPattern.Length, dotIndex - urlPattern.Length);
    }

    public static string GetLocationByRegion(string region)
    {
        switch (region)
        {
            case "krem":
                return "Київська область";
            case "kem":
                return "м.Київ";
            case "oem":
                return "Одеська область";
            default:
                throw new Exception($"Unknown region {region}");
        }
    }
}
