## DORIS RDS-connector

This connector is used in SUNET for publishing data to S3 buckets and pushing ro-crate manifest to SND:s manifest index.

### Describo profile

The connector will only suport the dataset `name` atrribute for the first release.
This profile for describo should limit and set the name as required:

```json
{
    "metadata": {
        "name": "DORIS Profile",
        "description": "Just the label",
        "version": 0.1,
        "warnMissingProperty": true,
        "keywords": ["doris", "Dataset"]
    },
    "hide": {
        "Dataset": []
    },
    "layouts": {
        "Dataset": [
            {
                "name": "About",
                "description": "descriptive name for the dataset",
                "inputs": [
                    "name"
                ]
            }
        ]
    },
    "classes": {
        "Dataset": {
            "definition": "override",
            "subClassOf": [],
            "inputs": [
                {
                    "id": "https://schema.org/name",
                    "name": "name",
                    "label": "Title",
                    "help": "Use a descriptive name for the dataset",
                    "required": true,
                    "multiple": false,
                    "type": ["Text"]
                }
            ]
        }
    },
    "lookup": {
    }
}
```
