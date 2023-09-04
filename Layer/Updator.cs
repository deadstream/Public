using Amazon.SQS.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Framework.Caspar.Api;

namespace Framework.Caspar
{
    public partial class Layer
    {
        internal static object lockObj = new Object();
        static ConcurrentQueue<Entity> queue = null;

        internal static object mainLock = new Object();

        internal static int _state = 0;
        internal static int _handled = 0;
        public static void Process()
        {

            while (true)
            {
                try
                {
                    while (queue.Count > 0)
                    {
                        if (!queue.TryDequeue(out var e))
                        {
                            continue;
                        }

                        e.ToRun();
                        e.ToIdle();
                        Interlocked.Increment(ref _handled);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                lock (lockObj)
                {
                    if (Interlocked.CompareExchange(ref _handled, int.MaxValue, _state) == _state)
                    {
                        lock (mainLock)
                        {
                            Monitor.Pulse(mainLock);
                        }
                    }
                    Monitor.Wait(lockObj);
                }

            }

        }

        public static BlockingCollection<ConcurrentQueue<Entity>> Entities = new();
        public static BlockingCollection<Layer> WaitLayers = new();
        public static void UpdateInit()
        {
            queue = new ConcurrentQueue<Entity>();

            for (int i = 0; i < 16; ++i)
            {
                var thread = new Thread(Process);
                thread.Priority = System.Threading.ThreadPriority.Highest;
                thread.Start();
            }

            new Thread(() =>
            {
                while (true)
                {

                    var layer = WaitLayers.Take();
                    ConcurrentQueue<Entity> q = null;
                    lock (layer)
                    {
                        if (layer.waitEntities.Count == 0)
                        {
                            Console.WriteLine("q is 0");
                        }
                        else
                        {

                        }

                        q = layer.waitEntities;
                        layer.waitEntities = new ConcurrentQueue<Entity>();

                    }

                    if (layer.ToRun())
                    {
                        layer.OnUpdate();

                        lock (mainLock)
                        {
                            if (q.Count > 0)
                            {
                                lock (lockObj)
                                {
                                    Interlocked.Exchange(ref _state, q.Count);
                                    Interlocked.Exchange(ref _handled, 0);
                                    queue = q;
                                    Monitor.PulseAll(lockObj);
                                }
                            }
                            else
                            {
                            }
                            Monitor.Wait(mainLock);
                        }
                    }
                    else
                    {
                    }
                    layer.ToIdle();
                    if (layer.IsPost())
                    {
                        if (layer.ToWait())
                        {
                            WaitLayers.Add(layer);
                        }
                    }
                }
            }).Start();
        }

    }
}
