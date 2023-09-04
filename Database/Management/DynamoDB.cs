﻿using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Framework.Caspar.Api;

namespace Framework.Caspar.Database.NoSql
{
    public class DynamoDB : IConnection
    {

        public class DateTimeUtcConverter : IPropertyConverter
        {
            public DynamoDBEntry ToEntry(object value) => ((DateTime)value).ToUniversalTime();

            public object FromEntry(DynamoDBEntry entry)
            {
                var dateTime = entry.AsDateTime();
                return dateTime.ToUniversalTime();
            }
        }

        public ThreadLocal<AmazonDynamoDBClient> Connection = new ThreadLocal<AmazonDynamoDBClient>();

        public string AwsAccessKeyId { get; set; } = "";
        public string AwsSecretAccessKey { get; set; } = "";
        public RegionEndpoint Endpoint { get; set; } = RegionEndpoint.APNortheast2;
        public string Name { get; set; }
        public void Initialize()
        {
            if (Connection.Value == null)
            {
                AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();
                clientConfig.RegionEndpoint = Endpoint;
                Connection.Value = new AmazonDynamoDBClient(AwsAccessKeyId, AwsSecretAccessKey, clientConfig);
            }
        }

        public IConnection Create()
        {
            return this;
        }

        public AmazonDynamoDBClient GetClient()
        {

            if (Connection.Value == null)
            {
                AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();
                clientConfig.RegionEndpoint = Endpoint;
                Connection.Value = new AmazonDynamoDBClient(AwsAccessKeyId, AwsSecretAccessKey, clientConfig);
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
