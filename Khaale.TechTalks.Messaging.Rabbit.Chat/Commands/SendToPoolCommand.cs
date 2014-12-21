using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class SendToPoolCommand : ConsoleCommand
	{
		public SendToPoolCommand()
		{
			IsCommand("send-to-pool");
			HasAdditionalArguments(3, " <title> <skill> <message>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var title = remainingArguments[0];
			var skill = remainingArguments[1];
			var message = remainingArguments[2];

			ChatClient.GetInstance().SendToPool(title, skill, message);

			return 0;
		}
	}
}
