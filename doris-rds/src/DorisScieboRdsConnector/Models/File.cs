using System;

namespace DorisScieboRdsConnector.Models;

public record File(
    string Id,
    long? ContentSize = null,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Md5 = null,
    Uri? Url = null);
