using Microsoft.EntityFrameworkCore;
using Vmx.Messenger.DataBase.Models;
using Vmx.Common.Builders.WebAppBuilder;

namespace Vmx.Messenger.DataBase
{
    public partial class MessengerDbContext : DbContext
    {
        public MessengerDbContext()
        {
        }

        public MessengerDbContext(DbContextOptions<MessengerDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<EmailClients> EmailClients { get; set; }
        public virtual DbSet<EmailMessages> EmailMessages { get; set; }
        public virtual DbSet<KafkaClients> KafkaClients { get; set; }
        public virtual DbSet<KafkaReceivedMessage> KafkaReceivedMessage { get; set; }
        public virtual DbSet<KafkaSentMessage> KafkaSentMessage { get; set; }
        public virtual DbSet<MqttClients> MqttClients { get; set; }
        public virtual DbSet<MqttMessages> MqttMessages { get; set; }
        public virtual DbSet<TelegramClients> TelegramClients { get; set; }
        public virtual DbSet<TelegramMessages> TelegramMessages { get; set; }
        public virtual DbSet<TaskClients> TaskClients { get; set; }
        public virtual DbSet<TaskMessages> TaskMessages { get; set; }
        public virtual DbSet<MAXClients> MAXClients { get; set; }
        public virtual DbSet<MAXMessages> MAXMessages { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            SQLServiceBuilder.Instance.OnVmxConfigure(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("messenger");
            SQLServiceBuilder.Instance.CheckNameConvention(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
