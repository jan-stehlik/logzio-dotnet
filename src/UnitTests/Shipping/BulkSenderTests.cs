﻿using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Logzio.DotNet.Core.InternalLogger;
using Logzio.DotNet.Core.Shipping;
using Logzio.DotNet.Core.WebClient;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Logzio.DotNet.UnitTests.Shipping
{
    [TestFixture]
    public class BulkSenderTests
    {
        private BulkSender _target;
        private IHttpClient _httpClient;

        [SetUp]
        public void SetUp()
        {
            _httpClient = Substitute.For<IHttpClient>();
            _target = new BulkSender(_httpClient);
        }

        [Test]
        public void SendAsync_Logs_LogsAreSent()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData.Remove("@timestamp");
            _target.SendAsync(new[] { log }, new BulkSenderOptions()).Wait();
            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(ms.ToArray()).Equals("{\"message\":\"hey\"}")), Arg.Any<Encoding>());
        }

        [Test]
        public void Send_Logs_LogsAreFormatted()
        {
            _target.SendAsync(new[] { GetLoggingEventWithSomeData(), GetLoggingEventWithSomeData(), GetLoggingEventWithSomeData() },
                new BulkSenderOptions()).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(ms.ToArray()).Contains("\"message\":\"hey\"")), Arg.Any<Encoding>());
        }

        [Test]
        public void Send_LogWithNumericField_LogsAreFormatted()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData["id"] = 300;

            _target.SendAsync(new[] { log }, new BulkSenderOptions()).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(ms.ToArray()).Contains("\"id\":300")), Arg.Any<Encoding>());
        }

        [Test]
        public void Send_LogWithObjectField_LogsAreFormatted()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData["dummy"] = new DummyLogObject
            {
                SomeId = 42,
                SomeString = "The Answer"
            };

            _target.SendAsync(new[] { log }, new BulkSenderOptions()).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(ms.ToArray()).Contains("\"dummy\":{\"someId\":42,\"someString\":\"The Answer\"}")), Arg.Any<Encoding>());
        }

        [Test]
        public void Send_EmptyLogsList_ShouldntSendAnything()
        {
            _target.SendAsync(new List<LogzioLoggingEvent>(), new BulkSenderOptions()).Wait();

            _httpClient.ReceivedCalls().ShouldBeEmpty();
        }

        [Test]
        public void SendAsync_Logs_LogsAreSent_with_compression()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData.Remove("@timestamp");
            _target.SendAsync(new[] { log }, new BulkSenderOptions { UseCompression = true }).Wait();
            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(Decompress(ms.ToArray())).Equals("{\"message\":\"hey\"}")), Arg.Any<Encoding>(),true);
        }

        [Test]
        public void Send_Logs_LogsAreFormatted_with_compression()
        {
            _target.SendAsync(new[] { GetLoggingEventWithSomeData(), GetLoggingEventWithSomeData(), GetLoggingEventWithSomeData() },
                new BulkSenderOptions { UseCompression = true }).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(Decompress(ms.ToArray())).Contains("\"message\":\"hey\"")), Arg.Any<Encoding>(), true);
        }

        [Test]
        public void Send_LogWithNumericField_LogsAreFormatted_with_compression()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData["id"] = 300;

            _target.SendAsync(new[] { log }, new BulkSenderOptions { UseCompression = true }).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(Decompress(ms.ToArray())).Contains("\"id\":300")), Arg.Any<Encoding>(), true);
        }

        [Test]
        public void Send_LogWithObjectField_LogsAreFormatted_with_compression()
        {
            var log = GetLoggingEventWithSomeData();
            log.LogData["dummy"] = new DummyLogObject
            {
                SomeId = 42,
                SomeString = "The Answer"
            };

            _target.SendAsync(new[] { log }, new BulkSenderOptions { UseCompression = true }).Wait();

            _httpClient.Received()
                .PostAsync(Arg.Any<string>(), Arg.Is<MemoryStream>(ms => Encoding.UTF8.GetString(Decompress(ms.ToArray())).Contains("\"dummy\":{\"someId\":42,\"someString\":\"The Answer\"}")), Arg.Any<Encoding>(), true);
        }

        [Test]
        public void Send_EmptyLogsList_ShouldntSendAnything_with_compression()
        {
            _target.SendAsync(new List<LogzioLoggingEvent>(), new BulkSenderOptions { UseCompression = true }).Wait();

            _httpClient.ReceivedCalls().ShouldBeEmpty();
        }

        private LogzioLoggingEvent GetLoggingEventWithSomeData()
        {
            return new LogzioLoggingEvent(new Dictionary<string, object>
            {
                { "message", "hey" },
                { "@timestamp", "2016-01-01T01:01:01Z" },
            });
        }

        private static byte[] Decompress(byte[] gzip)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (var memory = new MemoryStream())
                {
                    int count;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }
    }

    public class DummyLogObject
    {
        public int SomeId { get; set; }
        public string SomeString { get; set; }
    }
}
