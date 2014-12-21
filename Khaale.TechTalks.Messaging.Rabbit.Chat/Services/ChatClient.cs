using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Services
{
	public class ChatClient
	{
		#region Constants

		private const string EXCHANGE_DIRECT = "direct";
		private const string EXCHANGE_FANOUT_ALERTS = "fanout.alerts";
		private const string EXCHANGE_TOPIC_GROUPS = "topic.groups";
		private const string EXCHANGE_HEADERS_POOL = "headers.pool";

		private const string HEADERS_SENT_FROM = "X-SentFrom";
		private const string HEADERS_SENT_TO = "X-SentTo";
		private const string HEADERS_TITLE = "X-Title"; 
		private const string HEADERS_SKILL = "X-Skill";

		#endregion

		#region Private fields

		private static ChatClient _instance;

		private IConnection _connection;
		private IModel _publisherChannel;
		private IModel _subscriberChannel;
		
		private string _userName;
		private string _consumerTag;
		private EventingBasicConsumer _consumer;
		private Action<MessageReceivedEventArgs> _messageRecievedHandler;

		#endregion

		#region Common methods

		public static ChatClient GetInstance()
		{
			return _instance ?? (_instance = new ChatClient());
		}

		public void Connect()
		{
			var factory = new ConnectionFactory { HostName = "localhost", VirtualHost = "TechTalksChat", UserName = "test", Password = "test" };
			_connection = factory.CreateConnection();
			_publisherChannel = _connection.CreateModel();
			_subscriberChannel = _connection.CreateModel();

			SetupExchanges();
		}


		public void Disconnect()
		{
			_connection.Close();
		}

		private ChatClient()
		{
		}

		private void SetupExchanges()
		{
			_publisherChannel.ExchangeDeclare(EXCHANGE_DIRECT, "direct", true);
			_publisherChannel.ExchangeDeclare(EXCHANGE_FANOUT_ALERTS, "fanout", true);
			_publisherChannel.ExchangeDeclare(EXCHANGE_TOPIC_GROUPS, "topic", true);
			_publisherChannel.ExchangeDeclare(EXCHANGE_HEADERS_POOL, "headers", true);
		}

		private void SetupUserQueue()
		{
			var queueName = GetUserInputQueue();
			_subscriberChannel.QueueDeclare(queueName, true, false, false, null);
			_subscriberChannel.QueueBind(queueName, EXCHANGE_DIRECT, Sanitize(_userName));
			_subscriberChannel.QueueBind(queueName, EXCHANGE_FANOUT_ALERTS, "");
		}

		private static string Sanitize(string userName)
		{
			return userName.ToLowerInvariant().Replace(' ', '-');
		}

		private string GetUserInputQueue()
		{
			return string.Format("users.{0}", Sanitize(_userName));
		}

		#endregion

		#region Consuming

		public void Login(string userName, Action<MessageReceivedEventArgs> messageRecievedHandler)
		{
			if (_userName != null)
				Unsubscribe();

			_userName = userName;
			SetupUserQueue();
			Subscribe(messageRecievedHandler);
		}

		private void Subscribe(Action<MessageReceivedEventArgs> messageRecievedHandler)
		{
			_messageRecievedHandler = messageRecievedHandler;

			_consumer = new EventingBasicConsumer(_subscriberChannel);
			_consumer.Received += ConsumerOnReceived;
			_consumerTag = _subscriberChannel.BasicConsume(GetUserInputQueue(), true, _consumer);
		}

		private void Unsubscribe()
		{
			_consumer.Received -= ConsumerOnReceived;
			_subscriberChannel.BasicCancel(_consumerTag);
		}

		private void ConsumerOnReceived(IBasicConsumer basicConsumer, BasicDeliverEventArgs args)
		{
			var message = Encoding.GetEncoding(args.BasicProperties.ContentEncoding).GetString(args.Body);
			var sentFrom = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers[HEADERS_SENT_FROM]);
			var sentTo = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers[HEADERS_SENT_TO]);

			if (_messageRecievedHandler != null)
			{
				_messageRecievedHandler(new MessageReceivedEventArgs(message, sentFrom, sentTo));
			}
		}

		#endregion

		#region Direct exchange

		public void SendToUser(string sendTo, string message)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			var safeSendTo = Sanitize(sendTo);
			var msg = PrepareMessage(message, "@" + safeSendTo);
			_publisherChannel.BasicPublish(EXCHANGE_DIRECT, safeSendTo, true, msg.Item1, msg.Item2);
		}

		private Tuple<BasicProperties, byte[]> PrepareMessage(string message, string sendTo)
		{
			var safeUserName = Sanitize(_userName);

			var prop = new BasicProperties();
			prop.ContentEncoding = Encoding.UTF8.WebName;
			prop.SetPersistent(true);
			prop.Headers = new Dictionary<string, object>
			{
				{HEADERS_SENT_FROM, "@" + safeUserName},
				{HEADERS_SENT_TO, sendTo}
			};
			var body = Encoding.UTF8.GetBytes(message);
			return Tuple.Create(prop, body);
		}

		#endregion

		#region Fanout exchange

		public void SendBroadcast(string message)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			var msg = PrepareMessage(message, "#all");
			_publisherChannel.BasicPublish(EXCHANGE_FANOUT_ALERTS, "", true, msg.Item1, msg.Item2);
		}

		#endregion

		#region Topic exchange

		public void JoinTeam(string groupName)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			_subscriberChannel.QueueBind(GetUserInputQueue(), EXCHANGE_TOPIC_GROUPS, Sanitize(groupName));
		}

		public void SendToTeam(string sendTo, string message)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			var safeSendTo = Sanitize(sendTo);
			var msg = PrepareMessage(message, "#" + safeSendTo);
			_publisherChannel.BasicPublish(EXCHANGE_TOPIC_GROUPS, safeSendTo, true, msg.Item1, msg.Item2);
		}

		#endregion

		#region Headers exchange

		public void JoinPool(string title, string skill)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			_subscriberChannel.QueueBind(
				GetUserInputQueue(), 
				EXCHANGE_HEADERS_POOL, 
				"",
				new Dictionary<string, object>
				{
					{"x-match", "all"},
					{ HEADERS_TITLE, title},
					{ HEADERS_SKILL, skill},
				} );
		}
		
		public void SendToPool(string title, string skill, string message)
		{
			if (_userName == null)
				throw new ApplicationException("You are not logged in.");

			var msg = PrepareMessage(message, string.Format("#{0}/{1}", title, skill));
			msg.Item1.Headers[HEADERS_TITLE] = title;
			msg.Item1.Headers[HEADERS_SKILL] = skill;
			_publisherChannel.BasicPublish(EXCHANGE_HEADERS_POOL, "", true, msg.Item1, msg.Item2);
		}

		#endregion
	}

	public class MessageReceivedEventArgs
	{
		public string Message { get; set; }
		public string SentFrom { get; set; }
		public string SentTo { get; set; }

		public MessageReceivedEventArgs(string message, string sentFrom, string sentTo)
		{
			Message = message;
			SentFrom = sentFrom;
			SentTo = sentTo;
		}
	}
}
