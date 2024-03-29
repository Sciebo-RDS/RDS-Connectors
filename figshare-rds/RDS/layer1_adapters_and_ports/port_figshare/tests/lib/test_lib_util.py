import unittest

from lib.Util import from_jsonld, to_jsonld
from constant import req, result
import copy

jsonld = {
    "https://schema.org/creator": [
        {
            "https://schema.org/affiliation": "WWU",
            "https://schema.org/name": "Peter Heiss",
        },
        {"https://schema.org/name": "Jens Stegmann"},
    ],
    "https://www.research-data-services.org/jsonld/figsharecategory": "publication/thesis",
    "https://schema.org/name": "testtitle",
    "https://schema.org/description": "Beispieltest. Ganz viel<br><br>asd mit umbruch",
    "https://www.research-data-services.org/jsonld/doi": "10.5072/figshare.1234",
    "https://schema.org/identifier": 1234,
    "https://schema.org/publicAccess": True,
    "https://schema.org/datePublished": "2020-09-29T22:00:00.000Z",
}


class TestJsonLd(unittest.TestCase):
    def test_from_jsonld_multiple_creator(self):
        self.assertEqual(from_jsonld(req["metadata"]), result)

    def test_from_jsonld_single_creator(self):
        singleld = copy.deepcopy(result)
        del singleld["creators"][1]
        singlereq = copy.deepcopy(req)
        del singlereq["metadata"]["@graph"][1]["creator"][1]
        del singlereq["metadata"]["@graph"][4]

        self.assertEqual(from_jsonld(singlereq["metadata"]), singleld)

        singleld = copy.deepcopy(result)
        del singleld["creators"][0]
        singlereq = copy.deepcopy(req)
        del singlereq["metadata"]["@graph"][1]["creator"][0]
        del singlereq["metadata"]["@graph"][2]

        self.assertEqual(from_jsonld(singlereq["metadata"]), singleld)

    def test_to_jsonld(self):
        self.assertDictContainsSubset(to_jsonld(result), jsonld)

