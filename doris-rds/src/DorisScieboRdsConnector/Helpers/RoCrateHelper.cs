namespace DorisScieboRdsConnector.Helpers.RoCrateHelper
{
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using DorisScieboRdsConnector.Models;

    public static class RoCrateHelper{

        public static JsonArray generateRoCrateManifest(string projctId, string domain, string eduPersonPrincipalName, File[] files){

            var graph = new JsonArray();
            
            graph.Add(new JsonObject{
                ["@type"] = "CreativeWork",
                ["@id"] = "ro-crate-metadata.json",
                ["identifier"] = "38d0324a-9fa6-11ec-b909-0242ac120002", //must be persistent
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
                    ["@id"] = "https://ror.org/01tm6cn81", //Reference to Organization (could also be a local id)
                },
                ["creator"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["@id"] = "https://orcid.org/0000-0003-4908-2169", //reference to person object, (could also be a local id)
                    }
                }
            });

            return graph;
        }


    }
}
