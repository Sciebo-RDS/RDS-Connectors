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

    irods_address = None
    if "address" in kwargs:
        irods_address = kwargs["address"]
        del kwargs["address"]

    irods_api_address = None
    if "api_address" in kwargs:
        irods_api_address = kwargs["api_address"]
        del kwargs["api_address"]


    app = App(name, *args, **kwargs)

    app.app.irods_address = irods_address
    app.app.irods_api_address = irods_api_address

    for oai in list_openapi:
        app.add_api(
            oai,
            resolver=MultipleResourceResolver("api", collection_endpoint_name="index"),
            validate_responses=True,
        )

    return app


app = bootstrap("PortIrods", all=True)
