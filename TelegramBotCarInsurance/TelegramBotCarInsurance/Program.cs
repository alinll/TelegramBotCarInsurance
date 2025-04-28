using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotCarInsurance;

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
    async (bot, update, token) => await UpdateHandler(bot, update, token, userStates),
    ErrorHandler,
    _receiverOptions,
    cts.Token
);

await Task.Delay(-1); // setting an infinite delay so that bot works constantly

static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken,
    Dictionary<long, UserDocuments> userStates)
{
    try
    {
        switch (update.Type)
        {
            // type message
            case UpdateType.Message:
                {
                    Message? message = update.Message;

                    // if message is null we return from this method
                    if (message is null)
                    {
                        return;
                    }

                    switch (message.Type)
                    {
                        // message type text
                        case MessageType.Text:
                            {
                                var groqService = new GroqService(Constants.apiKeyGroq);

                                // start message
                                if (message.Text == "/start")
                                {
                                    await StartMessage(botClient, update, cancellationToken);
                                    return;
                                }
                                else
                                {
                                    if (update.Message is null || message.Text is null)
                                    {
                                        return;
                                    }

                                    var chatId = update.Message.Chat.Id;
                                    var groqAnswer = await groqService.GetGroqResponseAsync(message.Text);

                                    await botClient.SendMessage(
                                        chatId: chatId,
                                        text: groqAnswer,
                                        cancellationToken: cancellationToken
                                    );

                                    return;
                                }
                            }
                        // photo type of message
                        case MessageType.Photo:
                            {
                                await AskingDocumentPhoto(botClient, update, cancellationToken, userStates);
                                return;
                            }
                    }
                    return;
                }
            // callback query type of message
            case UpdateType.CallbackQuery:
                {
                    await AskingDocumentCallbackQuery(botClient, update, cancellationToken, userStates);
                    return;
                }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}

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

static async Task StartMessage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is null)
    {
        return;
    }

    var groqService = new GroqService(Constants.apiKeyGroq);
    var responseFromGroq = await groqService.GetGroqResponseAsync("User starts a conversation with Telegram chat bot for " +
        "assistance with car insurance, " +
        "so the bot (you) should introduce itself and explain that its purpose is to assist with car insurance purchases.");

    await botClient.SendMessage(update.Message.Chat.Id, text: responseFromGroq, replyMarkup: AskingDocumentKeyboard(),
        cancellationToken: cancellationToken); // sending message with text and input keyboard
}

// keyboard with inline buttons "Passport" and "Vehicle identification document"
static InlineKeyboardMarkup AskingDocumentKeyboard()
{
    var inlineKeyboard = new InlineKeyboardMarkup(
        new List<InlineKeyboardButton[]>()
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("Passport", "Passport"),
                InlineKeyboardButton.WithCallbackData("Vehicle identification document", "Vehicle identification document"),
            },
        }
    );

    return inlineKeyboard;
}

static async Task AskingDocumentCallbackQuery(ITelegramBotClient botClient, Update update,
    CancellationToken cancellationToken, Dictionary<long, UserDocuments> userStates)
{
    CallbackQuery? callbackQuery = update.CallbackQuery;

    if (callbackQuery is null || callbackQuery.Message is null)
    {
        return;
    }

    var chatId = callbackQuery.Message.Chat.Id;
    var groqService = new GroqService(Constants.apiKeyGroq);

    // if user didn't send any photos before, he will be added to dictionary with user documents
    if (!userStates.ContainsKey(chatId))
    {
        userStates[chatId] = new UserDocuments();
    }

    var userState = userStates[chatId];

    // if user choose button passport
    if (callbackQuery.Data == "Passport")
    {
        userState.WaitingFor = "Passport"; // bot waiting for photo of passport

        var responseFromGroq = await groqService.GetGroqResponseAsync("User press button for sending photo of their passport. " +
            "You should say that you are waiting for photo of their passport.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            cancellationToken: cancellationToken); // sending message that bot wait for photo of passport
    }
    else if (callbackQuery.Data == "Vehicle identification document")
    {
        userState.WaitingFor = "Vehicle identification document";

        var responseFromGroq = await groqService.GetGroqResponseAsync("User press button for sending photo of their " +
            "vehicle identification document. " +
            "You should say that you are waiting for photo of their vehicle identification document.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            cancellationToken: cancellationToken);
    }
    // if button is yes or no we call confirmation of extracted information from photos
    else if (callbackQuery.Data == "Yes" || callbackQuery.Data == "No")
    {
        await AskingDocumentConfirmationCallbackQuery(botClient, update, cancellationToken, userStates);
    }
    // if button is yes or no we call confirmation of price for dummy insurance policy document
    else if (callbackQuery.Data == "YesPrice" || callbackQuery.Data == "NoPrice")
    {
        await AskingPriceConfirmationCallbackQuery(botClient, update, cancellationToken);
    }

    // The following method must be used in this type of keyboard in order to send a telegram request
    // that we clicked on the button
    await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
}

// method that asking user for sending photos of documents
static async Task AskingDocumentPhoto(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken,
    Dictionary<long, UserDocuments> userStates)
{
    if (update.Message is null)
    {
        return;
    }

    var chatId = update.Message.Chat.Id;
    var groqService = new GroqService(Constants.apiKeyGroq);

    // if we haven't user at the dictionary of user document's
    if (!userStates.ContainsKey(chatId))
    {
        var responseFromGroq = await groqService.GetGroqResponseAsync("User doesn't press button to choose which document they " +
            "want to send. You should say that user should press button to choose which document they want to send.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            cancellationToken: cancellationToken);
        return;
    }

    var userState = userStates[chatId];

    // if user don't pressed any button
    if (userState.WaitingFor == null)
    {
        var responseFromGroq = await groqService.GetGroqResponseAsync("User doesn't press button to choose which document they " +
            "want to send. You should say that user should press button to choose which document they want to send.");

        await botClient.SendMessage(
            chatId: chatId,
            text: "Please, select a document using the buttons first.",
            cancellationToken: cancellationToken);
        return;
    }

    // if user choosed send passport, bot waiting for passport document
    if (userState.WaitingFor == "Passport")
    {
        userState.PassportReceived = true; // passport received
        userState.WaitingFor = ""; // clearing bot's waiting for

        var responseFromGroq = await groqService.GetGroqResponseAsync("User sent a photo of their passport. " +
            "You should inform user that chat (you) received a photo of their passport.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            cancellationToken: cancellationToken); // sending message that passport received by bot
    }
    else if (userState.WaitingFor == "Vehicle identification document")
    {
        userState.VehicleDocReceived = true;
        userState.WaitingFor = "";

        var responseFromGroq = await groqService.GetGroqResponseAsync("User sent a photo of their " +
            "vehicle identification document. You should inform user that chat (you) received a photo of their passport.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            cancellationToken: cancellationToken);
    }

    // if user sended passport and vehicle identification document
    if (userState.PassportReceived && userState.VehicleDocReceived)
    {
        var extractedData = await ExtractDataAsync(); // mock Mindee API extract data from sended documents
        var responseFromGroq = await groqService.GetGroqResponseAsync($"User sent a both of document's photos. " +
            $"Display the extracted data to the user for confirmation. You have this extracted data: {extractedData}.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq, // sending message with extracted data
            replyMarkup: AskingConfirmationDataKeyboard(), // input keyboard which help agree or disagree with extracted data
            cancellationToken: cancellationToken);
    }
    // if user didn't send a passport photo
    else if (!userState.PassportReceived)
    {
        // inline button which ask to send passport
        var inlineKeyboard = new InlineKeyboardMarkup(
            new List<InlineKeyboardButton[]>()
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("Send passport", "Passport"),
                },
            }
        );

        var responseFromGroq = await groqService.GetGroqResponseAsync("Prompt the user to submit a photo of their passport.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq, // sending message which ask to send passport
            replyMarkup: inlineKeyboard, // inline keyboard with prompt to send passport
            cancellationToken: cancellationToken);
    }
    else if (!userState.VehicleDocReceived)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(
            new List<InlineKeyboardButton[]>()
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("Send vehicle identification document",
                    "Vehicle identification document"),
                },
            }
        );

        var responseFromGroq = await groqService.GetGroqResponseAsync("Prompt the user to submit a photo " +
            "of their vehicle identification document.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }
}

//This is just a custom mock of Mindee API
static async Task<string> ExtractDataAsync()
{
    // document data
    string passportName = "Ivan Ivanov";
    string passportNumber = "DA123456";
    string passportBirthDate = "01.01.1995";
    string vehicleVIN = "WDD2050801R123456";
    string vehicleRegistrationNumber = "AC 0001 CI";

    await Task.Delay(500); // simulate API call delay

    // returning extracted data
    return $"Passport\nName: {passportName}\nNumber: {passportNumber}\nDate of birth: {passportBirthDate}\n" +
        $"\nVehicle registration\nVIN: {vehicleVIN}\nRegistration number: {vehicleRegistrationNumber}";
}

// inline keyboard for confirmation of extracted data from photos
static InlineKeyboardMarkup AskingConfirmationDataKeyboard()
{
    var inlineKeyboard = new InlineKeyboardMarkup(
        new List<InlineKeyboardButton[]>()
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("Yes", "Yes"),
                InlineKeyboardButton.WithCallbackData("No", "No"),
            },
        }
    );

    return inlineKeyboard;
}

// method which ask about correctness of documents
static async Task AskingDocumentConfirmationCallbackQuery(ITelegramBotClient botClient, Update update,
    CancellationToken cancellationToken, Dictionary<long, UserDocuments> userStates)
{
    CallbackQuery? callbackQuery = update.CallbackQuery;

    if (callbackQuery is null || callbackQuery.Message is null)
    {
        return;
    }

    var chatId = callbackQuery.Message.Chat.Id;

    if (!userStates.ContainsKey(chatId))
    {
        userStates[chatId] = new UserDocuments();
    }

    var userState = userStates[chatId];
    var groqService = new GroqService(Constants.apiKeyGroq);

    if (callbackQuery.Data == "No")
    {
        userState.PassportReceived = false;
        userState.VehicleDocReceived = false;
        userState.WaitingFor = "";

        var responseFromGroq = await groqService.GetGroqResponseAsync("User disagrees with the extracted data, " +
            "so the bot (you) request that they retake and resubmit the photo");

        // if extracted data isn't correct and user said about it, chat ask to retake and resend photos
        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            replyMarkup: AskingDocumentKeyboard(),
            cancellationToken: cancellationToken);
    }
    // if user said that extracted data is correct, chat say about price for the insurance
    else if (callbackQuery.Data == "Yes")
    {
        var responseFromGroq = await groqService.GetGroqResponseAsync("User sagrees with the extracted data, so the bot (you) " +
            "should inform the user that the fixed price for the insurance is 100 USD. " +
            "Ask the user if they agree with the price.");

        await botClient.SendMessage(
            chatId: chatId,
            text: responseFromGroq,
            replyMarkup: AskingPriceConfirmationKeyboard(), // input keyboard to agree or disagree with price of insurance
            cancellationToken: cancellationToken);
    }

    await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
}

// input keyboard for price confiration
static InlineKeyboardMarkup AskingPriceConfirmationKeyboard()
{
    var inlineKeyboard = new InlineKeyboardMarkup(
        new List<InlineKeyboardButton[]>()
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("Yes", "YesPrice"),
                InlineKeyboardButton.WithCallbackData("No", "NoPrice"),
            },
        }
    );

    return inlineKeyboard;
}

// method for answer after user agree or disagree with price for insurance
static async Task AskingPriceConfirmationCallbackQuery(ITelegramBotClient botClient, Update update,
    CancellationToken cancellationToken)
{
    CallbackQuery? callbackQuery = update.CallbackQuery;

    if (callbackQuery is null || callbackQuery.Message is null)
    {
        return;
    }

    var chatId = callbackQuery.Message.Chat.Id;
    var groqService = new GroqService(Constants.apiKeyGroq);

    // user disagreed with price
    if (callbackQuery.Data == "NoPrice")
    {
        var responseFromGroq = await groqService.GetGroqResponseAsync("User disagrees with the fixed price for the insurance " +
            "which 100 USD, so the bot (you) should apologize and explain that 100 USD is the only available price. " +
            "After ask user if they want to continue");

        await botClient.SendMessage(
            chatId: chatId,
            // chat say that price is fixed
            text: responseFromGroq,
            replyMarkup: AskingPriceConfirmationKeyboard(), // input keyboard for agree or disagree
            cancellationToken: cancellationToken);
    }
    // user agreed with price
    else if (callbackQuery.Data == "YesPrice")
    {
        await GenerateDummyInsurancePolicyDocument(botClient, update, cancellationToken); // sending maked insurance
    }

    await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
}

//This is usage of a custom mock of Open AI API
static async Task GenerateDummyInsurancePolicyDocument(ITelegramBotClient botClient, Update update,
    CancellationToken cancellationToken)
{
    try
    {
        if (update.CallbackQuery is null || update.CallbackQuery.Message is null)
        {
            return;
        }

        var chatId = update.CallbackQuery.Message.Chat.Id;
        string extractedData = await ExtractDataAsync();
        var groqService = new GroqService(Constants.apiKeyGroq);
        string policyText = await groqService.GenerateDummyInsurancePolicyAsync(extractedData);

        string pdfPath = Path.Combine("Documents", $"InsurancePolicy_{chatId}.pdf");
        Directory.CreateDirectory("Documents");
        await PdfService.GeneratePdfAsync(policyText, pdfPath);

        using var fileStream = File.OpenRead(pdfPath);
        await botClient.SendDocument(
            chatId: chatId,
            document: new InputFileStream(fileStream, $"InsurancePolicy_{chatId}.pdf"),
            caption: "Here is your insurance policy document. Thank you for your purchase!",
            cancellationToken: cancellationToken
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}