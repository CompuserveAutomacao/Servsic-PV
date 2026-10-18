namespace ServSic.Core.Config;

public class ServcomConfig
{
    public string Server { get; set; } = @".\qg";
    public string Database { get; set; } = "prod silcon";
    public string UserId { get; set; } = "sa";
    public string Password { get; set; } = "123";
    public string RawConnectionString { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public string BuildSqlConnectionString(int timeoutSeconds = 15)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = UserId,
            Password = Password,
            TrustServerCertificate = true,
            Encrypt = false,
            MultipleActiveResultSets = true,
            ConnectTimeout = timeoutSeconds,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 100
        };

        return builder.ConnectionString;
    }
}
