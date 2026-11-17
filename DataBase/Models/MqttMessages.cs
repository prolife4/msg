namespace Vmx.Messenger.DataBase.Models
{
    public partial class MqttMessages
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string Message { get; set; }
        public int? AttemptCount { get; set; }
        public DateTime? DateSent { get; set; }
    }
}
