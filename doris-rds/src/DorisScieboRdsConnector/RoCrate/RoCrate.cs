namespace DorisScieboRdsConnector.RoCrate;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public class RoCrate
{
    public string EduPersonPrincipalName { get; }
    public string ProjectId { get; }
    public string Domain { get; }

    public IEnumerable<RoFile> Files { get; }

    public RoCrate(string projectId, string eduPersonPrincipalName, string domain, IEnumerable<RoFile> files)
    {
        EduPersonPrincipalName = eduPersonPrincipalName;
        ProjectId = projectId;
        Domain = domain;
        Files = files;
    }

    public JsonObject ToGraph()
    {
        var graph = new JsonArray();

        graph.Add(new JsonObject
        {
            ["@type"] = "CreativeWork",
            ["@id"] = "ro-crate-metadata.json",
            ["identifier"] = Guid.NewGuid(),
            ["alternateName"] = ProjectId,
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
                ["@id"] = $"https://{Domain}"
            },
            ["creator"] = new JsonObject
            {
                ["@id"] = $"https://{Domain}#{EduPersonPrincipalName}"
            }
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "Organization",
            ["@id"] = $"https://{Domain}",
            ["identifier"] = new JsonObject
            {
                ["@id"] = $"#domain-{Domain}"
            }
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["@id"] = $"#domain-{Domain}",
            ["propertyID"] = "domain",
            ["value"] = Domain
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = $"https://{Domain}#{EduPersonPrincipalName}",
            ["identifier"] = new JsonObject
            {
                ["@id"] = "#eduPersonPrincipalName-0" //reference to PropertyValue holding edugain id
            }
        });

        graph.Add(new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["@id"] = "#eduPersonPrincipalName-0",
            ["propertyID"] = "eduPersonPrincipalName",
            ["value"] = EduPersonPrincipalName
        });

        var hasPart = new JsonArray();

        foreach (var file in Files)
        {
            string id = string.Join('/', file.Id.Split('/').Select(Uri.EscapeDataString));

            hasPart.Add(new JsonObject
            {
                ["@id"] = id
            });

            var fileObject = new JsonObject
            {
                ["@type"] = "File",
                ["@id"] = id,
                ["additionalType"] = file.Type.ToString(),
                ["contentSize"] = file.ContentSize.ToString()
            };

            if (file.DateModified != null) fileObject["dateModified"] = file.DateModified;
            if (file.EncodingFormat != null) fileObject["encodingFormat"] = file.EncodingFormat;
            if (file.Url != null) fileObject["url"] = file.Url.AbsoluteUri;

            graph.Add(fileObject);
        }

        graph.Add(new JsonObject
        {
            ["@type"] = "Dataset",
            ["@id"] = "./",
            ["hasPart"] = hasPart
        });

        return new JsonObject
        {
            ["@context"] = "https://w3id.org/ro/crate/1.1/context",
            ["@graph"] = graph
        };
    }
}