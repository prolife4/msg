using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Vmx.Messenger.DataBase.Models;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Confluent.Kafka;
using Newtonsoft.Json.Linq;

namespace Vmx.Messenger.Models.DTO
{
    public class KafkaClientDTO : VmxBaseDTO
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(KafkaClientDTO));

        public int Id { get; set; }
        public string ServerUrl { get; set; }
        public string RequestParams { get; set; }
        public string Cluster { get; set; }
        public string DatasetTopic { get; set; }
        public string Version { get; set; }
        public int? SchemaId { get; set; }
        public string SchemaValue { get; set; }
        public string BootstrapServer { get; set; }
        public string SchemaRegister { get; set; }
        public string GroupId { get; set; }
        public bool? IsActive { get; set; }
        public bool? SchemaWasSent { get; set; }
        public string FullServerUrl => GetServerUrl();
        public string Description { get; set; }
        public AutoOffsetReset AutoOffsetReset { get; set; }
        public bool UseSsl { get; set; }
        public string SslCaLocation { get; set; }
        public string SslCertificateLocation { get; set; }
        public string SslKeyLocation { get; set; }
        public string RelayUrl { get; set; }

        public ConcurrentQueue<KafkaSentMessageDTO> SentMessages { get; set; } = [];
        public List<KafkaReceivedMessageDTO> ReceivedMessages { get; set; }

        public KafkaClientDTO() { }

        public KafkaClientDTO(KafkaClients kafkaClient)
        { 
            Id = kafkaClient.Id;
            ServerUrl = kafkaClient.ServerUrl;
            RequestParams = kafkaClient.RequestParams;
            Cluster = kafkaClient.Cluster;
            DatasetTopic = kafkaClient.DatasetTopic;
            Version = kafkaClient.Version;
            BootstrapServer = kafkaClient.BootstrapServer;
            SchemaRegister = kafkaClient.SchemaRegister;
            GroupId = kafkaClient.GroupId;
            IsActive = kafkaClient.IsActive.GetValueOrDefault();
            SchemaWasSent = kafkaClient.SchemaWasSent.GetValueOrDefault();
            Description = $"{kafkaClient.DatasetTopic}";
            AutoOffsetReset = (AutoOffsetReset)kafkaClient.AutoOffsetReset.GetValueOrDefault();
            UseSsl = kafkaClient.UseSsl.GetValueOrDefault();
            SslCaLocation = kafkaClient.SslCaLocation;
            SslCertificateLocation = kafkaClient.SslCertificateLocation;
            SslKeyLocation = kafkaClient.SslKeyLocation;
            RelayUrl = kafkaClient.RelayUrl;
            SchemaId = kafkaClient.SchemaId;
            SchemaValue = kafkaClient.SchemaValue;
        }

        public KafkaClients ToDbEntry() =>
            new()
            {
                Id = Id,
                ServerUrl = ServerUrl,
                RequestParams = RequestParams,
                Cluster = Cluster,
                DatasetTopic = DatasetTopic,
                Version = Version,
                BootstrapServer = BootstrapServer,
                SchemaRegister = SchemaRegister,
                GroupId = GroupId,
                IsActive = IsActive,
                SchemaWasSent = SchemaWasSent,
                RelayUrl = RelayUrl,
                SchemaId = SchemaId,
                SchemaValue = SchemaValue
            };

        private string GetServerUrl()
        {
            if (string.IsNullOrEmpty(ServerUrl) || string.IsNullOrEmpty(DatasetTopic) ||
                string.IsNullOrEmpty(Cluster) || string.IsNullOrEmpty(Version))
            {
                return string.Empty;
            }

            return $"{ServerUrl}/topics/{Cluster}.{DatasetTopic}.{Version}";
        }

        private string GetSentMessageContent()
        {
            if (!SentMessages.TryDequeue(out KafkaSentMessageDTO message))
            {
                return string.Empty;
            }            

            try
            {
                var kafkaMessage = new
                {
                    value_schema = SchemaWasSent.GetValueOrDefault(false) ? null : SchemaValue,
                    value_schema_id = SchemaWasSent.GetValueOrDefault(false) ? SchemaId : null,
                    records = JsonConvert.DeserializeObject<JArray>(message.Records)
                };

                string content = JsonConvert.SerializeObject(kafkaMessage, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Include });
                content = content.Replace("_AVRO_DATASET_NAMESPACE_", $"nlmk.{DatasetTopic.Replace('-', '.')}.ver{Version}.RecordData");
                return content;
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
                return string.Empty;
            }
        }

        public async Task SendData()
        {
            try
            {
                string url = FullServerUrl;
                if (string.IsNullOrEmpty(url))
                {
                    VmxLogger.Instance.Error(logger, "Kafka server url is empty");
                    return;
                }

                string data = GetSentMessageContent();
                if (string.IsNullOrEmpty(data))
                {
                    VmxLogger.Instance.Warn(logger, "No one message to send");
                    return;
                }

                using HttpClient client = new();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.kafka.v2+json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", RequestParams);
                StringContent stringContent = new(data, Encoding.UTF8, "application/vnd.kafka.avro.v2+json");

                VmxLogger.Instance.Info(logger, $"Send data to: {url} \n {data}");

                HttpResponseMessage result = await client.PostAsync(url, stringContent);
                string response = await result.Content.ReadAsStringAsync();
                if (!result.IsSuccessStatusCode)
                {
                    VmxLogger.Instance.Debug(logger, $"Error response: {response}");
                }
                else
                {
                    VmxLogger.Instance.Debug(logger, $"Kafka message was sent: {response}");
                }
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
            }
        }
    }
}
