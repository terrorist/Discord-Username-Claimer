using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordUsernameClaimer
{
    internal class Program
    {
        public struct DiscordInfo
        {
            public string Id { get; set; }
            public string CurrentUsername { get; set; }
            public string CurrentDiscriminator { get; set; }
        }

        static void Main()
        {
            string[] checkerTokens = File.ReadAllLines("checkerTokens.txt");
            Console.WriteLine($"{checkerTokens.Length} checker tokens loaded.");

            Dictionary<string, string> map = new Dictionary<string, string>();
            foreach (var account in File.ReadAllLines("claimerTokens.txt"))
            {
                string[] slice = account.Split(":");
                map.Add(slice[0], slice[1]);
            }

            Console.WriteLine($"{map.Count} claimer tokens loaded.");
            Thread.Sleep(TimeSpan.FromSeconds(1));

            string[] userIds = File.ReadAllLines("ids.txt");
            Console.WriteLine($"{userIds.Length} Discord ID's loaded.");
            Thread.Sleep(TimeSpan.FromSeconds(1));

            string[] proxies = null;
            if (File.Exists("proxies.txt"))
            {
                proxies = File.ReadAllLines("proxies.txt");
                Console.WriteLine($"{proxies.Length} proxies loaded.");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            Console.WriteLine("Fetching DiscordInfo array.");
            DiscordInfo[] discordInfos = Program.GetDiscordInfos(userIds, checkerTokens, proxies);

            Console.WriteLine("Fetched DiscordInfo array.");
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Console.WriteLine("Claimer running.");
            Thread.Sleep(TimeSpan.FromSeconds(1));

            List<Thread> threads = new List<Thread>();
            foreach (DiscordInfo info in discordInfos)
            {
                Thread thread = new Thread(() =>
                {
                    while (true)
                    {
                        while (!Program.IsDiscordChange(info, checkerTokens, proxies)) ;
                        KeyValuePair<string, string> pair = map.ElementAt(new Random().Next(map.Count - 1));
                        if (Program.AttemptReserve(info, pair))
                        {
                            Console.WriteLine($"{info.CurrentUsername}#{info.CurrentDiscriminator} claimed.");
                            File.WriteAllText($"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.txt", $"Username Claimed: {info.CurrentUsername}¤{info.CurrentDiscriminator}" + Environment.NewLine + $"Token: {pair.Key}" + Environment.NewLine + $"Time: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"{info.CurrentUsername}#{info.CurrentDiscriminator} claim failed.");
                        }
                    }
                });
                thread.Start();
                threads.Add(thread);
            }
            foreach (Thread _t in threads)
            {
                _t.Join();
            }
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        public static bool AttemptReserve(DiscordInfo discordInfo, KeyValuePair<string, string> pair)
        {
            using (HttpClientHandler httpClientHandler = new HttpClientHandler() { UseCookies = false })
            {
                using (HttpClient httpClient = new HttpClient(handler: httpClientHandler))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(pair.Key);
                    using (HttpRequestMessage message = new HttpRequestMessage(new HttpMethod("PATCH"), "https://discord.com/api/v9/users/@me"))
                    {
                        using (StringContent stringContent = new StringContent("{\"username\":\"" + discordInfo.CurrentUsername + "\",\"password\":\"" + pair.Value + "\",\"discriminator\":\"" + discordInfo.CurrentDiscriminator + "\"}"))
                        {
                            stringContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            message.Content = stringContent;

                            using (HttpResponseMessage httpResponseMessage = httpClient.SendAsync(message).Result)
                            {
                                return httpResponseMessage.IsSuccessStatusCode;
                            }
                        }
                    }
                }
            }
        }

        public static bool IsDiscordChange(DiscordInfo discordInfo, string[] tokens, string[] proxies = null)
        {
            using (HttpClientHandler httpClientHandler = new HttpClientHandler() { UseCookies = false })
            {
                if (proxies != null)
                {
                    httpClientHandler.Proxy = new WebProxy($"http://{proxies[new Random().Next(proxies.Length - 1)]}");
                }
                using (HttpClient httpClient = new HttpClient(handler: httpClientHandler))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(tokens[new Random().Next(tokens.Length)]);
                    using (HttpResponseMessage httpResponseMessage = httpClient.GetAsync($"https://discord.com/api/v9/users/{discordInfo.Id}/profile?with_mutual_guilds=false").Result)
                    {
                        try
                        {
                            string response = httpResponseMessage.Content.ReadAsStringAsync().Result;
                            JObject jobject = JsonConvert.DeserializeObject<JObject>(response);

                            string username = jobject.SelectToken("user.username").ToObject<String>();
                            string discriminator = jobject.SelectToken("user.discriminator").ToObject<String>();
                            return discordInfo.CurrentUsername != username || discordInfo.CurrentDiscriminator != discriminator;

                        }
                        catch
                        {
                            return false;
                        }

                    }
                }
            }
        }


        public static DiscordInfo[] GetDiscordInfos(string[] ids, string[] tokens, string[] proxies = null)
        {
            DiscordInfo[] discordInfos = new DiscordInfo[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                using (HttpClientHandler httpClientHandler = new HttpClientHandler() { UseCookies = false})
                {
                    if (proxies != null)
                    {
                        httpClientHandler.Proxy = new WebProxy($"http://{proxies[new Random().Next(proxies.Length - 1)]}");
                    }
                    using (HttpClient httpClient = new HttpClient(handler: httpClientHandler))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(tokens[new Random().Next(tokens.Length)]);
                        using (HttpResponseMessage httpResponseMessage = httpClient.GetAsync($"https://discord.com/api/v9/users/{ids[i]}/profile?with_mutual_guilds=false").Result)
                        {
                            string response =httpResponseMessage.Content.ReadAsStringAsync().Result;
                            JObject jobject = JsonConvert.DeserializeObject<JObject>(response);

                            discordInfos[i] = new DiscordInfo
                            {
                                Id = ids[i],
                                CurrentUsername = jobject.SelectToken("user.username").ToObject<String>(),
                                CurrentDiscriminator = jobject.SelectToken("user.discriminator").ToObject<String>()
                            };
                        }
                    }
                }
            }
            return discordInfos;
        }
    }
}