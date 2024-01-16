using Newtonsoft.Json;
using Octokit;
using Patreon.Net;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

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

        private string? FindGitHubUserName(dynamic meta_data)
        {
            foreach(var data in meta_data)
            {
                if (data.key.ToString() == "github_login")
                {
                    return data.value.ToString();
                }
            }

            return null;
        }

        private void GitHubAdd(string username, GitHubClient client, IReadOnlyList<User> teamMembers, int teamId)
        {
            foreach(var user in teamMembers)
            {
                if (user.Login == username)
                {
                    return;
                }
            }

            client.Organization.Team.AddOrEditMembership(teamId, username, new UpdateTeamMembership(TeamRole.Member));
        }

        private void GitHubRemove(string username, GitHubClient client, IReadOnlyList<User> teamMembers, int teamId)
        {
            foreach (var user in teamMembers)
            {
                if (user.Login == username && username != "zoggins")
                {
                    client.Organization.Team.RemoveMembership(teamId, username);
                    return;
                }
            }
        }

        public async Task ValidateAndMoveSubscribers(PatreonClient client)
        {
            List<string> supporterNames = new List<string>();

            // Get my Campaign ID
            var campaigns = await client.GetCampaignsAsync(Includes.All);
            var me = campaigns.Resources[0].Id;

            var allPatrons = await client.GetCampaignMembersAsync(me, Includes.All);
            
            GitHubClient ghClient = new GitHubClient(
                new Octokit.ProductHeaderValue("RetroSpy"));

            ghClient.Credentials = new Credentials(Environment.GetEnvironmentVariable("GitHubPatreonLogin"));
            var teams = await ghClient.Organization.Team.GetAll("retrospy");
            int teamId = 0;
            foreach(var team in teams)
            {
                if (team.Name == "Patrons")
                {
                    teamId = team.Id;
                }
            }
            var teamMembers = await ghClient.Organization.Team.GetAllMembers(teamId);

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
                                    supporterNames.Add(data.value.data.attributes.vanity.ToString());
                                    
                                    var githubName = FindGitHubUserName(user.meta_data);
                                    if (githubName != null)
                                    {
                                        GitHubAdd(githubName, ghClient, teamMembers, teamId);
                                    }

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

                            if (noMatch)
                            {
                                if (userData?.roles.Contains("patreon_role_subplan_300")
                                    || userData?.roles.Contains("patreon_role_subplan_700")
                                    || userData?.roles.Contains("patreon_role_subplan_2500"))
                                {
                                    userData?.roles.Remove("patreon_role_subplan_300");
                                    userData?.roles.Remove("patreon_role_subplan_700");
                                    userData?.roles.Remove("patreon_role_subplan_2500");
                                    update = true;
                                }

                                string userName = FindGitHubUserName(user.meta_data);
                                if (userName != null)
                                {
                                    GitHubRemove(userName, ghClient, teamMembers, teamId);
                                }
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

                        string userName = FindGitHubUserName(user.meta_data);
                        if (userName != null)
                        {
                            GitHubRemove(userName, ghClient, teamMembers, teamId);
                        }

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

            int count = 0;
            string pageContent = string.Empty;
            foreach(var name in supporterNames)
            {
                if (count % 3 == 0)
                {
                    pageContent += "<tr>\n";
                }

                pageContent += "<td align=\"CENTER\">\n<h5>" + name + "</h5>\n</td>";

                if (count % 3 == 0)
                {
                    pageContent += "</tr>\n";
                }

                count++;
            }

            while (count % 3 != 0)
            {
                if (count % 3 == 0)
                {
                    pageContent += "<tr>\n";
                }

                pageContent += "<td align=\"CENTER\">\n<h5>Placeholder</h5>\n</td>";

                if (count % 3 == 0)
                {
                    pageContent += "</tr>\n";
                }

                count++;
            }

            string finalContent = supporterContentStart + pageContent + supporterContentEnd;


          //  < tr >\n < td align =\"CENTER\">\n<h5>40wattrange</h5>\n</td>\n<td align=\"CENTER\">\n<h5> Future Supporter 2</h5>\n</td>\n<td align=\"CENTER\">\n<h5> Future Supporter 3</h5>\n</td>\n</tr>\n
        }

        private string supporterContentStart = "<p style=\"text-align: center;\">Version: <br />\nBuild Timestamp: </p>\n<h3 style=\"text-align: center;\">Supported By</h3>\n<table border=\"0\" width=\"100%\">\n<tbody>\n";
        private string supporterContentEnd = "</tbody>\n</table>\n<p style=\"text-align: center;\"><img /><a href =\"https://patreon.com/retrospydisplay\"><img loading=\"lazy\" decoding=\"async\" class=\"alignnone size-medium wp-image-1207\" src=\"https://retro-spy.com/wp-content/uploads/2023/01/PatreonButton-300x82.png\" alt=\"\" width=\"300\" height=\"82\" srcset=\"https://retro-spy.com/wp-content/uploads/2023/01/PatreonButton-300x82.png 300w, https://retro-spy.com/wp-content/uploads/2023/01/PatreonButton.png 374w\" sizes=\"(max-width: 300px) 100vw, 300px\" /></a></p>\n";
    }

}
