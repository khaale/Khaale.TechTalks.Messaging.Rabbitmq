using System;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples.Demo.Entities
{
	public class ConsumedItem
	{
		public ConsumedItem()
		{
			CreatedOn = DateTime.UtcNow;
		}

		public int Id { get; set; }
		public DateTime CreatedOn { get; set; }
	}
}
