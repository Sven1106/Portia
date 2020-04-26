using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExampleApp.Models;
using Newtonsoft.Json.Schema;
using System.Reflection;
using System.IO;

namespace ExampleApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _currentDirectory;
        private readonly JSchema _createRecipeSchema;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            _currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            _createRecipeSchema = JSchema.Parse(System.IO.File.ReadAllText(Path.Combine(_currentDirectory, "requestSchema.json")));
        }

        public IActionResult Index()
        {
            return View(_createRecipeSchema);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
