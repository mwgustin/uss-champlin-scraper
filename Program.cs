using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;


namespace uss_champlin_scraper
{
    class Program
    {
        // static ScrapingBrowser _browser = new ScrapingBrowser();

        static HtmlWeb _browser = new HtmlWeb();
        
        static List<string> PageNav = new List<string>();

        static List<string> KnownIssues = new List<string>();

        static ILogger _logger;
        static async Task Main(string[] args)
        {
            //logger
            var fac = LoggerFactory.Create(builder => 
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });
            
            _logger = fac.CreateLogger<Program>();
            
            _logger.LogCritical("Beginning");
            Thread.Sleep(2000);
            
            //get default nav URLs from history page.
            PageNav = await GetAllUrls("https://www.usschamplin.com/page2.html");

            var crewUrls = await GetAllUrls("https://www.usschamplin.com/page4.html");

            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page6.html"));
            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page7.html"));
            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page8.html"));

            _logger.LogInformation($"Count of Crew: {crewUrls.Count}");

            // var test = await GetCrew("https://www.usschamplin.com/crew_ag/WilliamDGustin.html");
            // var test2 = await GetCrew("https://www.usschamplin.com/crew_ag/ChaddArthurB.html");
            // var test3 = await GetCrew("https://www.usschamplin.com/crew_ag/ChiassonGilbertn.html");
            List<Crew> crewList = new List<Crew>();
            foreach(var url in crewUrls)
            {
                crewList.Add(await GetCrew(url));
            }
            crewList = crewList.Where(x => !KnownIssues.Contains(x.OriginalUrl)).ToList();

            _logger.LogInformation($"Crew found: {crewList.Count}");
            _logger.LogInformation($"Issues list: {KnownIssues.Count}");

            Thread.Sleep(2000);
        }

        static void CheckCrew(Crew c)
        {
            if( String.IsNullOrEmpty(c.DateOfEnlistment)
                && String.IsNullOrEmpty(c.PlaceOfEnlistment)
                && String.IsNullOrEmpty(c.ServiceNumber))
            {
                _logger.LogCritical($"Issue parsing crew: {c.Name} - {c.OriginalUrl}");
                KnownIssues.Add(c.OriginalUrl);
            }
        }

        static async Task<List<string>> GetAllUrls(string url)
        {
            var doc = await _browser.LoadFromWebAsync(url);
            
            var pageLinks = doc.DocumentNode.Descendants("a")
                            .Select(x => x.GetAttributeValue("href", ""))
                            .Where(x => !PageNav.Contains(x))
                            .ToList();

            return pageLinks;
        }

        static async Task<Crew> GetCrew(string url)
        {
            try 
            {
                var doc = await _browser.LoadFromWebAsync(url);
            
                var crew = new Crew();
                
                crew.OriginalUrl = url;
                
                crew.Name = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[1]/u[1]");

                crew.DateOfBirth = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[2]");

                // crew.PlaceOfBirth = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[4]" );
                                
                crew.ServiceNumber = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[5]");

                crew.Rank = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[7]");

                crew.DateOfEnlistment = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[9]");

                crew.PlaceOfEnlistment = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[11]");

                crew.DateOnShip = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[13]");
                
                crew.DateOffShip = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[15]");

                crew.DateDischarged = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[17]");

                crew.DateOfDeath = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[19]");
                
                CheckCrew(crew);
                return crew;
            }
            catch(Exception e)
            {
                _logger.LogError($"ERROR - {e.Message}");
                var crew = new Crew();
                crew.OriginalUrl = url;
                CheckCrew(crew);
                return crew;
            }
        }

        static DateTime ParseDateTime(HtmlDocument doc, string path)
        {
            var dateText = ParseString(doc, path);
            return !String.IsNullOrEmpty(dateText) ? DateTime.Parse(dateText) : DateTime.MinValue;
        }
        static string ParseString(HtmlDocument doc, string path)
        {
            return doc.DocumentNode.SelectSingleNode(path)?
                            .InnerText
                            .Replace("&nbsp;","")
                            .Trim();
        }

    }
}
