using System;

namespace DorisScieboRdsConnector.Models;

public record File(
    string Id,
    ulong? ContentSize,
    DateTime? DateModified,
    string? EncodingFormat,
    string? Md5,
    Uri? Url = null);
