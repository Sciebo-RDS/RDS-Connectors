using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Models;

public record OcsGetResponse(
    OcsGetBody ocs);

public record OcsPostResponse(
    OcsPostBody ocs);

public record OcsGetBody(
    OcsMeta meta,
    List<OcsShare> data);

public record OcsPostBody(
    OcsMeta meta,
    OcsShare data);

public record OcsMeta(
    string status,
    int statuscode,
    string? message);

public record OcsShare(
    string id,
    int share_type,
    string? uid_owner,
    string? displayname_owner,
    int permissions,
    bool can_edit,
    bool can_delete,
    long stime,
    object? parent,
    string? expiration,
    string? token,
    string? uid_file_owner,
    string? note,
    string? label,
    string? displayname_file_owner,
    string? path,
    string? item_type,
    string? mimetype,
    bool has_preview,
    string? storage_id,
    int storage,
    int item_source,
    int file_source,
    int file_parent,
    string? file_target,
    string? share_with,
    string? share_with_displayname,
    string? password,
    string? password_expiration_time,
    bool? send_password_by_talk,
    string? url,
    int mail_send,
    int hide_download,
    JsonArray? attributes);