using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace TelegramBotCarInsurance
{
    internal static class TelegramBotService
    {
        static readonly GroqService groqService = new(Constants.apiKeyGroq);

        public static async Task UpdateHandler(ITelegramBotClient botClient, Update update, 
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
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
                                        else
                                        {
                                            if (message is null || message.Text is null)
                                            {
                                                return;
                                            }

                                            await botClient.SendMessage(
                                                chatId: message.Chat.Id,
                                                text: await groqService.GetGroqResponseAsync(message.Text),
                                                cancellationToken: cancellationToken
                                            );

                                            return;
                                        }
                                    }
                                // photo type of message
                                case MessageType.Document:
                                    {
                                        await AskingDocumentPhoto(botClient, update, userStates, cancellationToken);
                                        return;
                                    }
                            }
                            return;
                        }
                    // callback query type of message
                    case UpdateType.CallbackQuery:
                        {
                            await AskingDocumentCallbackQuery(botClient, update, userStates, cancellationToken);
                            return;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        static async Task StartMessage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is null)
                {
                    return;
                }

                await botClient.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: await groqService.GetGroqResponseAsync("User starts a conversation with Telegram chat bot for " +
                    "assistance with car insurance, so the bot (you) should introduce itself and explain that its purpose " +
                    "is to assist with car insurance purchases."), 
                    replyMarkup: AskingDocumentKeyboard(),
                    cancellationToken: cancellationToken); // sending message with text and input keyboard
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        // keyboard with inline buttons "Passport" and "Vehicle identification document"
        static InlineKeyboardMarkup AskingDocumentKeyboard()
        {
            return new InlineKeyboardMarkup(
                new List<InlineKeyboardButton[]>()
                {
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("Passport", "Passport"),
                        InlineKeyboardButton.WithCallbackData("Vehicle identification document",
                        "Vehicle identification document"),
                    },
                }
            );
        }

        static async Task AskingDocumentCallbackQuery(ITelegramBotClient botClient, Update update, 
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
        {
            try
            {
                CallbackQuery? callbackQuery = update.CallbackQuery;

                if (callbackQuery is null || callbackQuery.Message is null)
                {
                    return;
                }

                var chatId = callbackQuery.Message.Chat.Id;

                // if user didn't send any photos before, he will be added to dictionary with user documents
                if (!userStates.TryGetValue(chatId, out UserDocuments? userState))
                {
                    userState = new UserDocuments();
                    userStates[chatId] = userState;
                }

                // if user choose button passport
                if (callbackQuery.Data == "Passport")
                {
                    userState.WaitingFor = "Passport"; // bot waiting for photo of passport

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User press button for sending photo of their passport. " +
                        "You should say that you are waiting for photo of their passport."),
                        cancellationToken: cancellationToken); // sending message that bot wait for photo of passport
                }
                else if (callbackQuery.Data == "Vehicle identification document")
                {
                    userState.WaitingFor = "Vehicle identification document";

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User press button for sending photo of their vehicle " +
                        "identification document. " +
                        "You should say that you are waiting for photo of their vehicle identification document."),
                        cancellationToken: cancellationToken);
                }
                // if button is yes or no we call confirmation of extracted information from photos
                else if (callbackQuery.Data == "Yes" || callbackQuery.Data == "No")
                {
                    await AskingDocumentConfirmationCallbackQuery(botClient, update, userStates, cancellationToken);
                }
                // if button is yes or no we call confirmation of price for dummy insurance policy document
                else if (callbackQuery.Data == "YesPrice" || callbackQuery.Data == "NoPrice")
                {
                    await AskingPriceConfirmationCallbackQuery(botClient, update, userStates, cancellationToken);
                }

                // The following method must be used in this type of keyboard in order to send a telegram request
                // that we clicked on the button
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        // method that asking user for sending photos of documents
        static async Task AskingDocumentPhoto(ITelegramBotClient botClient, Update update, 
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is null || update.Message.Document is null)
                {
                    return;
                }

                var chatId = update.Message.Chat.Id;

                // if we haven't user at the dictionary of user document's
                if (!userStates.TryGetValue(chatId, out UserDocuments? userState))
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User doesn't press button to choose which document they " +
                        "want to send. You should say that user should press button to choose which document they want to send."),
                        cancellationToken: cancellationToken);
                    return;
                }

                // if user don't pressed any button
                if (userState.WaitingFor == null)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User doesn't press button to choose which document they " +
                        "want to send. You should say that user should press button to choose which document they want to send."),
                        cancellationToken: cancellationToken);
                    return;
                }

                // if user choosed send passport, bot waiting for passport document
                if (userState.WaitingFor == "Passport")
                {
                    userState.PassportReceived = true; // passport received
                    userState.WaitingFor = ""; // clearing bot's waiting for
                    userState.PassportFileId = update.Message.Document.FileId;

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User sent a photo of their passport. " +
                        "You should inform user that chat (you) received a photo of their passport."),
                        cancellationToken: cancellationToken); // sending message that passport received by bot
                }
                else if (userState.WaitingFor == "Vehicle identification document")
                {
                    userState.VehicleDocReceived = true;
                    userState.WaitingFor = "";
                    userState.VehicleFileId = update.Message.Document.FileId;

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User sent a photo of their vehicle identification " +
                        "document. You should inform user that chat (you) received a photo of their passport."),
                        cancellationToken: cancellationToken);
                }

                // if user sended passport and vehicle identification document
                if (userState.PassportReceived && userState.VehicleDocReceived)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync($"User sent a both of document's photos. " +
                        $"Display the extracted data to the user for confirmation. " +
                        $"You have this extracted data: {await ExtractDataAsync(botClient, update, userStates)}."),
                        replyMarkup: AskingConfirmationDataKeyboard(), 
                        cancellationToken: cancellationToken);
                }
                // if user didn't send a passport photo
                else if (!userState.PassportReceived)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("Prompt the user to submit a photo of their passport."),
                        replyMarkup: new InlineKeyboardMarkup(
                        new List<InlineKeyboardButton[]>()
                        {
                            new InlineKeyboardButton[]
                            {
                                InlineKeyboardButton.WithCallbackData("Send passport", "Passport"),
                            },
                        }),
                        cancellationToken: cancellationToken);
                }
                else if (!userState.VehicleDocReceived)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("Prompt the user to submit a photo of their vehicle " +
                        "identification document."),
                        replyMarkup: new InlineKeyboardMarkup(
                        new List<InlineKeyboardButton[]>()
                        {
                            new InlineKeyboardButton[]
                            {
                                InlineKeyboardButton.WithCallbackData("Send vehicle identification document",
                                "Vehicle identification document"),
                            },
                        }),
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        static async Task<string> ExtractDataAsync(ITelegramBotClient botClient, Update update, 
            Dictionary<long, UserDocuments> userState)
        {
            try
            {
                var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;

                if (chatId == 0)
                {
                    return "Error with finding chat";
                }

                var passportFileId = userState[chatId].PassportFileId;
                var vehicleFileId = userState[chatId].VehicleFileId;

                if (string.IsNullOrEmpty(passportFileId) || string.IsNullOrEmpty(vehicleFileId))
                {
                    return "Both documents must be uploaded.";
                }

                return $"Passport Info:\n{await ExtractTextFromTelegramFile(botClient, passportFileId)}\n\n" +
                    $"Vehicle Info:\n{await ExtractTextFromTelegramFile(botClient, vehicleFileId)}";
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }

        static async Task<string> ExtractTextFromTelegramFile(ITelegramBotClient botClient, string fileId)
        {
            try
            {
                var file = await botClient.GetFile(fileId);

                if (file.FilePath is null)
                {
                    return "Error with finding file path";
                }

                var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(file.FilePath) ?? ".jpg"}");

                await using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    await botClient.DownloadFile(file.FilePath, stream);
                }

                var text = TesseractOcrService.ExtractTextFromImage(tempFile);

                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    Console.WriteLine("Error with deleting file");
                }

                return text;
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }

        // inline keyboard for confirmation of extracted data from photos
        static InlineKeyboardMarkup AskingConfirmationDataKeyboard()
        {
            return new InlineKeyboardMarkup(
                new List<InlineKeyboardButton[]>()
                {
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("Yes", "Yes"),
                        InlineKeyboardButton.WithCallbackData("No", "No"),
                    },
                }
            );
        }

        // method which ask about correctness of documents
        static async Task AskingDocumentConfirmationCallbackQuery(ITelegramBotClient botClient, Update update,
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
        {
            try
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
                        text: await groqService.GetGroqResponseAsync("User disagrees with the extracted data, so the bot (you) " +
                        "request that they retake and resubmit the photo"),
                        replyMarkup: AskingDocumentKeyboard(),
                        cancellationToken: cancellationToken);
                }
                // if user said that extracted data is correct, chat say about price for the insurance
                else if (callbackQuery.Data == "Yes")
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: await groqService.GetGroqResponseAsync("User sagrees with the extracted data, so the bot (you) " +
                        "should inform the user that the fixed price for the insurance is 100 USD. " +
                        "Ask the user if they agree with the price."),
                        replyMarkup: AskingPriceConfirmationKeyboard(),
                        cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        // input keyboard for price confiration
        static InlineKeyboardMarkup AskingPriceConfirmationKeyboard()
        {
            return new InlineKeyboardMarkup(
                new List<InlineKeyboardButton[]>()
                {
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("Yes", "YesPrice"),
                        InlineKeyboardButton.WithCallbackData("No", "NoPrice"),
                    },
                }
            );
        }

        // method for answer after user agree or disagree with price for insurance
        static async Task AskingPriceConfirmationCallbackQuery(ITelegramBotClient botClient, Update update,
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
        {
            try
            {
                CallbackQuery? callbackQuery = update.CallbackQuery;

                if (callbackQuery is null || callbackQuery.Message is null)
                {
                    return;
                }

                // user disagreed with price
                if (callbackQuery.Data == "NoPrice")
                {
                    await botClient.SendMessage(
                        chatId: callbackQuery.Message.Chat.Id,
                        // chat say that price is fixed
                        text: await groqService.GetGroqResponseAsync("User disagrees with the fixed price for the insurance " +
                        "which 100 USD, so the bot (you) should apologize and explain that 100 USD is the only available price. " +
                        "After ask user if they want to continue"),
                        replyMarkup: AskingPriceConfirmationKeyboard(), // input keyboard for agree or disagree
                        cancellationToken: cancellationToken);
                }
                // user agreed with price
                else if (callbackQuery.Data == "YesPrice")
                {
                    // sending maked insurance
                    await GenerateDummyInsurancePolicyDocument(botClient, update, userStates, cancellationToken); 
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        //This is usage of a custom mock of Open AI API
        static async Task GenerateDummyInsurancePolicyDocument(ITelegramBotClient botClient, Update update,
            Dictionary<long, UserDocuments> userStates, CancellationToken cancellationToken)
        {
            try
            {
                if (update.CallbackQuery is null || update.CallbackQuery.Message is null)
                {
                    return;
                }

                var chatId = update.CallbackQuery.Message.Chat.Id;
                string extractedData = await ExtractDataAsync(botClient, update, userStates);
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
                Console.WriteLine(ex.Message.ToString());
            }
        }
    }
}
