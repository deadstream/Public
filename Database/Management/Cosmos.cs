using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Caspar.Database.NoSql
{
    public class Cosmos : IConnection
    {

        public ThreadLocal<CosmosClient> Connection = new ThreadLocal<CosmosClient>();

        public string EndPoint { get; set; } = "";
        public string Name { get; internal set; }

        public void Initialize()
        {

            if (Connection.Value == null)
            {
                Connection.Value = new CosmosClient(EndPoint);
            }

        }

        public IConnection Create()
        {
            return this;
        }


        public CosmosClient GetCosmosClient()
        {
            if (Connection.Value == null)
            {
                Connection.Value = new CosmosClient(EndPoint);
            }

            return Connection.Value;
        }

        public void BeginTransaction() { }
        public void Commit() { }
        public void Rollback() { }
        public async Task<IConnection> Open(CancellationToken token = default, bool transaction = true)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return null;
        }
        public void Close() { }
        public void CopyFrom(IConnection value) { }

        public void Dispose()
        {

        }

    }
}
