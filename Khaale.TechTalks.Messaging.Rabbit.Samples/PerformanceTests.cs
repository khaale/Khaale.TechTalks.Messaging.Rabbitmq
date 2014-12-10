using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Infrastructure;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples
{
	[TestFixture, Ignore("Taking a lot of time")]
	public class PerformanceTests
	{
		private RabbitFacade _publisher;
		private RabbitFacade _consumer;

		[SetUp]
		public void SetUp()
		{
			_publisher = new RabbitFacade();
			_publisher.Initialize();
			_consumer= new RabbitFacade();
			_consumer.Initialize();
		}
		
		[TearDown]
		public void TearDown()
		{
			_publisher.Deinitialize();
			_consumer.Deinitialize();
		}

		[Test, Combinatorial]
		public void PublishPerformance(
			[Values(true, false)]bool isPersistent, 
			[Values(10, 1024, 1024*1024, 50*1024*1024)] int messageSize, 
			[Values(1, 2, 5, 10)] int connectionCount,
			[Values(1, 2, 5, 10)] int channelsPerConnectionCount)
		{
			var queueName = RabbitFacade.QueueName;

			var totalCount = Math.Min(100*1024*1024/messageSize, 10000);
			var countPerWorker = totalCount/connectionCount/channelsPerConnectionCount;

			Prepare(queueName);

			Func<IConnection, Action> publish = connection => (() =>
			{
				using (var channel = connection.CreateModel())
				{
					var body = Enumerable.Range(1, messageSize).Select(_ => (byte) 'X').ToArray();
					Run(
						body,
						countPerWorker,
						msgBody =>
						{
							var prop = new BasicProperties();
							prop.SetPersistent(isPersistent);
							channel.BasicPublish("", queueName, prop, msgBody);
						},
						false);
				}
			});

			var publishActions = Enumerable.Range(1, connectionCount)
				.Select(_ => _publisher.CreateConnection())
				.SelectMany(conn => Enumerable.Range(1, channelsPerConnectionCount).Select(_ => publish(conn)))
				.ToArray();

			var stw = Stopwatch.StartNew();

			Parallel.Invoke(publishActions);

			stw.Stop();

			PrintResult(messageSize, totalCount, stw);
			_publisher.DeleteQueue(queueName);
		}

		private void Run(byte[] body, int count, Action<byte[]> action, bool printResult = true)
		{
			var stw = Stopwatch.StartNew();

			for (var i = 0; i < count; i++)
			{
				action(body);
			}

			stw.Stop();

			if (printResult)
				PrintResult(body.Length, count, stw);

		}

		private static void PrintResult(int length, int count, Stopwatch stw)
		{
			Debug.WriteLine(
				"{0:.00} messages/sec, {1} mb/sec, total {2} for {3} messages and {4} mb",
				count/stw.Elapsed.TotalSeconds,
				count * length / stw.Elapsed.TotalSeconds / (double)(1024 * 1024),
				stw.Elapsed,
				count,
				count * length / (double)(1024 * 1024));
		}

		private void Prepare(string queueName)
		{
			_publisher.CreateQueue(queueName);
			_publisher.PublishMessage(queueName, "test");
			_publisher.ConsumeMessage(queueName);
		}
	}
}
