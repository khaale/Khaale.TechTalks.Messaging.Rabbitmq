using System;
using System.Collections.Concurrent;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Demo.DAL;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Demo.Entities;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Infrastructure;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples
{
	[TestFixture]
	public class ReliabilityTests
	{
		#region Support 

		private RabbitFacade _publisher;
		private RabbitFacade _consumerFacade;
		private int _counter;

		private static void ResetDb()
		{
			var initializer = new DropCreateDatabaseAlways<DemoDataContext>();
			Database.SetInitializer(initializer);
			using (var ctx = new DemoDataContext())
			{
				ctx.Database.Initialize(force: true);
			}
		}

		private static bool RunPublish(Func<int, Task> publishFunc, int count)
		{
			var maxRetriesCount = 3;
			var tasks = Enumerable.Range(1, count).Select(async i =>
			{
				var retriesCount = 0;
				do
				{
					try
					{
						await publishFunc(i);
						break;
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex.Message);
						retriesCount++;
					}
				} while (retriesCount < maxRetriesCount);
			}).ToArray();

			var completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

			return completed;
		}

		private void ThrowException(int iteration, ref int counter)
		{
			counter++;

			if (iteration == counter)
			{
				throw new ApplicationException(string.Format("Error on {0} iteration!", iteration));
			}
		}

		[SetUp]
		public void SetUp()
		{
			ResetDb();

			_publisher = new RabbitFacade();
			_publisher.Initialize();
			_consumerFacade = new RabbitFacade();
			_consumerFacade.Initialize();

			_publisher.CreateQueue(RabbitFacade.QueueName);

			_counter = 0;

		}

		[TearDown]
		public void TearDown()
		{
			_publisher.Deinitialize();
			_consumerFacade.Deinitialize();
		}

		#endregion

		#region At most once

		[Test]
		public void Publish_AtMostOnce()
		{
			//arrange
			Func<int, Task> publish = async id =>
			{
				using (var ctx = new DemoDataContext())
				//Transaction Scope with async was fixed only in .NET 4.5.1
				//Transaction Scope usage is not recommended by MS
				using (var tx = ctx.Database.BeginTransaction())
				//TODO: is it OK to create channel for a single publish from a performance point of view?
				using (var channel = _publisher.CreateChannel())
				{
					//create new item
					var newItem = new ProducedItem { Id = id, IsSent = false };
					ctx.ProducedItems.Add(newItem);

					//send message
					var message = _publisher.PrepareMessage(id.ToString());

					channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);
					newItem.IsSent = true;

					//ThrowException(50, ref _counter);

					//commit
					await ctx.SaveChangesAsync();
					tx.Commit();
				}
			};

			//act			
			var completed = RunPublish(publish, 100);

			//assert
			Assert.That(completed, Is.True);
			var messagesCount = _publisher.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messagesCount, Is.EqualTo(100));
		}
		
		[Test]
		public void Consume_AtMostOnce()
		{
			//arrange
			for (var i = 0; i < 100; i++)
			{
				_publisher.PublishMessage(RabbitFacade.QueueName, i.ToString());
			}

			//act
			var channel = _consumerFacade.CreateChannel();
			var consumer = new QueueingBasicConsumer(channel);
			channel.BasicConsume(RabbitFacade.QueueName, true, consumer);
			BasicDeliverEventArgs ea;

			//receiving message
			while (consumer.Queue.Dequeue(100, out ea))
			{
				try
				{
					using (var ctx = new DemoDataContext())
					using (var tx = ctx.Database.BeginTransaction())
					{
						//some business logic
						var message = int.Parse(Encoding.UTF8.GetString(ea.Body));
						var consumedItem = new ConsumedItem { Id = message };
						ctx.ConsumedItems.Add(consumedItem);

						//ThrowException(50, ref _counter);

						//commit db transaction
						ctx.SaveChanges();
						tx.Commit();
					}
				}
				catch (ApplicationException ex)
				{
					Debug.WriteLine(ex.Message);
				}
			}

			//assert
			using (var ctx = new DemoDataContext())
			{
				var consumedCount = ctx.ConsumedItems.Count();
				Assert.That(consumedCount, Is.EqualTo(100));
			}
		}

		#endregion

		#region At least once

		[Test]
		public void Publish_AtLeastOnce_Transactional()
		{
			//arrange
			Func<int, Task> publish = async id =>
			{
				using (var ctx = new DemoDataContext())
				using (var tx = ctx.Database.BeginTransaction())
				using (var channel = _publisher.CreateChannel())
				{
					//create new item
					var newItem = new ProducedItem { Id = id, IsSent = false };
					ctx.ProducedItems.Add(newItem);

					//send message
					var message = _publisher.PrepareMessage(id.ToString());

					try
					{
						channel.TxSelect();
						channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);
						newItem.IsSent = true;

						//ThrowException(50, ref _counter);

						channel.TxCommit();
					}
					catch (Exception)
					{
						channel.TxRollback();
						throw;
					}

					//commit
					await ctx.SaveChangesAsync();
					tx.Commit();
				}
			};

			//act			
			var completed = RunPublish(publish, 100);

			//assert
			Assert.That(completed, Is.True);
			var messagesCount = _publisher.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messagesCount, Is.EqualTo(100));
		}

		[Test]
		public void Publish_AtLeastOnce_PublisherConfirm()
		{
			//configuring main processing
			Func<int, Task> publishFunc = (async id =>
			{
				using (var ctx = new DemoDataContext())
				using (var tx = ctx.Database.BeginTransaction())
				using (var channel = _publisher.CreateChannel())
				{
					//configuring acks processing
					var pendingAckTasks = new ConcurrentDictionary<ulong, TaskCompletionSource<ulong>>();
					channel.ConfirmSelect();
					channel.BasicAcks += (model, args) =>
					{
						Debug.WriteLine("Confirm: dt={0}, multiple={1}", args.DeliveryTag, args.Multiple);
						if (!args.Multiple)
						{
							TaskCompletionSource<ulong> tcs;
							if (pendingAckTasks.TryGetValue(args.DeliveryTag, out tcs))
							{
								tcs.TrySetResult(args.DeliveryTag);
							}
						}
						else
						{
							foreach (var kvp in pendingAckTasks.Where(kvp => kvp.Key <= args.DeliveryTag))
							{
								kvp.Value.TrySetResult(args.DeliveryTag);
							}
						}
					};

					//create new item
					var newItem = new ProducedItem { Id = id, IsSent = false };
					ctx.ProducedItems.Add(newItem);

					//send message
					var message = _publisher.PrepareMessage(id.ToString());

					var deliveryTag = channel.NextPublishSeqNo;
					var ackReceived = new TaskCompletionSource<ulong>(deliveryTag);
					pendingAckTasks[deliveryTag] = ackReceived;

					channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);

					//async waiting for ack
					if (await Task.WhenAny(ackReceived.Task, Task.Delay(TimeSpan.FromSeconds(1))) != ackReceived.Task)
					{
						ackReceived.TrySetCanceled();
						throw new ApplicationException(string.Format("Id = {0}, DeliveryTag = {1}: Message wasn't acked!", id, deliveryTag));
					}

					newItem.IsSent = true;

					//commit
					await ctx.SaveChangesAsync();
					tx.Commit();
				}
			});

			//act			
			var completed = RunPublish(publishFunc, 100);

			//assert
			Assert.That(completed, Is.True);
			var messagesCount = _publisher.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messagesCount, Is.EqualTo(100));
		}

		[Test]
		public void Consume_AtLeastOnce()
		{
			//arrange
			for (var i = 0; i < 100; i++)
			{
				_publisher.PublishMessage(RabbitFacade.QueueName, i.ToString());
			}

			//act
			var channel = _consumerFacade.CreateChannel();
			var consumer = new QueueingBasicConsumer(channel);
			channel.BasicConsume(RabbitFacade.QueueName, false, consumer);
			BasicDeliverEventArgs ea;

			//receiving message
			while (consumer.Queue.Dequeue(100, out ea))
			{
				try
				{
					using (var ctx = new DemoDataContext())
					using (var tx = ctx.Database.BeginTransaction())
					{
						//some business logic
						var message = int.Parse(Encoding.UTF8.GetString(ea.Body));
						var consumedItem = new ConsumedItem { Id = message };
						ctx.ConsumedItems.Add(consumedItem);

						ThrowException(50, ref _counter);

						//commit db transaction
						ctx.SaveChanges();
						tx.Commit();
					}
					channel.BasicAck(ea.DeliveryTag, false);
				}
				catch (ApplicationException ex)
				{
					Debug.WriteLine(ex.Message);
					channel.BasicNack(ea.DeliveryTag, false, true);
				}
			}
			channel.Dispose();

			//assert
			using (var ctx = new DemoDataContext())
			{
				var consumedCount = ctx.ConsumedItems.Count();
				Assert.That(consumedCount, Is.EqualTo(100));
			}
		}

		#endregion
	}
}
