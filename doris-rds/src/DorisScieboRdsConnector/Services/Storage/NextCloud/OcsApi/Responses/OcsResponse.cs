namespace DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Responses;

public record OcsResponse<T>(Ocs<T> ocs);