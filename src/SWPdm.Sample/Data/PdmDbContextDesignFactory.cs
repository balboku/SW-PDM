namespace SWPdm.Sample.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class PdmDbContextDesignFactory : IDesignTimeDbContextFactory<PdmDbContext>
{
    public PdmDbContext CreateDbContext(string[] args)
    {
        string provider = Environment.GetEnvironmentVariable("PDM_DB_PROVIDER") ?? "PostgreSql";
        string connectionString = Environment.GetEnvironmentVariable("PDM_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5433;Database=swpdm;Username=swpdm_user;Password=CHANGE_ME";

        DbContextOptionsBuilder<PdmDbContext> optionsBuilder = new();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        return new PdmDbContext(optionsBuilder.Options);
    }
}
