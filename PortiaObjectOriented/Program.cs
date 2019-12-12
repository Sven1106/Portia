using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using PortiaObjectOriented.Dto;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Http;
using PuppeteerSharp;
using System.Threading.Tasks.Dataflow;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace PortiaObjectOriented
{
    //class Program
    //{
    //    static void Main(string[] args)
    //    {
    //        List<string> blacklistedWords = new List<string>(new string[] {});

    //        Recipe recipe = new Recipe();
    //        Type type = recipe.GetType();
    //        var result = Webcrawler.StartCrawlerAsync("https://www.arla.dk/opskrifter/", blacklistedWords, type).Result; //https://www.arla.dk/opskrifter/
    //        var list = result.ToList();
    //        var responseJson = JsonConvert.SerializeObject(list, Formatting.Indented);
    //        System.IO.File.WriteAllText("response.json", responseJson);
    //    }
    //}

    class UrlHtmlPair
    {
        public Uri Url { get; set; }
        public string Html { get; set; }
        public UrlHtmlPair(Uri url, string html)
        {
            Url = url;
            Html = html;
        }

    }

    class Program
    {
        private static Uri rootUrl;
        private static BlockingCollection<Uri> legalUrls = new BlockingCollection<Uri>();
        private static ConcurrentDictionary<string, JArray> tasks = new ConcurrentDictionary<string, JArray>();
        private static IList<string> disallowedStrings = new List<string>() { };
        static async Task Main(string[] args)
        {
            await RunAsync();
        }
        static async Task RunAsync()
        {
            rootUrl = new Uri("https://www.automobile.tn/fr");
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
            });

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            var htmlDownloaderOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3, //enforce fairness, after handling n messages the block's task will be re-schedule.
                MaxDegreeOfParallelism = 2,// by default Tpl dataflow assign a single task per block
                BoundedCapacity = 8 // the size of the block input buffer
            };
            var parserOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3,
                MaxDegreeOfParallelism = 8
            };

            Predicate<Uri> urlFilter = url =>
            {
                if (IsLegalUrl(url) == false)
                {
                    return false;
                }
                legalUrls.TryAdd(url);
                return true;
            };

            TransformBlock<Uri, UrlHtmlPair> htmlDownloader = new TransformBlock<Uri, UrlHtmlPair>(
                async url => await GetUrlHtmlPairAsync(url, browser), htmlDownloaderOptions);
            TransformManyBlock<UrlHtmlPair, Uri> urlParser = new TransformManyBlock<UrlHtmlPair, Uri>(
                urlHtmlPair => ParseUrls(urlHtmlPair.Html), parserOptions);
            TransformBlock<UrlHtmlPair, string> objParser = new TransformBlock<UrlHtmlPair, string>(
                urlHtmlPair =>
                {
                    var value = ObjParser(urlHtmlPair);
                    return value;
                }, parserOptions);

            var writer = new ActionBlock<string>(text =>
            {

                Console.WriteLine(text);
            });




            BroadcastBlock<UrlHtmlPair> htmlContentBroadcaster = new BroadcastBlock<UrlHtmlPair>(urlHtml => urlHtml);
            BroadcastBlock<Uri> urlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                Console.WriteLine("urlBroadcaster cloned: {0}", url);
                return url;
            });

            urlBroadcaster.LinkTo(htmlDownloader, linkOptions, urlFilter);
            htmlDownloader.LinkTo(htmlContentBroadcaster, linkOptions);
            htmlContentBroadcaster.LinkTo(objParser, linkOptions);
            htmlContentBroadcaster.LinkTo(urlParser, linkOptions);
            objParser.LinkTo(writer, linkOptions);
            urlParser.LinkTo(urlBroadcaster, linkOptions);


            Console.WriteLine("Starting");
            await urlBroadcaster.SendAsync(rootUrl);
            await Task.Delay(5000);
            await Task.WhenAll(htmlDownloader.Completion, urlParser.Completion, objParser.Completion);
            await browser.CloseAsync();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        static async Task<UrlHtmlPair> GetUrlHtmlPairAsync(Uri url, Browser browser)
        {
            string html = "";
            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(url.ToString());
                html = await page.GetContentAsync();
            }
            return new UrlHtmlPair(url, html);
        }

        static List<Uri> ParseUrls(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var aTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            List<Uri> newUrls = new List<Uri>();
            if (aTags != null)
            {
                foreach (var aTag in aTags)
                {
                    string hrefValue = aTag.Attributes["href"].Value;
                    Uri url = new Uri(hrefValue, UriKind.RelativeOrAbsolute);
                    url = new Uri(rootUrl, url);
                    newUrls.Add(url);
                }
            }
            return newUrls;
        }

        static string ObjParser(UrlHtmlPair urlHtmlPair)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(urlHtmlPair.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;


            return "11";
        }



        private static bool IsLegalUrl(Uri url)
        {
            if (url.OriginalString.Contains(rootUrl.OriginalString) == false ||
                legalUrls.Contains(url) == true ||
                ContainsAnyWords(url, disallowedStrings) == true)
            {
                return false;
            }
            return true;
        }

        private static bool ContainsAnyWords(Uri url, IList<string> words)
        {
            foreach (var word in words)
            {
                if (url.ToString().Contains(word))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public partial class MainWindow
    {

        TransformManyBlock<string, string> dirToFilesBlock;
        ActionBlock<string> fileActionBlock;
        ObservableCollection<string> files;
        CancellationTokenSource cts;
        CancellationToken ct;
        public MainWindow()
        {
            InitializeComponent();

            files = new ObservableCollection<string>();

            lst.DataContext = files;

            cts = new CancellationTokenSource();
            ct = cts.Token;
        }

        private async Task Start(string path)
        {
            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            dirToFilesBlock = new TransformManyBlock<string, string>((Func<string, IEnumerable<string>>)(GetFileSystemItems), new ExecutionDataflowBlockOptions() { CancellationToken = ct });
            fileActionBlock = new ActionBlock<string>((Action<string>)ProcessFile, new ExecutionDataflowBlockOptions() { CancellationToken = ct, TaskScheduler = uiScheduler });

            // Order of LinkTo's important here!
            dirToFilesBlock.LinkTo(dirToFilesBlock, new DataflowLinkOptions() { PropagateCompletion = true }, IsDirectory);
            dirToFilesBlock.LinkTo(fileActionBlock, new DataflowLinkOptions() { PropagateCompletion = true }, IsRequiredDocType);

            // Kick off the recursion.
            dirToFilesBlock.Post(path);

            await ProcessingIsComplete();
            dirToFilesBlock.Complete();
            await Task.WhenAll(dirToFilesBlock.Completion, fileActionBlock.Completion);
        }

        private async Task ProcessingIsComplete()
        {
            while (!ct.IsCancellationRequested && DirectoryToFilesBlockIsIdle())
            {
                await Task.Delay(500);
            }
        }

        private bool DirectoryToFilesBlockIsIdle()
        {
            return dirToFilesBlock.InputCount == 0 &&
                dirToFilesBlock.OutputCount == 0 &&
                directoriesBeingProcessed <= 0;
        }

        private bool IsDirectory(string path)
        {
            return Directory.Exists(path);
        }


        private bool IsRequiredDocType(string fileName)
        {
            return System.IO.Path.GetExtension(fileName) == ".xlsx";
        }

        private int directoriesBeingProcessed = 0;

        private IEnumerable<string> GetFilesInDirectory(string path)
        {
            Interlocked.Increment(ref directoriesBeingProcessed);
            // Check for cancellation with each new dir.
            ct.ThrowIfCancellationRequested();

            // Check in case of Dir access problems
            try
            {
                return Directory.EnumerateFileSystemEntries(path);
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
            finally
            {
                Interlocked.Decrement(ref directoriesBeingProcessed);
            }
        }

        private IEnumerable<string> GetFileSystemItems(string dir)
        {
            return GetFilesInDirectory(dir);
        }

        private void ProcessFile(string fileName)
        {
            ct.ThrowIfCancellationRequested();

            files.Add(fileName);
        }
    }
}