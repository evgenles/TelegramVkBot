using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Citrina;
using Newtonsoft.Json;
using Telegram.Bot.Types.ReplyMarkups;
using VkLibrary.Core;
using VkLibrary.Core.LongPolling;
using Telegram.Bot.Types;

namespace TelegramVkBot
{
    public class TelegramSender : IDisposable
    {
        private readonly string _telegramToken;
        private readonly string _vkToken;
        private readonly string _userId;

        private readonly TelegramBotClient _telegram;
        private readonly Vkontakte _vk;
        private readonly LongPollClient _vkPool;

        private DateTime endOfDialog = new DateTime();
        private int idReceiver;
        public TelegramSender(string telegramToken, string vkToken, string userId)
        {
            _telegramToken = telegramToken;
            _vkToken = vkToken;
            _userId = userId;
            _telegram = new TelegramBotClient(telegramToken);
            _telegram.OnMessage += _telegram_OnMessage;
            _telegram.OnMessageEdited += _telegram_OnMessageEdited;
            _telegram.OnCallbackQuery += _telegram_OnCallbackQuery;
            _telegram.StartReceiving();
            _vk = new Vkontakte(0)
            {
                AccessToken = new VkLibrary.Core.Auth.AccessToken
                {
                    Token = vkToken,
                    ExpiresIn = 0
                }
            };
            var poolServer = _vk.Messages.GetLongPollServer().GetAwaiter().GetResult();
            _vkPool = _vk.StartLongPollClient(poolServer.Server, poolServer.Key, poolServer.Ts).GetAwaiter().GetResult();
            _vkPool.AddMessageEvent += _vkPool_AddMessageEvent;
        }

        private async void _vkPool_AddMessageEvent(object sender, Tuple<int, MessageFlags, Newtonsoft.Json.Linq.JArray> e)
        {
            var messageId = e.Item1;
            var flag = e.Item2;
            var fields = e.Item3;
            if (flag == MessageFlags.Unread)
            {
                var senderMsg = (await _vk.Users.Get(userIds: new List<string> {fields[3].ToString() })).First();
                await _telegram.SendTextMessageAsync(_userId, $"{senderMsg.FirstName} {senderMsg.LastName} (/{senderMsg.Id}) : {Environment.NewLine}" +
                    $" {  fields[6].ToString()}");
            }
        }

        private async void _telegram_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            await MenuAsync(e.CallbackQuery.Data, e.CallbackQuery.Message.Chat.Id);
        }

        private void _telegram_OnMessageEdited(object sender, Telegram.Bot.Args.MessageEventArgs messageEventArgs)
        {
            throw new NotImplementedException();
        }

        private async void _telegram_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            if (_userId != message.Chat.Id.ToString())
            {
                await _telegram.SendTextMessageAsync(message.Chat.Id, "Unauthorized user");
                return;
            }

            if (message?.Type == MessageType.Text)
            {
                var splittedMessage = message.Text.Split();
                if (endOfDialog > DateTime.Now && splittedMessage.First().First() != '/')
                {
                    await _vk.Messages.Send(idReceiver, message: message.Text);
                    endOfDialog = DateTime.Now.AddMinutes(10);
                }
                else if (splittedMessage.First().First() == '/')
                {
                    if (Int32.TryParse(splittedMessage.First().Substring(1), out idReceiver))
                    {
                        endOfDialog = DateTime.Now.AddMinutes(10);
                        var userOnlineRequest = await _vk.Users.Get(userIds: new List<string> { idReceiver.ToString() });
                       
                        await _telegram.SendTextMessageAsync(message.Chat.Id, $"User selected for dialog: {userOnlineRequest.First().FirstName} " +
                            $"{userOnlineRequest.First().LastName} (/{idReceiver})");
                    }
                    else
                    {
                        await MenuAsync(splittedMessage.First(), message.Chat.Id);
                    }
                }
                else
                {
                    await _telegram.SendTextMessageAsync(message.Chat.Id, "Please select user to start dialog");
                }

                //await _telegram.SendTextMessageAsync(message.Chat.Id, "Sended");
            }
        }

        private async Task MenuAsync(string query, long chatId)
        {
            switch (query)
            {
                case "/start":
                    await _telegram.SendTextMessageAsync(chatId, "Select command", replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("FriendsOn", "/friendson"),
                                InlineKeyboardButton.WithCallbackData("All Friends", "/allfriends"),
                                InlineKeyboardButton.WithCallbackData("LastDialogs", "/lastdialogs")
                            }
                        }));
                    break;
                case "/friendson":
                    var usersOnlineIdRequest = await _vk.Friends.GetOnline();
                    //if (usersOnlineIdRequest.IsError)
                    //{
                    //    await _telegram.SendTextMessageAsync(chatId, $"Error while getting usersId: {usersOnlineIdRequest.Error.Message}");
                    //}
                    //else
                    //{
                    var userOnlineRequest = await _vk.Users.Get(userIds: usersOnlineIdRequest.Select(id => id.ToString()));
                    //if (userOnlineRequest.IsError)
                    //{
                    //    await _telegram.SendTextMessageAsync(chatId, $"Error while getting users: {userOnlineRequest.Error.Message}");
                    //}
                    //else
                    //{
                    var usersOnlineMsgs = "Friends online list: " + Environment.NewLine +
                        string.Join(Environment.NewLine, userOnlineRequest.Select(user => $"{user.FirstName} {user.LastName}  (/{user.Id})"));
                    await _telegram.SendTextMessageAsync(chatId, usersOnlineMsgs);
                    //}
                    // }
                    break;
            }
        }
        public void Dispose()
        {
            _telegram.StopReceiving();
        }
    }
}
