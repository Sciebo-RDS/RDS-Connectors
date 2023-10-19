namespace DorisScieboRdsConnector.Services.Storage.OcsApi.Responses;

public record OcsMeta(
    string status,
    int statuscode,
    string? message);
