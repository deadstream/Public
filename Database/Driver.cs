﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Framework.Caspar;
using static Framework.Caspar.Api;
using System.Diagnostics;
using Framework.Caspar.Container;


namespace Framework.Caspar.Database
{
    public class Driver
    {

        public static Driver Singleton => Singleton<Driver>.Instance;


        private bool isOpen = false;


        static public int SessionCount { get { return Databases.Count; } }
        static public void AddDatabase<T>(string db, T value) where T : IConnection
        {

            IConnection session;
            if (Databases.TryGetValue(db, out session) == true)
            {
                return;
            }
            else
            {
                Databases.Add(db, value);
            }

        }

        public static Dictionary<string, IConnection> Databases = new Dictionary<string, IConnection>();
        public void Run()
        {
            if (isOpen == true) return;

            isOpen = true;

            foreach (var e in Databases)
            {
                e.Value.Initialize();
            }


            Logger.Info($"Database Driver Run. ThreadCount : {ThreadCount}");
            Logger.Verbose($"Process Count : {Environment.ProcessorCount}");
            Logger.Verbose($"Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0) : {Math.Ceiling(Environment.ProcessorCount * 0.75 * 1.0)}");

            new Thread(() =>
            {
                try
                {
                    Poll(0);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }).Start();


            Session.Closer.Interval = StandAlone == true ? 60 : 5;
            Session.Closer.ExpireAt = DateTime.UtcNow.AddSeconds(Session.Closer.Interval).Ticks;
            new Thread(async () =>
            {
                while (IsOk())
                {
                    try
                    {
                        await Task.Delay(1000);
                        Session.Closer.Update();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }

            }).Start();
        }

        virtual protected bool IsOk()
        {
            return isOpen;
        }

        public void Close()
        {
            isOpen = false;
        }
        public static ConcurrentDictionary<long, ConcurrentQueue<Session>> sessions = new ConcurrentDictionary<long, ConcurrentQueue<Session>>();

        ConcurrentDictionary<long, System.Threading.Tasks.Task> progresses = new();

        protected void Poll(int strand)
        {

            ConcurrentDictionary<long, Session> inProgress = new ConcurrentDictionary<long, Session>();

            while (IsOk())
            {
                bool wait = true;

                if (inProgress.Count > Api.MaxSession)
                {
                    Thread.Sleep(16);
                    continue;
                }

                foreach (var e in sessions)
                {
                    if (e.Value.Count == 0) { continue; }
                    if (inProgress.ContainsKey(e.Key))
                    {
                        continue;
                    }

                    if (e.Value.TryDequeue(out Session query) == false)
                    {
                        continue;
                    }

                    inProgress.TryAdd(e.Key, query);

                    async Task Execute(Session query)
                    {
                        try
                        {
                            query.CTS.CancelAfter(global::Framework.Caspar.Extensions.Database.QueryTimeoutSec);
                            await query.Command();
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Query Exception : {query.Trace}");
                            Logger.Error(e);
                            query.Rollback();
                            try
                            {
                                query.SetException(e);
                            }
                            catch
                            {

                            }

                        }
                        finally
                        {
                            query.Commit();
                        }

                        try
                        {
                            query.Close();
                            query.SetResult(0);
                        }
                        catch
                        {

                        }
                        inProgress.Pop(query.UID);
                    }

                    wait |= e.Value.Count == 0;


                    try
                    {
                        var task = Execute(query);

                    }
                    catch (Exception)
                    {

                    }
                }

                if (wait == true)
                {
                    Thread.Sleep(16);
                }
            }
        }

        public uint GetQueryCount()
        {
            uint count = 0;
            return count;
        }
    }
}
