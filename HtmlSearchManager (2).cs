using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;


namespace ConsoleApplication1.Manager
{    
    public class HtmlSearchManager
    {
        private TaskFactory _taskFactory;
        private LimitedConcurrencyLevelTaskScheduler _taskLimit;
        private string _textToSearch;
        private string _url;
        public HtmlSearchManager(string text, string url, int threadNumber)
        {
            _textToSearch = text;
            _url = url;
            _taskLimit = new LimitedConcurrencyLevelTaskScheduler(threadNumber);
            _taskFactory = new TaskFactory(_taskLimit);
        }

        public bool SearchForText(int numberOfUrlSearch)
        {
            //get text from HTML page
            bool isTextFounded = false;
            string HTMLText = DownloadHTML(_url).Result;
            Regex findText = new Regex(_textToSearch);
            isTextFounded = findText.IsMatch(HTMLText);

            //TODO:
            //Limit number of threads created in factory
            //Task.Factory.Scheduler.MaximumConcurrencyLevel = 

            var urlList = GetUrlFromPage(HTMLText, numberOfUrlSearch);

            foreach (var item in urlList)
            {
                //Task<IEnumerable<string>> task = Task<IEnumerable<string>>.Factory.StartNew(()                               
                //=> GetUrlFromPage(GetHTMLFromUrl(_url), numberOfUrlSearch));

                //var result = task.Result;
                var task = DownloadHTML(item);
                var result = GetUrlFromPage(task.Result, numberOfUrlSearch);
                Console.WriteLine("At this url {0} were founded:",item);
                foreach (var it in result)
                {
                    Console.WriteLine("-->  {0}",it);
                }
            }
            return isTextFounded;
        }

        private IEnumerable<string> GetUrlFromPage(string HTMLText, int maxNumberOfUrl)
        {
            List<string> urlList = new List<string>();
            Regex regex = new Regex(@"<a\s+href=""(http[^>\s]*)""\s+.*>.+?</a>");
            var anotherFoundedUrl = regex.Match(HTMLText);
            {
                int i = 0;
                while (anotherFoundedUrl.Success && i < maxNumberOfUrl)
                {
                    urlList.Add(anotherFoundedUrl.Groups[1].Value);
                    anotherFoundedUrl = anotherFoundedUrl.NextMatch();
                    i++;
                }
            }
            return urlList;
        }

        //get the HTML
        //for test purposes assume that url is valid

        //there is Download
        private string GetHTMLFromUrl(string url)
        {
            string result = "";
            WebRequest request; 
            WebResponse response;            
            //getting response            
            try
            {
                //for test purposes assume that url is valid
                request = WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.ContentType = "text/html";
                response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                StreamReader responseStreamReader = new StreamReader(responseStream);
                result = responseStreamReader.ReadToEnd();

                //close response and reader
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return result;
        }

        private Task<string> DownloadHTML(string url)
        {
            return _taskFactory.StartNew(() =>
                {
                    Console.WriteLine("Page {0} is downloading...", url);
                    return GetHTMLFromUrl(url);
                });
        }
    }

    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed 
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        // The maximum concurrency level allowed by this scheduler. 
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items. 
        private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism. 
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }
}
