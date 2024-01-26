# LearnKeyVault

## Preparation
In local running please following environment variables in `launchSettings.json` with your values in Azure:
```
        //"AZURE_CLIENT_ID": "00000000-0000-0000-0000-000000000000",
        //"AZURE_TENANT_ID": "00000000-0000-0000-0000-000000000000",
        //"AZURE_CLIENT_SECRET": ""
```

## Demo contains
1. read value from key vault for key "DefaultCosmosDBConnectionString"
1. overwrite appsetting's value for key "Key02" with value from KeyVault "LearnKeyVault-Key02", the prefix got set as "LearnKeyVault" here.