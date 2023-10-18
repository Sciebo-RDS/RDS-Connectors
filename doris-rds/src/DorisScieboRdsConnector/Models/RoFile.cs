namespace DorisScieboRdsConnector.Models;

using System;

public record RoFile(
    string Id,
    string? ContentSize = null,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Sha256 = null,
    Uri? Url = null);
