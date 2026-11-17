using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO
{
    public class KafkaReceivedMessageDTO : KafkaMessageDTO
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(KafkaSentMessageDTO));

        public DateTime? DateReceived { get; set; }

        public KafkaReceivedMessageDTO() { }

        public KafkaReceivedMessageDTO(KafkaReceivedMessage receviedMessage)
        {
            Id = receviedMessage.Id;
            ClientId = receviedMessage.ClientId;
            DateCreated = receviedMessage.DateCreated;
            DateReceived = receviedMessage.DateReceived;
            Status = (MessageStatus)receviedMessage.Status.GetValueOrDefault(0);
            AttemptCount = receviedMessage.AttemptCount;
            Records = receviedMessage.Records;
        }

        public KafkaReceivedMessage ToDbEntry() =>
            new()
            {
                Id = Id,
                ClientId = ClientId,
                DateCreated = DateCreated,
                DateReceived = DateReceived,
                Status = (int)Status,
                AttemptCount = AttemptCount,
                Records = Records
            };
    }
}
