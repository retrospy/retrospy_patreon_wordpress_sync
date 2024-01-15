using Newtonsoft.Json;
using Patreon.Net;

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
                    bool noKey = true;
                    foreach (var data in user.meta_data)
                    {
                        if (data.key.ToString() == "patreon_latest_patron_info")
                        {
                            noKey = false;
                            bool noMatch = true;
                            bool update = false;
                            foreach (var patron in allPatrons.Resources)
                            {
                                if (patron.Email == data.value.data.attributes.email.ToString() && patron.PatronStatus == Patreon.Net.Models.Member.PatronStatusValue.ActivePatron)
                                {
                                    response = wcHttpClient.GetAsync("/wp-json/wp/v2/users/" + user.id + "?context=edit").Result;
                                    using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                                    {
                                        responseStr = stream.ReadToEnd();
                                    }
                                    userData = JsonConvert.DeserializeObject(responseStr);

                                    userData?.roles.Remove("patreon_role_subplan_300");
                                    userData?.roles.Remove("patreon_role_subplan_700");
                                    userData?.roles.Remove("patreon_role_subplan_2500");

                                    if (patron.CurrentlyEntitledAmountCents == 300)
                                    {
                                        userData?.roles.Add("patreon_role_subplan_300");
                                    }
                                    else if (patron.CurrentlyEntitledAmountCents == 700)
                                    {
                                        userData?.roles.Add("patreon_role_subplan_700");
                                    }
                                    else if (patron.CurrentlyEntitledAmountCents == 2500)
                                    {
                                        userData?.roles.Add("patreon_role_subplan_2500");
                                    }
                                    noMatch = false;
                                    update = true;
                                    break;
                                }
                            }

                            if (noMatch &&
                                (userData?.roles.Contains("patreon_role_subplan_300")
                                || userData?.roles.Contains("patreon_role_subplan_700")
                                || userData?.roles.Contains("patreon_role_subplan_2500")))
                            {
                                userData?.roles.Remove("patreon_role_subplan_300");
                                userData?.roles.Remove("patreon_role_subplan_700");
                                userData?.roles.Remove("patreon_role_subplan_2500");
                                update = true;
                            }

                            if (update)
                            {
                                var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
                                s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                                response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
                            }
                            break;
                        }
                    }

                    if (noKey)
                    {
                        response = wcHttpClient.GetAsync("/wp-json/wp/v2/users/" + user.id + "?context=edit").Result;
                        using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                        {
                            responseStr = stream.ReadToEnd();
                        }
                        userData = JsonConvert.DeserializeObject(responseStr);

                        if (userData?.roles.Contains("patreon_role_subplan_300")
                           || userData?.roles.Contains("patreon_role_subplan_700")
                           || userData?.roles.Contains("patreon_role_subplan_2500"))
                        {
                            userData?.roles.Remove("patreon_role_subplan_300");
                            userData?.roles.Remove("patreon_role_subplan_700");
                            userData?.roles.Remove("patreon_role_subplan_2500");

                            var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
                            s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
                        }
                    }
                }
            } while (cont);

        }
    }

}
