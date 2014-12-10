using System.Data.Entity;
using Khaale.TechTalks.Messaging.Rabbit.Samples.Demo.Entities;

namespace Khaale.TechTalks.Messaging.Rabbit.Samples.Demo.DAL
{
	public class DemoDataContext : DbContext
	{
		public DbSet<ProducedItem> ProducedItems { get; set; }
		public DbSet<ConsumedItem> ConsumedItems { get; set; }
 
	}
}
