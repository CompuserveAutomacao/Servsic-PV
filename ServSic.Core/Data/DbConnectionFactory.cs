using System;
using System.Data;
using Microsoft.Data.SqlClient;
using ServSic.Core.Config;

namespace ServSic.Core.Data;

public class DbConnectionFactory
{
    private static ServcomConfig? _cachedConfig;
    private static readonly object _lock = new();

    public static ServcomConfig GetConfig()
    {
        if (_cachedConfig == null)
        {
            lock (_lock)
            {
                if (_cachedConfig == null)
                {
                    if (ServcomReader.TryLoad(out var loaded))
                    {
                        _cachedConfig = loaded;
                    }
                    else
                    {
                        _cachedConfig = new ServcomConfig();
                    }
                }
            }
        }
        return _cachedConfig;
    }

    public static void SaveAndApplyConfig(ServcomConfig config)
    {
        lock (_lock)
        {
            ServcomReader.Save(config);
            _cachedConfig = config;
        }
    }

    public static void RefreshConfig()
    {
        lock (_lock)
        {
            if (ServcomReader.TryLoad(out var loaded))
            {
                _cachedConfig = loaded;
            }
        }
    }

    public static IDbConnection CreateConnection()
    {
        var config = GetConfig();
        string connStr = config.BuildSqlConnectionString();
        return new SqlConnection(connStr);
    }
}
