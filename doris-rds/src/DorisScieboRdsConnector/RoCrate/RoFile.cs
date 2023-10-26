namespace DorisScieboRdsConnector.RoCrate;

using System;

public record RoFile(
    string Id,
    long ContentSize,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Sha256 = null,
    Uri? Url = null);
