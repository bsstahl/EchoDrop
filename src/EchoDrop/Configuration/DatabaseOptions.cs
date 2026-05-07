namespace EchoDrop.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Data Source=echodrop.db";
}
