using System;
using System.Data;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TinyDragon.Data
{
    public sealed class SqliteDatabase : IDisposable
    {
        private readonly string databasePath;
        private IDbConnection connection;
        private string providerName;

        public SqliteDatabase(string databasePath)
        {
            this.databasePath = databasePath;
        }

        public bool IsOpen => connection != null;

        public bool Open()
        {
            if (connection != null)
            {
                return true;
            }

            Type connectionType = FindSqliteConnectionType(out providerName);
            if (connectionType == null)
            {
                Debug.LogWarning(
                    "SQLite provider not found. Add Microsoft.Data.Sqlite + SQLitePCLRaw plugins to enable Tiny-Dragon database saves."
                );
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            string connectionString = providerName == "Microsoft.Data.Sqlite"
                ? $"Data Source={databasePath}"
                : $"URI=file:{databasePath}";
            connection = (IDbConnection)Activator.CreateInstance(connectionType, connectionString);
            connection.Open();
            Execute("PRAGMA foreign_keys = ON;");
            return true;
        }

        public void ExecuteScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                return;
            }

            Execute(script);
        }

        public void Execute(string sql)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public IDataReader Query(string sql)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteReader(CommandBehavior.CloseConnection);
        }

        public IDbCommand CreateCommand(string sql)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        public void Dispose()
        {
            if (connection == null)
            {
                return;
            }

            connection.Dispose();
            connection = null;
        }

        private static Type FindSqliteConnectionType(out string providerName)
        {
            providerName = null;

            InitializeSQLitePCL();
            Type type = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
            if (type != null)
            {
                providerName = "Microsoft.Data.Sqlite";
                return type;
            }

            try
            {
                Assembly assembly = Assembly.Load("Microsoft.Data.Sqlite");
                type = assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection");
                if (type != null)
                {
                    providerName = "Microsoft.Data.Sqlite";
                    return type;
                }
            }
            catch
            {
                // Fall back to Mono.Data.Sqlite below.
            }

            type = Type.GetType("Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite");
            if (type != null)
            {
                providerName = "Mono.Data.Sqlite";
                return type;
            }

            try
            {
                Assembly assembly = Assembly.Load("Mono.Data.Sqlite");
                providerName = "Mono.Data.Sqlite";
                return assembly.GetType("Mono.Data.Sqlite.SqliteConnection");
            }
            catch
            {
                return null;
            }
        }

        private static void InitializeSQLitePCL()
        {
            Type batteriesType = Type.GetType("SQLitePCL.Batteries_V2, SQLitePCLRaw.batteries_v2");
            if (batteriesType == null)
            {
                try
                {
                    Assembly assembly = Assembly.Load("SQLitePCLRaw.batteries_v2");
                    batteriesType = assembly.GetType("SQLitePCL.Batteries_V2");
                }
                catch
                {
                    return;
                }
            }

            MethodInfo initMethod = batteriesType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            initMethod?.Invoke(null, null);
        }
    }
}
