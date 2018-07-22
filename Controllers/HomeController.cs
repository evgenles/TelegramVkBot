using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TelegramVkBot.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return Ok("Bot online");
        }
    }
}