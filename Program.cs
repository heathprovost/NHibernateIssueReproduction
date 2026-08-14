using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using NHibernate.Tool.hbm2ddl;

namespace NHibernateIssueReproduction;

public class Product
{
    public virtual int Id { get; set; }
    public virtual string Name { get; set; } = string.Empty;
    public virtual string Category { get; set; } = string.Empty;
}

/// NHibernate 5.7.0 only ships a System.Data.SQLite driver, which has no macOS/Linux
/// native binaries, so this minimal driver binds to the cross-platform Microsoft.Data.Sqlite.
public class MicrosoftDataSqliteDriver : ReflectionBasedDriver
{
    public MicrosoftDataSqliteDriver()
        : base(
            "Microsoft.Data.Sqlite",
            "Microsoft.Data.Sqlite.SqliteConnection",
            "Microsoft.Data.Sqlite.SqliteCommand")
    {
    }

    public override bool UseNamedPrefixInSql => true;

    public override bool UseNamedPrefixInParameter => true;

    public override string NamedPrefix => "@";

    public override bool SupportsMultipleOpenReaders => false;
}

public class ProductMap : ClassMapping<Product>
{
    public ProductMap()
    {
        Table("Products");
        Id(x => x.Id, m => m.Generator(Generators.Identity));
        Property(x => x.Name);
        Property(x => x.Category);
    }
}

public static class Program
{
    // A SQLite in-memory database lives only as long as its connection, so the
    // same connection is reused for the schema export and every session.
    private const string ConnectionString = "Data Source=:memory:";

    public static void Main()
    {
        var configuration = new Configuration();
        configuration.DataBaseIntegration(db =>
        {
            db.Dialect<SQLiteDialect>();
            db.Driver<MicrosoftDataSqliteDriver>();
            db.ConnectionString = ConnectionString;
            db.KeywordsAutoImport = Hbm2DDLKeyWords.None;
            db.LogSqlInConsole = true;
            db.LogFormattedSql = true;
        });

        var mapper = new ModelMapper();
        mapper.AddMapping<ProductMap>();
        HbmMapping mapping = mapper.CompileMappingForAllExplicitlyAddedEntities();
        configuration.AddMapping(mapping);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        new SchemaExport(configuration).Execute(false, true, false, connection, null);

        using ISessionFactory sessionFactory = configuration.BuildSessionFactory();

        using (var session = sessionFactory.WithOptions().Connection(connection).OpenSession())
        using (var tx = session.BeginTransaction())
        {
            session.Save(new Product { Name = "Widget", Category = "Tools" });
            session.Save(new Product { Name = "Gadget", Category = "Tools" });
            session.Save(new Product { Name = "Doohickey", Category = "Toys" });
            tx.Commit();
        }

        using (var session = sessionFactory.WithOptions().Connection(connection).OpenSession())
        {
            // works
            // var names = new [] { "Widget", "Gadget" };

            //does not work
            var names = new string[] { "Widget", "Gadget" };

            var results = session.Query<Product>()
                .Where(p => names.Contains(p.Name))
                .ToList();

            Console.WriteLine($"Matched {results.Count} product(s):");
            foreach (var product in results)
            {
                Console.WriteLine($"  {product.Id}: {product.Name} ({product.Category})");
            }
        }
    }
}
