using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

internal class Program
{
    private static Host? bot;
    private static Dictionary<long, string> stage = new();
    private static Dictionary<long, List<string>> locationSelection = new();
    private static Dictionary<long, List<string>> categorySelection = new();
    private static Dictionary<long, string> range = new();
    private static Dictionary<long, List<string>> itemCondition = new();
    private static Dictionary<long, List<string>> sourceType = new();
    private static Dictionary<long, bool> searchAvailable = new();
    private static Dictionary<long, bool> isSearching = new();
    private static readonly Dictionary<long, DateTime> userStartTimes = new();
    private static Dictionary<long, string> changeMessage = new();
    private static Dictionary<long, int> lastFilterMessageId = new();
    private static Dictionary<long, List<string>> sentItems = new();

    private static void Main()
    {
        Program program = new Program();
        DataBase dataBase = new DataBase(program);
        dataBase.StartPeriodicCheck();

        bot = new Host("YOUR_BOT_TOKEN");
        bot.Start();
        bot.OnMessage += OnMessage;
        Console.ReadLine();
    }

    private static async void OnMessage(ITelegramBotClient client, Update update)
    {
        if (update.Message?.Chat.Type != ChatType.Private) return;

        var chatId = update.Message.Chat.Id;

        if (!isSearching.ContainsKey(chatId))
        {
            isSearching[chatId] = false;
        }

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🔍 Filters") },
            new[] { new KeyboardButton("❓ Help"), new KeyboardButton("💙 Support") },
            new[] { new KeyboardButton("📢 Ads") }
        })
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };

        var startKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("⚙️ Set Filters", "Start") }
        });

        switch (update.Message.Text)
        {
            case "/start":
                await client.SendMessage(chatId, "🚀 Welcome!", keyboard, ParseMode.Markdown);
                await client.SendMessage(chatId, "Set up filters to start!", startKeyboard, ParseMode.Markdown);
                break;

            case "🔍 Filters":
                if (lastFilterMessageId.ContainsKey(chatId))
                {
                    try { await client.DeleteMessage(chatId, lastFilterMessageId[chatId]); } catch { }
                    lastFilterMessageId.Remove(chatId);
                }

                if (categorySelection.ContainsKey(chatId) && categorySelection[chatId].Any() &&
                    locationSelection.ContainsKey(chatId) && locationSelection[chatId].Any() &&
                    !string.IsNullOrEmpty(range[chatId]) && itemCondition.ContainsKey(chatId) &&
                    itemCondition[chatId].Any() && sourceType.ContainsKey(chatId) && sourceType[chatId].Any())
                {
                    searchAvailable[chatId] = true;
                    var sentMessage = await client.SendMessage(
                        chatId, 
                        $"Filters set:\nCategory: {string.Join(", ", categorySelection[chatId])}\nLocations: {string.Join(", ", locationSelection[chatId])}\nRange: {range[chatId]}\nCondition: {string.Join(", ", itemCondition[chatId])}\nSource: {string.Join(", ", sourceType[chatId])}",
                        GetSearchKeyboard(chatId),
                        ParseMode.Markdown
                    );
                    lastFilterMessageId[chatId] = sentMessage.MessageId;
                }
                else
                {
                    searchAvailable[chatId] = false;
                    await client.SendMessage(chatId, "❌ Not all filters are set!", startKeyboard, ParseMode.Markdown);
                }
                break;

            case "❓ Help":
                await client.SendMessage(chatId, "Contact us for help!", InlineKeyboardMarkup.Empty(), ParseMode.Markdown);
                break;

            case "💙 Support":
                await client.SendMessage(chatId, "Support us!", InlineKeyboardMarkup.Empty(), ParseMode.Markdown);
                break;

            case "📢 Ads":
                await client.SendMessage(chatId, "Contact for ads!", InlineKeyboardMarkup.Empty(), ParseMode.Markdown);
                break;

            default:
                await client.SendMessage(chatId, "Use /start for info.", keyboard);
                break;
        }

        if (update.CallbackQuery != null)
        {
            var chatId = update.CallbackQuery.Message.Chat.Id;
            var selection = update.CallbackQuery.Data;

            var categoryKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Category1", "Category1"), InlineKeyboardButton.WithCallbackData("Category2", "Category2") },
                new[] { InlineKeyboardButton.WithCallbackData("Next 👇", "Next") }
            });

            var rangeKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Low", "Low"), InlineKeyboardButton.WithCallbackData("Medium", "Medium") },
                new[] { InlineKeyboardButton.WithCallbackData("Next 👇", "Next") }
            });

            if (selection == "Start")
            {
                isSearching[chatId] = false;
                searchAvailable[chatId] = false;
                InitializeUserData(chatId);

                var message = await client.SendMessage(
                    chatId,
                    "🔹 Step 1: Select category",
                    categoryKeyboard,
                    ParseMode.MarkdownV2
                );
                changeMessage[chatId] = message.MessageId.ToString();
            }
            else if (selection == "search" && searchAvailable[chatId])
            {
                userStartTimes[chatId] = DateTime.UtcNow.AddMinutes(-5);
                isSearching[chatId] = true;
                await client.SendMessage(chatId, "Search started! 🕵️", ParseMode.Markdown);
                _ = Task.Run(async () => await PeriodicSearch(client, chatId));
            }
            else if (selection == "stopsearch")
            {
                isSearching[chatId] = false;
                await client.SendMessage(chatId, "Search stopped ❌", ParseMode.Markdown);
                UpdateFilterMessage(client, chatId);
            }
        }
    }

    private static void InitializeUserData(long chatId)
    {
        categorySelection[chatId] = new List<string>();
        locationSelection[chatId] = new List<string>();
        range[chatId] = "";
        stage[chatId] = "category";
        itemCondition[chatId] = new List<string>();
        sourceType[chatId] = new List<string>();
        sentItems[chatId] = new List<string>();
    }

    private static InlineKeyboardMarkup GetSearchKeyboard(long chatId)
    {
        return isSearching[chatId]
            ? new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Stop Search ❌", "stopsearch") },
                new[] { InlineKeyboardButton.WithCallbackData("⚙️ Set Filters", "Start") }
            })
            : new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🚀 Start Search", "search") },
                new[] { InlineKeyboardButton.WithCallbackData("⚙️ Set Filters", "Start") }
            });
    }

    private static async Task UpdateFilterMessage(ITelegramBotClient client, long chatId)
    {
        if (lastFilterMessageId.TryGetValue(chatId, out int messageId))
        {
            try
            {
                await client.EditMessageReplyMarkup(chatId, messageId, GetSearchKeyboard(chatId));
            }
            catch { }
        }
    }

    private static async Task PeriodicSearch(ITelegramBotClient client, long chatId)
    {
        while (isSearching[chatId])
        {
            // Placeholder for search logic
            await Task.Delay(TimeSpan.FromMinutes(15));
        }
    }
}