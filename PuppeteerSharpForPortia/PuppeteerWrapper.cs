using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace PuppeteerSharpForPortia
{
    public class PuppeteerWrapper : IAsyncDisposable
    {
        public Browser browser = null;
        public ViewPortOptions viewPortOptions;
        public List<string> xpathsToWaitFor;
        public string xpathForLoadMoreButton;
        public LaunchOptions launchOptions;
        public int timeout = 30000;
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
            launchOptions = new LaunchOptions { Headless = true, Args = args, IgnoreHTTPSErrors = true };
            KillPuppeteerIfRunning();
            await LaunchAsync();
            return this;
        }
        private async Task LaunchAsync()
        {
            browser = await Puppeteer.LaunchAsync(launchOptions);
        }

        public static Task<PuppeteerWrapper> CreateAsync(List<string> xpathsToWaitFor, string xpathForLoadMoreButton, int width = 1920, int height = 1080)
        {
            PuppeteerWrapper ret = new PuppeteerWrapper
            {
                xpathsToWaitFor = xpathsToWaitFor,
                xpathForLoadMoreButton = xpathForLoadMoreButton,
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
                            case ResourceType.StyleSheet:
                            case ResourceType.Xhr:
                            case ResourceType.Script:
                            case ResourceType.Document:
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


                bool loadMoreVisible = await IsElementVisible(page, xpathForLoadMoreButton);
                while (loadMoreVisible)
                {
                    try
                    {
                        var element = await page.WaitForXPathAsync(xpathForLoadMoreButton,
                            new WaitForSelectorOptions { Visible = true, Timeout = 30000 }
                        );
                        await element.ClickAsync(new ClickOptions() {Delay = 1000 });
                    }
                    catch (PuppeteerException ex) when (ex is WaitTaskTimeoutException)
                    {
                        loadMoreVisible = false;
                    }
                }

                foreach (var xpath in xpathsToWaitFor)
                {
                    bool elementVisible = false;
                    elementVisible = await IsElementVisible(page, xpath);
                    if (elementVisible == false) // to break out of the loop as soon as possible when not found.
                    {
                        break;
                    }

                }

                html = await page.GetContentAsync();
            }
            return new HtmlContent(url, html);
        }

        private async Task<bool> IsElementVisible(Page page, string xpath)
        {
            int tries = 0;
            int oldHeight = (int)await page.EvaluateExpressionAsync("document.body.scrollHeight");
            while (tries <= 3)
            {
                try
                {
                    tries++;
                    await page.WaitForXPathAsync(xpath, new WaitForSelectorOptions { Visible = true, Timeout = 2500 });
                    return true;
                }
                catch (Exception)
                {
                    int newHeight = (int)await page.EvaluateExpressionAsync("document.body.scrollHeight");
                    await page.EvaluateExpressionAsync("window.scrollBy({top:" + newHeight + ",behavior:'smooth'})"); // To trigger autoload of new content.
                    if (oldHeight == newHeight)
                    {
                        break;
                    }
                    oldHeight = newHeight;
                }
            }
            return false;
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

        public ValueTask DisposeAsync()
        {
            return ((IAsyncDisposable)browser).DisposeAsync();
        }
    }
}
