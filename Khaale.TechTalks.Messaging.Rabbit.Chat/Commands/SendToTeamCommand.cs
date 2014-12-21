using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class SendToTeamCommand : ConsoleCommand
	{
		public SendToTeamCommand()
		{
			IsCommand("send-to-team");
			HasAdditionalArguments(2, " <team> <message>");
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var sendTo = remainingArguments[0];
			var message = remainingArguments[1];

			ChatClient.GetInstance().SendToTeam(sendTo, message);

			return 0;
		}
	}
}
