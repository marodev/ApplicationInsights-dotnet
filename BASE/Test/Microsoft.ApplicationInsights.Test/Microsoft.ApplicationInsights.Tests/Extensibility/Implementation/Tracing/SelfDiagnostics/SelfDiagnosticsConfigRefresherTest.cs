﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing.SelfDiagnostics
{
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SelfDiagnosticsConfigRefresherTest
    {
        private static readonly string ConfigFilePath = SelfDiagnosticsConfigParser.ConfigFileName;
        private static readonly byte[] MessageOnNewFile = MemoryMappedFileHandler.MessageOnNewFile;
        private static readonly string MessageOnNewFileString = Encoding.UTF8.GetString(MessageOnNewFile);

        private static readonly Regex TimeStringRegex = new Regex(
            @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [TestMethod]
        public void SelfDiagnosticsConfigRefresher_OmitAsConfigured()
        {
            try
            {
                CreateConfigFile();
                using (var configRefresher = new SelfDiagnosticsConfigRefresher())
                {
                    // Emitting event of EventLevel.Warning
                    CoreEventSource.Log.OperationIsNullWarning();

                    int bufferSize = 512;
                    byte[] actualBytes = ReadFile(bufferSize);
                    string logText = Encoding.UTF8.GetString(actualBytes);
                    Assert.IsTrue(logText.StartsWith(MessageOnNewFileString));

                    // The event was omitted
                    Assert.AreEqual('\0', (char)actualBytes[MessageOnNewFile.Length]);
                }}
            finally
            {
                CleanupConfigFile();
            }
        }

        [TestMethod]
        public void SelfDiagnosticsConfigRefresher_CaptureAsConfigured()
        {
            try
            {
                CreateConfigFile();
                using (var configRefresher = new SelfDiagnosticsConfigRefresher())
                {
                    // Emitting event of EventLevel.Error
                    CoreEventSource.Log.InvalidOperationToStopError();

                    int bufferSize = 512;
                    byte[] actualBytes = ReadFile(bufferSize);
                    string logText = Encoding.UTF8.GetString(actualBytes);
                    Assert.IsTrue(logText.StartsWith(MessageOnNewFileString));

                    // The event was captured
                    string logLine = logText.Substring(MessageOnNewFileString.Length);
                    string logMessage = ParseLogMessage(logLine);
                    string expectedMessage = "Operation to stop does not match the current operation. Telemetry is not tracked.";
                    Assert.IsTrue(logMessage.StartsWith(expectedMessage));
                }
            }
            finally
            {
                CleanupConfigFile();
            }
        }

        private static void AssertStartsWith(string expectedStartString, string actualString)
        {
            Assert.IsNotNull(expectedStartString);
            Assert.IsTrue(expectedStartString.StartsWith(actualString));
        }

        private static string ParseLogMessage(string logLine)
        {
            int timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
            Assert.IsTrue(TimeStringRegex.IsMatch(logLine.Substring(0, timestampPrefixLength)));
            return logLine.Substring(timestampPrefixLength);
        }

        private static byte[] ReadFile(int byteCount)
        {
            var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + "."
                    + Process.GetCurrentProcess().Id + ".log";
            var outputFilePath = Path.Combine(".", outputFileName);
            using (var file = File.Open(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] actualBytes = new byte[byteCount];
                file.Read(actualBytes, 0, byteCount);
                return actualBytes;
            }
        }

        private static void CreateConfigFile()
        {
            string configJson = @"{
                    ""LogDirectory"": ""."",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }";
            using (FileStream file = File.Open(ConfigFilePath, FileMode.Create, FileAccess.Write))
            {
                byte[] configBytes = Encoding.UTF8.GetBytes(configJson);
                file.Write(configBytes, 0, configBytes.Length);
            }
        }

        private static void CleanupConfigFile()
        {
            try
            {
                File.Delete(ConfigFilePath);
            }
            catch
            {
            }
        }
    }
}
