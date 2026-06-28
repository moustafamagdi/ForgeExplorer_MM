using ForgeExplorer.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Threading.Tasks;

namespace ForgeExplorer.Models
{
    public class DataManagementController
    {
        private const string DataManagementBaseUrl = "https://developer.api.autodesk.com";
        private Credentials Credentials { get; set; }

        public async Task<string> GetUserProfileAsync()
        {
            JObject user = await GetJsonAsync("/userprofile/v1/users/@me");
            string name = user?["firstName"]?.ToString();
            Debug.WriteLine(user?.ToString());
            return name ?? string.Empty;
        }

        public async Task<IList<Item>> GetItemAsync(string id)
        {
            DiagnosticLogger.Write($"GetItemAsync requested id={id}");
            if (!EnsureCredentials())
            {
                return null;
            }

            if (id == "#")
            {
                return await GetHubsAsync();
            }

            string[] idParams = id.Split('/');
            if (idParams.Length < 2)
            {
                return new List<Item>();
            }

            string resource = idParams[idParams.Length - 2];
            switch (resource)
            {
                case "hubs":
                    return await GetProjectsAsync(id);
                case "projects":
                    return await GetProjectContents(id);
                case "folders":
                    return await GetFolderContents(id);
                default:
                    return new List<Item>();
            }
        }

        private bool EnsureCredentials()
        {
            if (Credentials != null)
            {
                return true;
            }

            Credentials = Credentials.GetFromAdWebServices();
            return Credentials != null;
        }

        private async Task<IList<Item>> GetHubsAsync()
        {
            IList<Item> items = new List<Item>();
            DiagnosticLogger.Write("Loading hubs.");
            JObject hubs = await GetJsonAsync("/project/v1/hubs");

            foreach (JToken hub in hubs?["data"]?.Children() ?? Enumerable.Empty<JToken>())
            {
                string nodeType = "hubs";
                switch (hub["attributes"]?["extension"]?["type"]?.ToString())
                {
                    case "hubs:autodesk.core:Hub":
                    case "hubs:autodesk.a360:PersonalHub":
                        nodeType = "unsupported";
                        break;
                    case "hubs:autodesk.bim360:Account":
                        nodeType = "bim360Hubs";
                        break;
                }

                string href = hub["links"]?["self"]?["href"]?.ToString();
                string name = hub["attributes"]?["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(href) && !string.IsNullOrWhiteSpace(name))
                {
                    items.Add(new Item(href, name, nodeType, nodeType != "unsupported"));
                }
            }

            DiagnosticLogger.Write($"Loaded {items.Count} hubs.");
            return items;
        }

        private async Task<IList<Item>> GetProjectsAsync(string href)
        {
            IList<Item> items = new List<Item>();
            string hubId = GetLastPathSegment(href);
            DiagnosticLogger.Write($"Loading projects for hubId={hubId}.");
            JObject projects = await GetJsonAsync($"/project/v1/hubs/{EncodePathSegment(hubId)}/projects");

            foreach (JToken project in projects?["data"]?.Children() ?? Enumerable.Empty<JToken>())
            {
                string nodeType = "projects";
                switch (project["attributes"]?["extension"]?["type"]?.ToString())
                {
                    case "projects:autodesk.core:Project":
                        nodeType = "a360projects";
                        break;
                    case "projects:autodesk.bim360:Project":
                        nodeType = "bim360projects";
                        break;
                }

                string projectHref = project["links"]?["self"]?["href"]?.ToString();
                string name = project["attributes"]?["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(projectHref) && !string.IsNullOrWhiteSpace(name))
                {
                    items.Add(new Item(projectHref, name, nodeType, true));
                }
            }

            DiagnosticLogger.Write($"Loaded {items.Count} projects for hubId={hubId}.");
            return items;
        }

        private async Task<IList<Item>> GetProjectContents(string href)
        {
            string[] idParams = href.Split('/');
            string hubId = idParams[idParams.Length - 3];
            string projectId = idParams[idParams.Length - 1];
            DiagnosticLogger.Write($"Loading project contents for hubId={hubId}, projectId={projectId}.");

            JObject project = await GetJsonAsync($"/project/v1/hubs/{EncodePathSegment(hubId)}/projects/{EncodePathSegment(projectId)}");
            string rootFolderHref = project?["data"]?["relationships"]?["rootFolder"]?["meta"]?["link"]?["href"]?.ToString();

            return string.IsNullOrWhiteSpace(rootFolderHref)
                ? new List<Item>()
                : await GetFolderContents(rootFolderHref);
        }

        private async Task<IList<Item>> GetFolderContents(string href)
        {
            IList<Item> folderItems = new List<Item>();
            string[] idParams = href.Split('/');
            string projectId = idParams[idParams.Length - 3];
            string folderId = idParams[idParams.Length - 1];
            DiagnosticLogger.Write($"Loading folder contents for projectId={projectId}, folderId={folderId}.");

            JObject folderContents = await GetJsonAsync($"/data/v1/projects/{EncodePathSegment(projectId)}/folders/{EncodePathSegment(folderId)}/contents");
            foreach (JToken folderContentItem in folderContents?["data"]?.Children() ?? Enumerable.Empty<JToken>())
            {
                string displayName = folderContentItem["attributes"]?["displayName"]?.ToString();
                string itemHref = folderContentItem["links"]?["self"]?["href"]?.ToString();
                string itemType = folderContentItem["type"]?.ToString();

                if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(itemHref) && !string.IsNullOrWhiteSpace(itemType))
                {
                    folderItems.Add(new Item(itemHref, displayName, itemType, true));
                }
            }

            DiagnosticLogger.Write($"Loaded {folderItems.Count} folder content items for folderId={folderId}.");
            return folderItems;
        }

        private async Task<JObject> GetJsonAsync(string pathOrUrl)
        {
            string requestUri = pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? pathOrUrl
                : DataManagementBaseUrl + pathOrUrl;

            using (HttpClient client = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Credentials.TokenInternal);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                DiagnosticLogger.Write($"GET {requestUri}");
                HttpResponseMessage response = await client.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                DiagnosticLogger.Write($"Response {(int)response.StatusCode} {response.ReasonPhrase} from {requestUri}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Autodesk Data Management request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
                }

                return JObject.Parse(responseBody);
            }
        }

        private static string GetLastPathSegment(string href)
        {
            string[] idParams = href.Split('/');
            return idParams[idParams.Length - 1];
        }

        private static string EncodePathSegment(string segment)
        {
            return Uri.EscapeDataString(Uri.UnescapeDataString(segment));
        }
    }
}
