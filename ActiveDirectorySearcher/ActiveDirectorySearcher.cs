using System.Collections;
using System.DirectoryServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActiveDirectorySearcher.DTOs;

namespace ActiveDirectorySearcher;

#pragma warning disable CA1416 //suppress windows warning 
public class ActiveDirectoryHelper
{
    public static void GetADObjects(InputCreds inputCreds, IProgress<Status>? progress, ObjectType objectType, CancellationToken cancellationToken)
    {

        var currentTime = DateTime.Now;
        var whenChangedFilter = currentTime.AddMinutes(-1000).ToString("yyyyMMddHHmmss.0Z");
        //var adObjectsList = new List<T>();
        var objectsList = new List<SearchResult>();


        var path = $"LDAP://{inputCreds.Domain}{(inputCreds.Port is 0 ? "" : $":{inputCreds.Port}")}";
        using var root = string.IsNullOrEmpty(inputCreds.UserName) ? new DirectoryEntry(path) : new DirectoryEntry(path, inputCreds.UserName, inputCreds.Password);
        _ = root.Name; // checking connection; will throw if connection is not succesful

        using var searcher = new DirectorySearcher(root);
        searcher.Filter = PrepareLdapQuery(objectType, "");
        searcher.PageSize = 500;
        searcher.SizeLimit = 0;

        using var results = searcher?.FindAll();
        var resultsEnumerator = results?.GetEnumerator();

        if (resultsEnumerator != null)
        {
            var dnList = new List<string>();
            for (int i = 0; resultsEnumerator.MoveNext(); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = (SearchResult)resultsEnumerator.Current;
                objectsList.Add(result);
                //string json = JsonSerializer.Serialize(result);
                var distinguishedName = result.Properties["distinguishedName"][0] as string ?? "";
                dnList.Add(distinguishedName);
                if ((i + 1) % 20 == 0)
                {
                    progress?.Report(new(FetchObjectsMessage(dnList) + " id:" + (i + 1), ""));
                    dnList.Clear();
                }

                #region Sending Objects
                if ((i + 1) % 1000 == 0)
                {
                    progress?.Report(new("", SendingObjectsRequestMessage(objectsList.Count, objectType)));
                    var json = JsonSerializer.Serialize(objectsList);

                    progress?.Report(new("", SendingObjectsRequestMessage(1000, objectType)));

                    objectsList.Clear();
                }
                #endregion
            }

        }
    }

    public static bool TestConnection(InputCreds inputCreds)
    {
        var path = $"LDAP://{inputCreds.Domain}{(inputCreds.Port is 0 ? "" : $":{inputCreds.Port}")}";
        using var root = string.IsNullOrEmpty(inputCreds.UserName) ? new DirectoryEntry(path) : new DirectoryEntry(path, inputCreds.UserName, inputCreds.Password);
        _ = root.Name; // checking connection; will throw if connection is not succesful
        return true;
    }

    #region Private static helper methods
    private static string PrepareLdapQuery(ObjectType objectType, string whenChangedFilter)
    {
        string ldapfilter = objectType switch
        {
            ObjectType.User => string.IsNullOrEmpty(whenChangedFilter) ? "(objectClass=user)" : $"(&(objectClass=user)(whenChanged>={whenChangedFilter}))",
            ObjectType.Group => string.IsNullOrEmpty(whenChangedFilter) ? "(objectClass=group)" : $"(&(objectClass=group)(whenChanged>={whenChangedFilter}))",
            _ => ""
        };

        return ldapfilter;
    }
    private static string FetchObjectsMessage(List<string> list)
    {
        var sb = new StringBuilder();
        list.ForEach(x => sb.Append($"fetched {x}.{Environment.NewLine}"));
        return sb.ToString();
    }

    private static string SendingObjectsRequestMessage(int count, ObjectType objectType)
    {
        return $"Sending ${count} {objectType}s request.";
    }

    private static double PercentageProcessed(int i, int total)
    {
        return ((double)i / total) * 100;
    }
    #endregion

}
#pragma warning restore CA1416