using System.Collections.Specialized;
using System.DirectoryServices;
using System.Text;
using System.Text.Json;
using ActiveDirectorySearcher.DTOs;

namespace ActiveDirectorySearcher;

#pragma warning disable CA1416 //suppress windows warning 
public class ActiveDirectoryHelper
{
    private static Dictionary<string, string> keyValuePairs = new();

    public static async Task LoadOUReplication()
    {
        var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        var filePath = Path.Combine(basePath ?? "", "Info", "OUReplicationTime.txt");
        using var fstream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        keyValuePairs = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fstream) ?? new Dictionary<string, string>();
    }

    public static async Task WriteOUReplication()
    {
        var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        var filePath = Path.Combine(basePath ?? "", "Info", "OUReplicationTime.txt");
        var json = await GetSerializedObject(keyValuePairs);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task ProcessADObjects(InputCreds inputCreds, IProgress<Status>? progress, ObjectType objectType, ICollection<string> containers, CancellationToken cancellationToken)
    {
        if (containers.Count > 0)
        {
            foreach (var container in containers)
            {
                var currReplicationTime = DateTime.Now.ToUniversalTime().ToString();
                string? lastReplicationTime = "";

                if (keyValuePairs.ContainsKey($"{container}_{objectType}"))
                    lastReplicationTime = keyValuePairs[$"{container}_{objectType}"];

                await ProcessADObjects(inputCreds, progress, objectType, cancellationToken, lastReplicationTime, container);
                keyValuePairs[$"{container}_{objectType}"] = currReplicationTime;
            }
        }
        else
        {
            var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            string filePath = objectType switch
            {
                ObjectType.User => Path.Combine(basePath ?? "", "Info", "UserReplicationTime.txt"),
                ObjectType.Group => Path.Combine(basePath ?? "", "Info", "GroupReplicationTime.txt"),
                _ => ""
            };

            var currReplicationTime = DateTime.Now.ToUniversalTime().ToString();
            var lastReplicationTime = await File.ReadAllTextAsync(filePath);
            await ProcessADObjects(inputCreds, progress, objectType, cancellationToken, lastReplicationTime);
            await File.WriteAllTextAsync(filePath, currReplicationTime, cancellationToken);
        }
    }
    private static async Task ProcessADObjects(InputCreds inputCreds, IProgress<Status>? progress, ObjectType objectType, CancellationToken cancellationToken, string? lastReplicationTime, string ouPath = "")
    {
        progress?.Report(new($"Processing {objectType} {ouPath}. {Environment.NewLine}", ""));

        var whenChangedFilter = string.IsNullOrEmpty(lastReplicationTime) ? "" : DateTime.Parse(lastReplicationTime).ToString("yyyyMMddHHmmss.0Z");
        var objectsList = new List<SearchResult>();
        using var root = await GetRootEntry(inputCreds);

        using var searcher = new DirectorySearcher(root);
        searcher.Filter = PrepareLdapQuery(objectType, whenChangedFilter);
        searcher.PageSize = 500;
        searcher.SizeLimit = 0;

        using var results = await Task.Run(() => searcher?.FindAll());
        var resultsEnumerator = results?.GetEnumerator();

        if (resultsEnumerator != null)
        {
            var dnList = new List<string>();
            int i = 0;
            for (; resultsEnumerator.MoveNext(); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = (SearchResult)resultsEnumerator.Current;
                objectsList.Add(result);
                var distinguishedName = result.Properties["distinguishedName"][0] as string ?? "";
                dnList.Add(distinguishedName);

                if ((i + 1) % 20 == 0)
                    ReportFetchObjects(objectType, dnList, i + 1, progress);

                if ((i + 1) % 1000 == 0)
                    await SendObjectListToWebService(inputCreds.License, objectsList, objectType, progress);
            }
            if (dnList.Count > 0)
                ReportFetchObjects(objectType, dnList, i, progress);

            if (objectsList.Count > 0)
                await SendObjectListToWebService(inputCreds.License, objectsList, objectType, progress);


        }
    }

    public static async Task<DirectoryEntry> GetRootEntry(InputCreds inputCreds)
    {
        var entry = await Task.Run(() =>
        {
            var path = $"LDAP://{inputCreds.Domain}{(inputCreds.Port is 0 ? "" : $":{inputCreds.Port}")}";
            var root = string.IsNullOrEmpty(inputCreds.UserName) ? new DirectoryEntry(path) : new DirectoryEntry(path, inputCreds.UserName, inputCreds.Password);
            _ = root.Name; // checking connection; will throw if connection is not succesful
            return root;
        });

        return entry;
    }

    #region Private static helper methods
    private static async Task SendObjectListToWebService(string licenseID, List<SearchResult> objectsList, ObjectType objectType, IProgress<Status>? progress)
    {
        progress?.Report(new("", SendingObjectsRequestMessage(objectsList.Count, objectType)));

        var json = await GetSerializedObject(objectsList);
        string apiUrl = objectType switch
        {
            ObjectType.User => $"https://ns-server.vantagemdm.com/active-directory/sync?licenseId={licenseID}&type=user",
            ObjectType.Group => $"https://ns-server.vantagemdm.com/active-directory/sync?licenseId={licenseID}&type=group",
            _ => ""
        };
        using var client = new HttpClient();
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        response = await client.PutAsync(apiUrl, content);
        // Check the response status
        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            progress?.Report(new("", $"Request failed with status code {response.StatusCode} and ResponseBody {responseBody}"));
            throw new Exception(responseBody);
        }
        objectsList.Clear();
    }
    private static string PrepareLdapQuery(ObjectType objectType, string whenChangedFilter)
    {
        string ldapfilter = objectType switch
        {
            ObjectType.User => string.IsNullOrEmpty(whenChangedFilter) ? $"(objectClass=user)" : $"(&(objectClass=user)(whenChanged>={whenChangedFilter}))",
            ObjectType.Group => string.IsNullOrEmpty(whenChangedFilter) ? "(objectClass=group)" : $"(&(objectClass=group)(whenChanged>={whenChangedFilter}))",
            _ => ""
        };

        return ldapfilter;
    }
    private static string FetchObjectsMessage(ObjectType objectType, List<string> list)
    {
        var sb = new StringBuilder();
        list.ForEach(x => sb.Append($"fetch {objectType} {x}.{Environment.NewLine}"));
        return sb.ToString();
    }

    private static string SendingObjectsRequestMessage(int count, ObjectType objectType)
    {
        return $"Sending ${count} {objectType}s request.{Environment.NewLine}";
    }

    private static void ReportFetchObjects(ObjectType objectType, List<string> dnList, int i, IProgress<Status>? progress)
    {
        progress?.Report(new($"{FetchObjectsMessage(objectType, dnList)} id: {i} {Environment.NewLine}", ""));
        dnList.Clear();
    }

    #endregion

    private static async Task<string> GetSerializedObject(object obj)
    {
        using var memStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memStream, obj);
        var json = await Task.Run(() => Encoding.UTF8.GetString(memStream.GetBuffer()));
        return json;
    }
}
#pragma warning restore CA1416