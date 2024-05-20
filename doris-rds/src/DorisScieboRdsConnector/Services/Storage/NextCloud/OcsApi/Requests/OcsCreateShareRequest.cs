namespace DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Requests;

public class OcsCreateShareRequest
{
    public string? attributes { get; set; }
    public string? expireDate { get; set; }
    public string? label { get; set; }
    public string? note { get; set; }
    public string? password { get; set; }
    public string? path { get; set; }
    public int? permissions { get; set; }
    public bool? publicUpload { get; set; }
    public bool? sendPasswordByTalk { get; set; }
    public int? shareType { get; set; }
    public string? shareWith { get; set; }
}