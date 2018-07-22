# TelegramVkBot
Бот преднанчен для общения с пользователями [vk.com](vk.com) через telegram

## Зависимости
Бот написан на [.Net Core 2.1](https://www.microsoft.com/net/download/dotnet-core/2.1)

Бот использует .Nuget пакеты:
- [Telegram.Bot](https://github.com/TelegramBots/telegram.bot) для работы с Telegram
- [VkNet](https://github.com/vknet/vk) для работы с Vk Api
- [VkLibrary](https://github.com/worldbeater/VkLibrary) для работы с Vk.LongPool Api
- [NLog](https://github.com/NLog/NLog) для логирования
- [CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages/) для поддержки Encoding(1251)

## Настройка
Для работы приложения необходимо получить 2 токена и ваш id в telegram:
1. Токен Telegram бота (Tokens:Telegram)

Для получения данного токена необходимо создать бота в телеграмм. Для этого обратитесь к боту [BotFather](https://telegram.me/botfather), попросите создать нового бота коммандой `(/newbot)` и введите необходимые данные

2. Токен VK (Tokens:Vk)

  Для получения токена VK необходимо [создать Standalon приложение] (https://vk.com/editapp?act=create), перейти в настройки, скопировать ID приложения и вставить в ссылку вместо **{ID}**
  ```
  https://oauth.vk.com/authorize?client_id={ID}&display=page&redirect_uri=https://oauth.vk.com/blank.html&response_type=token&v=5.65&scope=offline,messages,video,photos,docs,friends
  ```
  В адресной строке ответа находится access_token, это и есть нужный токен

3. Ваш Id в telegram (Tokens:TelegramId)

  Можно получить с помощью [User Info Bot](https://telegram.me/userinfobot) или запустив данного бота используя комманду `(/gettelegramid)`


Полученные данные необходимо записать в appsettings.json в соответствующие поля

## Работа с ботом

В данный момент доступны такие команды: 
- `/start` - получение доступных комманд
- `/friendson` - получение друзей онлайн
- `/friends` - получение 50 друзей (в порядке аналогичном вк) 
- `/gettelegramid` - получение вашего id в телеграмме
- `/{id_vk_получателя}` - выбор пользователя вк для диалога

## Разработка и улучшение проекта
Информация про разрабатываемые в данный момент функции находится в [Projects](https://github.com/evgenles/TelegramVkBot/projects/1)

Если вы нашли ошибку или хотите предложить новый функционал - пишите в [Issues](https://github.com/evgenles/TelegramVkBot/issues/new)

## Changelog
- 21.07.2018 Создан бот. Реализован обмен сообщениями и просмотр друзей онлайн
- 22.07.2018 Реализована отправлка фотографий в сообщениях. Добавлена комманда просмотра своего id. Добавлена команда `/friends`

