using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace uss_champlin_scraper
{
    class Program
    {
        static HtmlWeb _browser = new HtmlWeb();
        static List<string> PageNav = new List<string>();
        static List<string> KnownIssues = new List<string>();

        static ILogger _logger;
        static async Task Main(string[] args)
        {
            //setup logger
            var fac = LoggerFactory.Create(builder => 
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });
            
            _logger = fac.CreateLogger<Program>();
            
            //startup
            _logger.LogCritical("Beginning");
            Thread.Sleep(2000);
            
            //get default nav URLs from history page so we can ignore them.
            PageNav = await GetAllUrls("https://www.usschamplin.com/page2.html");

            //get all crew page urls
            var crewUrls = await GetAllUrls("https://www.usschamplin.com/page4.html");
            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page6.html"));
            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page7.html"));
            crewUrls.AddRange(await GetAllUrls("https://www.usschamplin.com/page8.html"));

            _logger.LogInformation($"Count of Crew: {crewUrls.Count}");


            // parse out crew details for each link in list.
            List<Crew> crewList = new List<Crew>();
            foreach(var url in crewUrls)
            {
                crewList.Add(await GetCrew(url));
            }

            // make sure we remove any we may have flagged as problematic
            crewList = crewList.Where(x => !KnownIssues.Contains(x.OriginalUrl)).ToList();

            _logger.LogInformation($"Crew found: {crewList.Count}");
            _logger.LogInformation($"Issues list: {KnownIssues.Count}");

            Thread.Sleep(2000);

            //write out the crew json and issues list.
            await WriteDataToFile(JsonSerializer.Serialize(crewList), "CrewList.json");
            await WriteDataToFile(JsonSerializer.Serialize(KnownIssues), "IssuesList.json");


        }
        
        /// <summary>
        /// Simple helper to write data to file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        static async Task WriteDataToFile(string data, string fileName)
        {
            using(System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
            {
                await file.WriteAsync(data);
            }
        }

        /// <summary>
        /// Helper to validate Crew data with common params. If problematic, add to issues list.
        /// </summary>
        /// <param name="c"></param>
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

        /// <summary>
        /// Fetch all URLs from a webpage
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static async Task<List<string>> GetAllUrls(string url)
        {
            var doc = await _browser.LoadFromWebAsync(url);
            
            var pageLinks = doc.DocumentNode.Descendants("a")
                            .Select(x => x.GetAttributeValue("href", "").Trim())
                            .Where(x => !PageNav.Contains(x))
                            .ToList();

            return pageLinks;
        }

        /// <summary>
        /// Parse crew from a crew webpage link
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static async Task<Crew> GetCrew(string url)
        {
            try 
            {

                var doc = await _browser.LoadFromWebAsync(url);
            
                var crew = new Crew();
                
                crew.OriginalUrl = url;
                
                crew.Name = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[1]/u[1]");
                
                crew.PictureUrl = ParseImageSrc(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[1]/img[1]");

                crew.DateOfBirth = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[2]");

                //not included for most entries... just gustin.
                // crew.PlaceOfBirth = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[4]" );
                                
                crew.ServiceNumber = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[5]");

                crew.Rank = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[7]");

                crew.DateOfEnlistment = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[9]");

                crew.PlaceOfEnlistment = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[11]");

                crew.DateOnShip = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[13]");
                
                crew.DateOffShip = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[15]");

                crew.DateDischarged = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[17]");

                crew.DateOfDeath = ParseString(doc, "/html[1]/body[1]/div[1]/table[1]/tr[1]/td[2]/p[2]/text()[19]");
                
                crew.PostWarExperience = ParseString(doc, "/html[1]/body[1]/div[1]/table[2]/tr[2]/td[1]")?.Replace("Post World War II Life Experience:\n\n\n", "");

                ParsePersonalDetails(doc, crew);

                CheckCrew(crew);
                return crew;
            }
            catch(Exception e)
            {
                // if there's an exception, log it and add to issues list for manual processing.
                _logger.LogError($"ERROR - {e.Message}");
                var crew = new Crew();
                crew.OriginalUrl = url;
                CheckCrew(crew);
                return crew;
            }
        }

        /// <summary>
        /// If crew member has additional personal details, parse those.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="c"></param>
        static void ParsePersonalDetails(HtmlDocument doc, Crew c)
        {
            var detailsTable = doc.DocumentNode.SelectSingleNode("/html[1]/body[1]/div[1]/table[2]/tr[1]");
            if(detailsTable != null && !detailsTable.InnerText.Contains("To submit additions, corrections, or to contribute photos"))
            {
                
                var text = detailsTable.InnerText;

                c.Spouse = ExtractString(text, "Wife:","Children:")?.Trim();
                c.Children = ExtractStringList(text, "Children:","Grandchildren:");
                c.Grandchildren = ExtractStringList(text, "Grandchildren:","Great Grandchildren:");    
                c.GreatGrandchildren = ExtractStringList(text, "Great Grandchildren:","High School:");

                //this would be better with regex but mine kept breaking. doing it the quick and dirty way instead.
                c.HighSchool = ExtractStringList(text, "High School:", "College:", "Interests and Hobbies:"); 
                c.College = ExtractStringList(text, "College:", "Interests and Hobbies:", "$"); 
                c.Interests = ExtractStringList(text, "Interests and Hobbies:", "$"); 
                
            }
            
            return;
        }

        /// <summary>
        /// Parse a substring into a list of items (ie list of children)
        /// </summary>
        /// <param name="text"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="alternativeEnd"></param>
        /// <returns></returns>
        static List<string> ExtractStringList(string text, string start, string end, string alternativeEnd = "")
        {
            List<string> list = new List<string>();

            string results = ExtractString(text, start, end, alternativeEnd);

            var arrayResults = results?.Trim().Split("\n");
            foreach(var r in arrayResults)
            {
                if(r.Length > 0)
                {
                    list.Add(r.Trim());
                }
            }

            return list;

        }

        /// <summary>
        /// parse substring. alternative end lets you specify an alternative if end isn't found.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="alternativeEnd"></param>
        /// <returns></returns>
        static string ExtractString(string text, string start, string end, string alternativeEnd = "")
        {
            int startIndex =  -1;
            if (text.IndexOf(start) >= 0)
                startIndex = text.IndexOf(start) + start.Length;
            if(startIndex < 0)
                return "";

            int endIndex = -1;
            if(end == "$")
            {
                endIndex = text.Length;
            }
            else
            {
                endIndex = text.IndexOf(end, startIndex);
            }
            if(endIndex < 0 && !String.IsNullOrEmpty(alternativeEnd))
            {
                if(alternativeEnd == "$")
                {
                    endIndex = text.Length;
                }
                else
                {
                    endIndex = text.IndexOf(alternativeEnd, startIndex);
                }
            }

            if(endIndex < 0 )
            {
                return "";
            }
            return text.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Parse out a string result from an xPath
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        static string ParseString(HtmlDocument doc, string path)
        {
            return doc.DocumentNode.SelectSingleNode(path)?
                            .InnerText
                            .Replace("&nbsp;","")
                            .Trim();
        }

        /// <summary>
        /// Get image source from xPath
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        static string ParseImageSrc(HtmlDocument doc, string path)
        {
            return doc.DocumentNode.SelectSingleNode(path)?.GetAttributeValue("src", "");
        }

    }
}
