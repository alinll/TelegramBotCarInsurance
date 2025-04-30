using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBotCarInsurance;
using Telegram.Bot.Exceptions;

ITelegramBotClient _botClient; // adding client for work with Telegram Bot API
ReceiverOptions _receiverOptions;
var token = Constants.token;
_botClient = new TelegramBotClient(token);
_receiverOptions = new ReceiverOptions
{
    AllowedUpdates = new[]
    {
        UpdateType.Message,
        UpdateType.CallbackQuery
    },
    DropPendingUpdates = true, // when bot is offline, it don't process messages
};
var userStates = new Dictionary<long, UserDocuments>();

using var cts = new CancellationTokenSource();

_botClient.StartReceiving(
    async (bot, update, token) => await TelegramBotService.UpdateHandler(bot, update, userStates, token),
    ErrorHandler,
    _receiverOptions,
    cts.Token
);

await Task.Delay(-1); // setting an infinite delay so that bot works constantly

static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
{
    var ErrorMessage = error switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => error.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}