using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO
{
    public class KafkaSentMessageDTO : KafkaMessageDTO
    {
        public DateTime? DateSent { get; set; }

        public KafkaSentMessageDTO() { }

        public KafkaSentMessageDTO(KafkaSentMessage sentMessage)
        { 
            Id = sentMessage.Id;
            ClientId = sentMessage.ClientId;
            DateCreated = sentMessage.DateCreated;
            DateSent = sentMessage.DateSent;
            Status = (MessageStatus)sentMessage.Status.GetValueOrDefault(0);
            AttemptCount = sentMessage.AttemptCount;
            Records = sentMessage.Records;
        }

        public KafkaSentMessage ToDbEntry() =>
            new()
            {
                Id = Id,
                ClientId = ClientId,
                DateCreated = DateCreated,
                DateSent = DateSent,
                Status = (int)Status,
                AttemptCount = AttemptCount,
                Records = Records
            };
    }
}
