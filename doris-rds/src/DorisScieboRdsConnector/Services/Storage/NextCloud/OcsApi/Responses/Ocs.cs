namespace DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Responses;

public record Ocs<TData>(
    OcsMeta meta,
    TData data);
