using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace PuppeteerSharpForPortia
{
    public class PuppeteerWrapper : IDisposable
    {
        public Browser browser;
        public ViewPortOptions viewPortOptions;
        public List<string> xpathsToWaitFor;
        public LaunchOptions launchOptions;
        public PuppeteerWrapper()
        {
        }
        private async Task<PuppeteerWrapper> InitializeAsync()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var args = new string[] {
                "--no-sandbox",
                "--disable-plugins",
                "--disable-sync",
                "--disable-gpu",
                "--disable-speech-api",
                "--disable-remote-fonts",
                "--disable-shared-workers",
                "--disable-webgl",
                "--no-experiments",
                "--no-first-run",
                "--no-default-browser-check",
                "--no-wifi",
                "--no-pings",
                "--no-service-autorun",
                "--disable-databases",
                "--disable-default-apps",
                "--disable-demo-mode",
                "--disable-notifications",
                "--disable-permissions-api",
                "--disable-background-networking",
                "--disable-3d-apis",
                "--disable-bundled-ppapi-flash"
            };
            launchOptions = new LaunchOptions { Headless = false, Args = args, IgnoreHTTPSErrors = true };
            return this;
        }
        public async Task LaunchAsync() {
            browser = await Puppeteer.LaunchAsync(launchOptions);
        }
        public static Task<PuppeteerWrapper> CreateAsync()
        {
            return CreateAsync(new List<string>());
        }
        public static Task<PuppeteerWrapper> CreateAsync(List<string> xpathsToWaitFor, int width = 1920, int height = 1080)
        {
            PuppeteerWrapper ret = new PuppeteerWrapper
            {
                xpathsToWaitFor = xpathsToWaitFor,
                viewPortOptions = new ViewPortOptions() { Width = width, Height = height }
            };
            return ret.InitializeAsync();
        }
        public async Task<HtmlContent> GetHtmlContentAsync(Uri url)
        {
            string html = "";
            using (Page page = await browser.NewPageAsync())
            {
                await page.SetViewportAsync(viewPortOptions);
                await page.SetRequestInterceptionAsync(true); // Intercepting the page seems to finish it prematurely
                page.Request += async (sender, e) =>
                {
                    try
                    {
                        switch (e.Request.ResourceType)
                        {
                            case ResourceType.Media:
                            case ResourceType.Font:
                            case ResourceType.TextTrack:
                            case ResourceType.Unknown:
                            case ResourceType.Image:
                            case ResourceType.Ping:
                            case ResourceType.Fetch:
                            case ResourceType.EventSource:
                            case ResourceType.Manifest:
                            case ResourceType.Other:
                            case ResourceType.WebSocket:
                                await e.Request.AbortAsync();
                                break;
                            case ResourceType.Document:
                            case ResourceType.StyleSheet:
                            case ResourceType.Script:
                            case ResourceType.Xhr:
                            default:
                                await e.Request.ContinueAsync();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error => {ex.Message}");
                        await e.Request.ContinueAsync();
                    }
                };
                await page.GoToAsync(url.ToString(), WaitUntilNavigation.Networkidle0);

                int oldHeight = 0;
                foreach (var xpath in xpathsToWaitFor)
                {
                    bool xpathSelectorAddedToDOM = false;
                    int timeoutCount = 0;
                    while (timeoutCount <= 3 && xpathSelectorAddedToDOM == false)
                    {
                        try
                        {
                            await page.WaitForXPathAsync(xpath, new WaitForSelectorOptions { Timeout = 500 });
                            xpathSelectorAddedToDOM = true;
                        }
                        catch (Exception ex)
                        {
                            int newHeight = (int)await page.EvaluateExpressionAsync("document.body.scrollHeight");
                            if (oldHeight != newHeight)
                            {
                                await page.EvaluateExpressionAsync("window.scrollBy({top:" + newHeight + ",behavior:'smooth'})");
                                oldHeight = newHeight;
                                timeoutCount++;
                            }
                            else
                            {
                                break;
                            }

                        }
                    }

                }


                html = await page.GetContentAsync();
            }
            return new HtmlContent(url, html);
        }
        private void KillPuppeteerIfRunning() //Win32 only
        {
            var puppeteerExecutablePath = new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision).Replace(@"\", @"\\");
            List<int> processIdsToKill = new List<int>();
            string wmiQueryString = @"SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE ExecutablePath LIKE '" + puppeteerExecutablePath + "'";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        if (item != null)
                        {
                            var processId = Convert.ToInt32(item["ProcessId"]);
                            processIdsToKill.Add(processId);
                        }
                    }
                }
            }
            List<Process> processesToKill = Process.GetProcesses().Where(p => processIdsToKill.Where(x => x == p.Id).Any()).ToList();
            if (processesToKill.Count > 0)// Is running
            {
                processesToKill.ForEach((x) =>
                {
                    x.Kill();
                });
            }
        }

        public void Dispose()
        {
            browser.Dispose();
        }
    }
}
