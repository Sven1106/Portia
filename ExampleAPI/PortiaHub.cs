using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Schema;
using System.Collections.Generic;
using System;
using PortiaJsonOriented.Models;
using PuppeteerSharpForPortia;
using Newtonsoft.Json.Linq;
using System.Linq;
using PortiaJsonOriented;
using PortiaJsonOriented.DTO;
using PuppeteerSharp;
using System.Management;
using System.Diagnostics;

namespace ExampleAPI
{
    public class PortiaHub : Hub
    {
        private static readonly Dictionary<Guid, ProjectCrawler> ProjectCrawlerById = new Dictionary<Guid, ProjectCrawler>();
        public PortiaHub()
        {

        }
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SignalR Users");
            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SignalR Users");
            await base.OnDisconnectedAsync(exception);
        }
        public async Task RegisterProjectMessage(string json)
        {
            JSchemaValidatingReader jSchemaReader = new JSchemaValidatingReader(new JsonTextReader(new StringReader(json)))
            {
                Schema = JSchema.Parse(File.ReadAllText("requestSchema.json"))
            };
            IList<string> errorMessages = new List<string>();
            jSchemaReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);
            JsonSerializer serializer = new JsonSerializer();
            PortiaRequest request = serializer.Deserialize<PortiaRequest>(jSchemaReader);
            if (errorMessages.Count > 0)
            {
                throw new HubException("The json provided was invalid");
            }

            if (ProjectCrawlerById.ContainsKey(request.Id))
            {
                throw new HubException("Project already registered");
            }

            ProjectCrawler newProjectCrawler = new ProjectCrawler(request);


            if (ProjectCrawlerById.TryAdd(newProjectCrawler.portiaRequest.Id, newProjectCrawler) == false)
            {
                throw new HubException("Project registration failed");
            }
            await Clients.All.SendAsync("RegisterProjectReply", newProjectCrawler.portiaRequest);
        }

        public async IAsyncEnumerable<object> GetProgress(Guid id)
        {
            ProjectCrawlerById.TryGetValue(id, out ProjectCrawler projectCrawler);
            if (projectCrawler == null)
            {
                throw new HubException("Project not registered");
            }

            do
            {
                yield return new
                {
                    VisitedUrlsCount = projectCrawler.logOfAllVisitedUrls.Count,
                    TotalQueuedUrlsCount = projectCrawler.urlsToFixedList.Count + projectCrawler.logOfAllQueuedUrls.Count,
                    IsRunning = projectCrawler.IsCrawling
                };
                await Task.Delay(1000);
            }
            while (projectCrawler.IsCrawling);
        }

        public async Task ToggleProjectMessage(Guid id)
        {
            ProjectCrawlerById.TryGetValue(id, out ProjectCrawler crawlerProject);
            if (crawlerProject == null)
            {
                throw new HubException("Project not registered");
            }

            try
            {
                if (crawlerProject.Crawler == null)
                {
                    crawlerProject.Start();
                }
                else if(crawlerProject.Crawler.Status == TaskStatus.WaitingForActivation)
                {
                    await crawlerProject.StopAsync();
                }
            }
            catch (Exception ex)
            {
                throw new HubException(ex.ToString());
            }
        }

    }
    public class ProjectCrawler : PortiaCore
    {
        public Task Crawler { get; set; } = null;
        public bool IsCrawling { get; set; } = false;
        public bool IsTerminated { get; set; } = false;
        public List<string> xpathsToWaitFor = new List<string>();
        public int maxConcurrentDownload = 3; // Should be in the puppeteerWrapper
        public ProjectCrawler(PortiaRequest request)
        {
            portiaRequest = request;
            portiaRequest.Jobs.ForEach((job) =>
            {
                dataByJobName.TryAdd(job.Name, new JArray());// Initialize a new Key-value Pair for each Job.
                job.Nodes.ForEach(jobNode => xpathsToWaitFor.AddRange(GetAllXpath(jobNode))); // Creates the list of xpathsToWaitFor.
            });

            // Configure which crawler to use:
            if (portiaRequest.IsFixedListOfUrls) // static list
            {
                FilterAndAddUrls(portiaRequest.StartUrls, ref urlsToFixedList);
            }
            else // traversable
            {
                FilterAndAddUrls(portiaRequest.StartUrls, ref currentQueuedUrls);
            }

        }
        public async Task CrawlerForFixedListOfUrls()
        {
            IsCrawling = true;
            puppeteerWrapper = await PuppeteerWrapper.CreateAsync(xpathsToWaitFor, "");
            List<Task> runningTasks = new List<Task>();
            while (urlsToFixedList.Any() && IsTerminated == false || runningTasks.Any())
            {
                if (runningTasks.Count > 0)
                {
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
                while (urlsToFixedList.Any() && IsTerminated == false && runningTasks.Count < maxConcurrentDownload)
                {
                    if (urlsToFixedList.TryTake(out Uri url))
                    {
                        runningTasks.Add(ParseUrlsAndObjectsFromUrl(url));
                    }
                }
                await Task.Delay(1000);
            }
            while (currentQueuedUrls.Any() && IsTerminated == false || runningTasks.Any())
            {
                if (runningTasks.Count > 0)
                {
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
                while (currentQueuedUrls.Any() && IsTerminated == false && runningTasks.Count < maxConcurrentDownload)
                {
                    if (currentQueuedUrls.TryTake(out Uri url))
                    {
                        runningTasks.Add(ParseObjectsFromUrl(url));
                    }
                }
                await Task.Delay(1000);
            }
            IsCrawling = false;
            await puppeteerWrapper.DisposeAsync();
        }
        public async Task CrawlerTraversableListOfUrls()
        {
            IsCrawling = true;
            puppeteerWrapper = await PuppeteerWrapper.CreateAsync(xpathsToWaitFor,"");
            List<Task> runningTasks = new List<Task>();
            while (currentQueuedUrls.Any() && IsTerminated == false || runningTasks.Any())
            {
                if (runningTasks.Count > 0)
                {
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
                while (currentQueuedUrls.Any() && IsTerminated == false && runningTasks.Count < maxConcurrentDownload)
                {
                    if (currentQueuedUrls.TryTake(out Uri url))
                    {
                        runningTasks.Add(ParseUrlsAndObjectsFromUrl(url));
                    }
                    await Task.Delay(10);
                }
                await Task.Delay(10);
            }
            IsCrawling = false;
            await puppeteerWrapper.DisposeAsync();
        }

        public void Start()
        {
            if (Crawler == null)
            {
                IsTerminated = false;
                if (portiaRequest.IsFixedListOfUrls) // static list
                {
                    Crawler = CrawlerForFixedListOfUrls();
                }
                else // traversable
                {
                    Crawler = CrawlerTraversableListOfUrls();
                }
            }
            else if (Crawler.Status == TaskStatus.WaitingForActivation)
            {
                throw new Exception("Crawler task is already started");
            }
            else if (Crawler.Status == TaskStatus.RanToCompletion)
            {
                throw new Exception("Crawler task is Completed");
            }
        }

        public async Task StopAsync()
        {
            if (Crawler == null)
            {
                throw new Exception("No Crawler task exists");
            }
            else if (Crawler.Status == TaskStatus.WaitingForActivation)
            {
                IsTerminated = true;
                await Crawler.ContinueWith((x) =>
                {
                    Crawler.Dispose();
                    Crawler = null;
                });
            }
            else if (Crawler.Status == TaskStatus.RanToCompletion)
            {
                throw new Exception("Crawler task is already Completed");
            }

        }

    }
}