namespace DorisScieboRdsConnector.Models;

using System;

public record RoFile(
    string Id,
    RoFileType Type,
    long ContentSize,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Sha256 = null,
    Uri? Url = null);
