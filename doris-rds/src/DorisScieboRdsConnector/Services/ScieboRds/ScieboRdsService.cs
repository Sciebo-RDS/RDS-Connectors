namespace DorisScieboRdsConnector.Services.ScieboRds;

using DorisScieboRdsConnector.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class ScieboRdsService : IScieboRdsService
{
    private readonly HttpClient httpClient;
    private readonly ScieboRdsConfiguration configuration;

    private const string icon = "data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4KPCEtLSBHZW5lcmF0b3I6IEFkb2JlIElsbHVzdHJhdG9yIDIzLjEuMCwgU1ZHIEV4cG9ydCBQbHVnLUluIC4gU1ZHIFZlcnNpb246IDYuMDAgQnVpbGQgMCkgIC0tPgo8c3ZnIHZlcnNpb249IjEuMSIgaWQ9IkxhZ2VyXzEiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyIgeG1sbnM6eGxpbms9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkveGxpbmsiIHg9IjBweCIgeT0iMHB4IiB2aWV3Qm94PSIwIDAgMTY2IDEzMiIgc3R5bGU9ImVuYWJsZS1iYWNrZ3JvdW5kOm5ldyAwIDAgMTY2IDEzMjsiIHhtbDpzcGFjZT0icHJlc2VydmUiPgo8c3R5bGUgdHlwZT0idGV4dC9jc3MiPgoJLnN0MHtmaWxsOiNDN0Q5RTU7fQoJLnN0MXtmaWxsOiM2NjlERDM7fQoJLnN0MntmaWxsOiNFNTQ2MkE7fQo8L3N0eWxlPgo8Zz4KCTxnPgoJCTxnPgoJCQk8Zz4KCQkJCTxnPgoJCQkJCTxwYXRoIGNsYXNzPSJzdDAiIGQ9Ik0xMTUuMiw5My40YzAuNCwxLDAuMywyLjUtMC40LDRjLTAuOSwyLTIuNiwzLjItMy44LDIuOGMtMi40LDMtNS4zLDUuNS04LjUsNy4zYy0wLjcsMC40LTEuNSwwLjctMi4yLDEuMSAgICAgICBjLTEuMSwwLjQtMi4yLDAuOC0zLjMsMS4xYy0xLjQsMC4zLTIuOSwwLjUtNC4zLDAuNmMtMS4xLDAuMS0yLjIsMC0zLjMtMC4xYy0xLjctMC4yLTMuNC0wLjUtNS4xLTFjLTAuMSwwLjEtMC4yLDAuMi0wLjMsMC4zICAgICAgIGMtMC4xLDAtMC4xLDAuMS0wLjIsMC4xYy0xLjksMS4zLTUsMC42LTctMS42Yy0wLjktMS0xLjQtMi4xLTEuNi0zLjFjLTQuMy0yLjktMTMuMS0xMi43LTE3LjUtMTkuOGg1LjkgICAgICAgYzMuNCw1LjIsMTEuMywxMy41LDE0LjEsMTUuN2MxLjktMC41LDQuMywwLjMsNiwyLjFjMC42LDAuNywxLjEsMS40LDEuMywyLjJjMi4xLDAuOCw0LjIsMS40LDYuMiwxLjZjMC45LDAuMSwxLjksMC4xLDIuOCwwICAgICAgIGMwLjYsMCwxLjMtMC4xLDEuOS0wLjJjMS0wLjIsMS45LTAuNCwyLjgtMC43YzEtMC40LDItMC44LDMtMS40YzMuMi0xLjgsNS45LTQuMiw4LjItNi45YzAuMS0wLjcsMC4zLTEuNCwwLjYtMi4xICAgICAgIGMwLjctMS40LDEuNy0yLjUsMi43LTIuOGMwLjYtMS4xLDEuMS0xLjksMS42LTMuM2MwLjUtMS41LDEuMS0yLjgsMS40LTQuMmgxLjhDMTE3LjIsODcuNCwxMTYsOTEuNywxMTUuMiw5My40eiIvPgoJCQkJCTxwYXRoIGNsYXNzPSJzdDAiIGQ9Ik0xMTMuOCw0MC41aC0xLjVjLTIuMS01LjEtNy4yLTEyLjEtNy40LTEyLjVjLTAuOC0xLjQtMS44LTIuNy0yLjgtMy45Yy0xLDAuMy0yLjUtMC41LTMuOC0xLjkgICAgICAgYy0xLTEuMS0xLjUtMi40LTEuNS0zLjRjLTguMi02LjgtMTguNi05LjMtMjYuNC00LjhjLTAuOSwwLjUtMS45LDEuMi0yLjcsMS45YzAsMCwwLDAsMCwwYzAsMC42LTAuMiwxLjMtMC41LDEuOSAgICAgICBjLTAuMywwLjYtMC44LDEuMy0xLjQsMS45Yy0xLjEsMS4xLTIuNCwxLjktMy41LDIuMmMtMS43LDIuNi01LjksMTMuNi02LjgsMTguNWgtNC43YzEtNS4xLDUuOS0xNi45LDguMi0xOS45ICAgICAgIGMtMC4zLTEuMywwLjMtMy4xLDEuOC00LjdjMC40LTAuNSwwLjktMC44LDEuNC0xLjJjMC44LTAuNSwxLjYtMC45LDIuNC0xLjFjMC43LTAuMiwxLjQtMC4xLDIsMC4xYzAuNC0wLjIsMC44LTAuNSwxLjItMC43ICAgICAgIGMxMC45LTUuNiwyMS4zLTMuMSwzMC4xLDQuNGMxLTAuMywyLjUsMC41LDMuOCwyYzAuOSwxLjEsMS41LDIuNCwxLjUsMy4zQzEwNy4xLDI3LjcsMTEwLjgsMzMuOCwxMTMuOCw0MC41eiIvPgoJCQkJPC9nPgoJCQk8L2c+CgkJCTxnPgoJCQkJPGc+CgkJCQkJPHBhdGggY2xhc3M9InN0MSIgZD0iTTk2LjUsODQuOWMtMC4xLDQtMC40LDEzLjktMi45LDIxLjZjLTAuNCwxLjMtMC45LDIuNi0xLjQsMy43Yy0wLjMsMC42LTAuNiwxLjEtMC45LDEuNyAgICAgICBjLTAuNiwwLjktMS4xLDEuOS0xLjcsMi43Yy0yLjIsMy4xLTQuNyw1LjctNy42LDcuNmMtMC4zLDEuOS0yLjUsMy43LTUuNCw0LjRjLTEuMSwwLjItMi4yLDAuMy0zLjIsMC4yICAgICAgIGMtMC45LTAuMS0xLjctMC4zLTIuMy0wLjVjLTkuOSwxLjQtMjAtMS44LTI5LjktOS41Yy0xLjUsMC4yLTQuOCwwLjEtNy42LTEuOGMtNC40LTMtNy04LjUtNi41LTEyLjFDMjEuNyw5NiwxNyw4OCwxMy41LDc4LjkgICAgICAgYy0zLjgtMC44LTcuMS00LjgtNy40LTkuM0M1LjksNjYuOCw3LDY0LjQsOC43LDYzQzYuNiw1Mi41LDYuNiw0Mi41LDguNCwzMy45Yy0xLjQtMS44LTEuNy00LjktMC42LThjMS4yLTMuMiwzLjYtNS42LDYtNi4xICAgICAgIGMzLjgtNi4xLDktMTAuNiwxNS42LTEyLjhjNC44LTEuNiw5LjktMS44LDE1LjEtMC44YzEuMS0xLjIsMy43LTEuNSw2LjMtMC42YzIuNCwwLjksNCwyLjYsNCw0LjFjMi4zLDEuMSw0LjgsMi40LDcuNCw0ICAgICAgIGMxLjEsMC43LDIuMywxLjQsMy40LDIuMkM3My41LDIxLjMsODEuNCwyOSw4Niw0MC41aC0xLjhjLTQuOS0xMC4zLTExLjYtMTcuNy0xOS4xLTIyLjZjLTEuNy0xLjItMy41LTIuMi01LjMtMy4xICAgICAgIGMtMi4yLTEuMS00LjUtMi02LjgtMi44Yy0xLjMsMC41LTMuMSwwLjQtNC45LTAuMmMtMS0wLjQtMS44LTAuOC0yLjUtMS40Yy0zLjgtMC40LTcuNSwwLTExLDEuMWMtNi43LDIuMi0xMS44LDYuOS0xNS4yLDEzLjQgICAgICAgYzAuMSwxLjQtMC4xLDIuOS0wLjcsNC40Yy0wLjYsMS42LTEuNSwzLjEtMi42LDQuMUMxMy44LDQyLDEzLjcsNTIuMSwxNiw2Mi44YzIuNywxLjYsNC43LDQuOCw1LDguM2MwLjEsMS43LTAuMiwzLjItMC44LDQuNCAgICAgICBjMCwwLjMsMCwwLjYsMCwwLjljMy41LDguOSw3LjgsMTYuMSwxMy4yLDIyLjdjMy4yLTAuMiw2LjEsMS44LDcuMiwyLjVjMy41LDIuMyw1LjMsNi40LDUuMSw5LjNjOC42LDYuMywxNy45LDkuNCwyNi41LDguNCAgICAgICBjMC44LTAuNCwxLjYtMC44LDIuNi0xYzAuOS0wLjIsMS44LTAuMywyLjctMC4yYzMuMy0xLjEsNi4xLTIuOSw4LjYtNS4yYzAuOC0wLjcsMS41LTEuNSwyLjItMi4zYzAuMi0wLjIsMC4zLTAuMywwLjQtMC41ICAgICAgIGMwLjgtMSwxLjQtMi4yLDItMy43YzIuNy02LjcsMy41LTE3LjMsMy43LTIxLjVIOTYuNXoiLz4KCQkJCQk8cGF0aCBjbGFzcz0ic3QxIiBkPSJNODQuMyw0NS4zIi8+CgkJCQk8L2c+CgkJCTwvZz4KCQk8L2c+CgkJPGc+CgkJCTxnPgoJCQkJPHBhdGggZD0iTTM4LjIsNDYuOWw4LjMsMGM5LjYsMC4xLDE1LjIsNS4zLDE1LjIsMTUuNnMtNS44LDE1LjctMTUuMSwxNS43bC04LjYsMEwzOC4yLDQ2Ljl6IE00Niw3My43YzYuMywwLDkuOS0zLjUsMTAtMTEuMiAgICAgIHMtMy42LTExLTkuOS0xMS4xbC0yLjQsMGwtMC4xLDIyLjNMNDYsNzMuN3oiLz4KCQkJCTxwYXRoIGQ9Ik05Ny44LDQ3LjJsMTAuNSwwLjFjNi41LDAsMTEuNSwyLjQsMTEuNSw5LjNjMCw2LjctNS4xLDkuOC0xMS42LDkuN2wtNSwwbC0wLjEsMTIuMmwtNS41LDBMOTcuOCw0Ny4yeiBNMTA3LjcsNjEuOSAgICAgIGM0LjMsMCw2LjYtMS43LDYuNy01LjNjMC0zLjYtMi4zLTQuOS02LjYtNC45bC00LjQsMGwtMC4xLDEwLjJMMTA3LjcsNjEuOXogTTEwNy4zLDY0LjhsNC0zLjNsOS43LDE3LjFsLTYuMiwwTDEwNy4zLDY0Ljh6Ii8+CgkJCQk8cGF0aCBkPSJNMTI2LjIsNDcuNGw1LjYsMGwtMC4yLDMxLjNsLTUuNiwwTDEyNi4yLDQ3LjR6Ii8+CgkJCQk8cGF0aCBkPSJNMTM3LjQsNzQuN2wzLjMtMy44YzIuMiwyLjEsNS4yLDMuNiw4LjEsMy42YzMuNSwwLDUuNS0xLjYsNS41LTRjMC0yLjYtMi0zLjQtNC44LTQuNmwtNC4zLTEuOSAgICAgIGMtMy4xLTEuMy02LjMtMy44LTYuMy04LjNjMC01LDQuNS04LjgsMTAuNi04LjhjMy43LDAsNy4yLDEuNiw5LjYsNGwtMi45LDMuNWMtMi0xLjctNC4xLTIuNy02LjgtMi43Yy0zLDAtNC45LDEuNC00LjksMy42ICAgICAgYzAsMi41LDIuNCwzLjQsNSw0LjVsNC4yLDEuOGMzLjcsMS42LDYuMywzLjksNi4zLDguNWMwLDUuMS00LjMsOS4zLTExLjMsOS4yQzE0NC40LDc5LjMsMTQwLjMsNzcuNiwxMzcuNCw3NC43eiIvPgoJCQk8L2c+CgkJCTxwYXRoIGQ9Ik02Ni4zLDYyLjljMC4xLTEwLjEsNS41LTE2LjEsMTMuMi0xNmM3LjgsMCwxMy4xLDYuMSwxMy4xLDE2LjJjLTAuMSwxMC4xLTUuNSwxNi4zLTEzLjIsMTYuMyAgICAgQzcxLjYsNzkuMyw2Ni4zLDczLjEsNjYuMyw2Mi45eiBNODcuMiw2My4xYzAtNy0zLTExLjMtNy43LTExLjNjLTQuNywwLTcuNyw0LjItNy44LDExLjJjMCw3LDMsMTEuNSw3LjcsMTEuNiAgICAgQzg0LjEsNzQuNiw4Ny4yLDcwLjEsODcuMiw2My4xeiIvPgoJCTwvZz4KCTwvZz4KCTxlbGxpcHNlIHRyYW5zZm9ybT0ibWF0cml4KDAuMjk3MSAtMC45NTQ4IDAuOTU0OCAwLjI5NzEgLTE3LjA2MzkgMzEuNzk1NCkiIGNsYXNzPSJzdDIiIGN4PSIxMy4xIiBjeT0iMjcuNSIgcng9IjguNyIgcnk9IjYuNSIvPgo8L2c+Cjwvc3ZnPg==";

    private const string metadataProfile = """
{
    "metadata": {
        "name": "DORIS Profile",
        "description": "DORIS Profile",
        "version": 0.1,
        "warnMissingProperty": true
    },
    "classes": {
        "Dataset": {
            "definition": "override",
            "subClassOf": [],
            "inputs": []
        }
    },
    "enabledClasses": ["Dataset"]
    "lookup": {}
}
""";

    public ScieboRdsService(HttpClient httpClient, IOptions<ScieboRdsConfiguration> configuration)
    {
        this.httpClient = httpClient;
        this.configuration = configuration.Value;
    }

    public async Task RegisterConnector()
    {
        var response = await httpClient.PostAsJsonAsync(configuration.TokenStorageUrl + "/service", new
        {
            type = "LoginService",
            data = new
            {
                servicename = configuration.ConnectorServiceName,
                implements = new[] { "metadata" },
                loginMode = 0, // credentials
                credentials = new
                {
                    userId = false,
                    password = false
                },
                fileTransferMode = 0, // active
                fileTransferArchive = 0, // none
                description = new
                {
                    en = "Connector for publishing file metadata to Doris",
                    sv = "Connector för filmetadata till Doris"
                },
                icon = icon,
                infoUrl = "https://doris.snd.se",
                helpUrl = "https://doris.snd.se",
                displayName = "Doris",
                metadataProfile = metadataProfile,
                //projectLinkTemplate = ""
            }
        });

        response.EnsureSuccessStatusCode();
    }
}
