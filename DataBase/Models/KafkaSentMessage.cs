namespace Vmx.Messenger.DataBase.Models
{
    public partial class KafkaSentMessage
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateSent { get; set; }
        public string Records { get; set; }
        public int? Status { get; set; }
        public int? AttemptCount { get; set; }
    }
}
