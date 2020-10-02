using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AkkaWebcrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(JSchema.Parse(Common.Models.RequestSchema.Json));
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}