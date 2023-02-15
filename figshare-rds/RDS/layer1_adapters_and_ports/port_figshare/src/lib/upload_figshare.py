import hashlib
import inspect
import requests
import json
import os
import logging
from flask import abort
import functools
import time
from requests.exceptions import HTTPError

log = logging.getLogger()


def _rate_limit(func=None, per_second=1):
    """Limit number of requests made per second.

    Will sleep for 1/``per_second`` seconds if the last request was
    made too recently.
    """

    if not func:
        return functools.partial(_rate_limit, per_second=per_second)

    @functools.wraps(func)
    def wrapper(self, *args, **kwargs):
        if hasattr(self, "last_request") and self.last_request is not None:
            now = time.time()
            delta = now - self.last_request
            if delta < (1 / per_second):
                waittimer = (1 / per_second) - delta
                log.debug("rate limiter wait for {}ms", waittimer)
                time.sleep(waittimer)

        self.last_request = time.time()
        return func(self, *args, **kwargs)

    return wrapper


class Figshare(object):

    def __init__(self, api_key, api_address=None, *args, **kwargs):
        self.figshare_api_address = api_address
        if api_address is None:
            self.figshare_api_address = os.getenv(
                "FIGSHARE_API_ADDRESS", "https://api.figshare.com/v2"
            )

        self.api_key = api_key

        # monkeypatching all functions with internals
        self.get_article = self.get_article_internal
        self.create_new_article = self.create_new_article_internal
        self.remove_article = self.remove_article_internal
        self.upload_new_file_to_article = self.upload_new_file_to_article_internal
        self.change_metadata_in_article = self.change_metadata_in_article_internal
        self.publish_article = self.publish_article_internal
        self.delete_all_files_from_article = (
            self.delete_all_files_from_article_internal
        )

    # start helper functions as per the Figshare documentation
    # slightly modified into methods
    # added comments and doc strings
    # https://docs.figshare.com/#upload_files_example_upload_on_figshare

    def raw_issue_request(self, method, url, data=None, binary=False):
        """makes a request with method and url to be specified

        Args:
            method (str): any of the following methods: GET, POST, PUT, DELETE
            url (str): the url for the request
            data (json, optional): payload for the request. Defaults to None.
            binary (bool, optional): if set to True, then binary data can be passed to the data argument. Defaults to False.

        Raises:
            HTTPError: error raised if request fails

        Returns:
            json: the response content.
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        headers = {'Authorization': 'token ' + self.api_key}
        if data is not None and not binary:
            data = json.dumps(data)
        response = requests.request(method, url, headers=headers, data=data)
        try:
            response.raise_for_status()
            try:
                data = json.loads(response.content)
            except ValueError:
                log.error(
                    f"Exception at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
                log.error("Could not load response content as json, therefore we will return it as is.")
                data = response.content
        except HTTPError as error:
            log.error(
                f"Exception at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(f'Caught an HTTPError: {error}')
            log.error(f'Body: {response.content}')
            raise HTTPError
        return data

    def issue_request(self, method, endpoint, *args, **kwargs):
        """calls the raw_issue_request method while formatting the endpoint with the api address
        and passing on additional optional arguments.

        Args:
            method (str): any of the following methods: GET, POST, PUT, DELETE
            endpoint (str): the endpoint that needs to be called on the api.

        Returns:
            json: the response content.
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        return self.raw_issue_request(method, self.figshare_api_address+"{}".format(endpoint), *args, **kwargs)

    def get_file_check_data(self, file_path):
        """will generate a md5 hash of a file and calculate the filesize and then return both.

        Args:
            file_path (str): path to the file

        Returns:
            tuple: hash and filesize
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        with open(file_path, 'rb') as fin:
            md5 = hashlib.md5()
            size = 0
            chunk_size = 1048576
            data = fin.read(chunk_size)
            while data:
                size += len(data)
                md5.update(data)
                data = fin.read(chunk_size)
            return md5.hexdigest(), size

    def initiate_new_upload(self, article_id, file_path):
        """will initiate a new upload by calling an endpoint that will return
        a location (url) for uploading

        Args:
            article_id (_type_): _description_
            file_path (_type_): _description_

        Returns:
            json: result of the call to location
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        endpoint = f'/account/articles/{article_id}/files'
        md5, size = self.get_file_check_data(file_path)
        data = {'name': file_path,
                'md5': md5,
                'size': size}
        result = self.issue_request('POST', endpoint, data=data)
        log.debug(f"Initiated file upload: {result['location']}")
        result = self.raw_issue_request('GET', result['location'])
        return result

    def complete_upload(self, article_id, file_id):
        """Will notify to the API that upload of file(s) has been completed.

        Args:
            article_id (int): the id of the article that was uploaded to
            file_id (int): the id of the file that was uploaded
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        self.issue_request(
            'POST', f'/account/articles/{article_id}/files/{file_id}')

    def upload_parts(self, file_info, file_path):
        """Will upload the possibly multiple parts of a file

        Args:
            file_info (dict): dictionary with relevant file info gotten from the call to the location url as retuirned by initiate_new_upload
            file_path (str): path to the file to be uploaded

        Raises:
            Exception: raised if file cannot be opened or uploaded.

        Returns:
            json: response content of GET request to upload url
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        # retrieve upload url from file_info
        url = '{upload_url}'.format(**file_info)
        result = self.raw_issue_request('GET', url)
        log.debug('Uploading parts:')
        try:
            with open(file_path, 'rb') as fin:
                for part in result['parts']:
                    self.upload_part(file_info, fin, part)
            return result
        except Exception as e:
            log.error(
                f"Exception at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(str(e))
            raise Exception

    def upload_part(self, file_info, stream, part):
        """Will upload one part of a file

        Args:
            file_info (_type_): _description_
            stream (_type_): _description_
            part (_type_): _description_
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        udata = file_info.copy()
        udata.update(part)
        # retrieve upload url from file_info
        url = '{upload_url}/{partNo}'.format(**udata)
        stream.seek(part['startOffset'])
        data = stream.read(part['endOffset'] - part['startOffset'] + 1)
        self.raw_issue_request('PUT', url, data=data, binary=True)
        log.debug(
            'Uploaded part {partNo} from {startOffset} to {endOffset}'.format(**part))

    # end helper functions

    @classmethod
    def get_article(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).get_article(*args, **kwargs)

    @classmethod
    def create_new_article(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).create_new_article_internal(
            *args, **kwargs
        )

    @classmethod
    def remove_article(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).remove_article(*args, **kwargs)

    @classmethod
    def upload_new_file_to_article(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).upload_new_file_to_article(
            *args, **kwargs
        )

    @classmethod
    def change_metadata_in_article(cls, api_key, *args, **kwargs):
        return cls(api_key, *args, **kwargs).change_metadata_in_article_internal(
            *args, **kwargs
        )

    @classmethod
    def publish_article(cls, api_key, *args, **kwargs):
        return cls(api_key).publish_article(*args, **kwargs)

    @classmethod
    def delete_all_files_from_article(cls, api_key, *args, **kwargs):
        return cls(api_key).delete_all_files_from_article_internal(*args, **kwargs)

    @classmethod
    def check_token(cls, api_key, *args, **kwargs):
        """Check the API-Token `api_key`.

        Returns `True` if the token is correct and usable, otherwise `False`."""
        log.debug("Check token: Starts")
        r = cls(api_key, *args, **kwargs).get_article(return_response=True)
        log.debug(f"Check Token: Status Code: {r.status_code}")

        return r.status_code == 200

    @_rate_limit(per_second=5)
    def get_article_internal(
        self, id: int = None, return_response: bool = False, metadataFilter: dict = None
    ):
        """Get all articles for the account, which owns the api-key.

        Args:
            id (int, optional): The article id. Defaults to None.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.
            metadataFilter (dict, optional): filter for selecting what data needs to returned. Defaults to None.

        Returns:
            json: : API response if return_response is set to True

        Example:
            API call:
                GET https://api.figshare.com/v2/account/articles
                Content-Type: application/json
                Authorization: token {{token}}
            API response:
                [
                    {
                        "id": 21631871,
                        "title": "Untitled",
                        "doi": "",
                        "handle": "",
                        "url": "https://api.figshare.com/v2/account/articles/21631871",
                        "published_date": null,
                        "thumb": "",
                        "defined_type": 3,
                        "defined_type_name": "dataset",
                        "group_id": null,
                        "url_private_api": "https://api.figshare.com/v2/account/articles/21631871",
                        "url_public_api": "https://api.figshare.com/v2/articles/21631871",
                        "url_private_html": "https://figshare.com/account/articles/21631871",
                        "url_public_html": "https://figshare.com/articles/dataset/_/21631871",
                        "timeline": {},
                        "resource_title": null,
                        "resource_doi": null
                    },
                    {
                        "id": 21631862,
                        "title": "Untitled",
                        "doi": "",
                        "handle": "",
                        "url": "https://api.figshare.com/v2/account/articles/21631862",
                        "published_date": null,
                        "thumb": "",
                        "defined_type": 3,
                        "defined_type_name": "dataset",
                        "group_id": null,
                        "url_private_api": "https://api.figshare.com/v2/account/articles/21631862",
                        "url_public_api": "https://api.figshare.com/v2/articles/21631862",
                        "url_private_html": "https://figshare.com/account/articles/21631862",
                        "url_public_html": "https://figshare.com/articles/dataset/_/21631862",
                        "timeline": {},
                        "resource_title": null,
                        "resource_doi": null
                    }
                ]

            or

            API call:
                GET https://api.figshare.com/v2/account/articles/21631871
                Content-Type: application/json
                Authorization: token {{token}}
            API response:
                {
                "files": [],
                "resource_title": null,
                "resource_doi": null,
                "group_resource_id": null,
                "custom_fields": [],
                "account_id": 3414786,
                "authors": [
                    {
                    "id": 14126808,
                    "full_name": "Dave Tromp",
                    "is_active": true,
                    "url_name": "Dave_Tromp",
                    "orcid_id": ""
                    }
                ],
                "figshare_url": "https://figshare.com/articles/dataset/_/21631871",
                "description": "",
                "funding": null,
                "funding_list": [],
                "version": 0,
                "status": "draft",
                "size": 0,
                "created_date": "2022-11-28T14:23:37Z",
                "modified_date": "2022-11-28T14:23:37Z",
                "is_public": false,
                "is_confidential": false,
                "is_metadata_record": false,
                "confidential_reason": "",
                "metadata_reason": "",
                "license": {
                    "value": 1,
                    "name": "CC BY 4.0",
                    "url": "https://creativecommons.org/licenses/by/4.0/"
                },
                "tags": [],
                "categories": [],
                "references": [],
                "has_linked_file": false,
                "citation": "Tromp, Dave (2022): Untitled. figshare. Dataset. https://figshare.com/articles/dataset/_/21631871",
                "is_embargoed": false,
                "embargo_date": null,
                "embargo_type": null,
                "embargo_title": "",
                "embargo_reason": "",
                "embargo_options": [],
                "id": 21631871,
                "title": "Untitled",
                "doi": "",
                "handle": "",
                "url": "https://api.figshare.com/v2/account/articles/21631871",
                "published_date": null,
                "thumb": "",
                "defined_type": 3,
                "defined_type_name": "dataset",
                "group_id": null,
                "url_private_api": "https://api.figshare.com/v2/account/articles/21631871",
                "url_public_api": "https://api.figshare.com/v2/articles/21631871",
                "url_private_html": "https://figshare.com/account/articles/21631871",
                "url_public_html": "https://figshare.com/articles/dataset/_/21631871",
                "timeline": {}
                }
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug(
            f"get articles from figshare, id? {id}, return response? {return_response}, metadata? {metadataFilter}"
        )
        headers = {'authorization': f"token {self.api_key}"}

        log.debug(headers)

        if id is not None:
            r = requests.get(
                f"{self.figshare_api_address}/account/articles/{id}",
                headers=headers
            )
        else:
            r = requests.get(
                f"{self.figshare_api_address}/account/articles",
                headers=headers
            )
            log.debug(
                f"Get Articles: Status Code: {r.status_code}")

        if return_response:
            return r

        if r.status_code >= 300:
            abort(r.status_code)

        result = r.json()

        if id is not None:
            result = [result]
        log.debug(f"filter only metadata, {result}")

        metadataList = []
        for res in result:
            if (
                "submitted" in res
                and res["submitted"]
                or "submitted" in res["metadata"]
                and res["metadata"]["submitted"]
            ):
                continue

            metadataList.append(res["metadata"])

        result = metadataList

        log.debug("apply filter")
        if metadataFilter is not None:
            result = {
                key: res[key]
                for res in result
                for key in metadataFilter.keys()
                if key in res
            }

        log.debug("finished applying filter")

        log.debug("return results")

        if id is not None:
            return result[0]

        return result

    def create_new_article_internal(self, metadata=None, return_response=False):
        """Creates a new untitled article. You can get the id with r.json()['id']
        If metadata is specified, it will changes metadata after creating.

        Args:
            metadata (_type_, optional): Metadata for the article. Defaults to None.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            json: API response if return_response is set to True

        Example:
            API call:
                POST https://api.figshare.com/v2/account/articles
                Content-Type: application/json
                Authorization: token {{token}}
            API response:
                {
                "entity_id": 21631871,
                "location": "https://api.figshare.com/v2/account/articles/21631871",
                "warnings": []
                }
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        log.debug("Create new article: Starts")

        headers = {
            'content-type': "application/json",
            'authorization': f"token {self.api_key}",
        }

        log.debug(headers)

        url = f"{self.figshare_api_address}/account/articles"

        log.debug(url)

        payload = {"title": "untitled"}

        log.debug(payload)

        r = requests.post(
            url,
            data=json.dumps(payload),
            headers=headers
        )

        log.debug(
            f"Create new articles: Status Code: {r.status_code}")

        if r.status_code != 201:
            return {} if not return_response else r

        log.debug(f"Metadata: {metadata}")
        if metadata is not None and isinstance(metadata, dict):
            log.debug(metadata)
            return self.change_metadata_in_article_internal(
                r.json()["id"], metadata, return_response=return_response
            )

        return r.json() if not return_response else r

    def remove_article_internal(self, id, return_response=False):
        """Will remove an article based on it's id.

        Args:
            id (_type_): the article id
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            json: API response if return_response is set to True

        Example:
            API call:
                DELETE https://api.figshare.com/v2/account/articles/21631871
                Content-Type: application/json
                Authorization: token {{token}}
            API response:
                HTTP/1.1 204 No Content
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        r = requests.delete(
            f"{self.figshare_api_address}/account/articles/{id}",
            headers={"Authorization": f"token {self.api_key}"},
            verify=(os.environ.get("VERIFY_SSL", "True") == "True"),
            timeout=1
        )

        return r.status_code == 204 if not return_response else r

    def upload_new_file_to_article_internal(
        self, article_id, path_to_file, file=None, return_response=False, test=False
    ):
        """Uploads a file to an article on Figshare.

        Args:
            article_id (int): id of the article to upload to (from get_article or create_new_article; r.json()['id'])
            path_to_file (str): path to the file to be uploaded. Example: ~/mydatapackage.csv
            file (bytes, optional): file as a bytes object. Defaults to None.
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            bool: Alternative: json if return_response=True
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        try:
            # in testing we do not have a file object passed in
            if not test:
                with open(path_to_file, 'wb') as ff:
                    ff.write(file.read())

            # we will get the latest created article_id
            article_id = self.get_article_internal(return_response=True).json()[0]['id']
            article_id = int(article_id)

            file_info = self.initiate_new_upload(article_id, path_to_file)
            self.upload_parts(file_info, path_to_file)
            self.complete_upload(article_id, file_info['id'])

            return {"success": True}
        except Exception as e:
            log.error(
                f"Exception at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
            log.error(str(e))


    def get_files_from_article(self, article_id):
        """will get all the files from the article requested.

        Args:
            article_id (int): id of the article

        Returns:
            json: json containing all the files from the article
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        req = requests.get(
            f"{self.figshare_api_address}/account/articles/{article_id}/files",
            headers={"Authorization": f"token {self.api_key}"}
        )
        result = req.json()
        return result

    def change_metadata_in_article_internal(
        self, article_id, metadata, return_response=False
    ):
        """Will update an article according to the provided metadata.

        Args:
            article_id (int): id of the article
            return_response (bool, optional): Set to True will return the API response. Defaults to False.
            metadata (dict): A data-dict json-like object
                ```python
                Example: data = {
                    'metadata': {
                        'title': 'My first upload',
                        'upload_type': 'poster',
                        'description': 'This is my first upload',
                        'creators': [{'name': 'Doe, John',
                                    'affiliation': 'Figshare'}]
                    }
                }
                ```
        Returns:
            object: response of the PUT request to the articles endpoint
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"token {self.api_key}",
        }

        data = {}
        data = metadata

        log.info(f"send data: {data}")

        r = requests.put(
            f"{self.figshare_api_address}/account/articles/{article_id}",
            data=json.dumps(data),
            headers=headers
        )

        return r.status_code == 205 if not return_response else r

    def publish_article_internal(self, article_id, return_response=False):
        """Will publish an article if it is not under embargo

        Args:
            article_id (_type_): id of the article
            return_response (bool, optional): Set to True will return the API response. Defaults to False.

        Returns:
            object: response of the POST request to the articles publish endpoint
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        r = requests.post(
            f"{self.figshare_api_address}/account/articles/{article_id}/publish",
            headers={'authorization': f"token {self.api_key}"}
        )

        return r.status_code == 201 if not return_response else r

    def delete_all_files_from_article_internal(self, article_id):
        """Will delete all files from an article.

        Args:
            article_id (int): id of an article

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        for file in self.get_files_from_article(article_id):
            log.debug(f"found file: {file}")

            if not self.delete_file_from_article_internal(article_id, file["id"]):
                return False

        return True

    def delete_file_from_article_internal(self, article_id, file_id):
        """Will delete a file from an article

        Args:
            article_id (int): id of the article
            file_id (int): id of file

        Returns:
            bool: True if successful, False if not
        """
        log.debug(
            f"Entering at lib/upload_figshare.py {inspect.getframeinfo(inspect.currentframe()).function}")
        r = requests.delete(
            f"{self.figshare_api_address}/account/articles/{article_id}/files/{file_id}",
            headers={"Authorization": f"token {self.api_key}"}
        )

        if r.status_code < 300:
            return True

        return False


if __name__ == "__main__":
    """Below code will test the code that interfaces the uploads to Figshare
    """
    api_key = "64ce2700274870bc2c7c26fcc52e2ecbfdd9d0afd6abe69a7f97e7bb4741fd4c91bbf5771178ae243d8ccb048efeb1daffb1dc6ff1823946c5ea34bb4b90433e"
    api_address = "https://api.figshare.com/v2"
    figshare = Figshare(api_key=api_key, api_address=api_address)
    article_id = figshare.create_new_article_internal()['entity_id']
    print(article_id)
    file_path = "../figshare.png"
    result = figshare.upload_new_file_to_article_internal(
        article_id=article_id, path_to_file=file_path)
    # print(figshare.get_article_internal(return_response=True).json()[0]['id'])
    # article_id = 21666326
    # print(f"Deleting files from article {article_id}")
    # print(figshare.delete_all_files_from_article_internal(article_id))
    # print(f"Deleting article {article_id}")
    # print(figshare.remove_article_internal(article_id))
