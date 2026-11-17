using MQTTnet;
using MQTTnet.Client;
using Vmx.Messenger.DataBase.Models;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;

namespace Vmx.Messenger.Models.DTO
{
    public class MQTTClientDTO : VmxBaseDTO
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTClientDTO));

        public int Id { get; set; }
        public string ServerUrl { get; set; }
        public string Topic { get; set; }
        public int Port { get; set; }
        public string RequestParams { get; set; }
        public bool IsActive { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; } = "Videomatrix";

        public MQTTClientDTO() { }

        public MQTTClientDTO(MqttClients client)
        { 
            Id = client.Id;
            ServerUrl = client.ServerUrl;
            Topic = client.Topic;
            Port = client.Port.GetValueOrDefault();
            RequestParams = client.RequestParams;
            IsActive = client.IsActive.GetValueOrDefault();
            Login = client.Login;
            Password = client.Password;
        }

        public MqttClients ToDbEntry() =>
            new()
            {
                Id = Id,
                Topic = Topic,
                Port = Port,
                RequestParams = RequestParams,
                IsActive = IsActive,
                ServerUrl = ServerUrl,
                Login = Login,
                Password = Password
            };

        private async Task<IMqttClient> GetClient()
        {
            try
            {
                IMqttClient client = new MqttFactory().CreateMqttClient();

                MqttClientOptionsBuilder optionsBulder = new MqttClientOptionsBuilder()
                    .WithClientId(ClientId)
                    .WithTcpServer(ServerUrl, Port);

                if (!string.IsNullOrEmpty(Login))
                {
                    optionsBulder.WithCredentials(Login, Password);
                }

                optionsBulder.WithCleanSession();

                await client.ConnectAsync(optionsBulder.Build());

                return client;
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
                return null;
            }
        }

        public async Task<bool> SendMessage(MqttApplicationMessageBuilder messageBuilder)
        {
            IMqttClient client = await GetClient();
            if (client == null)
            {
                return false;
            }

            try
            {
                MqttApplicationMessage message = messageBuilder.WithTopic(Topic).Build();

                MqttClientPublishResult result = await client.PublishAsync(message);
                if (result.IsSuccess)
                {
                    VmxLogger.Instance.Info(logger, $"Message '{message}' has been sent to Mqtt broker");
                    return true;
                }
                else
                {
                    VmxLogger.Instance.Error(logger, result.ReasonString);
                    return false;
                }                
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return false;
            }
        }

    }
}
