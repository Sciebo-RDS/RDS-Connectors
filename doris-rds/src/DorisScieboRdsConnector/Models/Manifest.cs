using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Models;

class Manifest{
  public string? eduPersonPrincipalName;
  public string? projectId;
  public string? domain;
  public string? label;
  private IEnumerable<Models.File> files;

  public Manifest(string projectId, string eduPersonPrincipalName, string domain, string label){
    this.files = new List<Models.File>();
  }

  public IEnumerable<Models.File> Files
  {
    get {
      return this.files;
    }
    set {
      this.files = value;
    }
  }

  public JsonObject ToGraph(){
    return new JsonObject();
  }
}