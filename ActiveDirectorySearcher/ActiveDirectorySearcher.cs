﻿using System;
using System.Collections;
using System.Diagnostics;
using System.DirectoryServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActiveDirectorySearcher.DTOs;

namespace ActiveDirectorySearcher;

#pragma warning disable CA1416 //suppress windows warning 
public class ActiveDirectoryHelper
{
    public static async Task GetADObjects(Int64 licenseID, InputCreds inputCreds, IProgress<Status>? progress, ObjectType objectType, CancellationToken cancellationToken)
    {
        progress?.Report(new($"Processing {objectType}. {Environment.NewLine}", ""));
        var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string filePath = objectType switch
        {
            ObjectType.User => Path.Combine(basePath ?? "", "Info", "UserReplicationTime.txt"),
            ObjectType.Group => Path.Combine(basePath ?? "", "Info", "GroupReplicationTime.txt"),
            _ => ""
        };

        var currReplicationTime = DateTime.Now.ToUniversalTime().ToString();
        var lastReplicationTime = File.ReadAllText(filePath);
        var whenChangedFilter = string.IsNullOrEmpty(lastReplicationTime) ? "" : DateTime.Parse(lastReplicationTime).ToString("yyyyMMddHHmmss.0Z");
        var objectsList = new List<SearchResult>();

        var path = $"LDAP://{inputCreds.Domain}{(inputCreds.Port is 0 ? "" : $":{inputCreds.Port}")}";
        using var root = string.IsNullOrEmpty(inputCreds.UserName) ? new DirectoryEntry(path) : new DirectoryEntry(path, inputCreds.UserName, inputCreds.Password);
        _ = root.Name; // checking connection; will throw if connection is not succesful

        using var searcher = new DirectorySearcher(root);
        searcher.Filter = PrepareLdapQuery(objectType, whenChangedFilter);
        searcher.PageSize = 500;
        searcher.SizeLimit = 0;

        using var results = searcher?.FindAll();
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

                //string json = JsonSerializer.Serialize(result);
                var distinguishedName = result.Properties["distinguishedName"][0] as string ?? "";
                dnList.Add(distinguishedName);

                if ((i + 1) % 20 == 0)
                    ReportFetchObjects(objectType, dnList, i + 1, progress);

                #region Sending Objects
                if ((i + 1) % 1000 == 0)
                    await SendObjectListToWebService(licenseID, objectsList, objectType, progress);

                #endregion
            }
            if (dnList.Count > 0)
                ReportFetchObjects(objectType, dnList, i, progress);

            if (objectsList.Count > 0)
                await SendObjectListToWebService(licenseID, objectsList, objectType, progress);


        }
        File.WriteAllText(filePath, currReplicationTime);
    }

    public static bool TestConnection(InputCreds inputCreds)
    {
        var path = $"LDAP://{inputCreds.Domain}{(inputCreds.Port is 0 ? "" : $":{inputCreds.Port}")}";
        using var root = string.IsNullOrEmpty(inputCreds.UserName) ? new DirectoryEntry(path) : new DirectoryEntry(path, inputCreds.UserName, inputCreds.Password);
        _ = root.Name; // checking connection; will throw if connection is not succesful
        return true;
    }

    #region Private static helper methods
    private static async Task SendObjectListToWebService(Int64 licenseID, List<SearchResult> objectsList, ObjectType objectType, IProgress<Status>? progress)
    {
        progress?.Report(new("", SendingObjectsRequestMessage(objectsList.Count, objectType)));
        var json = JsonSerializer.Serialize(objectsList);
        string apiUrl = objectType switch
        {
            ObjectType.User => $"https://ns-server.vantagemdm.com/active-directory/sync?licenseId={licenseID}&type=user",
            ObjectType.Group => $"https://ns-server.vantagemdm.com/active-directory/sync?licenseId={licenseID}&type=group",
            _ => ""
        };
        using HttpClient client = new HttpClient();
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

    private static double PercentageProcessed(int i, int total)
    {
        return ((double)i / total) * 100;
    }
    #endregion

}
#pragma warning restore CA1416