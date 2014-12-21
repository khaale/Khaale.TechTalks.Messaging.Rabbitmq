using System;
using System.Collections.Generic;
using System.Linq;
using ManyConsole;

namespace Khaale.TechTalks.Messaging.Rabbit.Chat
{
	class Program
	{
		static int Main(string[] args)
		{
			// locate any commands in the assembly (or use an IoC container, or whatever source)
			var commands = GetCommands();

			// then run them.
			return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
		}

		public static ICollection<ConsoleCommand> GetCommands()
		{
			return ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(Program)).ToArray();
		}
	}
}
