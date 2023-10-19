namespace DorisScieboRdsConnector.Services.Storage.OcsApi.Responses;

public record OcsResponse<T>(Ocs<T> ocs);