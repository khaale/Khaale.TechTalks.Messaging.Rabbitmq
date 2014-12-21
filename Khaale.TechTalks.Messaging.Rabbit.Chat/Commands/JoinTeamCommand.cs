using System;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class JoinTeamCommand : ConsoleCommand
	{
		public JoinTeamCommand()
		{
			IsCommand("join-team");
			HasAdditionalArguments(1, " <team>"); 
			SkipsCommandSummaryBeforeRunning();
		}

		public override int Run(string[] remainingArguments)
		{
			var groupName = remainingArguments[0];

			ChatClient.GetInstance().JoinTeam(groupName);

			return 0;
		}
	}
}
