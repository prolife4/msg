using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Net.Mime;
using Vmx.Common.Extensions;
using Vmx.Common.Logger;
using Vmx.Common.Models;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Common.Services.ObjectConfig;
using Vmx.Common.Settings.Services;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Kafka;

public interface IKafkaConsumerService : IBackgroundWorker
{
    Task RestartConsume();
    Task<List<KafkaReceivedMessageDTO>> GetKafkaMessages(int clientId = -1);
    Task CloseMessage(KafkaReceivedMessageDTO message);
}

public class KafkaConsumerService : VmxBaseService, IKafkaConsumerService
{
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(KafkaConsumerService));
    private static CancellationTokenSource _kafkaCancellationToken = new();

    private readonly IConfigService _configService;
    private readonly IHttpClientService _httpClientService;
    private KafkaDataModel Model => (KafkaDataModel)_model;

    public KafkaConsumerService(MessengerDbContext dbContext,
        IMemoryCache memoryCache,
        IHttpClientService httpClientService,
        IConfigService configService)
    {
        _model = new KafkaDataModel(dbContext, memoryCache);
        _configService = configService;
        _httpClientService = httpClientService;
    }

    public async Task<List<KafkaReceivedMessageDTO>> GetKafkaMessages(int clientId = -1) =>
        await Model.GetKafkaClientReceivedMessages(clientId);

    public async Task CloseMessage(KafkaReceivedMessageDTO message)
    {
        message.Status = Models.MessageStatus.Sent;
        await Model.UpdateReceivedMessage(message);
    }

    public async Task RestartConsume()
    {
        _kafkaCancellationToken.Cancel();
        await StartConsume();
    }

    private async Task StartConsume()
    {
        _kafkaCancellationToken = new CancellationTokenSource();

        List<KafkaClientDTO> kafkaClients = await Model.GetKafkaClients();
        if (kafkaClients.CheckEmptyOrFailedVmxCollection(out KafkaClientDTO failedElement))
        {
            _ = ServiceLogging<KafkaClientDTO>(logger, failedElement);
            _kafkaCancellationToken.Cancel();
            return;
        }

        KafkaClientDTO kafkaClient = kafkaClients.FirstOrDefault();

        var conf = new ConsumerConfig
        {
            GroupId = kafkaClient.GroupId,
            BootstrapServers = kafkaClient.BootstrapServer,
            AutoOffsetReset = kafkaClient.AutoOffsetReset,
        };

        if (kafkaClient.UseSsl)
        {
            conf.SecurityProtocol = SecurityProtocol.Ssl;
            conf.SslCaLocation = kafkaClient.SslCaLocation;
            conf.SslCertificateLocation = kafkaClient.SslCertificateLocation;
            conf.SslKeyLocation = kafkaClient.SslKeyLocation;
        }

        try
        {
            using CachedSchemaRegistryClient schemaRegistry = new(
                new SchemaRegistryConfig
                {
                    Url = kafkaClient.SchemaRegister
                });
            using IConsumer<string, GenericRecord> consumer = new ConsumerBuilder<string, GenericRecord>(conf)
                  .SetErrorHandler((_, e) =>
                  {
                      ServiceLogging<KafkaClientDTO>(logger, 500, $"[StartConsume] {e.Reason}");
                  })
                  .SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync())
                  .Build();

            _ = ServiceLogging<KafkaClientDTO>(logger, 200, $"[StartConsume] Subscribe on topic '{kafkaClient.DatasetTopic}'");

            consumer.Subscribe(kafkaClient.DatasetTopic);

            _ = ServiceLogging<KafkaClientDTO>(logger, 200, $"[StartConsume] Start consume: {JsonConvert.SerializeObject(conf)}");

            try
            {
                while (true)
                {
                    try
                    {
                        ConsumeResult<string, GenericRecord> result = consumer.Consume(_kafkaCancellationToken.Token);

                        KafkaReceivedMessageDTO message = new()
                        {
                            ClientId = kafkaClient.Id,
                            DateCreated = DateTime.Now,
                            DateReceived = Timestamp.UnixTimestampMsToDateTime(result.Message.Timestamp.UnixTimestampMs).ToLocalTime(),
                            Records = result.Message.Value.ToString(),
                            Status = Models.MessageStatus.InQueue
                        };

                        _ = ServiceLogging<KafkaClientDTO>(logger, 0,
                            $"[StartConsume] New message received from topic: '{kafkaClient.DatasetTopic}'. " + result.Offset.ToString());
                        //    $"Message: '{JsonConvert.SerializeObject(message)}'");

                        if (!string.IsNullOrWhiteSpace(kafkaClient.RelayUrl))
                        {
                            SendToRelay(message, kafkaClient.RelayUrl);
                        }
                        else
                        {
                            await Model.AddReceivedMessage(message);
                        }
                      //  consumer.Commit();
                    }
                    catch (ConsumeException ex)
                    {
                        _ = ServiceLogging<KafkaClientDTO>(logger, 500, $"[StartConsume] {ex.Error}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _ = ServiceLogging<KafkaClientDTO>(logger, 300, $"[StartConsume] Cancelled");
                consumer.Close();
            }
        }
        catch (Exception e)
        {
            _ = ServiceLogging<KafkaClientDTO>(logger, 500, $"[StartConsume] {e.Message}");
        }
    }

    private void SendToRelay(KafkaReceivedMessageDTO signal, string url, bool noLog = true)
    {
        try
        {
            var message = new VmxHttpMessage()
            {
                Url = VmxSettings.Instance.AppProjectSettings.WebApi.Url,
                Api = url,
                HttpMethod = HttpMethods.Post,
                Type = MediaTypeNames.Application.Json,
                Data = JsonConvert.SerializeObject(new KafkaReceivedMessageRelay() { DateReceived = signal.DateReceived, Records = signal.Records }),
                Timeout = 10000
            };

            //var client = new HttpClient
            //{
            //    Timeout = TimeSpan.FromSeconds(10)
            //};
            //var res = await client.PostAsync($"{VmxSettings.Instance.AppProjectSettings.WebApi.Url}{url}", message.Content);

            //if (!res.IsSuccessStatusCode)
            //{
            //    VmxLogger.Instance.Error(logger, res.ReasonPhrase);
            //}

            _httpClientService.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            VmxLogger.Instance.Error(logger, e);
        }
    }

    #region Background
    public async Task RunWorker() => await RestartConsume();

    public TimeSpan GetInterval() => TimeSpan.FromMinutes(20);

    public string GetName() => nameof(KafkaConsumerService);
    #endregion
}
