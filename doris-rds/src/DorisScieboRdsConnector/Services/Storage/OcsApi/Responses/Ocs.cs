namespace DorisScieboRdsConnector.Services.Storage.OcsApi.Responses;

public record Ocs<TData>(
    OcsMeta meta,
    TData data);
