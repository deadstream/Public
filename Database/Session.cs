using Framework.Caspar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Framework.Caspar.Api;
using Framework.Caspar.Container;
using MySqlConnector;
//using MySql.Data.MySqlClient;

namespace Framework.Caspar.Database
{
    public interface ICommandable
    {
        int ExecuteNonQuery() { return 0; }
        async Task<int> ExecuteNonQueryAsync() { await Task.CompletedTask; return 0; }
        System.Data.Common.DbDataReader ExecuteReader() { return null; }
        async Task<System.Data.Common.DbDataReader> ExecuteReaderAsync() { await Task.CompletedTask; return null; }
        object ExecuteScalar() { return null; }
        async Task<object> ExecuteScalarAsync() { await Task.CompletedTask; return null; }
        MySqlParameterCollection Parameters { get; }
        void Prepare() { }
        string CommandText { get; set; }
        System.Data.CommandType CommandType { get; set; }
        bool IsTransaction { get { return false; } }


    }
    public interface IConnection : IDisposable
    {
        void Initialize();
        void BeginTransaction();
        void Commit();
        void Rollback();
        Task<IConnection> Open(CancellationToken token = default, bool transaction = true);
        void Close();
        void CopyFrom(IConnection value);
        IConnection Create();
        ICommandable CreateCommand() { return null; }
        int IsPoolable() { return 0; }
        bool Ping() { return false; }

    }

    public class Session : IDisposable
    {

        public class Closer
        {

            protected static ConcurrentQueue<(Session, long)> Connections = new();

            internal static long ExpireAt { get; set; } = DateTime.UtcNow.AddMinutes(1).Ticks;
            internal static long Interval { get; set; } = 5;

            public static void Add(Session session)
            {
                Connections.Enqueue((session, ExpireAt));
            }
            public static void Update()
            {
                long now = DateTime.UtcNow.Ticks;
                ExpireAt = DateTime.UtcNow.AddSeconds(Interval).Ticks;

                while (Connections.Count > 0)
                {
                    try
                    {
                        if (Connections.TryPeek(out var item) == false)
                        {
                            break;
                        }

                        if (item.Item2 > now) { break; }

                        if (Connections.TryDequeue(out item) == false)
                        {
                            break;
                        }
                        if (item.Item1.IsDisposed == true) { continue; }

                        Logger.Info($"Session is not disposed.");
                        item.Item1.Log();
                        item.Item1.Rollback();
                        item.Item1.Dispose();
                    }
                    catch
                    {

                    }

                }
            }

        }
        public class RollbackException : System.Exception
        {
            public int ErrorCode { get; set; }
        }

        public string Trace = string.Empty;

        public static Amazon.DynamoDBv2.AmazonDynamoDBClient DynamoDB
        {
            get
            {
                Driver.Databases.TryGetValue("DynamoDB", out var connection);
                return (connection as global::Framework.Caspar.Database.NoSql.DynamoDB).GetClient();
            }
        }

        public static global::Framework.Caspar.Database.NoSql.Redis Redis
        {
            get
            {
                Driver.Databases.TryGetValue("Redis", out var connection);
                return (connection as Database.NoSql.Redis);
            }
        }

        public Session()
        {
            Command = async () => { await ValueTask.CompletedTask; };
            if (Layer.CurrentEntity.Value == null)
            {
                UID = global::Framework.Caspar.Api.UniqueKey;
                Closer.Add(this);
            }
            else
            {
                UID = Layer.CurrentEntity.Value.UID;
                owner = Layer.CurrentEntity.Value;
                Layer.CurrentEntity.Value.Add(this);
            }

        }

        internal Layer.Entity owner { get; set; }

        public Session(Layer.Entity entity)
        {
            Command = async () => { await ValueTask.CompletedTask; };
            UID = entity.UID;
            owner = entity;
            entity.Add(this);

        }


        public void Rollback()
        {
            foreach (var e in connections)
            {
                try
                {
                    e.Rollback();
                }
                catch
                {

                }
            }
        }

        public void Commit()
        {
            foreach (var e in connections)
            {
                try
                {
                    e.Commit();
                }
                catch
                {

                }
            }
        }

        internal void Close()
        {
            try
            {
                foreach (var e in connections)
                {
                    try
                    {
                        e.Dispose();
                    }
                    catch
                    {

                    }
                }
                connections.Clear();
            }
            catch
            {

            }

            try
            {
                TCS?.SetResult();
                TCS = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

        }
        private int disposed = 0;
        public bool IsDisposed => disposed == 1;

        internal void Log()
        {
            foreach (object connection in connections)
            {
                if (connection is Management.Relational.MySql)
                {
                    var conn = connection as Management.Relational.MySql;
                    if (conn.Command != null)
                    {
                        Logger.Error($"[Dispose Session] {conn.Command.CommandText}");
                    }
                }
            }
        }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }

            try
            {
                owner?.Remove(this);
            }
            catch
            {

            }

            owner = null;

            try
            {
                Rollback();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        List<IConnection> connections { get; set; } = new List<IConnection>();

        internal TaskCompletionSource TCS { get; set; } = null;

        public static ConcurrentQueue<Session> Timeouts = new();

        public async Task<IConnection> GetConnection(string name, bool open = true, bool transaction = true)
        {
            try
            {
                if (Driver.Databases.TryGetValue(name, out var connection) == false)
                {
                    Logger.Error($"Database {name} is not configuration");
                    return null;
                }

                connection = connection.Create();
                await connection.Open(this.CancellationToken, transaction);
                connections.Add(connection);
                return connection;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        public Amazon.DynamoDBv2.AmazonDynamoDBClient GetDynamoDB(string db = "DynamoDB")
        {
            Driver.Databases.TryGetValue(db, out var connection);
            return (connection as global::Framework.Caspar.Database.NoSql.DynamoDB).GetClient();
        }

        public dynamic ResultSet { get; set; } = Singleton<Caspar.Database.ResultSet>.Instance;
        public long RecordsAffected { get; set; }
        public Action ResponseCallBack { get; set; }
        public virtual string Host { get; }

        internal Func<Task> Command { get; set; }
        public int Error { get; protected set; }
        public System.Exception Exception { get; protected set; }
        public int Strand { get; set; }
        public long UID { get; set; }
        public DateTime Timeout { get; internal set; }
        internal protected System.Threading.CancellationTokenSource CTS { get; private set; } = new CancellationTokenSource();
        public System.Threading.CancellationToken CancellationToken => CTS.Token;


        internal protected virtual void SetResult(int result)
        {
            this.Error = result;
            TCS?.SetResult();
        }

        internal protected virtual void SetException(Exception e)
        {
            this.Exception = e;
            this.Error = -1;// e.HResult;
            TCS?.SetException(e);

        }

        public virtual IEnumerable<string> GetHost()
        {
            yield break;
        }

    }
}
