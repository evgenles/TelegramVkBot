﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using Telegram.Bot.Types.ReplyMarkups;
using VkLibrary.Core;
using VkLibrary.Core.LongPolling;
using VkNet.Model.Attachments;
using VkNet;
using Telegram.Bot.Types;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System.IO;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using Microsoft.Extensions.Logging;
using VkAudio = VkNet.Model.Attachments.Audio;
using VkSticker = VkNet.Model.Attachments.Sticker;

using Telegram.Bot.Types.InputFiles;
using VKVideo = VkNet.Model.Attachments.Video;
using System.Text.RegularExpressions;
using VkDocument = VkNet.Model.Attachments.Document;
using Microsoft.Extensions.Configuration;
using VkNet.Model;
using PhotoSize = Telegram.Bot.Types.PhotoSize;
using System.Collections.ObjectModel;

namespace TelegramVkBot
{
    public class TelegramSender : IDisposable
    {
        private readonly string _userId;

        private readonly TelegramBotClient _telegram;
        private readonly Vkontakte _vk;
        private readonly LongPollClient _vkPool;
        private DateTime endOfDialog = new DateTime();
        private long idReceiver;

        private readonly VkNet.VkApi _vkNet;
        private readonly HttpClient _httpClient = new HttpClient();
        private static Regex VideoStringURLRegex = new Regex(@"<script.*>.*var\s*playerParams\s*=\s*{(.*)}\s*;\s*var.*</script>", RegexOptions.Singleline);
        private static Regex VideoURLRegex = new Regex(@"""url(\d*)"":""([^""]+)""");

        public TelegramSender(IConfiguration configuration, ILogger<VkApi> logger)
        {
            var tokens = configuration.GetSection("Tokens").Get<TokenConfig>();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _userId = tokens.TelegramId;
            _telegram = new TelegramBotClient(tokens.Telegram);
            _telegram.OnMessage += _telegram_OnMessage;
            _telegram.OnMessageEdited += _telegram_OnMessageEdited;
            _telegram.OnCallbackQuery += _telegram_OnCallbackQuery;
            _telegram.StartReceiving();
            _vkNet = new VkApi(logger);
            _vk = new Vkontakte(0)
            {
                AccessToken = new VkLibrary.Core.Auth.AccessToken
                {
                    Token = tokens.Vk,
                    ExpiresIn = 0
                }
            };
            _vkNet.Authorize(new VkNet.Model.ApiAuthParams { AccessToken = tokens.Vk });

            var poolServer = _vkNet.Messages.GetLongPollServer();
            _vkPool = _vk.StartLongPollClient(poolServer.Server, poolServer.Key, (int)poolServer.Ts).GetAwaiter().GetResult();
            _vkPool.AddMessageEvent += _vkPool_AddMessageEvent;
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36");
        }

        private async void _vkPool_AddMessageEvent(object sender, Tuple<int, MessageFlags, Newtonsoft.Json.Linq.JArray> e)
        {
            var messageId = e.Item1;
            var flag = e.Item2;
            var fields = e.Item3;
            if ((flag & MessageFlags.Outbox) != MessageFlags.Outbox && (flag & MessageFlags.Deleted) != MessageFlags.Deleted)
            {
                var senderMsg = (await _vkNet.Users.GetAsync(userIds: new List<long> { long.Parse(fields[3].ToString()) })).First();

                await _telegram.SendTextMessageAsync(_userId, $"{senderMsg.FirstName} {senderMsg.LastName} (/{senderMsg.Id}) : {Environment.NewLine}" +
                                    $" {  fields[6].ToString()}");
                var attachments = fields[7];
                if (attachments != null)
                {
                    var msg = _vkNet.Messages.GetById(new List<ulong>() { (ulong)messageId }).First();
                    await ParseVKMessage(msg.Attachments, msg.Geo);
                }
            }
        }
        private async Task ParseVKMessage(ReadOnlyCollection<Attachment> msgAttach, Geo geo)
        {
            if (geo != null)
            {
                await _telegram.SendLocationAsync(_userId, (float)geo.Coordinates.Latitude, (float)geo.Coordinates.Longitude,
                    disableNotification: true);
            }
            foreach (var attach in msgAttach)
            {
                switch (attach.Type.Name)
                {
                    case "Photo":
                        var photo = (attach.Instance as Photo);
                        await _telegram.SendPhotoAsync(_userId,
                            new InputOnlineFile(
                                await _httpClient.GetStreamAsync(photo.Sizes.Last().Url)), caption: photo.Text);
                        break;
                    case "Audio":
                        var audio = (attach.Instance as VkAudio);
                        await _telegram.SendAudioAsync(_userId,
                            new InputOnlineFile(
                                await _httpClient.GetStreamAsync(audio.Url)), performer: audio.Artist, title: audio.Title);
                        break;
                    case "Sticker":
                        var sticker = await _httpClient.GetStreamAsync((attach.Instance as VkSticker).Images.Last().Url);
                        await _telegram.SendStickerAsync(_userId, new InputOnlineFile(sticker));
                        break;
                    case "Video":
                        var video = (attach.Instance as VKVideo);
                        var fullVideo = _vkNet.Video.Get(new VkNet.Model.RequestParams.VideoGetParams
                        {
                            Videos = new List<VKVideo> {
                                        new VKVideo
                                        {
                                            OwnerId = video.OwnerId,
                                            Id = video.Id,
                                            AccessKey = video.AccessKey
                                        }
                                    },
                            Extended = true,
                            Count = 1,
                        }).First();
                        string videoUrl = "";
                        if (fullVideo.Player.AbsoluteUri.StartsWith("https://vk.com/video_ext.php"))//Для вкшного видео загружаем себе видео
                        {
                            var htmlPlayer = await _httpClient.GetStringAsync(fullVideo.Player); //Загрузка HTML content
                            var parameters = VideoStringURLRegex.Matches(htmlPlayer).First().Groups[1].Value;  // Нахождение строки с параметрами
                            var videoUrls = VideoURLRegex.Matches(parameters); //ПОлучение массива ссылок
                            var videoGroups = videoUrls.Count > 1 ? videoUrls[2] : videoUrls.Last(); //480p если есть иначе 240p чтобы сильно не загружать канал
                            videoUrl = videoGroups.Groups[2].Value
                                .Replace(@"\/", "/");
                        }
                        await _telegram.SendTextMessageAsync(_userId, $"Player url: {Environment.NewLine} {fullVideo.Player}", disableNotification: true);
                        await _telegram.SendVideoAsync(_userId,
                                            new InputOnlineFile(
                                                await _httpClient.GetStreamAsync(videoUrl)), caption: fullVideo.Title, disableNotification: true);
                        break;

                    case "Document":
                        var doc = (attach.Instance as VkDocument);
                        if (doc.Ext == "gif")
                        {
                            await _telegram.SendVideoAsync(_userId,
                                    new InputOnlineFile(
                                                await _httpClient.GetStreamAsync(doc.Uri), doc.Title), disableNotification: true);
                        }
                        else
                            await _telegram.SendDocumentAsync(_userId, new InputOnlineFile(
                                                await _httpClient.GetStreamAsync(doc.Uri), doc.Title), disableNotification: true);
                        break;
                    case "Wall":
                        var post = (attach.Instance as Wall);
                        await _telegram.SendTextMessageAsync(_userId, "Sended post" + Environment.NewLine + post.Text, disableNotification: true);
                        await ParseVKMessage(post.Attachments, post.Geo);
                        break;
                }
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
            if (_userId != message.Chat.Id.ToString() && message?.Text != "/gettelegramid")
            {
                await _telegram.SendTextMessageAsync(message.Chat.Id, "Unauthorized user");
                return;
            }

            if (message?.Type == MessageType.Text)
            {
                var splittedMessage = message.Text.Split();
                if (endOfDialog > DateTime.Now && splittedMessage.First().First() != '/')
                {
                    await _vkNet.Messages.SendAsync(new VkNet.Model.RequestParams.MessagesSendParams
                    {
                        UserId = idReceiver,
                        Message = message.Text
                    });
                    endOfDialog = DateTime.Now.AddMinutes(10);
                }
                else if (splittedMessage.First().First() == '/')
                {
                    if (long.TryParse(splittedMessage.First().Substring(1), out idReceiver))
                    {
                        endOfDialog = DateTime.Now.AddMinutes(10);
                        var userOnlineRequest = await _vkNet.Users.GetAsync(new List<long> { idReceiver });

                        await _telegram.SendTextMessageAsync(message.Chat.Id, $"User selected for dialog: {userOnlineRequest.First().FirstName} " +
                            $"{userOnlineRequest.First().LastName} (/{idReceiver})");
                    }
                    else
                    {
                        await MenuAsync(splittedMessage.First(), message.Chat.Id);
                    }
                }

            }
            else if (endOfDialog > DateTime.Now)
            {
                if (message?.Type == MessageType.Photo)
                {
                    var photo = message.Photo.Last();
                    await TelegramToVkPhoto(photo);
                }
            }


            if (endOfDialog <= DateTime.Now)
            {
                await _telegram.SendTextMessageAsync(message.Chat.Id, "Выберите пользователя чтобы начать диалог");
            }
        }

        private async Task MenuAsync(string query, long chatId)
        {
            switch (query)
            {
                case "/gettelegramid":
                    await _telegram.SendTextMessageAsync(chatId, $"Ваш id: {chatId}");
                    break;
                case "/start":
                    await _telegram.SendTextMessageAsync(chatId, "Выберите комманду", replyMarkup: new ReplyKeyboardMarkup(new List<List<KeyboardButton>>
                    {
                            new List<KeyboardButton>
                            {
                                "/friendson", "/friends", "/lastdialogs"
                            }
                        }, true));
                    break;
                case "/friendson":
                    var usersOnlineIdRequest = await _vkNet.Friends.GetOnlineAsync(new VkNet.Model.RequestParams.FriendsGetOnlineParams());
                    var userOnlineRequest = await _vkNet.Users.GetAsync(usersOnlineIdRequest.Online);
                    var usersOnlineMsgs = "Друзья онлайн: " + Environment.NewLine +
                        string.Join(Environment.NewLine, userOnlineRequest.Select(user => $"{user.FirstName} {user.LastName}  (/{user.Id})"));
                    await _telegram.SendTextMessageAsync(chatId, usersOnlineMsgs);
                    break;
                case "/friends":
                    var friends = await _vkNet.Friends.GetAsync(new VkNet.Model.RequestParams.FriendsGetParams()
                    {
                        Count = 50,
                        Fields = ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.LastSeen | ProfileFields.Online | ProfileFields.Sex,
                        Order = FriendsOrder.Hints,
                    });
                    //TODO: Проверить last seen на время


                    var frindsMsg = "Друзья: " + Environment.NewLine +
                        string.Join(Environment.NewLine, friends.Select(fr => UserStr(fr)));
                    await _telegram.SendTextMessageAsync(chatId, frindsMsg);
                    break;
                case "/lastdialogs":
                    var dialogs = await _vkNet.Messages.GetDialogsAsync(new VkNet.Model.RequestParams.MessagesDialogsGetParams
                    {
                        Count = 20
                    });

                    var users = await _vkNet.Users.GetAsync(dialogs.Messages.Select(x => x.UserId.Value), ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.Online | ProfileFields.LastSeen | ProfileFields.Sex);
                    var dialogsMsg = "Последние диалоги: " + Environment.NewLine +
                        string.Join(Environment.NewLine, dialogs.Messages
                            .Join(users, msg => msg.UserId, usr => usr.Id, (msg, usr) => new { Message = msg, User = usr })
                            .Select(msg => $"Пользователь  {UserStr(msg.User)}  {Environment.NewLine} последнее сообщение {(msg.Message.Out.HasValue && msg.Message.Out.Value ? "отправлено" : "получено")} {msg.Message.Date?.ToString("dd.MM.yyyy HH-mm-ss")}"));
                    await _telegram.SendTextMessageAsync(chatId, dialogsMsg);
                    break;
            }
        }

        private string UserStr(VkNet.Model.User fr) =>
            $"{fr.FirstName} {fr.LastName} - /{fr.Id} -  {(fr.Online.Value ? "Онлайн" : $"Был{(fr.Sex == VkNet.Enums.Sex.Female ? "а" : "")} онлайн {fr.LastSeen?.Time?.ToString("dd.MM.yyyy HH-mm-ss")}")}";
        private async Task<long> TelegramToVkPhoto(PhotoSize photo)
        {
            try
            {
                var uplUrl = await _vkNet.Photo.GetMessagesUploadServerAsync(0);

                var file = await _telegram.GetFileAsync(photo.FileId);
                var fileStream = await _telegram.DownloadFileAsync(file.FilePath) as MemoryStream;

                var requestContent = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(fileStream.ToArray());
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                requestContent.Add(imageContent, "photo", "image.jpg");
                var uploadResponse = await _httpClient.PostAsync(uplUrl.UploadUrl, requestContent);

                var uploadRezult = await _vkNet.Photo.SaveMessagesPhotoAsync(await uploadResponse.Content.ReadAsStringAsync());
                return await _vkNet.Messages.SendAsync(new VkNet.Model.RequestParams.MessagesSendParams
                {
                    UserId = idReceiver,
                    Attachments = uploadRezult
                });
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                throw;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _telegram.StopReceiving();
            _vkPool.Stop();
            if (disposing)
            {
                if (_vk != null)
                    _vk.Dispose();

                if (_vkNet != null)
                    _vkNet.Dispose();

                if (_httpClient != null)
                    _httpClient.Dispose();
            }
        }

        ~TelegramSender()
        {
            Dispose(false);
        }
    }
}
