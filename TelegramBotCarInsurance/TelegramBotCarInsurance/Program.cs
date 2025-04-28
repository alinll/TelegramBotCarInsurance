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
                                // start message
                                if (message.Text == "/start")
                                {
                                    await StartMessage(botClient, update, cancellationToken);
                                    return;
                                }

                                return;
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

    string welcomeText = "Hello! I'm a bot that helps with car insurance.\nI'll help you get insurance.\n" +
        "Please, submit a photo of your passport and vehicle identification document.";

    await botClient.SendMessage(update.Message.Chat.Id, text: welcomeText, replyMarkup: AskingDocumentKeyboard(),
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

        await botClient.SendMessage(
            chatId: chatId,
            text: "I'm waiting for a photo of your passport.",
            cancellationToken: cancellationToken); // sending message that bot wait for photo of passport
    }
    else if (callbackQuery.Data == "Vehicle identification document")
    {
        userState.WaitingFor = "Vehicle identification document";

        await botClient.SendMessage(
            chatId: chatId,
            text: "I'm waiting for a photo of your vehicle identification document.",
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

    // if we haven't user at the dictionary of user document's
    if (!userStates.ContainsKey(chatId))
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "Please, select a document using the buttons first.",
            cancellationToken: cancellationToken);
        return;
    }

    var userState = userStates[chatId];

    // if user don't pressed any button
    if (userState.WaitingFor == null)
    {
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

        await botClient.SendMessage(
            chatId: chatId,
            text: "Passport received.",
            cancellationToken: cancellationToken); // sending message that passport received by bot
    }
    else if (userState.WaitingFor == "Vehicle identification document")
    {
        userState.VehicleDocReceived = true;
        userState.WaitingFor = "";

        await botClient.SendMessage(
            chatId: chatId,
            text: "Vehicle document received.",
            cancellationToken: cancellationToken);
    }

    // if user sended passport and vehicle identification document
    if (userState.PassportReceived && userState.VehicleDocReceived)
    {
        var extractedData = await ExtractDataAsync(); // mock Mindee API extract data from sended documents
        await botClient.SendMessage(
            chatId: chatId,
            text: $"Thank you! Both documents received. " +
            $"I found the following data:\n\n{extractedData}\n\nIs everything correct?", // sending message with extracted data
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
        await botClient.SendMessage(
            chatId: chatId,
            text: "Please send a photo of your passport.", // sending message which ask to send passport
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
        await botClient.SendMessage(
            chatId: chatId,
            text: "Please send a photo of your vehicle identification document.",
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

    if (callbackQuery.Data == "No")
    {
        userState.PassportReceived = false;
        userState.VehicleDocReceived = false;
        userState.WaitingFor = "";

        // if extracted data isn't correct and user said about it, chat ask to retake and resend photos
        await botClient.SendMessage(
            chatId: chatId,
            text: "Sorry for my mistake. Please, retake and resubmit photos.",
            replyMarkup: AskingDocumentKeyboard(),
            cancellationToken: cancellationToken);
    }
    // if user said that extracted data is correct, chat say about price for the insurance
    else if (callbackQuery.Data == "Yes")
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "Well, now I want to inform you that the fixed price for the insurance is 100 USD." +
            "\nDo you agree with this price?",
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

    // user disagreed with price
    if (callbackQuery.Data == "NoPrice")
    {
        await botClient.SendMessage(
            chatId: chatId,
            // chat say that price is fixed
            text: "I apologize, but 100 USD is the only available price. Do you want to continue?",
            replyMarkup: AskingPriceConfirmationKeyboard(), // input keyboard for agree or disagree
            cancellationToken: cancellationToken);
    }
    // user agreed with price
    else if (callbackQuery.Data == "YesPrice")
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "Okay, I'll send you your dummy insurance policy document.",
            cancellationToken: cancellationToken);
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
        string dummyInsurancePolicyText = await MockOpenAiGenerateInsurancePolicy(extractedData);
        string tempFilePath = Path.GetRandomFileName();
        string insurancePolicyFilePath = Path.ChangeExtension(tempFilePath, ".txt");

        await File.WriteAllTextAsync(insurancePolicyFilePath, dummyInsurancePolicyText, cancellationToken);

        using var fileStream = File.OpenRead(insurancePolicyFilePath);
        var inputFile = new InputFileStream(fileStream, Path.GetFileName(insurancePolicyFilePath));

        await botClient.SendDocument(
            chatId: chatId,
            document: inputFile,
            caption: "Here is your insurance policy. Thank you for your purchase!",
            cancellationToken: cancellationToken
        );

        File.Delete(insurancePolicyFilePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}

//This is just a custom mock of Open AI API
static async Task<string> MockOpenAiGenerateInsurancePolicy(string extractedData)
{
    await Task.Delay(1000);

    return $@"
INSURANCE POLICY
==================

Extracted Information:
{extractedData}

Policy Number: {Guid.NewGuid()}
Valid From: {DateTime.UtcNow:dd.MM.yyyy}
Valid To: {DateTime.UtcNow.AddYears(1):dd.MM.yyyy}

Coverage:
- Full coverage for vehicle damages
- Theft protection
- Roadside assistance

Issued by: Car Insurance Bot
Date of Issue: {DateTime.UtcNow:dd.MM.yyyy}

Thank you for choosing us!
";
}