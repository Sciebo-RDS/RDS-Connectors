using Microsoft.AspNetCore.Http;

namespace DorisScieboRdsConnector.Models;

public record AddFileRequest(
    PortUserName UserId,
    IFormFile Files,
    string FileName,
    string Folder);
