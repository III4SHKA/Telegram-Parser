using HtmlAgilityPack;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

class WebParser
{
    private static readonly HttpClient client = new HttpClient();
    
    public static async Task<List<string>> GetItemsAsync(string category, List<string> selectedLocations, string range, string condition, string sourceType, DateTime startTime)
    {
        var allItems = new List<string>();
        DateTime latestTime = startTime;
        string categoryFilter = "";

        switch (category)
        {
            case "Type1":
                categoryFilter = "filter_type=type1";
                break;
            case "Type2":
                categoryFilter = "filter_type=type2";
                break;
            default:
                categoryFilter = "filter_type=type1&filter_type=type2";
                break;
        }

        var locations = new Dictionary<string, string>
        {
            { "All Regions", "" },
            { "Region1", "r1" },
            { "Region2", "r2" },
            { "Region3", "r3" },
            { "Region4", "r4" }
        };

        string rangeFilter = "";
        string conditionFilter = "";
        string sourceFilter = "";

        switch (range)
        {
            case "Low":
                rangeFilter = "filter_range:to=7500";
                break;
            case "Medium":
                rangeFilter = "filter_range:from=6500&filter_range:to=15500";
                break;
            case "High":
                rangeFilter = "filter_range:from=14500";
                break;
            default:
                rangeFilter = "";
                break;
        }

        switch (condition)
        {
            case "New":
                conditionFilter = "filter_condition[]=new";
                break;
            case "Used":
                conditionFilter = "filter_condition[]=used";
                break;
            default:
                conditionFilter = "filter_condition[]=used&filter_condition[]=new";
                break;
        }

        if (sourceType == "Business")
        {
            sourceFilter = "filter_source=business";
        }
        else if (sourceType == "Private")
        {
            sourceFilter = "filter_source=private";
        }
        else
        {
            sourceFilter = "";
        }

        var options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--disable-gpu");
        
        foreach (var location in selectedLocations)
        {
            if (locations.ContainsKey(location))
            {
                var locationCode = locations[location];
                var url = $"https://example.com/items/{locationCode}?sort=created_at:desc&{categoryFilter}&{rangeFilter}&{conditionFilter}&{sourceFilter}";
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                string? html = null;

                try
                {
                    var requestTask = client.GetStringAsync(url);
                    if (await Task.WhenAny(requestTask, Task.Delay(10000)) == requestTask)
                    {
                        html = await requestTask;
                    }
                    else
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var itemNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'item-container')]");

                if (itemNodes == null || itemNodes.Count == 0)
                {
                    continue;
                }

                try
                {
                    foreach (var item in itemNodes)
                    {
                        var titleNode = item.SelectSingleNode(".//h4[contains(@class, 'item-title')]") ?? item.SelectSingleNode(".//h4");
                        var valueNode = item.SelectSingleNode(".//p[contains(@class, 'item-value')]") ?? item.SelectSingleNode(".//p[@data-testid='item-value']");
                        var timeNode = item.SelectSingleNode(".//p[contains(@class, 'item-date')]") ?? item.SelectSingleNode(".//p[@data-testid='location-date']");
                        var linkNode = item.SelectSingleNode(".//a[contains(@class, 'item-link')]") ?? item.SelectSingleNode(".//a");
                        
                        var otherInfoNodes = item.SelectNodes(".//span[contains(@class, 'item-details')]");
                        List<string> otherInfo = new List<string>();
                        foreach (var spanNode in otherInfoNodes)
                        {
                            var spanText = spanNode.InnerText.Trim();
                            if (!string.IsNullOrEmpty(spanText))
                            {
                                otherInfo.Add("📍 " + spanText);
                            }
                        }
                        var additionalInfo = string.Join("\n", otherInfo);

                        if (titleNode != null && valueNode != null && linkNode != null && timeNode != null)
                        {
                            var title = titleNode.InnerText.Trim();
                            var value = valueNode.InnerText.Trim();
                            var time = timeNode.InnerText.Trim();
                            var itemLink = linkNode.GetAttributeValue("href", string.Empty);
                            
                            var timeOnly = ExtractTime(time);

                            DateTime itemTime;
                            if (!time.Contains("Today"))
                            {
                                continue;
                            }

                            if (itemLink.Contains("no_results"))
                            {
                                continue;
                            }

                            if (DateTime.TryParse(timeOnly, out itemTime))
                            {
                                if (itemTime > startTime)
                                {
                                    string fullItemUrl = $"https://example.com{itemLink}";
                                    string? itemPageHtml = null;
                                    try
                                    {
                                        itemPageHtml = await client.GetStringAsync(fullItemUrl);
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }

                                    var itemPageDoc = new HtmlDocument();
                                    itemPageDoc.LoadHtml(itemPageHtml);

                                    var imageNode = itemPageDoc.DocumentNode.SelectSingleNode("//img[contains(@class, 'item-image')]") ?? itemPageDoc.DocumentNode.SelectSingleNode("//img");

                                    var imageUrl = imageNode.GetAttributeValue("src", string.Empty);
                                    string[] wordsToRemove = { "Sell", "sell", "item", "Item" };
                                    foreach (var word in wordsToRemove)
                                    {
                                        title = title.Replace(word, "");
                                    }
                                    title = title.Replace("!", "");
                                    if (time.Contains("- "))
                                    {
                                        time = time.Split(new[] { "- " }, StringSplitOptions.None)[0].Trim();
                                    }
                                    if (value.Contains("$"))
                                    {
                                        value = value.Replace("$", "$ ");
                                    }

                                    allItems.Add($"{imageUrl}\n📌 {title}\n\n🌍{time}\n\n{additionalInfo}\n\n💰 Value: {value}\nhttps://example.com{itemLink}");
                                    if (itemTime > latestTime)
                                    {
                                        latestTime = itemTime;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Missing data");
                        }
                    }
                }
                catch (Exception)
                {
                    
                }
            }
        }
        return allItems;
    }

    private static string ExtractTime(string timeText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(timeText, @"\d{1,2}:\d{2}");
        
        if (match.Success)
        {
            return match.Value;
        }
        return "Unknown time";
    }
}