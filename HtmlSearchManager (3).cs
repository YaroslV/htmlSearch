using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApplication1.Managers
{
    public class HtmlSearchManager_ver2
    {
        private string _startUrl;
        private string _textToSearch;
        private int _numberOfUrlsToSearch;
        private LimitedConcurrencyLevelTaskScheduler _th_limit = new LimitedConcurrencyLevelTaskScheduler(90);
        private TaskFactory _taskFactory;

        public HtmlSearchManager_ver2(string startUrl, string textToSearch, int numberOfUrlsToSearch)
        {
            _startUrl = startUrl;
            _textToSearch = textToSearch;
            _numberOfUrlsToSearch = numberOfUrlsToSearch;
            _taskFactory = new TaskFactory(_th_limit);
        }

        public async void StartSearch()
        {
            string startPageUrl = GetHTMLFromUrl(_startUrl);

            //should be \b+_textToSearch+\b
            Regex textPattern = new Regex(_textToSearch);
            var startMatcher = textPattern.Match(startPageUrl);
            int textOccurencesOnStartPage = 0;
            while (startMatcher.Success)
            {
                textOccurencesOnStartPage++;
                startMatcher = startMatcher.NextMatch();
            }
            Console.WriteLine("On start page were found {0} occurences", textOccurencesOnStartPage);

            var urlFromStartPage = GetUrlFromPage(startPageUrl, _numberOfUrlsToSearch);
            int urlCounter = 0;
            foreach (string url in urlFromStartPage)
            {
                Task<int> t = Task.Factory.StartNew(() =>
                {
                    string HTMLText = GetHTMLFromUrl(url);
                    Regex textToSearch = new Regex(_textToSearch);
                    var matcher = textToSearch.Match(HTMLText);
                    int textCounter = 0;
                    while (matcher.Success)
                    {
                        textCounter++;
                        matcher = matcher.NextMatch();  
                    }
                    return textCounter;
                });
                
                int i = urlCounter;
                urlCounter++;
                
                var awaiter = t.GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    Console.WriteLine("{2}  Task for {0} finished. Has founded {1} occurence(s)",url,t.Result, i);
                });

//                int res = await t;
//                Console.WriteLine("{2}  Task for {0} finished. Has founded {1} occurence(s)", url, res, i);
            }
        }

        private IEnumerable<string> GetUrlFromPage(string HTMLText, int maxNumberOfUrl)
        {
            LinkedList<string> urlList = new LinkedList<string>();
            Regex regex = new Regex(@"<a\s+href=""(http[^>\s]*)""\s+.*>.+?</a>");
            var anotherFoundedUrl = regex.Match(HTMLText);
            {
                int i = 0;
                while (anotherFoundedUrl.Success && i < maxNumberOfUrl)
                {
                    urlList.AddLast(anotherFoundedUrl.Groups[1].Value);
                    anotherFoundedUrl = anotherFoundedUrl.NextMatch();
                    i++;
                }
            }
            return urlList;
        }

        private string GetHTMLFromUrl(string url)
        {
            //
            Console.WriteLine("Starting download {0} ...",url);

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

            Console.WriteLine("Download of {0} finished.", url);
            return result;
        }
    }
}
