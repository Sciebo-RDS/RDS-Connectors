using System.Collections.Generic;
using System.Text.Json.Nodes;
using DorisScieboRdsConnector.Models;

namespace DorisScieboRdsConnector.Helpers;

public static class RoCrateHelper
{
    public static JsonObject GenerateRoCrateManifest(
        string projectId, 
        string domain, 
        string eduPersonPrincipalName, 
        IEnumerable<File> files)
    {

        var graph = new JsonArray();
        
        graph.Add(new JsonObject
        {
            ["@type"] = "CreativeWork",
            ["@id"] = "ro-crate-metadata.json",
            ["identifier"] = System.Guid.NewGuid(),
            ["alternateName"] = projectId,
            ["conformsTo"] = new JsonObject
            {
                ["@id"] = "https://w3id.org/ro/crate/1.1"
            },
            ["about"] = new JsonObject
            {
                ["@id"] = "./"
            },
            ["publisher"] = new JsonObject
            {
                ["@id"] = "https://" + domain
            },
            ["creator"] = new JsonArray
            {
                new JsonObject
                {
                    ["@id"] = "https://" + domain + "#" + eduPersonPrincipalName
                }
            }
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "Organization",
            ["@id"] = "https://" + domain,
            ["identifier"] = new JsonArray
            {
                new JsonObject
                {
                    ["@id"] = "#domain-0" 
                }
            },
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["@id"] = "#domain-0",
            ["propertyID"] = "domain",
            ["value"] = domain 
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = "https://" + domain + "#" + eduPersonPrincipalName,
            ["identifier"] = new JsonArray
            {
                new JsonObject
                {
                    ["@id"] = "#eduPersonPrincipalName-0" //reference to PropertyValue holding edugain id
                }
            }
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["@id"] = "#eduPersonPrincipalName-0",
            ["propertyID"] = "eduPersonPrincipalName",
            ["value"] = eduPersonPrincipalName
        });

        var hasPart = new JsonArray();

        foreach (var file in files)
        {
            hasPart.Add(new JsonObject
            {
                ["@id"] = file.Id
            });

            var fileObject = new JsonObject
            {
                ["@type"] = "File",
                ["@id"] = file.Id,
            };

            if (file.ContentSize != null) fileObject["contentSize"] = file.ContentSize;
            if (file.DateCreated != null) fileObject["dateCreated"] = file.DateCreated;
            if (file.DateModified != null) fileObject["dateModified"] = file.DateModified;
            if (file.EncodingFormat != null) fileObject["encodingFormat"] = file.EncodingFormat;
            if (file.Sha256 != null) fileObject["sha256"] = file.Sha256;
            if (file.Url != null) fileObject["url"] = file.Url.AbsoluteUri;

            graph.Add(fileObject);
        }

        graph.Add(new JsonObject
        {
            ["@type"] = "Dataset",
            ["@id"] = "./",
            ["hasPart"] = hasPart
        });

        var roCrate = new JsonObject
        {
            ["@context"] = "https://w3id.org/ro/crate/1.1/context",
            ["@graph"] = graph
        };

        return roCrate;
    }
}