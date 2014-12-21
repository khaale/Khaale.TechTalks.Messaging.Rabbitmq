using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class SendBroadcastCommand : ConsoleCommand
	{
		public SendBroadcastCommand()
		{
			IsCommand("send-broadcast");
			HasAdditionalArguments(1, " <message>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var message = remainingArguments[0];

			ChatClient.GetInstance().SendBroadcast(message);

			return 0;
		}
	}
}
