namespace DorisScieboRdsConnector.Models;

using Microsoft.AspNetCore.Http;

public record AddFileRequest(
    PortUserName UserId,
    IFormFile Files,
    string FileName,
    string Folder);
