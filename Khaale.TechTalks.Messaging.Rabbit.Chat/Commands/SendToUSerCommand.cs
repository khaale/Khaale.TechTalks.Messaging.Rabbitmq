using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class SendToUserCommand : ConsoleCommand
	{
		public SendToUserCommand()
		{
			IsCommand("send-to-user");
			HasAdditionalArguments(2, " <username> <message>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var sendTo = remainingArguments[0];
			var message = remainingArguments[1];

			ChatClient.GetInstance().SendToUser(sendTo, message);

			return 0;
		}
	}
}
