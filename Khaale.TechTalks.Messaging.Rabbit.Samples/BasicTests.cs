using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Infrastructure;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples
{
	[TestFixture]
    public class BasicTests
	{
		#region Support

		private RabbitFacade _rabbit;

		[SetUp]
		public void SetUp()
		{
			_rabbit = new RabbitFacade();
			_rabbit.Initialize();
		}

		[TearDown]
		public void TearDown()
		{
			_rabbit.Deinitialize();
		}
		
		#endregion

		#region Publish

		[Test]
		public void Publish_Basic()
		{
			var queueName = RabbitFacade.QueueName;

			//prepare model
			var factory = new ConnectionFactory
			{
				HostName = "localhost", 
				VirtualHost = RabbitFacade.VirtualHost
			};
			using (IConnection connection = factory.CreateConnection())
			using (IModel channel = connection.CreateModel())
			{
				//create queue
				channel.QueueDeclare(
					queue: queueName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);
				//purge queue
				channel.QueuePurge(queueName);

				//send message
				var message = Guid.NewGuid().ToString();
				var body = Encoding.UTF8.GetBytes(message);
				var properties = new BasicProperties();
				properties.SetPersistent(true);

				channel.BasicPublish(
					exchange: "",
					routingKey: queueName,
					mandatory: false,
					immediate: false,
					basicProperties: properties,
					body: body);
			}

			//assert
			var messageCount = _rabbit.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messageCount, Is.EqualTo(1));
		}
		
		[Test]
		public void Publish_Transactional_WithCommit()
		{
			//arrange
			_rabbit.CreateQueue(RabbitFacade.QueueName);
			var message = _rabbit.PrepareMessage(Guid.NewGuid().ToString());

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				channel.TxSelect();
				channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);
				channel.TxCommit();
			}

			//assert
			var messageCount = _rabbit.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messageCount, Is.EqualTo(1));
		}

		[Test]
		public void Publish_Transactional_WithRollback()
		{
			//arrange
			_rabbit.CreateQueue(RabbitFacade.QueueName);
			var message = _rabbit.PrepareMessage(Guid.NewGuid().ToString());

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				channel.TxSelect();
				channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);
				channel.TxRollback();
			}

			//assert
			var messageCount = _rabbit.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messageCount, Is.EqualTo(0));
		}

		[Test]
		public void Publish_Transactional_WithoutAction()
		{
			//arrange
			_rabbit.CreateQueue(RabbitFacade.QueueName);
			var message = _rabbit.PrepareMessage(Guid.NewGuid().ToString());

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				channel.TxSelect();
				channel.BasicPublish("", RabbitFacade.QueueName, message.Item2, message.Item1);
				Thread.Sleep(100);
			}

			//assert
			var messageCount = _rabbit.GetMessageCount(RabbitFacade.QueueName);
			Assert.That(messageCount, Is.EqualTo(0));
		}

		[Test]
		public void Publish_PublisherConfirms()
		{
			//arrange
			_rabbit.CreateQueue(RabbitFacade.QueueName);				

			var deliveredEvent = new AutoResetEvent(false);
			bool delivered = false;
			var message = _rabbit.PrepareMessage(Guid.NewGuid().ToString());

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				channel.ConfirmSelect();
				channel.BasicAcks += (model, args) =>
				{
					delivered = true;
					deliveredEvent.Set();
				};

				channel.BasicPublish("", RabbitFacade.QueueName, true, message.Item2, message.Item1);
				deliveredEvent.WaitOne(1000);
			}

			//assert
			Assert.That(delivered, Is.True);
		}

		#endregion

		#region Consume

		[Test]
		public void Consume_Poll()
		{
			//arrange
			var queueName = RabbitFacade.QueueName;
			_rabbit.CreateQueue(queueName);
			_rabbit.PublishMessage(queueName, "Test");
			BasicGetResult receivedMessage;

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				//receive message using synchronious blocking request to queue
				receivedMessage = channel.BasicGet(
					queue: queueName,
					noAck: false);
			}

			//assert
			Assert.IsNotNull(receivedMessage);
		}

		[Test]
		public void Consume_Push()
		{
			//arrange
			var queueName = RabbitFacade.QueueName;
			_rabbit.CreateQueue(queueName);
			_rabbit.PublishMessage(queueName, "Test");
			BasicDeliverEventArgs receivedMessage;

			//act
			using (var channel = _rabbit.CreateChannel())
			{
				var consumer = new QueueingBasicConsumer(channel);

				//init consuming
				channel.BasicConsume(
					queue: queueName,
					noAck: true,
					consumer: consumer);
				//getting messages from queue
				consumer.Queue.Dequeue(100, out receivedMessage);
			}

			//assert
			Assert.IsNotNull(receivedMessage);
		}

		#endregion
	}
}
