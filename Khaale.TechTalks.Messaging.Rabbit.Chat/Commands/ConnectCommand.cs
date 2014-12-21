using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Khaale.TechTalks.Messaging.Rabbit.Chat.Services;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat.Commands
{
	public class ConnectCommand : ConsoleModeCommand
	{
		public ConnectCommand()
		{
			IsCommand("connect");
		}

		public override int Run(string[] remainingArguments)
		{
			ChatClient.GetInstance().Connect();
			var result = base.Run(remainingArguments);
			ChatClient.GetInstance().Disconnect();

			return result;
		}

		public override IEnumerable<ConsoleCommand> GetNextCommands()
		{
			return Program.GetCommands().Where(c => !(c is ConsoleModeCommand));
		}

		public override void WritePromptForCommands()
		{
		}
	}
}
