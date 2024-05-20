namespace DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Responses;

public record OcsMeta(
    string status,
    int statuscode,
    string? message);
