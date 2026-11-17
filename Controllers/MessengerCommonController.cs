using Microsoft.AspNetCore.Mvc;
using Vmx.Messenger.Models.DTO;
using Vmx.Messenger.Services.Telegram;

namespace Vmx.Messenger.Controllers;


public class MessengerCommonController(ITelegramService telegramService) : Controller
{
    private readonly ITelegramService _telegramService = telegramService;

    [HttpPost("~/api/messenger/telegram/sendmessage")]
    public async Task<IActionResult> SendTelegramMessage([FromBody] TelegramMessageDTO message)
    {
        if (message == null)
        {
            return BadRequest("Message is not valid");
        }

        await _telegramService.AddTelegramMessageToSend(message);

        return Ok();
    }
}
