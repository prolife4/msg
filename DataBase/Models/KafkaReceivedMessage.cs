namespace Vmx.Messenger.DataBase.Models
{
    public partial class KafkaReceivedMessage
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateReceived { get; set; }
        public string Records { get; set; }
        public int? Status { get; set; }
        public int? AttemptCount { get; set; }
    }
}
