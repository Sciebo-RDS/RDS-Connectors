namespace DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Requests;

public class OcsGetSharesRequest
{
    public bool? include_tags { get; set; }
    public string? path { get; set; }
    public bool? reshares { get; set; }
    public bool? shared_with_me { get; set; }
    public bool? subfiles { get; set; }
}
