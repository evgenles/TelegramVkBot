# TelegramVkBot
Бот преднанчен для общения с пользователями (Vk.com) через telegram

## Настройка
Для работы приложения необходимо получить 2 токена и ваш id в telegram:
1. Токен Telegram бота
  Для получения данного токена необходимо создать бота в телеграмм. Для этого обратитесь к боту [BotFather](https://telegram.me/botfather), попросите создать нового бота коммандой `(/newbot)` и введите необходимые данные
2. Токен VK 
  Для получения токена VK необходимо [создать Standalon приложение] (https://vk.com/editapp?act=create), перейти в настройки, скопировать ID приложения и вставить в ссылку вместо **{ID}**
  ```
  https://oauth.vk.com/authorize?client_id={ID}&display=page&redirect_uri=https://oauth.vk.com/blank.html&response_type=token&v=5.65&scope=offline,messages,video,photos,docs,friends
  ```
  В адресной строке ответа находится access_token, это и есть нужный токен

3. Ваш Id в telegram 
  Можно получить с помощью [User Info Bot](https://telegram.me/userinfobot) или запустив данного бота используя комманду `(/gettelegramid)`


Полученные данные необходимо записать в appsettings.json в соответствующие поля

## Changelog
- 21.07.2018 Создан бот. Реализован обмен сообщениями и просмотр друзей онлайн
- 22.07.2018 Реализована отправлка фотографий в сообщениях. Добавлена комманда просмотра своего id

