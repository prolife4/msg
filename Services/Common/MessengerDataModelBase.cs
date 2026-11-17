using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Common;

/// <summary>
/// Базовая реализация DataModel для способов доставки уведомлений с таблицами Clients/Messages.
/// Убирает дублирование CRUD-кода между Email/Telegram/MAX (и будущими способами).
/// Потомок описывает только маппинг сущностей и доступ к полям сообщений.
/// </summary>
internal abstract class MessengerDataModelBase<TClientEntity, TMessageEntity, TClientDto, TMessageDto>
    where TClientEntity : class
    where TMessageEntity : class
    where TClientDto : VmxBaseDTO, IMessengerClientDto
    where TMessageDto : VmxBaseDTO, IMessengerMessageDto
{
    private static readonly log4net.ILog logger =
        log4net.LogManager.GetLogger(typeof(MessengerDataModelBase<TClientEntity, TMessageEntity, TClientDto, TMessageDto>));

    protected const int FailedCode = 500;
    /// <summary>Число попыток отправки для статусов с ретраем (Email).</summary>
    protected const int MaxSendAttempts = 5;

    protected MessengerDbContext DbContext { get; }
    protected IMemoryCache MemoryCache { get; }

    /// <summary>Ключ кэша списка клиентов способа доставки.</summary>
    protected abstract string ClientCacheKey { get; }

    /// <summary>Описание полей ORM-сущности сообщения (ClientId/Status/DateCreated/AttemptCount).</summary>
    protected abstract MessageEntityAccessors<TMessageEntity> Accessors { get; }

    /// <summary>Максимум попыток отправки; 0 — повторять не нужно (в очередь берется только InQueue).</summary>
    protected virtual int PendingMaxAttempts => 0;

    protected DbSet<TClientEntity> Clients => DbContext.Set<TClientEntity>();
    protected DbSet<TMessageEntity> Messages => DbContext.Set<TMessageEntity>();

    // ---- маппинг сущностей в DTO и обратно (реализуется в потомках) ----
    protected abstract TClientDto ToClientDto(TClientEntity entity);
    protected abstract TMessageDto ToMessageDto(TMessageEntity entity);
    protected abstract TMessageEntity ToMessageEntity(TMessageDto dto);
    protected abstract TClientEntity ToClientEntity(TClientDto dto);

    /// <summary>Дополнительная загрузка данных сообщения после маппинга (например, вложения из кэша у Email).</summary>
    protected virtual Task<TMessageDto> EnrichMessageAsync(TMessageDto dto) => Task.FromResult(dto);

    protected MessengerDataModelBase(MessengerDbContext dbContext, IMemoryCache memoryCache)
    {
        DbContext = dbContext;
        MemoryCache = memoryCache;
    }

    protected static TFailed ToFailed<TFailed>(Exception e)
    {
        VmxLogger.Instance.Error(logger, e);
        return VmxBaseDTO.ToFailedDTO<TFailed>(FailedCode, e.Message);
    }

    #region Clients CRUD

    public async Task<TClientDto> AddClientAsync(TClientDto client)
    {
        try
        {
            var entry = await Clients.AddAsync(ToClientEntity(client)).ConfigureAwait(false);
            await SaveAndClearAsync().ConfigureAwait(false);
            return ToClientDto(entry.Entity);
        }
        catch (Exception e)
        {
            return ToFailed<TClientDto>(e);
        }
    }

    public async Task<TClientDto> UpdateClientAsync(TClientDto client)
    {
        try
        {
            var entry = Clients.Update(ToClientEntity(client));
            await SaveAndClearAsync().ConfigureAwait(false);
            return ToClientDto(entry.Entity);
        }
        catch (Exception e)
        {
            return ToFailed<TClientDto>(e);
        }
    }

    public async Task<TClientDto> DeleteClientAsync(TClientDto client)
    {
        try
        {
            var entity = await Clients.FindAsync(client.Id).ConfigureAwait(false);
            if (entity is not null)
                Clients.Remove(entity);

            await SaveAndClearAsync().ConfigureAwait(false);
            return client;
        }
        catch (Exception e)
        {
            return ToFailed<TClientDto>(e);
        }
    }

    /// <summary>Список клиентов с кэшированием; при clientId &gt; 0 возвращает только этот клиент.</summary>
    public async Task<List<TClientDto>> GetClientsAsync(int clientId = 0, bool noCache = false)
    {
        try
        {
            List<TClientDto>? clients = MemoryCache.Get<List<TClientDto>>(ClientCacheKey);
            if (clients == null || clients.Count == 0 || noCache)
            {
                var fromDb = await Clients.AsNoTracking().ToListAsync().ConfigureAwait(false);
                clients = [.. fromDb.Select(ToClientDto)];
                MemoryCache.Set(ClientCacheKey, clients);
            }

            if (clientId > 0)
                clients = [.. clients.Where(x => x.Id == clientId)];

            return clients;
        }
        catch (Exception e)
        {
            return [ToFailed<TClientDto>(e)];
        }
    }

    #endregion

    #region Messages CRUD

    public async Task<TMessageDto> AddMessageAsync(TMessageDto message)
    {
        try
        {
            message.Id = default;
            var entity = ToMessageEntity(message);
            SetMessageDateCreated(entity);
            await Messages.AddAsync(entity).ConfigureAwait(false);
            await DbContext.SaveChangesAsync().ConfigureAwait(false);

            OnMessageAdded(message, entity);

            DbContext.ChangeTracker.Clear();
            return ToMessageDto(entity);
        }
        catch (Exception e)
        {
            return ToFailed<TMessageDto>(e);
        }
    }

    public async Task<TMessageDto> UpdateMessageAsync(TMessageDto message)
    {
        try
        {
            Messages.Update(ToMessageEntity(message));
            await DbContext.SaveChangesAsync().ConfigureAwait(false);
            DbContext.ChangeTracker.Clear();

            OnMessageUpdated(message);
            return message;
        }
        catch (Exception e)
        {
            return ToFailed<TMessageDto>(e);
        }
    }

    public async Task<TMessageDto> DeleteMessageAsync(TMessageDto message)
    {
        try
        {
            var entity = await Messages.FindAsync(message.Id).ConfigureAwait(false);
            if (entity is not null)
                Messages.Remove(entity);

            await DbContext.SaveChangesAsync().ConfigureAwait(false);
            DbContext.ChangeTracker.Clear();
            return message;
        }
        catch (Exception e)
        {
            return ToFailed<TMessageDto>(e);
        }
    }

    /// <summary>Запрос истории сообщений клиента (для UI), по дате создания — убыванию.</summary>
    public IQueryable<TMessageDto> GetMessagesQuery(int clientId)
    {
        try
        {
            var accessors = Accessors;
            return Messages.AsNoTracking()
                .Where(accessors.BelongsToClient(clientId))
                .OrderByDescending(accessors.DateCreated)
                .Select(ToMessageDto);
        }
        catch (Exception e)
        {
            VmxLogger.Instance.Error(logger, e);
            return new List<TMessageDto>().AsQueryable();
        }
    }

    /// <summary>Сообщения, ожидающие отправки: InQueue (+ Failed с остатком попыток, если PendingMaxAttempts &gt; 0).</summary>
    public async Task<List<TMessageDto>> GetPendingMessagesAsync(int clientId)
    {
        try
        {
            var accessors = Accessors;
            // Простые equality-фильтры транслируются в SQL, составной pending-фильтр — на клиенте.
            var entities = await Messages.AsNoTracking()
                .Where(accessors.BelongsToClient(clientId))
                .ToListAsync()
                .ConfigureAwait(false);

            var pending = accessors.PendingFilterCompiled(PendingMaxAttempts);
            var result = new List<TMessageDto>();
            foreach (var entity in entities.Where(pending).OrderBy(m => accessors.DateCreatedValue(m)))
                result.Add(await EnrichMessageAsync(ToMessageDto(entity)).ConfigureAwait(false));

            return result;
        }
        catch (Exception e)
        {
            return [ToFailed<TMessageDto>(e)];
        }
    }

    /// <summary>Удаление старых сообщений (созданные раньше olderThan и уже не состоящие в очереди).</summary>
    public async Task<VmxBaseDTO> DeleteOldMessagesAsync(DateTime olderThan)
    {
        try
        {
            var accessors = Accessors;
            // SQL-фильтр по дате, уточнение статуса — на клиенте.
            var dateCreated = accessors.DateCreated.Compile();
            var candidates = await Messages.Where(m => dateCreated(m) <= olderThan)
                                           .ToListAsync()
                                           .ConfigureAwait(false);

            var stale = candidates.Where(accessors.OlderThanNotInQueueCompiled(olderThan));
            Messages.RemoveRange(stale);
            await DbContext.SaveChangesAsync().ConfigureAwait(false);
            return new();
        }
        catch (Exception e)
        {
            return ToFailed<VmxBaseDTO>(e);
        }
    }

    #endregion

    #region Hooks

    /// <summary>Проставить дату создания при добавлении (нужно не всем способам).</summary>
    protected virtual void SetMessageDateCreated(TMessageEntity entity) { }

    /// <summary>Хук после добавления сообщения (например, кэш вложений у Email).</summary>
    protected virtual void OnMessageAdded(TMessageDto dto, TMessageEntity entity) { }

    /// <summary>Хук после обновления сообщения (например, очистка кэша вложений у Email).</summary>
    protected virtual void OnMessageUpdated(TMessageDto dto) { }

    private async Task SaveAndClearAsync()
    {
        await DbContext.SaveChangesAsync().ConfigureAwait(false);
        DbContext.ChangeTracker.Clear();
        MemoryCache.Remove(ClientCacheKey);
    }

    #endregion
}
