namespace TaskManagement.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TASKMANAGEMENT_TEST_SQL")))
            Skip = "Set TASKMANAGEMENT_TEST_SQL to run the SQL Server integration tests.";
    }
}
