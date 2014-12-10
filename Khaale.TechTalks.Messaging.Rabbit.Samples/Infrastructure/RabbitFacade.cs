using System;
using System.Text;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples.Infrastructure
{
	public class RabbitFacade
	{
		public const string VirtualHost = "TechTalks";

		private IConnection _connection;
		private IModel _internalChannel;

		public IModel CreateChannel()
		{
			return _connection.CreateModel();
		}

		public IConnection CreateConnection()
		{
			var factory = new ConnectionFactory { HostName = "localhost", VirtualHost = VirtualHost };
			return factory.CreateConnection();
		}

		public void Initialize()
		{
			var factory = new ConnectionFactory { HostName = "localhost", VirtualHost = VirtualHost };
			_connection = factory.CreateConnection();
			_internalChannel = _connection.CreateModel();
		}

		public void Deinitialize()
		{
			_internalChannel.Dispose();
			_connection.Dispose();
		}

		public void CreateQueue(string queueName, bool durable = true)
		{
			DeclareQueue(queueName, durable);
			_internalChannel.QueuePurge(queueName);			
		}


		public void DeleteQueue(string queueName)
		{
			_internalChannel.QueueDelete(queueName);
		}

		public uint GetMessageCount(string queueName)
		{
			var queue = _internalChannel.QueueDeclarePassive(queueName);
			return queue.MessageCount;
		}

		public void PublishMessage(string queueName, string message)
		{
			var body = Encoding.UTF8.GetBytes(message);
			var prop = new BasicProperties();
			prop.SetPersistent(true);

			_internalChannel.TxSelect();
			_internalChannel.BasicPublish("", queueName, prop, body);
			_internalChannel.TxCommit();
		}

		public Tuple<byte[], BasicProperties> PrepareMessage(string messageText, bool persistant = true)
		{
			var body = Encoding.UTF8.GetBytes(messageText);
			var prop = new BasicProperties();
			prop.SetPersistent(persistant);

			return Tuple.Create(body, prop);
		}

		public string ConsumeMessage(string queueName)
		{
			var result = _internalChannel.BasicGet(queueName, true);

			return result != null
				? Encoding.UTF8.GetString(result.Body)
				: null;
		}

		private QueueDeclareOk DeclareQueue(string queueName, bool durable)
		{
			return _internalChannel.QueueDeclare(queueName, durable, false, false, null);
		}

		public static string QueueName
		{
			get { return TestContext.CurrentContext.Test.FullName; }
		}
	}
}