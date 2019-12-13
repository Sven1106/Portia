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
    public class Job
    {
        public int Ticker { get; set; }

        public Type Type { get; }

        public Job(Type type)
        {
            Type = type;
        }

        public Task Prepare()
        {
            Console.WriteLine("Preparing");
            Ticker = 0;
            return Task.CompletedTask;
        }

        public Task Tick()
        {
            Console.WriteLine("Ticking");
            Ticker++;
            return Task.CompletedTask;
        }

        public bool IsCommitable()
        {
            Console.WriteLine("Trying to commit");
            return IsFinished() || (Ticker != 0 && Ticker % 100000 == 0);
        }

        public bool IsFinished()
        {
            Console.WriteLine("Trying to finish");
            return Ticker == 1000000;
        }

        public void IntermediateCleanUp()
        {
            Console.WriteLine("intermediate Cleanup");
            Ticker = Ticker - 120;
        }

        public void finalCleanUp()
        {
            Console.WriteLine("Final Cleanup");
            Ticker = -1;
        }
    }
    public class Dataflow
    {
        private TransformBlock<Job, Job> _preparationsBlock;

        private BufferBlock<Job> _balancerBlock;

        private readonly ExecutionDataflowBlockOptions _options = new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 4
        };

        private readonly DataflowLinkOptions _linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        private TransformBlock<Job, Job> _typeATickBlock;

        private TransformBlock<Job, Job> _typeBTickBlock;

        private TransformBlock<Job, Job> _writeBlock;

        private TransformBlock<Job, Job> _intermediateCleanupBlock;

        private ActionBlock<Job> _finalCleanupBlock;

        public async Task Process()
        {
            CreateBlocks();

            ConfigureBlocks();

            for (int i = 0; i < 500; i++)
            {
                await _preparationsBlock.SendAsync(new Job(i % 2 == 0 ? Type.A : Type.B));
            }
            _preparationsBlock.Complete();

            await Task.WhenAll(_preparationsBlock.Completion, _finalCleanupBlock.Completion);
        }

        private void CreateBlocks()
        {
            _preparationsBlock = new TransformBlock<Job, Job>(async job =>
            {
                await job.Prepare();
                return job;
            }, _options);

            _balancerBlock = new BufferBlock<Job>(_options);

            _typeATickBlock = new TransformBlock<Job, Job>(async job =>
            {
                Console.WriteLine($"Tick Block {_typeATickBlock.InputCount}/{_typeATickBlock.OutputCount}");
                await job.Tick();
                return job;
            }, _options);

            _typeBTickBlock = new TransformBlock<Job, Job>(async job =>
            {
                await job.Tick();
                await job.Tick();
                return job;
            }, _options);

            _writeBlock = new TransformBlock<Job, Job>(job =>
            {
                Console.WriteLine(job.Ticker);
                return job;
            }, _options);

            _finalCleanupBlock = new ActionBlock<Job>(job => job.finalCleanUp(), _options);

            _intermediateCleanupBlock = new TransformBlock<Job, Job>(job =>
            {
                job.IntermediateCleanUp();
                return job;
            }, _options);
        }

        private void ConfigureBlocks()
        {
            _preparationsBlock.LinkTo(_balancerBlock, _linkOptions);

            _balancerBlock.LinkTo(_typeATickBlock, _linkOptions, job => job.Type == Type.A);
            _balancerBlock.LinkTo(_typeBTickBlock, _linkOptions, job => job.Type == Type.B);

            _typeATickBlock.LinkTo(_typeATickBlock, _linkOptions, job => !job.IsCommitable());
            _typeATickBlock.LinkTo(_writeBlock, _linkOptions, job => job.IsCommitable());

            _typeBTickBlock.LinkTo(_typeBTickBlock, _linkOptions, job => !job.IsCommitable());

            _writeBlock.LinkTo(_intermediateCleanupBlock, _linkOptions, job => !job.IsFinished());
            _writeBlock.LinkTo(_finalCleanupBlock, _linkOptions, job => job.IsFinished());

            _intermediateCleanupBlock.LinkTo(_typeATickBlock, _linkOptions, job => job.Type == Type.A);
        }
    }

public class Scheduler
    {
        private readonly Timer _timer;

        private readonly Dataflow _flow;


        public Scheduler(int intervall)
        {
            _timer = new Timer(intervall);
            _flow = new Dataflow();
        }

        public void Start()
        {
            _timer.AutoReset = false;
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
        }

        private async void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _timer.Stop();
                Console.WriteLine("Timer stopped");
                await _flow.Process().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                Console.WriteLine("Timer started again.");
                _timer.Start();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var scheduler = new Scheduler(1000);
            scheduler.Start();

            Console.ReadKey();

        }
    }
}