using MQTTnet;
using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO
{
    public class MQTTMessageDTO
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Message { get; set; }
        public int AttemptCount { get; set; }
        public DateTime DateSent { get; set; }

        public MQTTMessageDTO() { }

        public MQTTMessageDTO(MqttMessages message)
        { 
            Id = message.Id;
            ClientId = message.ClientId.GetValueOrDefault(-1);
            Message = message.Message;
            AttemptCount = message.AttemptCount.GetValueOrDefault();
            DateSent = message.DateSent.GetValueOrDefault();
        }

        public MqttMessages ToDbEntry() =>
            new()
            {
                Id = Id,
                ClientId = ClientId,
                Message = Message,
                AttemptCount = AttemptCount,
                DateSent = DateSent
            };

        public MqttApplicationMessageBuilder GetMessageBuilder() =>
            new MqttApplicationMessageBuilder().WithPayload(Message);

        public override string ToString()
        {
            return Message;
        }
    }
}
