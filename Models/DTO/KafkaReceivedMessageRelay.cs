namespace Vmx.Messenger.Models.DTO
{
    public class KafkaReceivedMessageRelay
    {
        public DateTime? DateReceived { get; set; }
        public string Records { get; set; }

    }
}
