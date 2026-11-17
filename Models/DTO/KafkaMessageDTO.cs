using Vmx.Common.Models.DTO;

namespace Vmx.Messenger.Models.DTO
{

    public class KafkaMessageDTO : VmxBaseDTO
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public DateTime? DateCreated { get; set; }
        public string Records { get; set; }
        public MessageStatus Status { get; set; }
        public int? AttemptCount { get; set; }
    }
}
