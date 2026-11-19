using Vmx.Messenger.Models;

namespace Vmx.Messenger.Models.DTO;

/// <summary>
/// Единый контракт клиента способа доставки уведомлений (Email, Telegram, MAX...).
/// Позволяет обобщить CRUD-операции и фоновую отправку.
/// </summary>
public interface IMessengerClientDto
{
    int Id { get; }
    bool IsActive { get; }
    /// <summary>Человекочитаемое имя клиента для логов (адрес, имя чата и т.п.).</summary>
    string DisplayName { get; }
}

/// <summary>
/// Единый контракт сообщения способа доставки уведомлений.
/// </summary>
public interface IMessengerMessageDto
{
    int Id { get; set; }
    int ClientId { get; set; }
    MessageStatus Status { get; set; }
    DateTime? DateCreated { get; set; }
    DateTime? DateSent { get; set; }
    string Comment { get; set; }
    /// <summary>Краткий текст сообщения для логов (Subject у Email, Caption у мессенджеров).</summary>
    string DisplayText { get; }
}

/// <summary>Форматирование общих полей сообщений (статус, даты) — использовалось дублями в каждом DTO.</summary>
public static class MessengerMessageFormatting
{
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public static string FormatDate(DateTime? date) =>
        date.HasValue ? date.Value.ToString(DateFormat) : "--";

    public static string FormatStatus(MessageStatus status, string comment)
    {
        (string text, string color) = status switch
        {
            MessageStatus.Sent => ("Отправлен", "success"),
            MessageStatus.Failed => ($"Ошибка: {comment}", "danger"),
            MessageStatus.InQueue => ("В очереди", "info"),
            MessageStatus.Canceled => ("Отменен", "muted"),
            _ => ("Неизвестно", "muted"),
        };

        return $"<span class='text-{color}'>{text}</span>";
    }
}
