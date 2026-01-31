/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace SyncMPSC.Ipc.Sockets;

internal static class LogManager
{
    private const string javaStyleLogTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{ThreadId}] {Level:u4} {SourceContext} - {Message:lj}{NewLine}{Exception}";

    private static ILoggerFactory? _factory;

    internal static ILogger<T> GetLogger<T>()
        => _factory?.CreateLogger<T>() ?? throw new InvalidOperationException("LogManager is not initialized!");

    /// <summary>
    /// Initialize the global logging system.
    /// </summary>
    internal static void Initialize(string logPath, string minLevel, string? logTemplate = null)
    {
        // Serilog Logger-Konfiguration
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(Enum.Parse<LogEventLevel>(minLevel, true))
            .Enrich.WithThreadId() // needs package Serilog.Enrichers.Thread
            .WriteTo.Console() // needs package Serilog.Sinks.Console
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: logTemplate ?? javaStyleLogTemplate)
            .CreateLogger();

        // Connect the Microsoft LoggerFactory with Serilog
        // Note: No 'using' since the factory has to exist
        // for the whole application lifecycle
        _factory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(serilogLogger);
        });

        // Optional: Global access for static Serilog calls
        Log.Logger = serilogLogger;
    }
}
