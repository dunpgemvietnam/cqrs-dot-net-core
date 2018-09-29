﻿using IotHub.Core.Cqrs;
using IotHub.Core.Redis;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace IotHub.Core.CqrsEngine
{
    public static class EngineeEventWorkerQueue
    {
        //in-memory queue, can be use redis queue, rabitmq ...
        // remember dispatched by type of event
        static readonly ConcurrentDictionary<Type, ConcurrentQueue<IEvent>> _cmdDataQueue = new ConcurrentDictionary<Type, ConcurrentQueue<IEvent>>();

        static readonly ConcurrentDictionary<Type, List<Thread>> _cmdWorker = new ConcurrentDictionary<Type, List<Thread>>();
        static readonly ConcurrentDictionary<Type, bool> _stopWorker = new ConcurrentDictionary<Type, bool>();
        static readonly ConcurrentDictionary<Type, int> _workerCounterStoped = new ConcurrentDictionary<Type, int>();
        static readonly ConcurrentDictionary<Type, bool> _workerStoped = new ConcurrentDictionary<Type, bool>();
        static readonly ConcurrentDictionary<string, Type> _cmdTypeName = new ConcurrentDictionary<string, Type>();
        static readonly object _locker = new object();

        public static void Push(IEvent cmd)
        {
            var type = cmd.GetType();

            if (RedisServices.IsEnable)
            {
                var queueName = BuildRedisQueueName(type);

                if (RedisServices.RedisDatabase.KeyExists(queueName))
                {
                    RedisServices.RedisDatabase
                        .ListLeftPush(queueName, JsonConvert.SerializeObject(cmd));
                }
                else
                {
                    _cmdTypeName[type.FullName] = type;

                    RedisServices.RedisDatabase
                        .ListLeftPush(queueName, JsonConvert.SerializeObject(cmd));

                    InitFirstWorker(type);
                }
            }
            else
            {
                if (_cmdDataQueue.ContainsKey(type) && _cmdDataQueue[type] != null)
                {
                    //in-memory queue, can be use redis queue, rabitmq ...
                    _cmdDataQueue[type].Enqueue(cmd);
                }
                else
                {
                    _cmdTypeName[type.FullName] = type;

                    //in-memory queue, can be use redis queue, rabitmq ...
                    _cmdDataQueue[type] = new ConcurrentQueue<IEvent>();
                    _cmdDataQueue[type].Enqueue(cmd);

                    InitFirstWorker(type);
                }
            }

        }

        private static string BuildRedisQueueName(Type type)
        {
            return "EngineeEventWorkerQueue_" + type.FullName;
        }

        private static void InitFirstWorker(Type type)
        {
            while (_stopWorker.ContainsKey(type) && _stopWorker[type])
            {
                Thread.Sleep(100);
                //wait stopping
            }

            lock (_locker)
            {

                if (!_cmdWorker.ContainsKey(type) || _cmdWorker[type] == null || _cmdWorker[type].Count == 0)
                {
                    _stopWorker[type] = false;
                    _workerCounterStoped[type] = 0;
                    _workerStoped[type] = false;

                    _cmdWorker[type] = new List<Thread>();
                }

                var firstThread = new Thread(() => { WorkerDo(type); });

                _cmdWorker[type].Add(firstThread);

                firstThread.Start();
            }
        }

        static EngineeEventWorkerQueue()
        {

        }

        static void WorkerDo(Type type)
        {
            while (true)
            {
                try
                {
                    while (_stopWorker.ContainsKey(type) == false || _stopWorker[type] == false)
                    {
                        try
                        {
                            if (RedisServices.IsEnable)
                            {
                                var queueName = BuildRedisQueueName(type);

                                var cmdJson = RedisServices.RedisDatabase
                                    .ListRightPop(queueName);
                                if (cmdJson.HasValue)
                                {
                                    var evt = JsonConvert.DeserializeObject(cmdJson, type) as IEvent;
                                    if (evt != null)
                                    {
                                        CommandsAndEventsRegisterEngine.ExecEvent(evt);
                                    }
                                }
                            }
                            else
                            {
                                if (_cmdDataQueue.TryGetValue(type, out ConcurrentQueue<IEvent> cmdQueue) &&
                                    cmdQueue != null)
                                {
                                    //in-memory queue, can be use redis queue, rabitmq ...
                                    if (cmdQueue.TryDequeue(out IEvent evt) && evt != null)
                                    {
                                        CommandsAndEventsRegisterEngine.ExecEvent(evt);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            Thread.Sleep(0);
                        }
                    }

                    if (!_workerCounterStoped.ContainsKey(type))
                    {
                        _workerCounterStoped[type] = 0;
                    }
                    if (_workerStoped[type] == false)
                    {
                        var counter = _workerCounterStoped[type];
                        counter++;
                        _workerCounterStoped[type] = counter;

                        lock (_locker)
                        {
                            if (_cmdWorker.TryGetValue(type, out List<Thread> listThread))
                            {
                                if (listThread.Count == counter)
                                {
                                    _workerStoped[type] = true;
                                    _workerCounterStoped[type] = 0;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// reset thread to one. each command have one thread to process
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool ResetToOneWorker(Type type)
        {
            _stopWorker[type] = true;

            while (!_workerStoped.ContainsKey(type) || _workerStoped[type] == false)
            {
                Thread.Sleep(100);
                //wait all worker done its job
            }

            //List<Thread> threads;

            //if (_cmdWorker.TryGetValue(type, out threads))
            //{
            //    foreach (var t in threads)
            //    {
            //        try
            //        {
            //            t.Abort();
            //        }
            //        catch { }
            //    }
            //}

            _workerCounterStoped[type] = 0;
            _workerStoped[type] = false;
            _cmdWorker[type].Clear();
            _stopWorker[type] = false;

            InitFirstWorker(type);

            return true;
        }

        public static bool AddAndStartWorker(Type type)
        {
            if (!_cmdWorker.ContainsKey(type) || _cmdWorker[type] == null || _cmdWorker[type].Count == 0)
            {
                InitFirstWorker(type);
            }
            else
            {
                lock (_locker)
                {
                    _workerStoped[type] = false;
                    var thread = new Thread(() => WorkerDo(type));
                    _cmdWorker[type].Add(thread);
                    thread.Start();
                }
            }

            return true;
        }

        public static void CountStatistic(Type type, out int queueDataCount, out int workerCount)
        {
            queueDataCount = 0;
            workerCount = 0;
            if (_cmdWorker.TryGetValue(type, out List<Thread> list) && list != null)
            {
                workerCount = list.Count;
            }
            if (RedisServices.IsEnable)
            {
                var queueName = BuildRedisQueueName(type);

                queueDataCount = (int)RedisServices.RedisDatabase.ListLength(queueName);
            }
            else
            {
                if (_cmdDataQueue.TryGetValue(type, out ConcurrentQueue<IEvent> queue) && queue != null)
                {
                    queueDataCount = queue.Count;
                }
            }
        }

        public static bool IsWorkerStopping(Type type)
        {
            bool val;
            if (_stopWorker.TryGetValue(type, out val))
            {
                return val;
            }

            return false;
        }

        public static void Init()
        {

        }

        public static List<string> ListAllCommandName()
        {
            lock (_locker)
            {
                return _cmdTypeName.Select(i => i.Key).ToList();
            }
        }

        public static Type GetType(string fullName)
        {
            lock (_locker)
            {
                return _cmdTypeName[fullName];
            }
        }
    }
}