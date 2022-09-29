using System;

namespace DorisScieboRdsConnector.Models;

public record File(
    string Id,
    long? ContentSize,
    DateTime? DateCreated,
    DateTime? DateModified,
    string EncodingFormat,
    string Sha256,
    Uri Url);
