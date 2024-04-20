// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;

//public class SqlConfigurationProvider : ConfigurationProvider, IDisposable
//{
//    private readonly Timer? _refreshTimer = null;
//    public SqlDatabaseConfigurationSource Source { get; }

//    public SqlConfigurationProvider(SqlDatabaseConfigurationSource source)
//    {
//        Source = source;

//        if (Source.RefreshInterval.HasValue)
//            _refreshTimer = new Timer(_ => ReadDatabaseSettings(true), null, Timeout.Infinite, Timeout.Infinite);
//    }

//    private void ReadDatabaseSettings(bool isReload)
//    {
//        using var connection = new SqlConnection(Source.ConnectionString);
//        var command = new SqlCommand("SELECT SettingKey, SettingValue FROM dbo.Settings WHERE IsActive = 1", connection);

//        try
//        {
//            connection.Open();
//            var reader = command.ExecuteReader();

//            var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

//            while (reader.Read())
//            {
//                try
//                {
//                    settings[reader.GetString(0)] = reader.GetString(1);
//                }
//                catch (Exception readerEx)
//                {
//                    System.Diagnostics.Debug.WriteLine(readerEx);
//                }
//            }

//            reader.Close();

//            if (!isReload || !SettingsMatch(Data, settings))
//            {
//                Data = settings;

//                if (isReload)
//                    OnReload();
//            }
//        }
//        catch (Exception sqlEx)
//        {
//            System.Diagnostics.Debug.WriteLine(sqlEx);
//        }
//    }

//    private bool SettingsMatch(IDictionary<string, string?> oldSettings, IDictionary<string, string?> newSettings)
//    {
//        if (oldSettings.Count != newSettings.Count)
//            return false;

//        return oldSettings
//            .OrderBy(s => s.Key)
//            .SequenceEqual(newSettings.OrderBy(s => s.Key));
//    }

//    public void Dispose()
//    {
//        _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
//        _refreshTimer?.Dispose();
//    }
//}
