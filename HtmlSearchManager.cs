using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;

namespace ConsoleApplication1.Manager
{
    
    public class HtmlSearchManager
    {
        private string textToSearch;
        public bool SearchForText(string textToSearch, string url,int numberOfUrlSearch)
        {
            //get text from HTML page

            bool isTextFounded = false;
            string HTMLText = GetHTMLFromUrl(url);
            Regex findText = new Regex(textToSearch);
            isTextFounded = findText.IsMatch(HTMLText);

            var urlList = GetUrlFromPage(HTMLText, numberOfUrlSearch);

            foreach (var item in urlList)
            {
                Task<IEnumerable<string>> task = Task<IEnumerable<string>>.Factory.StartNew(()
                    => GetUrlFromPage(GetHTMLFromUrl(item), numberOfUrlSearch));

                var result = task.Result;
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
    }
}
