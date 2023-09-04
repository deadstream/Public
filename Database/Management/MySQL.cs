﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using MySql.Data;
using MySqlConnector;
//using MySql.Data.MySqlClient;
using System.Threading;
using System.Data;
using System.Collections;
using static Framework.Caspar.Api;
using Amazon;
using System.Data.SqlClient;
using System.Data.Odbc;

namespace Framework.Caspar.Database.Management.Relational
{
    public sealed class MySql : IConnection
    {
        public sealed class Queryable : ICommandable
        {
            public bool IsTransaction { get { return Command.Transaction != null; } }
            public int ExecuteNonQuery()
            {

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ret = Command.ExecuteNonQuery();
                long ms = sw.ElapsedMilliseconds;
                if (ms > global::Framework.Caspar.Extensions.Database.SlowQueryMilliseconds)
                {
                    Logger.Info($"{Command.CommandText} - {ms}ms");
                }
                return ret;
            }
            public System.Data.Common.DbDataReader ExecuteReader()
            {

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ret = Command.ExecuteReader();
                long ms = sw.ElapsedMilliseconds;
                if (ms > global::Framework.Caspar.Extensions.Database.SlowQueryMilliseconds)
                {
                    Logger.Info($"{Command.CommandText} - {ms}ms");
                }
                return ret;

            }
            public object ExecuteScalar()
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ret = Command.ExecuteScalar();
                long ms = sw.ElapsedMilliseconds;
                if (ms > global::Framework.Caspar.Extensions.Database.SlowQueryMilliseconds)
                {
                    Logger.Info($"{Command.CommandText} - {ms}ms");
                }
                return ret;
            }



            public async Task<int> ExecuteNonQueryAsync()
            {
                return await Command.ExecuteNonQueryAsync();
            }
            public async Task<System.Data.Common.DbDataReader> ExecuteReaderAsync()
            {
                return await Command.ExecuteReaderAsync();
            }
            public async Task<object> ExecuteScalarAsync()
            {
                return await Command.ExecuteScalarAsync();
            }
            public MySqlCommand Command { get; internal set; }
            public MySqlParameterCollection Parameters => Command.Parameters;
            public void Prepare() { Command.Prepare(); }
            public string CommandText { get { return Command.CommandText; } set { Command.CommandText = value; } }
            public System.Data.CommandType CommandType { get { return Command.CommandType; } set { Command.CommandType = value; } }
        }

        public string Name { get; set; }
        public string Id { get; set; }
        public string Pw { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }
        public string Db { get; set; }
        public MySqlConnection Connection { get; set; }
        public MySqlTransaction Transaction { get; set; }
        public MySqlCommand Command { get; set; }
        private string connectionStringValue;
        internal int MaxSession { get; set; } = 0;

        public int IsPoolable() { return 0; }
        public bool Ping()
        {
            if (Connection == null)
            {
                return false;
            }
            else
            {
                return Connection.Ping();
            }
        }
        public IConnection Create()
        {
            var session = new MySql();
            session.connectionStringValue = connectionStringValue;
            session.IAM = IAM;
            session.Id = Id;
            session.Pw = Pw;
            session.Ip = Ip;
            session.Port = Port;
            session.Db = Db;
            session.Name = Name;
            session.MaxSession = IsPoolable();
            return session;
        }


        public ICommandable CreateCommand()
        {
            if (Command == null)
            {
                Command = Connection.CreateCommand();
                Command.Transaction = Transaction;
            }
            Command.CommandType = CommandType.Text;
            Command.CommandText = "";
            Command.Parameters.Clear();
            return new Queryable() { Command = Command };
        }


        public void BeginTransaction()
        {
            if (Transaction == null)
            {
                Transaction = Connection.BeginTransaction();

            }

            if (Command != null)
            {
                Command.Transaction = Transaction;
            }
        }

        public void Commit()
        {
            Transaction?.Commit();
            Transaction = null;
            if (Command != null)
            {
                Command.Transaction = null;
            }
        }

        public void Rollback()
        {
            Transaction?.Rollback();
            Transaction = null;
            if (Command != null)
            {
                Command.Transaction = null;
            }
        }

        public bool IAM { get; set; } = false;

        public void Initialize()
        {

            if (Database.Driver.Databases.TryGetValue(Name, out var session) == false)
            {
                return;
            }
            if (session != null && session is MySql)
            {
                {
                    var connectionString = new MySqlConnectionStringBuilder();
                    connectionString.UserID = Id;
                    connectionString.Server = Ip;
                    connectionString.Port = Convert.ToUInt32(Port);
                    connectionString.Database = Db;

                    connectionString.Pooling = true;
                    connectionString.MinimumPoolSize = 2;
                    connectionString.MaximumPoolSize = 32;
                    connectionString.ConnectionIdleTimeout = 60;

                    connectionString.AllowZeroDateTime = true;
                    connectionString.CharacterSet = "utf8";
                    connectionStringValue = connectionString.ToString();//.GetConnectionString(true);
                }
            }
        }


        public async Task<IConnection> Open(CancellationToken token = default, bool transaction = true)
        {
            try
            {
                if (Connection == null)
                {
                    Connection = new MySqlConnection(connectionStringValue);
                    Connection.ProvidePasswordCallback = (context) =>
                    {
                        if (IAM == true)
                        {
                            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials((string)global::Framework.Caspar.Api.Config.AWS.Access.KeyId, (string)global::Framework.Caspar.Api.Config.AWS.Access.SecretAccessKey);
                            var pwd = Amazon.RDS.Util.RDSAuthTokenGenerator.GenerateAuthToken(awsCredentials, Ip, 3306, Id);
                            Logger.Info("mysql ProvidePasswordCallback");
                            Logger.Info($"connectionStringValue: {connectionStringValue}");
                            Logger.Info($"password: {pwd}");
                            return pwd;
                        }
                        else
                        {
                            return Pw;
                        }
                    };


                    await Connection.OpenAsync();
                }
                if (transaction == true)
                {
                    BeginTransaction();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Connection?.Close();
                Connection?.Dispose();
                Connection = null;
                Dispose();
                throw;
            }
            return this;
        }

        public void Close()
        {
            try
            {
                Rollback();
            }
            catch (Exception ex)
            {
                global::Framework.Caspar.Api.Logger.Info("Driver Level Rollback Exception " + ex);
            }

        }
        private int disposed = 0;
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }
            Close();

            Command?.Dispose();
            Command = null;

            Transaction?.Dispose();
            Transaction = null;

            Connection?.Close();
            Connection?.Dispose();
            Connection = null;
        }


        public void CopyFrom(IConnection value)
        {

            var rhs = value as MySql;
            if (rhs == null) { return; }

            Name = rhs.Name;
            Id = rhs.Id;
            Pw = rhs.Pw;
            Ip = rhs.Ip;
            Port = rhs.Port;
            Db = rhs.Db;

        }

    }
}
