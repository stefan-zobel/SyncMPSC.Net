/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using SyncMPSC;
using SyncMPSC.Ipc.Sockets;

namespace Showcase.Old;

public class MessagingLibShowcaseImpl
{
    private static MessagingLibClientServiceImpl _clientService = null!;
    private static MessagingLibServerServiceImpl _serverService = null!;

    private string _ipOrHostname = "localhost";
    private static readonly byte[] _writeBuf = new byte[5];

    private static readonly CallStats MainQueueStats = new("ABCDEF");
    private static readonly CallStats SecondaryQueueStats = new("GHIJKL");

    private const int SERVER_WRITE_PORT = 33332;
    private const int SERVER_READ_PORT_QUEUE_MAIN = 33333;
    private const int SERVER_READ_PORT_QUEUE_SECONDARY = 44444;

    public static void Main(string[] args)
    {
        PropertyService.LoadFromFile("./../../../showcase.properties");

        string logPath = PropertyService.GetProperty(MessagingConstants.LOG_PATH) ?? "logs/default.log";
        string minLevel = PropertyService.GetProperty(MessagingConstants.LOG_LEVEL) ?? "Information";
        string? logTemplate = PropertyService.GetProperty(MessagingConstants.LOG_TEMPLATE);

        LogManager.Initialize(logPath, minLevel, logTemplate);

        _serverService = new MessagingLibServerServiceImpl();
        _clientService = new MessagingLibClientServiceImpl();

        var showcase = new MessagingLibShowcaseImpl();
        showcase.Init();
        for (int i = 0; i < 7; i++)
        {
            showcase.Execute();
        }
    }

    public void Init()
    {
        _clientService.Init();
        _serverService.Init();

        string? optionalAddress = PropertyService.GetProperty(MessagingConstants.PROPERTY_SERVER);
        if (!string.IsNullOrWhiteSpace(optionalAddress))
        {
            _ipOrHostname = optionalAddress.Trim();
        }
    }

    public void Execute()
    {
        // handlers via lambdas (equivalent to anonymous inner classes)
        var mainHandler = new ActionMessageHandler(message =>
        {
            int value = GetIntL(message);
            MainQueueStats.Update(value);
        });

        var secondaryHandler = new ActionMessageHandler(message =>
        {
            int value = GetIntL(message);
            SecondaryQueueStats.Update(value);
        });

        string idSecondary = "GHIJKL";

        IQueueSender? sender = _clientService.GetDefaultQueueSender();
        if (sender == null) throw new InvalidOperationException("Sender not correctly configured!");

        // Receiver Setup 1
        IQueueReceiver rcMain = _clientService.CreateDefaultQueueReceiver(mainHandler);
        IQueueReceiver rcSec = _clientService.CreateQueueReceiver(idSecondary, _ipOrHostname, SERVER_READ_PORT_QUEUE_SECONDARY, secondaryHandler);

        rcMain.Start();
        rcSec.Start();

        StopSenderAndReceiver(rcMain, rcSec, 6, sender);

        // Receiver Setup 2
        rcMain = _clientService.CreateDefaultQueueReceiver(mainHandler);
        rcSec = _clientService.CreateQueueReceiver(idSecondary, _ipOrHostname, SERVER_READ_PORT_QUEUE_SECONDARY, secondaryHandler);

        rcMain.Start();
        rcSec.Start();

        StopSenderAndReceiver(rcMain, rcSec, 8, sender);

        Thread.Sleep(2_000);

        MainQueueStats.Print();
        SecondaryQueueStats.Print();
        MainQueueStats.Reset();
        SecondaryQueueStats.Reset();

        // Statistics (TODO)
        Console.WriteLine("Send stats placeholder - Average: 0.0 ms");

        // No shutdown, that would stop the threads
        /*
        _clientService.Shutdown();
        _serverService.Shutdown();
        */
    }

    private static void StopSenderAndReceiver(IQueueReceiver rc1, IQueueReceiver rc2, int stopAtIteration, IQueueSender sender)
    {
        long start = 0;
        bool ok = true;
        for (int i = 1; i <= 8; i++)
        {
            // send 10_000 messages
            for (int j = 1; j <= 10_000 && ok; j++)
            {
                PutIntL(MainQueueStats.StartVal, _writeBuf);
                ok = sender.SendMessage(_writeBuf);
                if (!ok) Console.Error.WriteLine("SHOWCASE FAILED TO SEND MESSAGE !!!");

                MainQueueStats.IncStartVal();
            }

            if (i == stopAtIteration)
            {
                start = Environment.TickCount64;
                Console.WriteLine($"{rc1.Id}.stop: {rc1.Stop()}");
                if (rc2 != null) Console.WriteLine($"{rc2.Id}.stop: {rc2.Stop()}");
                break;
            }
            else
            {
                Thread.Sleep(2_000);
            }
        } // for

        if (rc1.IsRunning) Console.WriteLine($"{rc1.Id}.stop: {rc1.Stop()}");
        if (rc2 != null && rc2.IsRunning) Console.WriteLine($"{rc2.Id}.stop: {rc2.Stop()}");

        sender.Dispose();
        long duration = Environment.TickCount64 - start;
        Console.WriteLine($"Done (shutdown took: {duration} ms)");
    }

    #region Byte Mapping Helpers

    static int GetIntL(byte[] bytes)
    {
        // Java: makeInt(bytes[5], bytes[4], bytes[3], bytes[2])
        return bytes[5] << 24 |
                (bytes[4] & 0xff) << 16 |
                (bytes[3] & 0xff) << 8 |
                bytes[2] & 0xff;
    }

    private static byte[] PutIntL(int x, byte[] bytes)
    {
        bytes[4] = (byte)(x >> 24);
        bytes[3] = (byte)(x >> 16);
        bytes[2] = (byte)(x >> 8);
        bytes[1] = (byte)x;
        bytes[0] = 0;
        return bytes;
    }

    #endregion

    private class CallStats(string name)
    {
        private int _firstSeen = -1;
        private int _lastSeen = -1;
        private int _maxSeen = int.MinValue;
        private int _callCount = 0;
        private int _startVal = 0;

        public int StartVal => Volatile.Read(ref _startVal);
        public void IncStartVal() => Interlocked.Increment(ref _startVal);

        public void Update(int value)
        {
            // Thread-safe update for maxSeen
            int currentMax;
            while (value > (currentMax = Volatile.Read(ref _maxSeen)))
            {
                Interlocked.CompareExchange(ref _maxSeen, value, currentMax);
            }

            int count = Interlocked.Increment(ref _callCount);
            if (count == 1) _firstSeen = value;
            _lastSeen = value;
        }

        public void Print()
        {
            Console.WriteLine($"--- {name} ---");
            Console.WriteLine($"first_seen        : {_firstSeen}");
            Console.WriteLine($"last_seen         : {_lastSeen}");
            Console.WriteLine($"max_seen          : {Volatile.Read(ref _maxSeen)}");
            Console.WriteLine($"call count        : {Volatile.Read(ref _callCount) - 1}");
            Console.WriteLine($"last value written: {StartVal - 1}\n");
        }

        public void Reset()
        {
            _firstSeen = -1;
            _lastSeen = -1;
            Interlocked.Exchange(ref _maxSeen, int.MinValue);
            Interlocked.Exchange(ref _callCount, 0);
        }
    }

    // Helper to map Action to IMessageHandler
    private class ActionMessageHandler(Action<byte[]> onNext) : IMessageHandler
    {
        public void OnNext(byte[] message) => onNext(message);
    }
}
