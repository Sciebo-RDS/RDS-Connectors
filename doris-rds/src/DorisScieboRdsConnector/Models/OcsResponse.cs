using System.Collections.Generic;

namespace DorisScieboRdsConnector.Models;

public class OcsGetResponse{
    public OcsGetBody? ocs {get;set;}
}

public class OcsPostResponse{
    public OcsPostBody? ocs {get;set;}
}

public class OcsGetBody{
    public OcsMeta? meta {get;set;}
    public List<OcsShare>? data {get;set;}
}

public class OcsPostBody{
    public OcsMeta? meta {get;set;}
    public OcsShare? data {get;set;}
}

public class OcsMeta
{
    public string? status { get; set; }
    public int statuscode { get; set; }
    public string? message { get; set; }
}


public class OcsShare{
    public string? id { get; set; }
    public int share_type { get; set; }
    public string? uid_owner { get; set; }
    public string? displayname_owner { get; set; }
    public int permissions { get; set; }
    public bool can_edit { get; set; }
    public bool can_delete { get; set; }
    public int stime { get; set; }
    public object? parent { get; set; }
    public object? expiration { get; set; }
    public string? token { get; set; }
    public string? uid_file_owner { get; set; }
    public string? note { get; set; }
    public string? label { get; set; }
    public string? displayname_file_owner { get; set; }
    public string? path { get; set; }
    public string? item_type { get; set; }
    public string? mimetype { get; set; }
    public bool has_preview { get; set; }
    public string? storage_id { get; set; }
    public int storage { get; set; }
    public int item_source { get; set; }
    public int file_source { get; set; }
    public int file_parent { get; set; }
    public string? file_target { get; set; }
    public object? share_with { get; set; }
    public string? share_with_displayname { get; set; }
    public object? password { get; set; }
    public bool send_password_by_talk { get; set; }
    public string? url { get; set; }
    public int mail_send { get; set; }
    public int hide_download { get; set; }
    public object? attributes { get; set; }   
}