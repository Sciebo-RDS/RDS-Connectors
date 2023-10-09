using System;

namespace DorisScieboRdsConnector.Models;

public record File(
    string Id,
    string? ContentSize = null,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Sha256 = null,
    Uri? Url = null);
