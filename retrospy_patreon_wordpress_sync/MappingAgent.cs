using Newtonsoft.Json;
using Patreon.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace retrospy_patreon_wordpress_sync
{


    public class MappingAgent
    {
        private HttpClient wcHttpClient;

        public MappingAgent()
        {
            HttpMessageHandler handler = new HttpClientHandler();
            wcHttpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://retro-spy.com"),
            };
            wcHttpClient.DefaultRequestHeaders.Add("ContentType", "application/json");
            wcHttpClient.DefaultRequestHeaders.Add("User-Agent", "RetroSpy-Patreon-Sub-Mapping");
            var authBytes = System.Text.Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WPLogin") + ":" + Environment.GetEnvironmentVariable("WPAppPassword"));
            string authHeaderString = System.Convert.ToBase64String(authBytes);
            wcHttpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + authHeaderString);
        }

        private dynamic? users;
        private dynamic? userData;

        public async Task ValidateAndMoveSubscribers(PatreonClient client)
        {

            // Get my Campaign ID
            var campaigns = await client.GetCampaignsAsync(Includes.All);
            var me = campaigns.Resources[0].Id;

            var allPatrons = await client.GetCampaignMembersAsync(me, Includes.All);
          
            bool cont = false;
            int numProcessed = 0;
            int page = 1;
            do
            {

                HttpResponseMessage response = wcHttpClient.GetAsync("/wp-json/wc/v3/customers?per_page=100&page=" + page + "&role=all").Result;
                page++;
                string responseStr = string.Empty;

                using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                {
                    responseStr = stream.ReadToEnd();
                }

                users = JsonConvert.DeserializeObject(responseStr);
                if (users == null)
                    return;

                cont = users.Count == 100;

                foreach (var user in users)
                {
                    numProcessed++;
                    //response = wcHttpClient.GetAsync("/wp-json/wp/v2/users/" + user.id + "?context=edit").Result;
                    //using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                    //{
                    //    responseStr = stream.ReadToEnd();
                    //}
                    //userData = JsonConvert.DeserializeObject(responseStr);
             //       if (userData?.meta.twitchpress_twitch_id != string.Empty && userData?.meta.twitchpress_twitch_id != me.Users[0].Id)
            //        {
            //            bool noMatch = true;
            //            bool update = false;
            //            foreach (var sub in allSubscriptions.Data)
            //            {
            //                if (userData?.meta.twitchpress_twitch_id == sub.UserId)
            //                {
            //                    userData.roles.Remove("twitchpress_role_subplan_1000");
            //                    userData.roles.Remove("twitchpress_role_subplan_2000");
            //                    userData.roles.Remove("twitchpress_role_subplan_3000");

            //                    if (sub.Tier == "1000")
            //                    {
            //                        userData.roles.Add("twitchpress_role_subplan_1000");
            //                    }
            //                    else if (sub.Tier == "2000")
            //                    {
            //                        userData.roles.Add("twitchpress_role_subplan_2000");
            //                    }
            //                    else if (sub.Tier == "3000")
            //                    {
            //                        userData.roles.Add("twitchpress_role_subplan_3000");
            //                    }
            //                    noMatch = false;
            //                    update = true;
            //                    break;
            //                }
            //            }

            //            if (noMatch &&
            //                (userData?.roles.Contains("twitchpress_role_subplan_1000")
            //                || userData?.roles.Contains("twitchpress_role_subplan_2000")
            //                || userData?.roles.Contains("twitchpress_role_subplan_3000")))
            //            {
            //                userData?.roles.Remove("twitchpress_role_subplan_1000");
            //                userData?.roles.Remove("twitchpress_role_subplan_2000");
            //                userData?.roles.Remove("twitchpress_role_subplan_3000");
            //                update = true;
            //            }

            //            if (update)
            //            {
            //                var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
            //                s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            //                response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
            //            }
            //        }
            //        else
            //        {
            //            if (userData?.roles.Contains("twitchpress_role_subplan_1000")
            //               || userData?.roles.Contains("twitchpress_role_subplan_2000")
            //               || userData?.roles.Contains("twitchpress_role_subplan_3000"))
            //            {
            //                userData?.roles.Remove("twitchpress_role_subplan_1000");
            //                userData?.roles.Remove("twitchpress_role_subplan_2000");
            //                userData?.roles.Remove("twitchpress_role_subplan_3000");

            //                var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
            //                s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            //                response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
            //            }
            //        }
                }
            } while (cont);

        }
    }

}
