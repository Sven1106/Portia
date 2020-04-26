using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using PortiaJsonOriented.Core.DTO;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Schema;
using System.Collections.Generic;
using System;
using PortiaJsonOriented.Core.Models;
using PuppeteerSharpForPortia;
using System.Diagnostics;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace ExampleAPI
{
    public class PortiaHub : Hub
    {
        private static readonly Dictionary<Guid, ProjectCrawler> CrawlerProjectByIds = new Dictionary<Guid, ProjectCrawler>();
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

            if (CrawlerProjectByIds.ContainsKey(request.Id))
            {
                throw new HubException("Project already registered");
            }

            ProjectCrawler newCrawlerProject = new ProjectCrawler(request);

            if (CrawlerProjectByIds.TryAdd(newCrawlerProject.PortiaRequest.Id, newCrawlerProject) == false)
            {
                throw new HubException("Project registration failed");
            }
            await Clients.All.SendAsync("RegisterProjectReply", "Project registered");
        }


        public async Task StartProjectMessage(Guid id)
        {
            CrawlerProjectByIds.TryGetValue(id, out ProjectCrawler crawlerProject);
            if (crawlerProject == null)
            {
                throw new HubException("Project not registered");
            }

            await crawlerProject.StartAsync();
            await Clients.All.SendAsync("StartProjectReply", "Project started");
        }

        public async Task StopProjectMessage(Guid id)
        {
            CrawlerProjectByIds.TryGetValue(id, out ProjectCrawler crawlerProject);
            if (crawlerProject == null)
            {
                throw new HubException("Project not registered");
            }

            await crawlerProject.StopAsync();
            await Clients.All.SendAsync("StopProjectReply", "Project stopped");
        }

    }
    public class ProjectCrawler
    {
        private ConcurrentDictionary<string, JArray> dataByJobName = new ConcurrentDictionary<string, JArray>();
        private List<string> xpathsToWaitFor = new List<string>();
        public PortiaRequest PortiaRequest { get; set; }
        public PuppeteerWrapper PuppeteerWrapper { get; set; }
        public ProjectCrawler(PortiaRequest portiaRequest)
        {
            PortiaRequest = portiaRequest;
            PortiaRequest.Jobs.ForEach((job) =>
            {
                dataByJobName.TryAdd(job.Name, new JArray());// Initialize a new Key-value Pair for each Job.
                job.Nodes.ForEach(jobNode => xpathsToWaitFor.AddRange(GetAllXpath(jobNode))); // Creates the list of xpathsToWaitFor.
            });
        }
        public void Start()
        {
            Task.Run(async () => await StartAsync());
        }
        public async Task StartAsync()
        {
            PuppeteerWrapper = await PuppeteerWrapper.CreateAsync(xpathsToWaitFor);
            await PuppeteerWrapper.GetHtmlContentAsync(new Uri("https://www.arla.dk/"));

            //if (PuppeteerWrapper == null)
            //{

            //    await InitializeAsync();
            //    await PuppeteerWrapper.GetHtmlContentAsync(new Uri("https://www.arla.dk/"));
            //}
            //else
            //{
            //    PuppeteerWrapper = null;
            //}

        }
        public void Processor()
        {

        }
        public async Task StopAsync()
        {
            await Task.Delay(2000);
            PuppeteerWrapper.Dispose();
        }


        private static List<string> GetAllXpath(NodeAttribute node)
        {
            List<string> xpaths = new List<string>();
            CreateAbsoluteXpathsRecursively(ref xpaths, node);
            return xpaths;

        }
        private static void CreateAbsoluteXpathsRecursively(ref List<string> xpaths, NodeAttribute node, string currentXpath = "")
        {
            string nodeXpath = node.Xpath;
            if (currentXpath != "")
            {
                nodeXpath = nodeXpath.Replace("./", "/"); //removes relative prefix and prepares xpath for absolute path
            }
            currentXpath += nodeXpath;
            if (node.Attributes == null)
            {
                xpaths.Add(currentXpath);
            }
            else
            {
                foreach (var attribute in node.Attributes)
                {
                    CreateAbsoluteXpathsRecursively(ref xpaths, attribute, currentXpath);
                }
            }
        }
    }
    public class Test
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; }
        public Uri Domain { get; set; }
        public List<Uri> StartUrls { get; set; }
        public bool IsFixedListOfUrls { get; set; }
        public List<Job> Jobs { get; set; }
    }
}