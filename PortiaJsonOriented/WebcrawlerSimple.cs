using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PortiaJsonOriented.DTO;
using PuppeteerSharpForPortia;

namespace PortiaJsonOriented
{
    public class WebcrawlerSimple : PortiaCore
    {
        public async Task<PortiaResponse> StartAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            portiaRequest = request;
            // TODO Add /robots.txt handling eg. Sitemap, Disallow
            #region preparation
            int maxConcurrentDownload = 3;
            List<string> xpathsToWaitFor = new List<string>();
            portiaRequest.Jobs.ForEach((job) =>
            {
                dataByJobName.TryAdd(job.Name, new JArray());// Initialize a new Key-value Pair for each Job.
                job.Nodes.ForEach(jobNode => xpathsToWaitFor.AddRange(GetAllXpath(jobNode))); // Creates the list of xpathsToWaitFor.
            });
            // TODO create SignalR connection and return it to client.
            #endregion
            puppeteerWrapper = await PuppeteerWrapper.CreateAsync(xpathsToWaitFor, portiaRequest.XpathForLoadMoreButton);
            Task render = RenderCrawling();

            var runningTasks = new List<Task>();
            bool isFixedListOfUrls = portiaRequest.IsFixedListOfUrls;
            if (isFixedListOfUrls)
            {
                FilterAndAddUrls(portiaRequest.StartUrls, ref urlsToFixedList);
                while (urlsToFixedList.Any() || runningTasks.Any())
                {
                    while (urlsToFixedList.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (urlsToFixedList.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseUrlsAndObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
                while (currentQueuedUrls.Any() || runningTasks.Any())
                {
                    while (currentQueuedUrls.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (currentQueuedUrls.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
            }
            else
            {
                FilterAndAddUrls(portiaRequest.StartUrls, ref currentQueuedUrls);
                while (currentQueuedUrls.Any() || runningTasks.Any())
                {
                    while (currentQueuedUrls.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (currentQueuedUrls.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseUrlsAndObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
            }
            Console.WriteLine("Adding was completed!");
            Console.ReadKey();
            PortiaResponse response = new PortiaResponse
            {
                ProjectName = request.ProjectName,
                Domain = request.Domain,
                Jobs = dataByJobName
            };
            return response;
        }
        
        
        private async Task RenderCrawling(int fps = 30)
        {
            while (true)
            {
                string format = $"\ncurrent urls in queue              = {currentQueuedUrls.Count}    " +
                                $"\nurls visited                       = {logOfAllVisitedUrls.Count}    " +
                                $"\nlog of all queued urls             = {logOfAllQueuedUrls.Count}    " +
                                $"\n" +
                                $"\nobjects parsed for:";
                foreach (var item in dataByJobName)
                {
                    format +=   $"\n    '{item.Key}' = {item.Value.Count}    ";
                }
                ConsoleColor color = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = color;
                Console.WriteLine(format);
                Console.ResetColor();
                await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds / fps);
            }
        }
    }
}
