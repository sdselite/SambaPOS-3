using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Announcers;
using FluentMigrator.Runner.Initialization;
using Samba.Infrastructure.Data;
using Samba.Infrastructure.Data.SQL;
using Samba.Infrastructure.Data.Text;
using Samba.Infrastructure.Settings;

namespace Samba.Persistance.Data
{
    public static class WorkspaceFactory
    {
        private static TextFileWorkspace _textFileWorkspace;
        private static string _connectionString;

        static WorkspaceFactory()
        {
            UpdateConnection(LocalSettings.ConnectionString);
        }

        public static void UpdateConnection(string connectionString)
        {
            _connectionString = connectionString;
            Database.SetInitializer(new Initializer());

            if (string.IsNullOrEmpty(_connectionString))
            {
                if (LocalSettings.IsSqlce40Installed())
                    _connectionString = string.Format("data source={0}\\{1}.sdf", LocalSettings.DocumentPath, LocalSettings.AppName);
                else _connectionString = GetTextFileName();
            }
            if (_connectionString.EndsWith(".sdf"))
            {
                if (!_connectionString.ToLower().Contains("data source") && !_connectionString.Contains(":\\"))
                    _connectionString = string.Format("data source={0}\\{1}", LocalSettings.DocumentPath, _connectionString);

                Database.DefaultConnectionFactory = new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0", "", _connectionString);
            }
            else if (_connectionString.EndsWith(".txt"))
            {
                _textFileWorkspace = GetTextFileWorkspace();
            }
            else if (!string.IsNullOrEmpty(_connectionString))
            {
                var cs = _connectionString;
                if (!cs.Trim().EndsWith(";"))
                    cs += ";";
                if (!cs.ToLower().Contains("multipleactiveresultsets"))
                    cs += " MultipleActiveResultSets=True;";
                if (!cs.ToLower(CultureInfo.InvariantCulture).Contains("user id") &&
                    (!cs.ToLower(CultureInfo.InvariantCulture).Contains("integrated security")))
                    cs += " Integrated Security=True;";
                if (cs.ToLower(CultureInfo.InvariantCulture).Contains("user id") &&
                    !cs.ToLower().Contains("persist security info"))
                    cs += " Persist Security Info=True;";
                Database.DefaultConnectionFactory = new SqlConnectionFactory(cs);
            }
        }

        public static IWorkspace Create()
        {
            if (_textFileWorkspace != null) return _textFileWorkspace;
            return new EFWorkspace(new DataContext(false));
        }

        public static IReadOnlyWorkspace CreateReadOnly()
        {
            if (_textFileWorkspace != null) return _textFileWorkspace;
            return new ReadOnlyEFWorkspace(new DataContext(true));
        }

        private static TextFileWorkspace GetTextFileWorkspace()
        {
            var fileName = GetTextFileName();
            return new TextFileWorkspace(fileName, false);
        }

        private static string GetTextFileName()
        {
            return _connectionString.EndsWith(".txt")
                ? _connectionString
                : string.Format("{0}\\{1}.txt", LocalSettings.DocumentPath, LocalSettings.AppName);
        }

        public static void SetDefaultConnectionString(string cTestdataTxt)
        {
            _connectionString = cTestdataTxt;
            if (string.IsNullOrEmpty(_connectionString) || _connectionString.EndsWith(".txt"))
                _textFileWorkspace = GetTextFileWorkspace();
        }
    }

    public class Initializer : IDatabaseInitializer<DataContext>
    {

        public void InitializeDatabase(DataContext context)
        {

            if (!context.Database.Exists() || LocalSettings.RecreateDatabase)
            {
                Create(context);
            }
            //#if DEBUG
            //            else if (!context.Database.CompatibleWithModel(false))
            //            {
            //                context.Database.Delete();
            //                Create(context);
            //            }
            //#else
            
            else
            {
                Migrate(context);
            }
            //#endif
            LocalSettings.CurrentDbVersion = GetVersionNumber(context);
        }

        public static long GetVersionNumber(CommonDbContext context)
        {
            var version = context.ObjContext().ExecuteStoreQuery<long>("select top(1) Version from VersionInfo  order by version desc").FirstOrDefault();
            return version;
        }

        private static void Create(CommonDbContext context)
        {
            try
            {
               
                int versionNumber = 0;
                if(LocalSettings.RecreateDatabase)
                {
                    if (context.Database.Exists())
                    {
                        context.ObjContext().DeleteDatabase();
                    }
                   
                    versionNumber = 1;
                }

                //Note - there is a bug with setting the default command timeout in the connection string
                //https://ttntuyen.wordpress.com/2017/05/30/entity-framework-the-wait-operation-timed-out/
                ((IObjectContextAdapter)context).ObjectContext.CommandTimeout = 60 * 15;
                try
                {
                    context.Database.CreateIfNotExists();
                }
                catch (Exception ex)
                {
                    try
                    {
                        // If we are connecting to an Azure database it tends to timeout before the tables are created
                        if (context.Database.Exists())
                        {
                            //Wait ten seconds to give Azure time to complete
                            Thread.Sleep(10000);
                            var dbScript = context.ObjContext().CreateDatabaseScript();
                            var result = context.ObjContext().ExecuteStoreCommand(dbScript);
                        }
                    }
                    catch (Exception ex2)
                    {

                        throw;
                    }
                   
                }
              
                ExecuteCommandIfNotExists(context, "CREATE TABLE VersionInfo (Version bigint not null)", "VersionInfo");
                ExecuteCommandIfNotExistsAndExists(context, "CREATE NONCLUSTERED INDEX IX_Tickets_LastPaymentDate ON Tickets(LastPaymentDate)", "IX_Tickets_LastPaymentDate", "Tickets");
                ExecuteCommandIfNotExistsAndExists(context, "CREATE UNIQUE INDEX IX_EntityStateValue_EntityId ON EntityStateValues (EntityId)", "IX_EntityStateValue_EntityId", "EntityStateValues");
                context.ObjContext().SaveChanges();
                SetMigrateVersions(context);
                LocalSettings.CurrentDbVersion = LocalSettings.DbVersion;
            }
            catch (Exception ex)
            {

                context.ObjContext().DeleteDatabase();

            }

        }


        public static void ExecuteCommandIfNotExists(CommonDbContext context, string commandText, string objectName, string schemaName = "")
        {
            if(DoesObjectExistInDatabase(context, objectName, schemaName) == false)
            {
                context.ObjContext().ExecuteStoreCommand(commandText);
            }
        }

        public static void ExecuteCommandIfNotExistsAndExists(CommonDbContext context, string commandText, string notExistsObjectName, string existsObjectName, string schemaName = "")
        {
            if (DoesObjectExistInDatabase(context, existsObjectName, schemaName))
            {
                ExecuteCommandIfNotExists(context, commandText, notExistsObjectName, schemaName);
            }
        }

        public static bool DoesObjectExistInDatabase(CommonDbContext context, string objectName, string objectSchemaName = "")
        {
            string queryUnformatted = @"select count(*) From
                                        (
                                            select sysobjects.[name], sysobjects.id, OBJECT_SCHEMA_NAME(id) as schema_name  from sysobjects
                                            where lower(sysobjects.[name]) = lower('{0}')
                                        ) as innerQuery
                                        where lower('{1}') = '' OR lower('{1}') = schema_name";

            var query = string.Format(queryUnformatted, objectName, objectSchemaName);
            var queryResult = context.ObjContext().ExecuteStoreQuery<int>(query).FirstOrDefault();

            return queryResult > 0;
        }

        private static void SetMigrateVersions(CommonDbContext context)
        {
         
            for (var i = 0; i < LocalSettings.DbVersion; i++)
            {
                var versionNumber = (i + 1).ToString();
                var queryText = "if Not Exists(select 'x' From  VersionInfo where Version = '" + versionNumber + "' ) begin  Insert into VersionInfo (Version) Values (" + versionNumber + ") end";

                try
                {
                    context.ObjContext().ExecuteStoreCommand(queryText);
                }
                catch (Exception ex)
                {

                    throw;
                }
           
            }
        }

        private static void Migrate(CommonDbContext context)
        {
            var migrateFileName = LocalSettings.UserPath + "\\migrate.txt";
            if (!File.Exists(migrateFileName) && LocalSettings.RecreateDatabase == false) return;


            var preVersion = GetVersionNumber(context);
            var db = context.Database.Connection.ConnectionString.Contains(".sdf") ? "sqlserverce" : "sqlserver";
            if (preVersion < 18 && db == "sqlserverce") ApplyV16Fix(context);

            IAnnouncer announcer = new TextWriterAnnouncer(Console.Out);

            var migrationAssemblyPath = LocalSettings.AppPath + "\\Samba.Persistance.DbMigration.dll";

            IRunnerContext migrationContext =
                new RunnerContext(announcer)
                {
                    ApplicationContext = context,
                    Connection = context.Database.Connection.ConnectionString,
                    Database = db,
                    Target = migrationAssemblyPath
                };

            new TaskExecutor(migrationContext).Execute();

            if (File.Exists(migrateFileName))
            {
                File.Delete(migrateFileName);
            }
        }

        private static void ApplyV16Fix(CommonDbContext context)
        {
            try
            {
                context.ObjContext().ExecuteStoreCommand("Alter Table PaymentTypeMaps drop column DisplayAtPaymentScreen");
            }
            catch { }

            try
            {
                context.ObjContext().ExecuteStoreCommand("Alter Table PaymentTypeMaps drop column DisplayUnderTicket");
            }
            catch { }
        }
    }
}
