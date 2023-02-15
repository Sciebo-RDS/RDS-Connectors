from connexion_plus import App, MultipleResourceResolver, Util

import logging, os

log_level = os.environ.get("LOGLEVEL", "DEBUG")
logger = logging.getLogger("")
logging.getLogger("").handlers = []
logging.basicConfig(format="%(asctime)s %(message)s", level=log_level)


def bootstrap(name="MicroService", *args, **kwargs):
    list_openapi = Util.load_oai(
        os.getenv(
            "OPENAPI_MULTIPLE_FILES",
            "../../layer2_use_cases/interface_port_metadata.yml"
        )
    )

    figshare_address = None
    if "address" in kwargs:
        figshare_address = kwargs["address"]
        del kwargs["address"]

    figshare_api_address = None
    if "api_address" in kwargs:
        figshare_api_address = kwargs["api_address"]
        del kwargs["api_address"]


    app = App(name, *args, **kwargs)

    app.app.figshare_address = figshare_address
    app.app.figshare_api_address = figshare_api_address

    for oai in list_openapi:
        app.add_api(
            oai,
            resolver=MultipleResourceResolver("api", collection_endpoint_name="index"),
            validate_responses=True,
        )

    return app


app = bootstrap("PortFigshare", all=True)
