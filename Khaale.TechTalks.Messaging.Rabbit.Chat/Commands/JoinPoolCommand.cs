using System;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class JoinPoolCommand : ConsoleCommand
	{
		public JoinPoolCommand() : base()
		{
			IsCommand("join-pool");
			HasAdditionalArguments(2," <title> <skill>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var title = remainingArguments[0];
			var skill = remainingArguments[1];

			ChatClient.GetInstance().JoinPool(title, skill);

			return 0;
		}
	}
}
