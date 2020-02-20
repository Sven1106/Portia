using Newtonsoft.Json.Linq;
using PortiaJsonOriented.Core.Models;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core
{
    public class PuppeteerWrapper
    {
        private Browser browser;
        public PuppeteerWrapper()
        {

        }
        ~PuppeteerWrapper()
        {
            browser.Dispose();
        }
        private async Task<PuppeteerWrapper> InitializeAsync()
        {
            KillPuppeteerIfRunning();
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
            var launchOptions = new LaunchOptions { Headless = false, Args = args, IgnoreHTTPSErrors = true };
            browser = await Puppeteer.LaunchAsync(launchOptions);
            return this;
        }
        public static Task<PuppeteerWrapper> CreateAsync()
        {
            PuppeteerWrapper ret = new PuppeteerWrapper();
            return ret.InitializeAsync();
        }
        public async Task<HtmlContent> GetHtmlContentAsync(Uri url, List<string> xpathsToWaitFor)
        {
            string html;
            using (Page page = await browser.NewPageAsync())
            {
                await page.SetViewportAsync(new ViewPortOptions() { Width = 1920, Height = 1080 });
                await page.SetRequestInterceptionAsync(true); //Intercepting the page seems to finish it prematurely
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
                if (xpathsToWaitFor != null)
                {
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
                            catch (Exception)
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
                }

                html = await page.GetContentAsync();
            }
            return new HtmlContent(url, html);
        }
        private void KillPuppeteerIfRunning()
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
    }
}
