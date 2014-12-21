using System;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class LoginCommand : ConsoleCommand
	{
		public LoginCommand()
		{
			IsCommand("login");
			HasAdditionalArguments(1, " <username>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var userName = remainingArguments[0];

			var client = ChatClient.GetInstance();
			client.Login(userName, args =>
			{
				Console.WriteLine("[{0} -> {1}]: {2}", args.SentFrom, args.SentTo, args.Message);
			});

			return 0;
		}
	}
}
