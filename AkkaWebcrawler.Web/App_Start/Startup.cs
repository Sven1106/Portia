using Microsoft.Owin;
using Owin;
using AkkaWebcrawler.Web.App_Start;
[assembly: OwinStartup(typeof(Startup))]
namespace AkkaWebcrawler.Web.App_Start
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}